using System.Net;
using System.Text.Json.Nodes;
using static SalemBonus.Api.Tests.Pos;

namespace SalemBonus.Api.Tests;

/// <summary>«Настройка»: дүкен профилі мен БСН, дүкендер (тек иесі), кассалар, төлем түрлері мен чек үлгісі, реквизиттер, тариф.</summary>
[Collection(ApiCollection.Name)]
public class SettingsTests(ApiFixture api)
{
    private const string S = "/api/pos/v1/settings";

    [Fact]
    public async Task Store_profile_and_business_details_are_edited_by_the_owner()
    {
        var pos = await api.NewDeviceAsync(activateRegister: false);
        var profile = (await pos.Get(pos.Owner, $"{S}/store")).Ok;
        Assert.True(profile["canEditOrganization"]!.GetValue<bool>());
        Assert.NotEmpty(profile["joinCode"]!.GetValue<string>());
        Assert.Contains(profile["timeZones"]!.AsArray(), z => z!.GetValue<string>() == "Asia/Almaty");
        Assert.Equal(HttpStatusCode.Forbidden, (await pos.Get(pos.Cashier, $"{S}/store")).Status);

        var name = profile["name"]!.GetValue<string>();
        var updated = (await pos.Put(pos.Owner, $"{S}/store", new
        {
            name, category = "Автозапчасти", address = "Алматы, Сейфуллина 502", phone = "+7 727 000 00 01", timeZone = "Asia/Aqtobe",
            organizationName = profile["organizationName"]!.GetValue<string>(), organizationBin = "123456789012",
        })).Ok;
        Assert.Equal("Asia/Aqtobe", updated["timeZone"]!.GetValue<string>());
        Assert.Equal("123456789012", updated["organizationBin"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Put(pos.Owner, $"{S}/store", new { name, timeZone = "Mars/Olympus" })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Put(pos.Owner, $"{S}/store", new { name, organizationBin = "12" })).Status);
        (await pos.Put(pos.Owner, $"{S}/store", new { name, timeZone = "Asia/Almaty", address = updated["address"]!.GetValue<string>(), phone = updated["phone"]!.GetValue<string>() })).EnsureOk();
    }

    [Fact]
    public async Task Owner_adds_a_store_and_cannot_disable_the_current_or_last_one()
    {
        var pos = await api.NewDeviceAsync(activateRegister: false);
        var before = (await pos.Get(pos.Owner, $"{S}/stores")).Ok.AsArray();
        var current = before.Single(s => s!["isCurrent"]!.GetValue<bool>())!;
        Assert.True(current["registers"]!.GetValue<int>() >= 1);
        Assert.True(current["staff"]!.GetValue<int>() >= 2);

        var created = (await pos.Post(pos.Owner, $"{S}/stores", new { name = "Филиал " + Guid.NewGuid().ToString("N")[..4], address = "Астана", timeZone = "Asia/Almaty" })).Ok;
        var id = created["id"]!.GetValue<Guid>();
        Assert.Equal(1, created["registers"]!.GetValue<int>()); // «Касса №1»
        Assert.True(created["staff"]!.GetValue<int>() >= 1); // иесі бірден кіреді
        Assert.NotEmpty(created["joinCode"]!.GetValue<string>());
        var me = (await pos.Get(pos.Owner, "/api/staff/v1/auth/me")).Ok;
        Assert.Contains(me["stores"]!.AsArray(), s => s!["id"]!.GetValue<Guid>() == id);

        // Ағымдағы дүкен — X-Store-Id (қосымша әрқашан жібереді); онсыз сервер атауы бойынша біріншісін алады.
        var currentId = current["id"]!.GetValue<string>();
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Send(HttpMethod.Post, pos.Owner, $"{S}/stores/{currentId}/active", new { active = false }, storeId: currentId)).Status); // ашық тұрған дүкен
        Assert.False((await pos.Send(HttpMethod.Post, pos.Owner, $"{S}/stores/{id}/active", new { active = false }, storeId: currentId)).Ok["isActive"]!.GetValue<bool>());
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Post(pos.Owner, $"{S}/stores", new { name = "X" })).Status);
    }

    [Fact]
    public async Task Registers_are_added_renamed_disabled_and_unbound_from_settings()
    {
        var pos = await api.NewDeviceAsync();
        var list = (await pos.Get(pos.Owner, $"{S}/registers")).Ok.AsArray();
        Assert.Contains(list, r => r!["isBound"]!.GetValue<bool>()); // фикстура құрылғысы байланған

        var created = (await pos.Post(pos.Owner, $"{S}/registers", new { name = "Касса №2 " + Guid.NewGuid().ToString("N")[..4], autoLockMinutes = 15 })).Ok;
        var id = created["id"]!.GetValue<Guid>();
        Assert.Equal(15, created["autoLockMinutes"]!.GetValue<int>());
        Assert.False(created["isBound"]!.GetValue<bool>());
        var renamed = (await pos.Put(pos.Owner, $"{S}/registers/{id}", new { name = "Бар", autoLockMinutes = (int?)null })).Ok;
        Assert.Equal("Бар", renamed["name"]!.GetValue<string>());
        Assert.Null(renamed["autoLockMinutes"]?.GetValue<int?>());
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Put(pos.Owner, $"{S}/registers/{id}", new { name = "Бар", autoLockMinutes = 1000 })).Status);

        Assert.False((await pos.Post(pos.Owner, $"{S}/registers/{id}/active", new { active = false })).Ok["isActive"]!.GetValue<bool>());
        Assert.DoesNotContain((await pos.Get(pos.Owner, "/api/pos/v1/registers")).Ok.AsArray(), r => r!["id"]!.GetValue<Guid>() == id); // кассада көрінбейді
        Assert.True((await pos.Post(pos.Owner, $"{S}/registers/{id}/active", new { active = true })).Ok["isActive"]!.GetValue<bool>());
        Assert.False((await pos.Post(pos.Owner, $"{S}/registers/{id}/unbind", new { })).Ok["isBound"]!.GetValue<bool>());
        Assert.Equal(HttpStatusCode.Forbidden, (await pos.Get(pos.Cashier, $"{S}/registers")).Status);
    }

    [Fact]
    public async Task Payment_methods_receipt_template_and_recipients_apply_to_receipts()
    {
        var pos = await api.NewDeviceAsync();
        var s = (await pos.Get(pos.Owner, $"{S}/cashier")).Ok;
        Assert.True(s["receiptShowAddress"]!.GetValue<bool>());
        var body = new
        {
            enabledMethods = new[] { "Cash", "Card", "Transfer" }, mixedEnabled = true, autoPrint = false, electronicReceipt = true,
            notifyOutOfStock = true, notifyLowStock = true, notifyDebt = false, notifyReturn = true, notifyCashierChange = true, lowStockThreshold = 5,
            receiptHeader = "ИП Абдужаббаров", receiptFooter = "Спасибо за покупку! Возврат — 14 дней.", receiptShowAddress = true, receiptShowPhone = false, receiptShowBin = true,
        };
        var saved = (await pos.Put(pos.Owner, $"{S}/cashier", body)).Ok;
        Assert.Equal(3, saved["enabledMethods"]!.AsArray().Count);
        Assert.Equal("ИП Абдужаббаров", saved["receiptHeader"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Put(pos.Owner, $"{S}/cashier", new { enabledMethods = new[] { "Debt" }, lowStockThreshold = 1 })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Put(pos.Owner, $"{S}/cashier", body with { receiptFooter = new string('x', 301) })).Status);

        var recipient = (await pos.Post(pos.Owner, $"{S}/recipients", new { bankName = "Kaspi", account = "+7 700 000 00 99", holderName = "Тест Т." })).Ok;
        Assert.Contains((await pos.Get(pos.Owner, $"{S}/cashier")).Ok["recipients"]!.AsArray(), r => r!["id"]!.GetValue<Guid>() == recipient["id"]!.GetValue<Guid>());

        // Чекте үлгі мен реквизиттер: БСН (settings/store тестінде қойылған болуы мүмкін), мекенжай бар, телефон жоқ.
        var product = await pos.CreateProductAsync(price: 1000, stock: 5);
        var sale = (await pos.SellAsync(pos.Owner, [Line(product, 1)], [Cash(1000)])).Ok;
        var receipt = (await pos.Get(pos.Owner, $"/api/pos/v1/sales/{sale["id"]}")).Ok;
        Assert.Equal("ИП Абдужаббаров", receipt["header"]!.GetValue<string>());
        Assert.Equal("Спасибо за покупку! Возврат — 14 дней.", receipt["footer"]!.GetValue<string>());
        Assert.Null(receipt["storePhone"]?.GetValue<string>());

        Assert.Equal(HttpStatusCode.NoContent, (await pos.Delete(pos.Owner, $"{S}/recipients/{recipient["id"]}")).Status);
        (await pos.Put(pos.Owner, $"{S}/cashier", body with { enabledMethods = new[] { "Cash", "Card", "Qr", "Transfer", "Debt" }, receiptHeader = (string?)null, receiptFooter = (string?)null, receiptShowPhone = true, receiptShowBin = false })).EnsureOk();
    }

    [Fact]
    public async Task Plan_shows_status_fee_and_modules()
    {
        var pos = await api.NewDeviceAsync(activateRegister: false);
        var plan = (await pos.Get(pos.Owner, $"{S}/plan")).Ok;
        Assert.Contains(plan["status"]!.GetValue<string>(), new[] { "trial", "active", "dueSoon", "overdue", "unpaid" });
        Assert.Equal(8, plan["modules"]!.AsArray().Count);
        Assert.All(plan["modules"]!.AsArray(), m => Assert.True(m!["enabled"]!.GetValue<bool>()));
        Assert.StartsWith("https://salemtech.kz", plan["supportUrl"]!.GetValue<string>());
    }
}
