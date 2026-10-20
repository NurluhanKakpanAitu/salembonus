using System.Net;
using System.Text.Json.Nodes;

namespace SalemBonus.Api.Tests;

/// <summary>
/// «Бонусы» (ТЗ «Бонусы» v1.0, §19 — 16 міндетті сценарий): мәртебелер, жану (партия бойынша, қайталанбайды), туған күн (жылына бір рет,
/// шарт орындалмаса — себебімен), тарату (өткен уақыт, келісім, қайталанбау), қолмен шегеру, бонус қайтару, сторно, статистика, экспорт, құқықтар.
/// Дүкен басқа тесттермен ортақ — әр тест өз клиентін жасайды, ортақ баптауларды соңында қалпына келтіреді.
/// </summary>
[Collection(ApiCollection.Name)]
public class BonusTests(ApiFixture api)
{
    private const string B = "/api/pos/v1/bonus";

    private static DateTime LocalNow => ApiFixture.StoreNow;
    private static string Today => DateOnly.FromDateTime(LocalNow).ToString("yyyy-MM-dd");

    private static object Status(string name, string rule, decimal percent, decimal? amount = null, int? count = null, bool active = true) => new
    {
        name, icon = "star", color = "#1769FF", rule, thresholdAmount = amount, thresholdCount = count, period = "AllTime", accrualPercent = percent,
        countReturns = true, validityDays = (int?)null, autoDowngrade = false, description = "Тест", isActive = active,
    };

    private static async Task<Guid> CustomerAsync(Pos pos, DateOnly? birthDate = null)
    {
        var phone = "+7708" + Random.Shared.Next(1_000_000, 9_999_999);
        var res = await pos.Post(pos.Owner, "/api/pos/v1/cashier/customers",
            new { phone, firstName = "Бонус", lastName = "Тест" + Random.Shared.Next(100, 999), birthDate = (birthDate ?? new DateOnly(1990, 5, 17)).ToString("yyyy-MM-dd") });
        return res.Ok["id"]!.GetValue<Guid>();
    }

    private static async Task<JsonNode> CardAsync(Pos pos, Guid customerId) => (await pos.Get(pos.Owner, $"{B}/customers/{customerId}")).Ok;
    private static async Task<int> BalanceAsync(Pos pos, Guid customerId) => (await CardAsync(pos, customerId))["customer"]!["balance"]!.GetValue<int>();

    private static Task<ApiResult> AccrueAsync(Pos pos, Guid customerId, int amount, int? validityDays = null, Guid? key = null) =>
        pos.Post(pos.Owner, $"{B}/operations/accrue", new { customerId, amount, reason = "Компенсация", validityDays, unlimited = false, clientRequestId = key });

    private static async Task<JsonArray> OperationsAsync(Pos pos, Guid customerId, string? type = null) =>
        (await pos.Get(pos.Owner, $"{B}/operations?customerId={customerId}&pageSize=100{(type is null ? "" : $"&type={type}")}")).Ok["items"]!.AsArray();

    // ---------- Тест 1–3: мәртебелер ----------

    [Fact]
    public async Task Statuses_are_created_unique_deactivated_without_losing_history_and_drive_accrual()
    {
        var pos = await api.NewDeviceAsync();
        var name = "Тест-статус " + Guid.NewGuid().ToString("N")[..6];
        var created = (await pos.Post(pos.Owner, $"{B}/statuses", Status(name, "PurchaseAmount", 49, amount: 1000))).Ok;
        var id = created["id"]!.GetValue<Guid>();
        try
        {
            Assert.Equal(49m, created["accrualPercent"]!.GetValue<decimal>());
            Assert.Contains((await pos.Get(pos.Owner, $"{B}/statuses")).Ok.AsArray(), s => s!["id"]!.GetValue<Guid>() == id); // тест 1

            var duplicate = await pos.Post(pos.Owner, $"{B}/statuses", Status(name.ToUpperInvariant(), "Manual", 3));
            Assert.Equal(HttpStatusCode.BadRequest, duplicate.Status); // тест 2
            Assert.Equal(HttpStatusCode.BadRequest, (await pos.Post(pos.Owner, $"{B}/statuses", Status("X", "Manual", 3))).Status);
            Assert.Equal(HttpStatusCode.BadRequest, (await pos.Post(pos.Owner, $"{B}/statuses", Status(name + "2", "PurchaseAmount", 3))).Status); // шегі жоқ
            Assert.Equal(HttpStatusCode.BadRequest, (await pos.Post(pos.Owner, $"{B}/statuses", Status(name + "3", "Manual", 51))).Status);

            // Сатып алу шегі орындалды → мәртебе көтеріледі; осы чек ескі пайызбен, келесі чек — жаңа пайызбен (артқа санап есептелмейді).
            var customer = await CustomerAsync(pos);
            var product = await pos.CreateProductAsync(price: 1500, stock: 10);
            (await pos.SellAsync(pos.Owner, [Pos.Line(product, 1)], [Pos.Cash(1500)], customerId: customer)).EnsureOk();
            var card = await CardAsync(pos, customer);
            Assert.Equal(id, card["customer"]!["statusId"]!.GetValue<Guid>());
            var second = (await pos.SellAsync(pos.Owner, [Pos.Line(product, 1)], [Pos.Cash(1500)], customerId: customer)).Ok;
            Assert.Equal(735, second["bonusAccrued"]!.GetValue<int>()); // 1500 × 49%
            var accrual = (await OperationsAsync(pos, customer, "Accrual")).First(o => o!["amount"]!.GetValue<int>() == 735)!;
            Assert.Equal(49m, accrual["percent"]!.GetValue<decimal>());
            Assert.Equal(BalanceAfter(accrual), await BalanceAsync(pos, customer));

            // Тест 3: қолданылатын мәртебе өшіріледі — клиент өз мәртебесінде қалады, жою — тыйым, тарих сақталған.
            var off = (await pos.Post(pos.Owner, $"{B}/statuses/{id}/deactivate", new { })).Ok;
            Assert.False(off["isActive"]!.GetValue<bool>());
            Assert.Equal(1, off["customers"]!.GetValue<int>());
            Assert.Equal(id, (await CardAsync(pos, customer))["customer"]!["statusId"]!.GetValue<Guid>());
            Assert.Equal(HttpStatusCode.BadRequest, (await pos.Delete(pos.Owner, $"{B}/statuses/{id}")).Status);
            Assert.NotEmpty(await OperationsAsync(pos, customer));

            // Көшірме — өшірулі, атауы бөлек.
            var copy = (await pos.Post(pos.Owner, $"{B}/statuses/{id}/duplicate", new { })).Ok;
            Assert.False(copy["isActive"]!.GetValue<bool>());
            Assert.NotEqual(name, copy["name"]!.GetValue<string>());
            Assert.Equal(HttpStatusCode.NoContent, (await pos.Delete(pos.Owner, $"{B}/statuses/{copy["id"]}")).Status);

            // Қолмен өзгерту — себеп міндетті; «по правилам» — қайта бағалау.
            Assert.Equal(HttpStatusCode.BadRequest, (await pos.Post(pos.Owner, $"{B}/customers/{customer}/status", new { mode = "manual", statusId = id })).Status);
            var back = (await pos.Post(pos.Owner, $"{B}/customers/{customer}/status", new { mode = "rules" })).Ok;
            Assert.NotEqual(id, back["customer"]!["statusId"]!.GetValue<Guid>());
        }
        finally
        {
            await pos.Delete(pos.Owner, $"{B}/statuses/{id}");
        }
        Assert.DoesNotContain((await pos.Get(pos.Owner, $"{B}/statuses")).Ok.AsArray(), s => s!["id"]!.GetValue<Guid>() == id);
    }

    private static int BalanceAfter(JsonNode op) => op["balanceAfter"]!.GetValue<int>();

    // ---------- Тест 4–5: партия мерзімі, жану қайталанбайды ----------

    [Fact]
    public async Task Lot_gets_its_own_expiry_and_burning_twice_creates_one_expiration()
    {
        var pos = await api.NewDeviceAsync();
        var before = (await pos.Get(pos.Owner, $"{B}/expiry/settings")).Ok;
        (await pos.Put(pos.Owner, $"{B}/expiry/settings", new { enabled = true, value = 12, unit = "Months", warnEnabled = false, applyToExisting = false })).EnsureOk();
        try
        {
            var customer = await CustomerAsync(pos);
            var op = (await AccrueAsync(pos, customer, 700, validityDays: 30)).Ok; // тест 4
            var lot = (await CardAsync(pos, customer))["lots"]!.AsArray().Single(l => l!["id"]!.GetValue<Guid>() == op["id"]!.GetValue<Guid>())!;
            var expires = lot["expiresAt"]!.GetValue<DateTime>().ToUniversalTime();
            Assert.InRange(expires, DateTime.UtcNow.AddDays(30).AddMinutes(-5), DateTime.UtcNow.AddDays(30).AddMinutes(5));
            Assert.Equal(700, await BalanceAsync(pos, customer));
            var upcoming = (await pos.Get(pos.Owner, $"{B}/expiry/upcoming?days=31&pageSize=100")).Ok;
            Assert.Contains(upcoming["items"]!.AsArray(), i => i!["lotId"]!.GetValue<Guid>() == op["id"]!.GetValue<Guid>());

            // Мерзімі өтті → фондық тапсырма екі рет → бір жану операциясы, баланс бір рет азаяды (тест 5).
            await api.SqlAsync($"UPDATE \"BonusTransactions\" SET \"ExpiresAt\" = now() - interval '1 minute' WHERE \"Id\" = '{op["id"]}'");
            (await pos.Post(pos.Owner, $"{B}/jobs/run", new { })).EnsureOk();
            (await pos.Post(pos.Owner, $"{B}/jobs/run", new { })).EnsureOk();
            var burned = await OperationsAsync(pos, customer, "Expiration");
            var expiration = Assert.Single(burned)!;
            Assert.Equal(-700, expiration["amount"]!.GetValue<int>());
            Assert.Equal(op["id"]!.GetValue<Guid>(), expiration["sourceTransactionId"]!.GetValue<Guid>());
            Assert.Equal(0, await BalanceAsync(pos, customer));

            // Қолмен мерзім өзгерту — себеп міндетті, өткен күн — тыйым.
            var other = (await AccrueAsync(pos, customer, 50, validityDays: 10)).Ok;
            Assert.Equal(HttpStatusCode.BadRequest, (await pos.Put(pos.Owner, $"{B}/lots/{other["id"]}/expiry", new { expiresAt = DateTime.UtcNow.AddDays(20) })).Status);
            Assert.Equal(HttpStatusCode.BadRequest, (await pos.Put(pos.Owner, $"{B}/lots/{other["id"]}/expiry", new { expiresAt = DateTime.UtcNow.AddDays(-1), reason = "Акция" })).Status);
            Assert.Equal(HttpStatusCode.NoContent, (await pos.Put(pos.Owner, $"{B}/lots/{other["id"]}/expiry", new { expiresAt = DateTime.UtcNow.AddDays(20), reason = "Продлили по акции" })).Status);
        }
        finally
        {
            (await pos.Put(pos.Owner, $"{B}/expiry/settings", new
            {
                enabled = before["enabled"]!.GetValue<bool>(), value = before["value"]!.GetValue<int>(), unit = before["unit"]!.GetValue<string>(),
                warnEnabled = false, applyToExisting = false,
            })).EnsureOk();
        }
    }

    // ---------- Тест 6–8: туған күн ----------

    [Fact]
    public async Task Birthday_gift_is_granted_once_a_year_and_skipped_with_a_reason()
    {
        var pos = await api.NewDeviceAsync();
        var today = DateOnly.FromDateTime(LocalNow);
        var born = new DateOnly(1990, today.Month, today.Month == 2 && today.Day == 29 ? 28 : today.Day);
        var meta = (await pos.Get(pos.Owner, $"{B}/meta")).Ok;
        var statuses = meta["statuses"]!.AsArray();
        var registration = statuses.First(s => s!["rule"]!.GetValue<string>() == "Registration")!["id"]!.GetValue<Guid>();
        var vip = statuses.Last(s => s!["isActive"]!.GetValue<bool>() && s["rule"]!.GetValue<string>() != "Registration")!["id"]!.GetValue<Guid>();

        var eligible = await CustomerAsync(pos, born);
        var other = await CustomerAsync(pos, born);
        (await pos.Post(pos.Owner, $"{B}/customers/{eligible}/status", new { mode = "manual", statusId = vip, reason = "Постоянный покупатель" })).EnsureOk();

        // Сома нөл — ережені қосуға болмайды; «До дня рождения» тек туған күннен бұрын.
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Put(pos.Owner, $"{B}/birthday/settings", Rule(0, [vip]))).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Put(pos.Owner, $"{B}/birthday/settings", Rule(5000, [vip], validity: "UntilBirthday"))).Status);
        (await pos.Put(pos.Owner, $"{B}/birthday/settings", Rule(5000, [vip]))).EnsureOk();
        try
        {
            var preview = (await pos.Get(pos.Owner, $"{B}/birthday/preview?customerId={eligible}")).Ok;
            Assert.Contains("5", preview["message"]!.GetValue<string>());

            (await pos.Post(pos.Owner, $"{B}/jobs/run", new { })).EnsureOk();
            (await pos.Post(pos.Owner, $"{B}/jobs/run", new { })).EnsureOk(); // тест 7: қайта өңдеу

            var gifts = await OperationsAsync(pos, eligible, "Birthday");
            var gift = Assert.Single(gifts)!; // тест 6
            Assert.Equal(5000, gift["amount"]!.GetValue<int>());
            Assert.InRange(gift["expiresAt"]!.GetValue<DateTime>().ToUniversalTime(), DateTime.UtcNow.AddDays(3).AddMinutes(-5), DateTime.UtcNow.AddDays(3).AddMinutes(5));
            Assert.Empty(await OperationsAsync(pos, other, "Birthday")); // тест 8: мәртебе сәйкес емес

            var list = (await pos.Get(pos.Owner, $"{B}/birthday/upcoming?days=1&pageSize=100")).Ok["items"]!.AsArray();
            Assert.Equal("granted", list.First(i => i!["customerId"]!.GetValue<Guid>() == eligible)!["state"]!.GetValue<string>());
            Assert.Equal("status", list.First(i => i!["customerId"]!.GetValue<Guid>() == other)!["state"]!.GetValue<string>());

            // Қолмен есептеу: сол жылы екінші рет — тыйым; өткізілгенге — себеппен рұқсат.
            Assert.Equal(HttpStatusCode.BadRequest, (await pos.Post(pos.Owner, $"{B}/birthday/grants", new { customerId = eligible, reason = "Повторно" })).Status);
            Assert.Equal(HttpStatusCode.BadRequest, (await pos.Post(pos.Owner, $"{B}/birthday/grants", new { customerId = other })).Status);
            (await pos.Post(pos.Owner, $"{B}/birthday/grants", new { customerId = other, reason = "Исключение по решению владельца" })).EnsureOk();
            Assert.Single(await OperationsAsync(pos, other, "Birthday"));
        }
        finally
        {
            (await pos.Put(pos.Owner, $"{B}/birthday/settings", Rule(5000, [registration], enabled: false))).EnsureOk();
        }
    }

    private static object Rule(int amount, Guid[] statusIds, bool enabled = true, string validity = "DaysAfterAccrual") => new
    {
        enabled, amount, timing = "OnDay", timingDays = 0, time = "00:00", validity, validityDays = 3, statusIds, purchasePeriodDays = 365,
        message = "{{Имя}}, дарим {{Бонусы}} бонусов до {{ДатаСгорания}}",
    };

    // ---------- Тест 9–10: тарату ----------

    [Fact]
    public async Task Campaign_cannot_be_scheduled_in_the_past_excludes_opted_out_and_sends_once()
    {
        var pos = await api.NewDeviceAsync();
        var a = await CustomerAsync(pos);
        var b = await CustomerAsync(pos);
        (await pos.Post(pos.Owner, $"{B}/customers/{b}/opt-out", new { optOut = true, reason = "Попросил не писать" })).EnsureOk();
        var body = new
        {
            name = "Осенняя акция", type = "Gift", channel = "app", audienceMode = "Manual", customerIds = new[] { a, b },
            message = "{{Имя}}, дарим {{Бонусы}} бонусов!", bonusAmount = 100, bonusValidityDays = 30,
        };
        var whatsapp = await pos.Post(pos.Owner, $"{B}/campaigns", new { body.name, body.type, channel = "whatsapp", body.audienceMode, body.customerIds, body.message });
        Assert.Equal(HttpStatusCode.BadRequest, whatsapp.Status); // қосылмаған арна

        var campaign = (await pos.Post(pos.Owner, $"{B}/campaigns", body)).Ok;
        var id = campaign["id"]!.GetValue<Guid>();
        var past = await pos.Post(pos.Owner, $"{B}/campaigns/{id}/schedule", new { scheduledAt = DateTime.UtcNow.AddMinutes(-10) });
        Assert.Equal(HttpStatusCode.BadRequest, past.Status); // тест 9

        var audience = (await pos.Post(pos.Owner, $"{B}/campaigns/audience", body)).Ok;
        Assert.Equal(1, audience["eligible"]!.GetValue<int>());
        Assert.Equal("optOut", audience["reasons"]![0]!["reason"]!.GetValue<string>());

        var test = (await pos.Post(pos.Owner, $"{B}/campaigns/{id}/test", new { customerId = a })).Ok;
        Assert.Contains("100", test["message"]!.GetValue<string>());
        Assert.Equal(0, await BalanceAsync(pos, a)); // тест бонус бермейді

        var sent = (await pos.Post(pos.Owner, $"{B}/campaigns/{id}/send", new { })).Ok;
        var again = (await pos.Post(pos.Owner, $"{B}/campaigns/{id}/send", new { })).Ok;
        Assert.Equal("Completed", sent["status"]!.GetValue<string>());
        Assert.Equal(1, again["sent"]!.GetValue<int>());
        Assert.Equal(1, again["excluded"]!.GetValue<int>());
        Assert.Equal(100, await BalanceAsync(pos, a)); // бір рет
        Assert.Equal(0, await BalanceAsync(pos, b)); // тест 10
        var recipients = (await pos.Get(pos.Owner, $"{B}/campaigns/{id}/recipients")).Ok["items"]!.AsArray();
        Assert.Equal("optOut", recipients.Single(r => r!["customerId"]!.GetValue<Guid>() == b)!["reason"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Post(pos.Owner, $"{B}/campaigns/{id}/cancel", new { })).Status);
        var promo = Assert.Single(await OperationsAsync(pos, a, "Promo"))!;
        Assert.Equal(id, promo["sourceId"]!.GetValue<Guid>());
    }

    // ---------- Тест 11–13: шегеру, қайтару, сторно ----------

    [Fact]
    public async Task Write_off_return_and_reversal_keep_the_balance_consistent()
    {
        var pos = await api.NewDeviceAsync();
        var customer = await CustomerAsync(pos);
        var key = Guid.NewGuid();
        var accrued = (await AccrueAsync(pos, customer, 1000, key: key)).Ok;
        var repeat = (await AccrueAsync(pos, customer, 1000, key: key)).Ok;
        Assert.Equal(accrued["id"]!.GetValue<Guid>(), repeat["id"]!.GetValue<Guid>()); // қайталанған сұраныс
        Assert.Equal(1000, await BalanceAsync(pos, customer));
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Post(pos.Owner, $"{B}/operations/accrue", new { customerId = customer, amount = 10 })).Status); // себепсіз

        var tooMuch = await pos.Post(pos.Owner, $"{B}/operations/write-off", new { customerId = customer, amount = 1001, reason = "Ошибка" });
        Assert.Equal(HttpStatusCode.BadRequest, tooMuch.Status); // тест 11
        Assert.Equal(1000, await BalanceAsync(pos, customer));

        // Сатылымда 300 бонус жұмсалды → қайтару: 200, тағы 200 — тыйым (тек 100 қалды), 100 — болады (тест 12).
        var product = await pos.CreateProductAsync(price: 2000, stock: 5);
        (await pos.SellAsync(pos.Owner, [Pos.Line(product, 1)], [Pos.Cash(1700)], customerId: customer, bonusRedeem: 300)).EnsureOk();
        var redemption = (await OperationsAsync(pos, customer, "Redemption")).Single()!;
        var rid = redemption["id"]!.GetValue<Guid>();
        Assert.True(redemption["canReturn"]!.GetValue<bool>());
        (await pos.Post(pos.Owner, $"{B}/operations/{rid}/return", new { amount = 200, reason = "Ошибка кассира" })).EnsureOk();
        var over = await pos.Post(pos.Owner, $"{B}/operations/{rid}/return", new { amount = 200, reason = "Ещё раз" });
        Assert.Equal(HttpStatusCode.BadRequest, over.Status);
        (await pos.Post(pos.Owner, $"{B}/operations/{rid}/return", new { amount = 100, reason = "Остаток" })).EnsureOk();
        Assert.Equal(0, (await pos.Get(pos.Owner, $"{B}/operations/{rid}")).Ok["returnableAmount"]!.GetValue<int>());

        // Сторно (тест 13): түпнұсқа қалады, байланысты кері операция пайда болады, екінші сторно — тыйым.
        var gift = (await AccrueAsync(pos, customer, 500)).Ok;
        var balance = await BalanceAsync(pos, customer);
        var reversal = (await pos.Post(pos.Owner, $"{B}/operations/{gift["id"]}/reverse", new { reason = "Начислили по ошибке" })).Ok;
        Assert.Equal("Reversal", reversal["type"]!.GetValue<string>());
        Assert.Equal(-500, reversal["amount"]!.GetValue<int>());
        Assert.Equal(gift["id"]!.GetValue<Guid>(), reversal["sourceTransactionId"]!.GetValue<Guid>());
        Assert.Equal(balance - 500, await BalanceAsync(pos, customer));
        var detail = (await pos.Get(pos.Owner, $"{B}/operations/{gift["id"]}")).Ok;
        Assert.Equal("reversed", detail["operation"]!["status"]!.GetValue<string>());
        Assert.Contains(detail["related"]!.AsArray(), r => r!["id"]!.GetValue<Guid>() == reversal["id"]!.GetValue<Guid>());
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Post(pos.Owner, $"{B}/operations/{gift["id"]}/reverse", new { reason = "Ещё" })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Post(pos.Owner, $"{B}/operations/{rid}/reverse", new { reason = "Чек" })).Status); // сатылым — қайтарым арқылы

        // Жұмсалған партияның сторносы — тыйым (теріс баланс болмайды).
        var spent = (await AccrueAsync(pos, customer, 50)).Ok;
        (await pos.Post(pos.Owner, $"{B}/operations/write-off", new { customerId = customer, amount = await BalanceAsync(pos, customer), reason = "Обнуление" })).EnsureOk();
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Post(pos.Owner, $"{B}/operations/{spent["id"]}/reverse", new { reason = "Ошибка" })).Status);
        Assert.Equal(0, await BalanceAsync(pos, customer));
    }

    // ---------- Тест 14–16: статистика, экспорт, құқықтар ----------

    [Fact]
    public async Task Statistics_use_one_period_export_respects_filters_and_rights_are_checked_on_the_server()
    {
        var pos = await api.NewDeviceAsync();
        var customer = await CustomerAsync(pos);
        (await AccrueAsync(pos, customer, 321)).EnsureOk();
        var from = DateOnly.FromDateTime(LocalNow).AddDays(-6).ToString("yyyy-MM-dd");

        var stats = (await pos.Get(pos.Owner, $"{B}/statistics?from={from}&to={Today}")).Ok; // тест 14
        Assert.Equal(7, stats["days"]!.AsArray().Count);
        var kpi = stats["kpis"]!.AsArray().ToDictionary(k => k!["key"]!.GetValue<string>(), k => k!["value"]);
        Assert.Equal(stats["days"]!.AsArray().Sum(d => d!["accrued"]!.GetValue<int>()), kpi["accrued"]!.GetValue<decimal>());
        Assert.Equal(stats["days"]!.AsArray().Sum(d => d!["used"]!.GetValue<int>()), kpi["used"]!.GetValue<decimal>());
        var overview = (await pos.Get(pos.Owner, $"{B}/overview?from={from}&to={Today}")).Ok;
        Assert.Equal(7, overview["days"]!.AsArray().Count);
        var ov = overview["kpis"]!.AsArray().ToDictionary(k => k!["key"]!.GetValue<string>(), k => k!["value"]!.GetValue<decimal>());
        Assert.Equal(kpi["accrued"]!.GetValue<decimal>(), ov["accrued"]);
        Assert.True(overview["kpis"]!.AsArray().First(k => k!["key"]!.GetValue<string>() == "balance")!["asOfNow"]!.GetValue<bool>());

        // Тест 15: сүзгімен экспорт.
        var xlsx = await pos.Raw(pos.Owner, $"{B}/export?report=operations&customerId={customer}");
        Assert.Equal(HttpStatusCode.OK, xlsx.Status);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", xlsx.ContentType);
        var csv = await pos.Raw(pos.Owner, $"{B}/export?report=operations&format=csv&customerId={customer}");
        Assert.StartsWith("text/csv", csv.ContentType);
        Assert.Equal(HttpStatusCode.OK, (await pos.Raw(pos.Owner, $"{B}/export?report=statistics&from={from}&to={Today}")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await pos.Raw(pos.Owner, $"{B}/export?report=nope")).Status);

        // Тест 16 және кассир: бөлім мен әрекеттер серверде жабық.
        Assert.Equal(HttpStatusCode.Forbidden, (await pos.Put(pos.Cashier, $"{B}/terms/draft", new { content = "<p>Правила</p>" })).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await pos.Post(pos.Cashier, $"{B}/terms/publish", new { })).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await pos.Get(pos.Cashier, $"{B}/overview")).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await pos.Post(pos.Cashier, $"{B}/operations/accrue", new { customerId = customer, amount = 10, reason = "Тест" })).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await pos.Raw(pos.Cashier, $"{B}/export?report=operations")).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await pos.Get(pos.Owner, $"{B}/overview?storeIds={Guid.NewGuid()}")).Status);
    }

    [Fact]
    public async Task Terms_are_sanitized_published_as_versions_and_restored_into_a_new_draft()
    {
        var pos = await api.NewDeviceAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Put(pos.Owner, $"{B}/terms/draft", new { content = "<p> </p>" })).Status);
        var draft = (await pos.Put(pos.Owner, $"{B}/terms/draft", new { content = "<h2>Правила</h2><p onclick=\"x()\">Текст <b>жирный</b><script>alert(1)</script></p><a href=\"javascript:alert(1)\">ссылка</a>" })).Ok;
        var content = draft["content"]!.GetValue<string>();
        Assert.DoesNotContain("script", content);
        Assert.DoesNotContain("onclick", content);
        Assert.DoesNotContain("javascript:", content);
        Assert.Contains("<b>жирный</b>", content);

        var v1 = (await pos.Post(pos.Owner, $"{B}/terms/publish", new { comment = "Первая версия" })).Ok;
        Assert.Equal("Published", v1["status"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Post(pos.Owner, $"{B}/terms/publish", new { })).Status); // черновик жоқ
        (await pos.Put(pos.Owner, $"{B}/terms/draft", new { content = "<p>Вторая редакция</p>" })).EnsureOk();
        var v2 = (await pos.Post(pos.Owner, $"{B}/terms/publish", new { })).Ok;
        Assert.Equal(v1["version"]!.GetValue<int>() + 1, v2["version"]!.GetValue<int>());

        var state = (await pos.Get(pos.Owner, $"{B}/terms")).Ok;
        Assert.Equal(v2["id"]!.GetValue<Guid>(), state["published"]!["id"]!.GetValue<Guid>());
        Assert.Equal("Archived", state["versions"]!.AsArray().First(v => v!["id"]!.GetValue<Guid>() == v1["id"]!.GetValue<Guid>())!["status"]!.GetValue<string>());
        var restored = (await pos.Post(pos.Owner, $"{B}/terms/{v1["id"]}/restore", new { })).Ok;
        Assert.Equal("Draft", restored["status"]!.GetValue<string>());
        Assert.Equal(content, restored["content"]!.GetValue<string>());
        Assert.Equal(v2["id"]!.GetValue<Guid>(), (await pos.Get(pos.Owner, $"{B}/terms")).Ok["published"]!["id"]!.GetValue<Guid>()); // жарияланған өзгермеді
    }
}
