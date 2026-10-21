using System.Net;
using System.Text.Json.Nodes;

namespace SalemBonus.Api.Tests;

/// <summary>«Сотрудники»: қосу (кассир PIN-мен, әкімші құпиясөзбен), рұқсаттар, өшіру сеанстарды жабады, иесін тек иесі, өзін өзгертуге болмайды, журнал.</summary>
[Collection(ApiCollection.Name)]
public class StaffTests(ApiFixture api)
{
    private const string S = "/api/pos/v1/staff";
    private static string Phone() => "+7705" + Random.Shared.Next(1_000_000, 9_999_999);

    [Fact]
    public async Task Owner_adds_cashier_and_admin_manages_access_and_the_log_records_it()
    {
        var pos = await api.NewDeviceAsync();
        var list = (await pos.Get(pos.Owner, S)).Ok;
        Assert.True(list["isOwner"]!.GetValue<bool>());
        var storeId = list["stores"]!.AsArray()[0]!["id"]!.GetValue<Guid>();
        var me = list["rows"]!.AsArray().Single(r => r!["isSelf"]!.GetValue<bool>())!;
        Assert.Equal("Owner", me["role"]!.GetValue<string>());

        // Кассир: құпиясөзсіз, PIN міндетті; ұсақ рұқсаттар мен жеңілдік шегі.
        var cashierPhone = Phone();
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Post(pos.Owner, S, new { firstName = "Дана", phone = cashierPhone, role = "Cashier", storeIds = new[] { storeId } })).Status);
        var cashier = (await pos.Post(pos.Owner, S, new
        {
            firstName = "Дана", lastName = "Кассир", phone = cashierPhone, role = "Cashier", storeIds = new[] { storeId }, pin = "4729", maxDiscountPercent = 10,
            permissions = new[] { "sales.create", "sales.return", "inventory.view", "staff.manage" },
        })).Ok;
        Assert.Null(cashier["tempPassword"]?.GetValue<string>());
        var c = cashier["staff"]!;
        Assert.Equal("Cashier", c["staff"]!["role"]!.GetValue<string>());
        Assert.True(c["staff"]!["hasPin"]!.GetValue<bool>());
        Assert.False(c["staff"]!["hasPassword"]!.GetValue<bool>());
        Assert.Equal(10m, c["maxDiscountPercent"]!.GetValue<decimal>());
        var cashierPerms = c["permissions"]!.AsArray().Select(p => p!.GetValue<string>()).ToList();
        Assert.Contains("sales.return", cashierPerms);
        Assert.DoesNotContain("staff.manage", cashierPerms); // кассирге басқару құқығы берілмейді

        // Әкімші: уақытша құпиясөзбен кіреді.
        var adminPhone = Phone();
        var created = (await pos.Post(pos.Owner, S, new { firstName = "Асель", lastName = "Админ", phone = adminPhone, role = "Admin", storeIds = new[] { storeId }, language = "kk" })).Ok;
        var temp = created["tempPassword"]!.GetValue<string>();
        var adminId = created["staff"]!["staff"]!["id"]!.GetValue<Guid>();
        Assert.False(created["staff"]!["permissions"]!.AsArray().Any(p => p!.GetValue<string>() == "staff.manage")); // әкімшінің әдепкісінде жоқ
        var adminToken = (await pos.Send(HttpMethod.Post, "", "/api/staff/v1/auth/login", new { phone = adminPhone, password = temp })).Ok["accessToken"]!.GetValue<string>();
        Assert.Equal(HttpStatusCode.Forbidden, (await pos.Get(adminToken, S)).Status); // staff.manage жоқ
        Assert.Equal(HttpStatusCode.Forbidden, (await pos.Get(pos.Cashier, S)).Status);

        // Рұқсат беру: әкімші енді қызметкерлерді көреді, бірақ иесін қоса алмайды.
        var updated = (await pos.Put(pos.Owner, $"{S}/{adminId}", new { role = "Admin", storeIds = new[] { storeId }, permissions = new[] { "staff.manage", "sales.view" }, maxDiscountPercent = 15 })).Ok;
        Assert.Equal(15m, updated["staff"]!["maxDiscountPercent"]!.GetValue<decimal>());
        Assert.Equal("Асель", updated["staff"]!["staff"]!["firstName"]!.GetValue<string>());
        Assert.Equal("kk", updated["staff"]!["staff"]!["language"]!.GetValue<string>());
        Assert.Contains((await pos.Get(adminToken, S)).Ok["rows"]!.AsArray(), r => r!["phone"]!.GetValue<string>() == cashierPhone);
        Assert.Equal(HttpStatusCode.Forbidden, (await pos.Post(adminToken, S, new { firstName = "Иесі", phone = Phone(), role = "Owner" })).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await pos.Post(adminToken, $"{S}/{me["id"]}/active", new { active = false })).Status); // иесін өшіруге де

        // Өзін-өзі: рөлін өзгерту мен өшіру тыйым.
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Post(pos.Owner, $"{S}/{me["id"]}/active", new { active = false })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Put(pos.Owner, $"{S}/{me["id"]}", new { role = "Admin", storeIds = new[] { storeId } })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Post(pos.Owner, $"{S}/{adminId}/pin", new { pin = "1234" })).Status); // қарапайым PIN
        Assert.True((await pos.Post(pos.Owner, $"{S}/{adminId}/pin", new { pin = "8642" })).Ok["staff"]!["hasPin"]!.GetValue<bool>());

        // Өшіру: сеанстары жабылады — токені енді дүкенге кірмейді; қайта қосу мен құпиясөзді қалпына келтіру.
        Assert.False((await pos.Post(pos.Owner, $"{S}/{adminId}/active", new { active = false })).Ok["staff"]!["isActive"]!.GetValue<bool>());
        Assert.Equal(HttpStatusCode.Forbidden, (await pos.Get(adminToken, S)).Status);
        Assert.True((await pos.Post(pos.Owner, $"{S}/{adminId}/active", new { active = true })).Ok["staff"]!["isActive"]!.GetValue<bool>());
        var reset = (await pos.Post(pos.Owner, $"{S}/{adminId}/reset-password", new { })).Ok;
        Assert.NotEqual(temp, reset["tempPassword"]!.GetValue<string>());
        var cashierId = c["staff"]!["id"]!.GetValue<Guid>();
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Post(pos.Owner, $"{S}/{cashierId}/reset-password", new { })).Status); // кассирде құпиясөз жоқ

        // Карточка: дүкендер бойынша қолжетімділік; журналда қосу жазбасы.
        var detail = (await pos.Get(pos.Owner, $"{S}/{cashierId}")).Ok;
        var row = detail["access"]!.AsArray().Single(a => a!["storeId"]!.GetValue<Guid>() == storeId)!;
        Assert.True(row["isActive"]!.GetValue<bool>());
        Assert.Equal("Cashier", row["role"]!.GetValue<string>());
        var audit = (await pos.Get(pos.Owner, $"{S}/audit?staffId={me["id"]}&action=staff.")).Ok;
        Assert.Contains(audit["rows"]!.AsArray(), a => a!["action"]!.GetValue<string>() == "staff.create" && a["entityId"]!.GetValue<string>() == cashierId.ToString());
        Assert.Contains(audit["actions"]!.AsArray(), a => a!.GetValue<string>() == "staff.update");

        // Телефон қайталанбайды; рөл белгісіз.
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Post(pos.Owner, S, new { firstName = "Қайта", phone = cashierPhone, role = "Cashier", pin = "8642", storeIds = new[] { storeId } })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Post(pos.Owner, S, new { firstName = "X", phone = Phone(), role = "Manager" })).Status);
    }

    [Fact]
    public async Task My_sessions_are_listed_and_other_devices_can_be_signed_out()
    {
        var pos = await api.NewDeviceAsync(activateRegister: false);
        var sessions = (await pos.Get(pos.Owner, "/api/staff/v1/auth/me/sessions")).Ok.AsArray();
        Assert.NotEmpty(sessions);
        Assert.All(sessions, s => Assert.NotNull(s!["createdAt"]));
        Assert.Equal(HttpStatusCode.NoContent, (await pos.Send(HttpMethod.Post, pos.Owner, "/api/staff/v1/auth/me/sessions/revoke-others", null)).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await pos.Delete(pos.Owner, $"/api/staff/v1/auth/me/sessions/{Guid.NewGuid()}")).Status);
    }
}
