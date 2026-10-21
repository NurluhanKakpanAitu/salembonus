using System.Net;
using System.Text.Json.Nodes;
using SalemBonus.Infrastructure.Persistence;

namespace SalemBonus.Api.Tests;

/// <summary>
/// SalemTech әкімшілігі (admin.salemtech.kz): кіру мен токен шекарасы, бизнес қосу (дүкен, касса, иесінің аккаунты), модульдер иесіне де
/// серверде жабылады, тоқтату кіруді және ашық сеансты бұғаттайды, төлем «төленген күнді» жылжытады, жою қайта есептейді, тарифтер мен әкімшілер.
/// Кіру шегі (IP бойынша минутына 10) қызметкермен ортақ — әкімші токені бір рет алынады.
/// </summary>
[Collection(ApiCollection.Name)]
public class AdminTests(ApiFixture api)
{
    private const string A = "/api/admin/v1";
    private static string? _adminToken;

    private async Task<(Pos Pos, string Token)> AdminAsync()
    {
        var pos = await api.NewDeviceAsync(activateRegister: false);
        _adminToken ??= (await pos.Send(HttpMethod.Post, "", $"{A}/auth/login", new { email = AdminSeeder.AdminEmail, password = AdminSeeder.AdminPassword }))
            .Ok["accessToken"]!.GetValue<string>();
        return (pos, _adminToken);
    }

    private static string Phone() => "+7705" + Random.Shared.Next(1_000_000, 9_999_999);

    private static async Task<JsonNode> CreateBusinessAsync(Pos pos, string token, string ownerPhone, object? modules = null, int trialDays = 14, Guid? planId = null) =>
        (await pos.Post(token, $"{A}/businesses", new
        {
            name = "Тест бизнес " + Guid.NewGuid().ToString("N")[..6], bin = "990101300123", city = "Алматы", contactName = "Айдос", contactPhone = "+7 701 000 00 00",
            planId, trialDays, modules,
            store = new { name = "Магазин на Абая", category = "Продукты", address = "Абая 10", timeZone = "Asia/Almaty" },
            owner = new { phone = ownerPhone, firstName = "Айдос", lastName = "Иесі" },
        })).Ok;

    private static async Task<string> StaffLoginAsync(Pos pos, string phone, string password) =>
        (await pos.Send(HttpMethod.Post, "", "/api/staff/v1/auth/login", new { phone, password })).Ok["accessToken"]!.GetValue<string>();

    [Fact]
    public async Task Login_is_checked_and_admin_and_staff_tokens_do_not_cross()
    {
        var (pos, token) = await AdminAsync();
        var wrong = await pos.Send(HttpMethod.Post, "", $"{A}/auth/login", new { email = AdminSeeder.AdminEmail, password = "wrong-password-1" });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.Status);

        Assert.Equal(AdminSeeder.AdminEmail, (await pos.Get(token, $"{A}/auth/me")).Ok["email"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.Unauthorized, (await pos.Get(pos.Owner, $"{A}/overview")).Status); // бизнес иесі әкімшілікке кіре алмайды
        Assert.Equal(HttpStatusCode.Unauthorized, (await pos.Get("", $"{A}/businesses")).Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await pos.Get(token, "/api/pos/v1/clients/meta")).Status); // әкімші токені бизнес API-ына жарамайды
    }

    [Fact]
    public async Task Created_business_gets_store_register_and_owner_and_modules_are_enforced_for_the_owner()
    {
        var (pos, token) = await AdminAsync();
        var plans = (await pos.Get(token, $"{A}/plans")).Ok.AsArray();
        var plan = plans.First(p => p!["name"]!.GetValue<string>() == "Бизнес")!;
        Assert.DoesNotContain("finance", plan["modules"]!.AsArray().Select(x => x!.GetValue<string>()));
        var ownerPhone = Phone();
        var created = await CreateBusinessAsync(pos, token, ownerPhone, planId: plan["id"]!.GetValue<Guid>());
        var business = created["business"]!;
        var id = business["business"]!["id"]!.GetValue<Guid>();
        Assert.Equal("trial", business["business"]!["status"]!.GetValue<string>());
        Assert.Equal(19_900m, business["business"]!["fee"]!.GetValue<decimal>());
        var store = Assert.Single(business["stores"]!.AsArray())!;
        Assert.Equal(1, store["registers"]!.GetValue<int>());
        Assert.Equal(7, store["joinCode"]!.GetValue<string>().Length);
        var owner = Assert.Single(business["staff"]!.AsArray())!;
        Assert.Equal("Owner", owner["roles"]![0]!["role"]!.GetValue<string>());
        Assert.Equal(ownerPhone, created["ownerPhone"]!.GetValue<string>());
        Assert.Contains(business["history"]!.AsArray(), h => h!["action"]!.GetValue<string>() == "business.create");

        // Бір телефон екі бизнеске иесі бола алмайды.
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Post(token, $"{A}/businesses", new { name = "Дубль", owner = new { phone = ownerPhone, firstName = "Х" } })).Status);

        var ownerToken = await StaffLoginAsync(pos, ownerPhone, created["tempPassword"]!.GetValue<string>());
        var me = (await pos.Get(ownerToken, "/api/staff/v1/auth/me")).Ok;
        var permissions = me["stores"]![0]!["permissions"]!.AsArray().Select(x => x!.GetValue<string>()).ToList();
        Assert.Contains("clients.view", permissions);
        Assert.DoesNotContain("finance.view", permissions); // тарифте «Финансы» жоқ
        Assert.Equal(HttpStatusCode.Forbidden, (await pos.Get(ownerToken, "/api/pos/v1/finance/overview")).Status); // иесіне де жабық
        Assert.Equal(HttpStatusCode.OK, (await pos.Get(ownerToken, "/api/pos/v1/clients/meta")).Status);

        var all = (await pos.Put(token, $"{A}/businesses/{id}/modules", new { modules = new[] { "cashier", "products", "finance", "clients" } })).Ok;
        Assert.Contains("finance", all["business"]!["modules"]!.AsArray().Select(x => x!.GetValue<string>()));
        Assert.Equal(HttpStatusCode.OK, (await pos.Get(ownerToken, "/api/pos/v1/finance/overview")).Status); // келесі сұраныстан бастап ашық
        Assert.Equal(HttpStatusCode.Forbidden, (await pos.Get(ownerToken, "/api/pos/v1/sales/history")).Status); // «Продажи» өшірілді
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Put(token, $"{A}/businesses/{id}/modules", new { modules = new[] { "crm" } })).Status);
        var history = (await pos.Get(token, $"{A}/businesses/{id}")).Ok["history"]!.AsArray();
        Assert.Contains(history, h => h!["action"]!.GetValue<string>() == "business.modules");

        // Екінші дүкен: иесі оған бірден кіреді; әкімші аккаунты — уақытша құпиясөзбен.
        var withStore = (await pos.Post(token, $"{A}/businesses/{id}/stores", new { name = "Магазин №2", category = "Продукты" })).Ok;
        Assert.Equal(2, withStore["stores"]!.AsArray().Count);
        Assert.Equal(2, withStore["staff"]![0]!["roles"]!.AsArray().Count);
        var account = (await pos.Post(token, $"{A}/businesses/{id}/accounts", new { phone = Phone(), firstName = "Әкімші", lastName = "Тест", role = "Admin" })).Ok;
        Assert.False(string.IsNullOrEmpty(account["tempPassword"]!.GetValue<string>()));
        Assert.Equal("Admin", account["account"]!["roles"]![0]!["role"]!.GetValue<string>());
        var reset = (await pos.Post(token, $"{A}/businesses/{id}/accounts/{owner["id"]}/reset-password", new { })).Ok;
        Assert.NotEqual(created["tempPassword"]!.GetValue<string>(), reset["tempPassword"]!.GetValue<string>());
    }

    [Fact]
    public async Task Suspension_blocks_login_and_open_sessions_and_activation_restores_access()
    {
        var (pos, token) = await AdminAsync();
        var ownerPhone = Phone();
        var created = await CreateBusinessAsync(pos, token, ownerPhone);
        var id = created["business"]!["business"]!["id"]!.GetValue<Guid>();
        var password = created["tempPassword"]!.GetValue<string>();
        var ownerToken = await StaffLoginAsync(pos, ownerPhone, password);
        Assert.Equal(HttpStatusCode.OK, (await pos.Get(ownerToken, "/api/pos/v1/clients/meta")).Status);

        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Post(token, $"{A}/businesses/{id}/suspend", new { reason = "" })).Status);
        var suspended = (await pos.Post(token, $"{A}/businesses/{id}/suspend", new { reason = "Не оплачено 2 месяца" })).Ok;
        Assert.Equal("suspended", suspended["business"]!["status"]!.GetValue<string>());
        var blocked = await pos.Get(ownerToken, "/api/pos/v1/clients/meta");
        Assert.Equal(HttpStatusCode.Forbidden, blocked.Status);
        Assert.Contains("SalemTech", blocked.Body!["detail"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Send(HttpMethod.Post, "", "/api/staff/v1/auth/login", new { phone = ownerPhone, password })).Status);

        var active = (await pos.Post(token, $"{A}/businesses/{id}/activate", new { })).Ok;
        Assert.Equal("trial", active["business"]!["status"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.OK, (await pos.Get(ownerToken, "/api/pos/v1/clients/meta")).Status);
    }

    [Fact]
    public async Task Payments_extend_paid_until_continuously_and_voiding_recalculates()
    {
        var (pos, token) = await AdminAsync();
        var created = await CreateBusinessAsync(pos, token, Phone(), trialDays: 0);
        var id = created["business"]!["business"]!["id"]!.GetValue<Guid>();
        Assert.Equal("unpaid", created["business"]!["business"]!["status"]!.GetValue<string>());
        var today = ApiFixture.StoreToday;

        var plan = (await pos.Post(token, $"{A}/plans", new { name = "Тест " + Guid.NewGuid().ToString("N")[..4], monthlyPrice = 15_000, modules = new[] { "cashier", "sales" } })).Ok;
        (await pos.Put(token, $"{A}/businesses/{id}", new { name = "Оплата тест", planId = plan["id"]!.GetValue<Guid>(), applyPlanModules = true, customFee = false })).EnsureOk();
        var suggest = (await pos.Get(token, $"{A}/payments/suggest?organizationId={id}&months=3")).Ok;
        Assert.Equal(today.ToString("yyyy-MM-dd"), suggest["periodFrom"]!.GetValue<string>());
        Assert.Equal(45_000m, suggest["amount"]!.GetValue<decimal>());

        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Post(token, $"{A}/payments", new { organizationId = id, amount = 0, months = 1 })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Post(token, $"{A}/payments", new { organizationId = id, amount = 100, months = 1, method = "bitcoin" })).Status);
        var first = (await pos.Post(token, $"{A}/payments", new { organizationId = id, amount = 45_000, months = 3, method = "kaspi", reference = "KP-1" })).Ok;
        var firstTo = today.AddMonths(3).AddDays(-1);
        Assert.Equal(firstTo.ToString("yyyy-MM-dd"), first["periodTo"]!.GetValue<string>());
        var second = (await pos.Post(token, $"{A}/payments", new { organizationId = id, amount = 15_000, months = 1, method = "transfer" })).Ok;
        Assert.Equal(firstTo.AddDays(1).ToString("yyyy-MM-dd"), second["periodFrom"]!.GetValue<string>()); // кезең үзіліссіз жалғасады

        var detail = (await pos.Get(token, $"{A}/businesses/{id}")).Ok;
        Assert.Equal(firstTo.AddMonths(1).ToString("yyyy-MM-dd"), detail["business"]!["paidUntil"]!.GetValue<string>());
        Assert.Equal("active", detail["business"]!["status"]!.GetValue<string>());
        Assert.Equal(60_000m, detail["paidTotal"]!.GetValue<decimal>());
        Assert.Equal(new[] { "cashier", "sales" }, detail["business"]!["modules"]!.AsArray().Select(x => x!.GetValue<string>()));

        var secondId = second["id"]!.GetValue<Guid>();
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Post(token, $"{A}/payments/{secondId}/void", new { reason = "" })).Status);
        var voided = (await pos.Post(token, $"{A}/payments/{secondId}/void", new { reason = "Ошибочная сумма" })).Ok;
        Assert.True(voided["voided"]!.GetValue<bool>());
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Post(token, $"{A}/payments/{secondId}/void", new { reason = "Ещё раз" })).Status);
        var after = (await pos.Get(token, $"{A}/businesses/{id}")).Ok;
        Assert.Equal(firstTo.ToString("yyyy-MM-dd"), after["business"]!["paidUntil"]!.GetValue<string>());
        Assert.Equal(45_000m, after["paidTotal"]!.GetValue<decimal>());

        var list = (await pos.Get(token, $"{A}/payments?organizationId={id}&includeVoided=true")).Ok;
        Assert.Equal(2, list["total"]!.GetValue<int>());
        Assert.Equal(45_000m, list["sum"]!.GetValue<decimal>());
        var overview = (await pos.Get(token, $"{A}/overview")).Ok;
        Assert.True(overview["mrr"]!.GetValue<decimal>() >= 15_000m);
        Assert.True(overview["paidThisMonth"]!.GetValue<decimal>() >= 45_000m);
        Assert.Equal(12, overview["revenue"]!.AsArray().Count);
        var page = (await pos.Get(token, $"{A}/businesses?search=Оплата тест&status=active")).Ok;
        Assert.Contains(page["rows"]!.AsArray(), r => r!["id"]!.GetValue<Guid>() == id);
    }

    [Fact]
    public async Task Admins_are_created_with_temp_password_and_cannot_disable_themselves()
    {
        var (pos, token) = await AdminAsync();
        var me = (await pos.Get(token, $"{A}/auth/me")).Ok["id"]!.GetValue<Guid>();
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Post(token, $"{A}/admins/{me}/active", new { active = false })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Post(token, $"{A}/admins", new { email = "not-an-email", name = "Х" })).Status);
        var email = $"manager{Random.Shared.Next(10000, 99999)}@salemtech.kz";
        var created = (await pos.Post(token, $"{A}/admins", new { email, name = "Менеджер" })).Ok;
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Post(token, $"{A}/admins", new { email, name = "Дубль" })).Status);
        var adminId = created["admin"]!["id"]!.GetValue<Guid>();
        var off = (await pos.Post(token, $"{A}/admins/{adminId}/active", new { active = false })).Ok;
        Assert.False(off["isActive"]!.GetValue<bool>());
        var login = await pos.Send(HttpMethod.Post, "", $"{A}/auth/login", new { email, password = created["tempPassword"]!.GetValue<string>() });
        Assert.Equal(HttpStatusCode.BadRequest, login.Status); // өшірілген әкімші кіре алмайды
        Assert.Contains((await pos.Get(token, $"{A}/admins")).Ok.AsArray(), a => a!["email"]!.GetValue<string>() == email);
    }

    // ---------- Лендинг: ашық тарифтер мен өтінім ----------

    [Fact]
    public async Task Landing_shows_active_plans_and_requests_reach_the_admin_panel()
    {
        var (pos, token) = await AdminAsync();
        var hidden = (await pos.Post(token, $"{A}/plans", new { name = "Скрытый " + Guid.NewGuid().ToString("N")[..4], monthlyPrice = 1, isActive = false })).Ok;
        var plans = (await pos.Get("", "/api/public/v1/plans")).Ok.AsArray();
        Assert.Contains(plans, p => p!["name"]!.GetValue<string>() == "Бизнес");
        Assert.DoesNotContain(plans, p => p!["id"]!.GetValue<Guid>() == hidden["id"]!.GetValue<Guid>()); // архивтегі тариф лендингте жоқ

        var phone = Phone();
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Send(HttpMethod.Post, "", "/api/public/v1/leads", new { name = "Асан", phone = "123" })).Status);
        (await pos.Send(HttpMethod.Post, "", "/api/public/v1/leads", new
        {
            name = "Асан Нурланов", phone = phone.Replace("+7", "8"), businessName = "Дүкен " + phone[^4..], city = "Шымкент", interest = "both", comment = "Две точки", language = "kk",
        })).EnsureOk();
        (await pos.Send(HttpMethod.Post, "", "/api/public/v1/leads", new { name = "Асан", phone })).EnsureOk(); // 10 минут ішінде қайталау — жаңа жазба жоқ
        (await pos.Send(HttpMethod.Post, "", "/api/public/v1/leads", new { name = "Бот", phone = Phone(), website = "http://spam" })).EnsureOk(); // тұзақ

        Assert.Equal(HttpStatusCode.Unauthorized, (await pos.Get("", $"{A}/leads")).Status);
        var list = (await pos.Get(token, $"{A}/leads?search={phone[^7..]}")).Ok;
        var lead = Assert.Single(list["rows"]!.AsArray())!;
        Assert.Equal(phone, lead["phone"]!.GetValue<string>());
        Assert.Equal("new", lead["status"]!.GetValue<string>());
        Assert.Equal("kk", lead["language"]!.GetValue<string>());
        Assert.Equal(0, (await pos.Get(token, $"{A}/leads?search=Бот")).Ok["total"]!.GetValue<int>());
        Assert.True((await pos.Get(token, $"{A}/overview")).Ok["newLeads"]!.GetValue<int>() >= 1);

        var id = lead["id"]!.GetValue<Guid>();
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Put(token, $"{A}/leads/{id}", new { status = "maybe" })).Status);
        var updated = (await pos.Put(token, $"{A}/leads/{id}", new { status = "contacted", adminComment = "Позвонить в понедельник" })).Ok;
        Assert.Equal("contacted", updated["status"]!.GetValue<string>());
        Assert.Equal("Позвонить в понедельник", updated["adminComment"]!.GetValue<string>());
        Assert.False(string.IsNullOrEmpty(updated["updatedBy"]!.GetValue<string>()));
    }
}
