using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace SalemBonus.Api.Tests;

/// <summary>
/// «Клиенты» (ТЗ «Клиенты» v1.0, §26 — міндетті сценарийлер): қосу мен тексеру, телефон телігі, өзгерту тарихы, аты/телефон/ID бойынша іздеу,
/// KPI мен сүзгінің сәйкестігі, динамикалық сегмент, ішінара төлем қарызды азайтады (түсім емес), бонус қалдығы «Бонусы»-мен бірдей,
/// құқықсыз әрекет жабық, экспорт сүзгі мен құқықты сақтайды, импорт растауға дейін телік пен қатені көрсетеді және қайталанбайды,
/// мұрағат тарихты сақтайды, біріктіру қарыз бен бонусты екі еселемейді, хабар келісімсіз/интеграциясыз жіберілмейді.
/// Дүкен басқа тесттермен ортақ — әр тест өз клиенттерін бірегей тегпен жасайды; ұйым баптауларын соңында қалпына келтіреді.
/// </summary>
[Collection(ApiCollection.Name)]
public class ClientsTests(ApiFixture api)
{
    private const string C = "/api/pos/v1/clients";

    private static string Phone() => "+7709" + Random.Shared.Next(1_000_000, 9_999_999);
    private static string Tag() => "Кл" + Guid.NewGuid().ToString("N")[..6];
    private static object Debt(decimal amount) => new { method = "Debt", amount, dueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(7)) };

    private static object NewClient(string phone, string first, string last, string? birthDate = "1992-03-04", object? customFields = null, bool consentApp = false) =>
        new { phone, firstName = first, lastName = last, birthDate, gender = "female", email = $"t{Random.Shared.Next(1000, 9999)}@example.kz", consentApp, customFields };

    private static async Task<JsonNode> CreateAsync(Pos pos, string last, string first = "Айгерим", string? phone = null) =>
        (await pos.Post(pos.Owner, C, NewClient(phone ?? Phone(), first, last))).Ok;

    private static Guid Id(JsonNode card) => card["client"]!["id"]!.GetValue<Guid>();
    private static Guid CustomerId(JsonNode card) => card["client"]!["customerId"]!.GetValue<Guid>();

    private static async Task<JsonNode> SearchAsync(Pos pos, object filter, Guid? segmentId = null, string? token = null) =>
        (await pos.Post(token ?? pos.Owner, $"{C}/search", new { filter, segmentId, pageSize = 200 })).Ok;

    private static async Task<int> TotalAsync(Pos pos, object filter, Guid? segmentId = null) => (await SearchAsync(pos, filter, segmentId))["total"]!.GetValue<int>();

    private static async Task<JsonNode> CardAsync(Pos pos, Guid id) => (await pos.Get(pos.Owner, $"{C}/{id}")).Ok;

    private static async Task<JsonArray> HistoryAsync(Pos pos, Guid id) => (await pos.Get(pos.Owner, $"{C}/{id}/history")).Ok.AsArray();

    private static Task<ApiResult> AccrueAsync(Pos pos, Guid customerId, int amount) =>
        pos.Post(pos.Owner, "/api/pos/v1/bonus/operations/accrue", new { customerId, amount, reason = "Компенсация", validityDays = (int?)null, unlimited = false });

    private static async Task<int> BonusModuleBalanceAsync(Pos pos, Guid customerId) =>
        (await pos.Get(pos.Owner, $"/api/pos/v1/bonus/customers/{customerId}")).Ok["customer"]!["balance"]!.GetValue<int>();

    private Task GiveAppAsync(Guid customerId) =>
        api.SqlAsync($"""INSERT INTO "RefreshTokens" ("Id", "CustomerId", "TokenHash", "ExpiresAt", "CreatedAt") VALUES (gen_random_uuid(), '{customerId}', md5(random()::text), now() + interval '1 day', now())""");

    // ---------- §26.1–3: қосу, тексеру, телік, іздеу ----------

    [Fact]
    public async Task Create_validates_fields_rejects_duplicate_phone_and_is_found_by_name_phone_and_id()
    {
        var pos = await api.NewDeviceAsync();
        var tag = Tag();
        var phone = Phone();
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Post(pos.Owner, C, NewClient(phone, "", tag))).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Post(pos.Owner, C, NewClient("12345", "Айгерим", tag))).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Post(pos.Owner, C, NewClient(phone, "Айгерим", ""))).Status); // тегі міндетті (әдепкі)
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Post(pos.Owner, C, new { phone, firstName = "Айгерим", lastName = tag, email = "not-an-email" })).Status);

        var card = await CreateAsync(pos, tag, phone: phone);
        var client = card["client"]!;
        Assert.Equal("Manual", client["source"]!.GetValue<string>());
        Assert.True(client["hasCard"]!.GetValue<bool>());
        Assert.Equal("Female", client["gender"]!.GetValue<string>());
        var number = client["number"]!.GetValue<long>();
        Assert.True(number > 0);

        var duplicate = await pos.Post(pos.Owner, C, NewClient(phone.Replace("+7", "8"), "Другое", "Имя"));
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.Status);
        Assert.Contains(number.ToString("000000"), duplicate.Body!["detail"]!.GetValue<string>());
        var lookup = (await pos.Get(pos.Owner, $"{C}/lookup?phone={Uri.EscapeDataString(phone)}")).Ok;
        Assert.True(lookup["exists"]!.GetValue<bool>());
        Assert.Equal(Id(card), lookup["id"]!.GetValue<Guid>());

        Assert.Equal(1, await TotalAsync(pos, new { search = tag }));
        Assert.Equal(1, await TotalAsync(pos, new { search = tag.ToLowerInvariant() }));
        Assert.Equal(1, await TotalAsync(pos, new { search = "Айгерим " + tag }));
        var byPhone = await SearchAsync(pos, new { search = phone[^7..] });
        Assert.Contains(byPhone["rows"]!.AsArray(), r => r!["id"]!.GetValue<Guid>() == Id(card));
        Assert.Equal(0, await TotalAsync(pos, new { search = "Нет" + phone[^4..] })); // әріп аралас мәтіндегі сандар телефонмен салыстырылмайды
        var byNumber = await SearchAsync(pos, new { search = number.ToString("000000") });
        Assert.Contains(byNumber["rows"]!.AsArray(), r => r!["id"]!.GetValue<Guid>() == Id(card));

        // Кассада тіркелген клиентке карточка өзі жасалады («Касса» көзі) — қолмен қосу оны телік деп көрсетеді.
        var cashierCustomer = await pos.CreateCustomerAsync();
        var cashierPhone = cashierCustomer["phone"]!.GetValue<string>();
        var found = (await SearchAsync(pos, new { search = cashierPhone[^7..] }))["rows"]!.AsArray()
            .First(r => r!["customerId"]!.GetValue<Guid>() == cashierCustomer["id"]!.GetValue<Guid>())!;
        Assert.Equal("Cashier", found["source"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Post(pos.Owner, C, NewClient(cashierPhone, "Тест", "Клиент"))).Status);
    }

    // ---------- §26.4: өзгерту мен аудит ----------

    [Fact]
    public async Task Edit_writes_field_level_history_and_phone_of_app_user_is_locked()
    {
        var pos = await api.NewDeviceAsync();
        var tag = Tag();
        var card = await CreateAsync(pos, tag);
        var id = Id(card);
        Assert.True(card["canEditPhone"]!.GetValue<bool>());
        var newPhone = Phone();
        var updated = (await pos.Put(pos.Owner, $"{C}/{id}", new
        {
            phone = newPhone, firstName = "Динара", lastName = tag, birthDate = "1992-03-04", gender = "female", email = "dinara@example.kz", address = "Абая 1",
        })).Ok;
        Assert.Equal("Динара", updated["client"]!["firstName"]!.GetValue<string>());
        Assert.Equal(newPhone, updated["client"]!["phone"]!.GetValue<string>());
        Assert.Equal("Абая 1", updated["address"]!.GetValue<string>());

        var history = await HistoryAsync(pos, id);
        var change = history.First(h => h!["action"]!.GetValue<string>() == "client.update")!;
        var changes = change["changes"]!.AsArray();
        Assert.Contains(changes, x => x!["field"]!.GetValue<string>() == "firstName" && x["old"]!.GetValue<string>() == "Айгерим" && x["new"]!.GetValue<string>() == "Динара");
        Assert.Contains(changes, x => x!["field"]!.GetValue<string>() == "phone" && x["new"]!.GetValue<string>() == newPhone);
        Assert.DoesNotContain(changes, x => x!["field"]!.GetValue<string>() == "lastName");
        Assert.Contains(history, h => h!["action"]!.GetValue<string>() == "client.create");

        // SalemBonus қосымшасын қолданатын адамның телефонын бизнес өзгерте алмайды.
        await GiveAppAsync(CustomerId(card));
        Assert.False((await CardAsync(pos, id))["canEditPhone"]!.GetValue<bool>());
        var locked = await pos.Put(pos.Owner, $"{C}/{id}", new { phone = Phone(), firstName = "Динара", lastName = tag });
        Assert.Equal(HttpStatusCode.BadRequest, locked.Status);
        Assert.Equal(HttpStatusCode.OK, (await pos.Put(pos.Owner, $"{C}/{id}", new { phone = newPhone, firstName = "Динара", lastName = tag, comment = "VIP" })).Status);
    }

    // ---------- §26.5–6: KPI = сүзгі, динамикалық сегмент ----------

    [Fact]
    public async Task Kpis_equal_the_list_behind_each_quick_filter_and_segments_are_dynamic()
    {
        var pos = await api.NewDeviceAsync();
        var tag = Tag();
        var product = await pos.CreateProductAsync(price: 1000, stock: 20);
        var a = await CreateAsync(pos, tag + "а");
        var b = await CreateAsync(pos, tag + "б");
        for (var i = 0; i < 2; i++) (await pos.SellAsync(pos.Owner, [Pos.Line(product, 1)], [Pos.Cash(1000)], customerId: CustomerId(b))).EnsureOk();
        var debtor = await CreateAsync(pos, tag + "в");
        (await pos.SellAsync(pos.Owner, [Pos.Line(product, 1)], [Debt(1000)], customerId: CustomerId(debtor))).EnsureOk();

        var kpis = (await pos.Post(pos.Owner, $"{C}/kpis", new { })).Ok;
        Assert.Equal(kpis["total"]!.GetValue<int>(), await TotalAsync(pos, new { }));
        foreach (var (kpi, quick) in new[] { ("active", "active"), ("passive", "passive"), ("new", "new"), ("repeat", "repeat"), ("withDebt", "debt"), ("overdue", "overdue"), ("withBonus", "bonus") })
            Assert.True(kpis[kpi]!.GetValue<int>() == await TotalAsync(pos, new { quick }), kpi);

        var bRow = (await SearchAsync(pos, new { search = tag + "б" }))["rows"]![0]!;
        Assert.Equal(2, bRow["purchases"]!.GetValue<int>());
        Assert.Equal(2000m, bRow["amount"]!.GetValue<decimal>());
        Assert.Equal(1000m, bRow["average"]!.GetValue<decimal>());
        Assert.Equal("active", bRow["activity"]!.GetValue<string>());
        Assert.Equal(1, await TotalAsync(pos, new { search = tag, quick = "repeat" }));
        Assert.Equal(1, await TotalAsync(pos, new { search = tag, quick = "noPurchases" }));
        Assert.Equal(1, await TotalAsync(pos, new { search = tag, hasDebt = true }));
        Assert.Equal(1, await TotalAsync(pos, new { search = tag, minPurchases = 2, minAmount = 1500 }));

        // Сегмент шарттармен сақталады: клиент шартқа кейін сәйкес келсе — сегментте өзі пайда болады.
        var segment = (await pos.Post(pos.Owner, $"{C}/segments", new { name = "Постоянные " + tag, filter = new { search = tag, minPurchases = 3 }, isShared = false })).Ok;
        var segmentId = segment["id"]!.GetValue<Guid>();
        try
        {
            Assert.Equal(0, segment["count"]!.GetValue<int>());
            (await pos.SellAsync(pos.Owner, [Pos.Line(product, 1)], [Pos.Cash(1000)], customerId: CustomerId(b))).EnsureOk();
            var members = await SearchAsync(pos, new { }, segmentId);
            Assert.Equal(Id(b), Assert.Single(members["rows"]!.AsArray())!["id"]!.GetValue<Guid>());
            var added = (await pos.Post(pos.Owner, $"{C}/bulk/segment", new { ids = new[] { Id(a) }, segmentId })).Ok;
            Assert.Equal(1, added["processed"]!.GetValue<int>());
            var list = (await pos.Get(pos.Owner, $"{C}/segments")).Ok.AsArray();
            var stored = list.First(s => s!["id"]!.GetValue<Guid>() == segmentId)!;
            Assert.Equal(2, stored["count"]!.GetValue<int>());
            Assert.Equal(1, stored["included"]!.GetValue<int>());
            Assert.Equal(1, await TotalAsync(pos, new { quick = "noPurchases" }, segmentId)); // сегмент үстінен сүзгі
        }
        finally
        {
            Assert.Equal(HttpStatusCode.NoContent, (await pos.Delete(pos.Owner, $"{C}/segments/{segmentId}")).Status);
        }
    }

    // ---------- §26.7–8: ішінара төлем, бонус қалдығы, бөлімдер ----------

    [Fact]
    public async Task Partial_payment_reduces_client_debt_and_bonus_matches_the_bonus_module()
    {
        var pos = await api.NewDeviceAsync();
        var tag = Tag();
        var product = await pos.CreateProductAsync(price: 4000, stock: 5);
        var card = await CreateAsync(pos, tag);
        var id = Id(card);
        var customer = CustomerId(card);
        var sale = (await pos.SellAsync(pos.Owner, [Pos.Line(product, 1)], [Pos.Cash(1000), Debt(3000)], customerId: customer)).Ok;
        var debtId = sale["debt"]!["id"]!.GetValue<Guid>();
        (await AccrueAsync(pos, customer, 70)).EnsureOk();

        var before = await CardAsync(pos, id);
        Assert.Equal(3000m, before["client"]!["debt"]!.GetValue<decimal>());
        Assert.Equal(1, before["client"]!["purchases"]!.GetValue<int>());
        Assert.Equal(await BonusModuleBalanceAsync(pos, customer), before["client"]!["bonus"]!.GetValue<int>());
        Assert.Equal(before["client"]!["bonus"]!.GetValue<int>(), before["bonus"]!["balance"]!.GetValue<int>());
        Assert.Equal(3000m, (await pos.Get(pos.Owner, $"{C}/{id}/debts")).Ok["total"]!.GetValue<decimal>());

        var account = (await pos.Post(pos.Owner, "/api/pos/v1/finance/accounts", new
        {
            name = "Шот " + tag, type = "Cash", methods = Array.Empty<string>(), openingBalance = 0, openingDate = ApiFixture.StoreToday.ToString("yyyy-MM-dd"),
        })).Ok["id"]!.GetValue<Guid>();
        (await pos.Post(pos.Owner, "/api/pos/v1/finance/customer-debts/payments",
            new { customerId = customer, accountId = account, method = "Cash", allocations = new[] { new { debtId, amount = 1200m } } })).EnsureOk();

        var after = await CardAsync(pos, id);
        Assert.Equal(id, Id((await pos.Get(pos.Owner, $"{C}/by-customer/{customer}")).Ok));
        Assert.Equal(HttpStatusCode.NotFound, (await pos.Get(pos.Owner, $"{C}/by-customer/{Guid.NewGuid()}")).Status);
        Assert.Equal(1800m, after["client"]!["debt"]!.GetValue<decimal>());
        Assert.Equal(4000m, after["client"]!["amount"]!.GetValue<decimal>()); // төлем сатып алу сомасын өзгертпейді
        var debts = (await pos.Get(pos.Owner, $"{C}/{id}/debts")).Ok;
        Assert.Equal(1800m, debts["total"]!.GetValue<decimal>());
        Assert.Equal(1200m, Assert.Single(debts["payments"]!.AsArray())!["amount"]!.GetValue<decimal>());
        var accounts = (await pos.Get(pos.Owner, "/api/pos/v1/finance/accounts")).Ok["accounts"]!.AsArray();
        Assert.Equal(1200m, accounts.First(a => a!["id"]!.GetValue<Guid>() == account)!["balance"]!.GetValue<decimal>());
        var registry = (await pos.Get(pos.Owner, $"{C}/debts?search={Uri.EscapeDataString(tag)}")).Ok;
        Assert.Contains(registry["rows"]!.AsArray(), r => r!["customerId"]!.GetValue<Guid>() == customer && r["total"]!.GetValue<decimal>() == 1800m);

        var receipts = (await pos.Get(pos.Owner, $"{C}/{id}/receipts")).Ok.AsArray();
        Assert.Contains(receipts, r => r!["kind"]!.GetValue<string>() == "sale" && r["id"]!.GetValue<Guid>() == sale["id"]!.GetValue<Guid>());
        var documents = (await pos.Get(pos.Owner, $"{C}/{id}/documents")).Ok.AsArray();
        Assert.Contains(documents, d => d!["kind"]!.GetValue<string>() == "sale");
        Assert.Contains(documents, d => d!["kind"]!.GetValue<string>() == "finance" && d["amount"]!.GetValue<decimal>() == 1200m);
        var sales = (await pos.Get(pos.Owner, $"{C}/{id}/sales")).Ok;
        Assert.Equal(1, sales["total"]!.GetValue<int>());
        Assert.Contains("Debt", sales["rows"]![0]!["methods"]!.AsArray().Select(x => x!.GetValue<string>()));
        var activity = (await pos.Get(pos.Owner, $"{C}/{id}/activity")).Ok;
        Assert.Equal(1, activity["purchases30"]!.GetValue<int>());
        Assert.Equal(12, activity["months"]!.AsArray().Count);
        Assert.Equal(product, after["topProducts"]![0]!["productId"]!.GetValue<Guid>());
    }

    // ---------- §26.9–10: құқықтар, экспорт ----------

    [Fact]
    public async Task Rights_are_checked_on_the_server_and_export_respects_filter_and_contacts()
    {
        var pos = await api.NewDeviceAsync();
        var tag = Tag();
        var phone = Phone();
        var card = await CreateAsync(pos, tag, phone: phone);
        await CreateAsync(pos, tag + "x");
        await CreateAsync(pos, "Другой" + Guid.NewGuid().ToString("N")[..4]);

        Assert.Equal(HttpStatusCode.Forbidden, (await pos.Post(pos.Cashier, $"{C}/search", new { })).Status);
        var export = await pos.PostRaw(pos.Owner, $"{C}/export", new { filter = new { search = tag }, format = "csv", fields = new[] { "number", "name", "phone" } });
        Assert.Equal(HttpStatusCode.OK, export.Status);
        var lines = Encoding.UTF8.GetString(export.Content).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length); // тақырып + сүзгіге сай екі клиент
        Assert.Contains(phone, lines[1] + lines[2]);
        var xlsx = await pos.PostRaw(pos.Owner, $"{C}/export", new { ids = new[] { Id(card) } });
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", xlsx.ContentType);

        var cashierId = (await pos.Get(pos.Cashier, "/api/staff/v1/auth/me")).Ok["id"]!.GetValue<Guid>();
        await api.SqlAsync($"""UPDATE core.store_memberships SET "Permissions" = array_append("Permissions", 'clients.view') WHERE "StaffUserId" = '{cashierId}'""");
        try
        {
            var rows = (await SearchAsync(pos, new { search = tag }, token: pos.Cashier))["rows"]!.AsArray();
            Assert.Equal(2, rows.Count);
            Assert.All(rows, r => Assert.Contains("***", r!["phone"]!.GetValue<string>())); // customers.contacts жоқ
            Assert.All(rows, r => Assert.Null(r!["email"]));
            Assert.Equal(HttpStatusCode.Forbidden, (await pos.Post(pos.Cashier, C, NewClient(Phone(), "Нельзя", tag))).Status);
            Assert.Equal(HttpStatusCode.Forbidden, (await pos.Put(pos.Cashier, $"{C}/{Id(card)}", new { firstName = "Нельзя", lastName = tag })).Status);
            Assert.Equal(HttpStatusCode.Forbidden, (await pos.Post(pos.Cashier, $"{C}/{Id(card)}/archive", new { reason = "Тест" })).Status);
            Assert.Equal(HttpStatusCode.Forbidden, (await pos.Post(pos.Cashier, $"{C}/merge", new { primaryId = Id(card), duplicateId = Id(card) })).Status);
            Assert.Equal(HttpStatusCode.Forbidden, (await pos.Get(pos.Cashier, $"{C}/{Id(card)}/debts")).Status);
            Assert.Equal(HttpStatusCode.Forbidden, (await pos.PostRaw(pos.Cashier, $"{C}/export", new { })).Status);
            Assert.Equal(HttpStatusCode.Forbidden, (await pos.Put(pos.Cashier, $"{C}/settings", new { activeDays = 10 })).Status);
            Assert.Equal(HttpStatusCode.Forbidden, (await pos.Post(pos.Cashier, "/api/pos/v1/bonus/operations/accrue",
                new { customerId = CustomerId(card), amount = 10, reason = "Тест" })).Status); // бонус есептеу — өз құқығымен

            await api.SqlAsync($"""UPDATE core.store_memberships SET "Permissions" = array_append("Permissions", 'clients.export') WHERE "StaffUserId" = '{cashierId}'""");
            var masked = await pos.PostRaw(pos.Cashier, $"{C}/export", new { filter = new { search = tag }, format = "csv", fields = new[] { "name", "phone", "email" } });
            Assert.Equal(HttpStatusCode.OK, masked.Status);
            var text = Encoding.UTF8.GetString(masked.Content);
            Assert.DoesNotContain(phone, text);
            Assert.Contains("***", text);
            Assert.DoesNotContain("E-mail", text); // жеке дерек бағаны құқықсыз шықпайды
        }
        finally
        {
            await api.SqlAsync($"""UPDATE core.store_memberships SET "Permissions" = array_remove(array_remove("Permissions", 'clients.view'), 'clients.export') WHERE "StaffUserId" = '{cashierId}'""");
        }
    }

    // ---------- §26.11: импорт ----------

    [Fact]
    public async Task Import_previews_duplicates_and_errors_before_commit_and_is_idempotent()
    {
        var pos = await api.NewDeviceAsync();
        var tag = Tag();
        var existing = await CreateAsync(pos, tag + "Есть");
        var existingPhone = existing["client"]!["phone"]!.GetValue<string>();
        var (p1, p2) = (Phone(), Phone());
        var csv = string.Join('\n',
            "Телефон;Фамилия;Имя;Дата рождения;Email",
            $"{p1};{tag}Один;Асель;14.05.1990;asel@example.kz",
            $"8{p2[2..]};{tag}Два;Ерлан;1985-11-02;",
            $"{existingPhone};{tag}Есть;Новое имя;;",
            "12345;Плохой;Телефон;;",
            $"{p1};{tag}Дубль;Асель;;",
            $"{Phone()};{tag}Почта;Ошибка;;bad-email");
        var bytes = new UTF8Encoding(true).GetBytes(csv);

        var preview = (await pos.PostFile(pos.Owner, $"{C}/import/preview", bytes, "text/csv", "clients.csv", ("strategy", "skip"))).Ok;
        Assert.Equal(6, preview["rows"]!.GetValue<int>());
        Assert.Equal(2, preview["new"]!.GetValue<int>());
        Assert.Equal(1, preview["skip"]!.GetValue<int>());
        Assert.Equal(3, preview["errors"]!.GetValue<int>());
        Assert.Equal(0, preview["mapping"]!["phone"]!.GetValue<int>());
        Assert.False((await pos.Get(pos.Owner, $"{C}/lookup?phone={Uri.EscapeDataString(p1)}")).Ok["exists"]!.GetValue<bool>()); // растауға дейін ештеңе жоқ

        var key = Guid.NewGuid().ToString();
        var result = (await pos.PostFile(pos.Owner, $"{C}/import/commit", bytes, "text/csv", "clients.csv", ("strategy", "skip"), ("key", key))).Ok;
        Assert.Equal((2, 0, 1, 3), (result["created"]!.GetValue<int>(), result["updated"]!.GetValue<int>(), result["skipped"]!.GetValue<int>(), result["errors"]!.GetValue<int>()));
        var again = (await pos.PostFile(pos.Owner, $"{C}/import/commit", bytes, "text/csv", "clients.csv", ("strategy", "skip"), ("key", key))).Ok;
        Assert.Equal(result["id"]!.GetValue<Guid>(), again["id"]!.GetValue<Guid>());
        Assert.Equal(2, await TotalAsync(pos, new { search = tag, source = "Import" })); // қайта жіберу екі есе жасамайды
        var imported = (await SearchAsync(pos, new { search = p2[^7..] }))["rows"]!.AsArray().First(r => r!["lastName"]!.GetValue<string>() == tag + "Два")!;
        Assert.Equal("1985-11-02", imported["birthDate"]!.GetValue<string>());
        Assert.Equal("Айгерим", (await CardAsync(pos, Id(existing)))["client"]!["firstName"]!.GetValue<string>()); // «өткізу» — бар клиент өзгермейді

        var update = (await pos.PostFile(pos.Owner, $"{C}/import/commit", bytes, "text/csv", "clients.csv", ("strategy", "update"), ("key", Guid.NewGuid().ToString()))).Ok;
        Assert.Equal(0, update["created"]!.GetValue<int>());
        Assert.Equal(3, update["updated"]!.GetValue<int>());
        Assert.Equal("Новое имя", (await CardAsync(pos, Id(existing)))["client"]!["firstName"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.PostFile(pos.Owner, $"{C}/import/preview", Encoding.UTF8.GetBytes("Имя;Фамилия\nА;Б"), "text/csv", "x.csv")).Status);
    }

    // ---------- §26.12: мұрағат ----------

    [Fact]
    public async Task Archive_requires_reason_blocks_open_debt_confirms_bonus_and_keeps_history()
    {
        var pos = await api.NewDeviceAsync();
        var tag = Tag();
        var product = await pos.CreateProductAsync(price: 500, stock: 5);
        var card = await CreateAsync(pos, tag);
        var id = Id(card);
        (await pos.SellAsync(pos.Owner, [Pos.Line(product, 1)], [Pos.Cash(500)], customerId: CustomerId(card))).EnsureOk();
        (await AccrueAsync(pos, CustomerId(card), 100)).EnsureOk();
        var debtor = await CreateAsync(pos, tag + "Долг");
        (await pos.SellAsync(pos.Owner, [Pos.Line(product, 1)], [Debt(500)], customerId: CustomerId(debtor))).EnsureOk();

        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Post(pos.Owner, $"{C}/{id}/archive", new { reason = "" })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Post(pos.Owner, $"{C}/{Id(debtor)}/archive", new { reason = "Переехал" })).Status);
        var needConfirm = await pos.Post(pos.Owner, $"{C}/{id}/archive", new { reason = "Переехал" });
        Assert.Equal(HttpStatusCode.BadRequest, needConfirm.Status);
        Assert.Contains("бонус", needConfirm.Body!["detail"]!.GetValue<string>());

        var archived = (await pos.Post(pos.Owner, $"{C}/{id}/archive", new { reason = "Переехал", force = true })).Ok;
        Assert.True(archived["client"]!["isArchived"]!.GetValue<bool>());
        Assert.Equal("Переехал", archived["archiveReason"]!.GetValue<string>());
        Assert.Equal(1, archived["client"]!["purchases"]!.GetValue<int>()); // сатылым тарихы сақталады
        Assert.Equal(1, await TotalAsync(pos, new { search = tag })); // тек борышкер
        Assert.Equal(1, await TotalAsync(pos, new { search = tag, quick = "archived" }));
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Put(pos.Owner, $"{C}/{id}", new { firstName = "Нельзя", lastName = tag })).Status);
        Assert.Contains(await HistoryAsync(pos, id), h => h!["action"]!.GetValue<string>() == "client.archive");

        var restored = (await pos.Post(pos.Owner, $"{C}/{id}/restore", new { })).Ok;
        Assert.False(restored["client"]!["isArchived"]!.GetValue<bool>());
        Assert.Equal(2, await TotalAsync(pos, new { search = tag }));
    }

    // ---------- §26.13: біріктіру ----------

    [Fact]
    public async Task Merge_moves_sales_debts_and_bonus_without_duplicates_and_is_audited()
    {
        var pos = await api.NewDeviceAsync();
        var tag = Tag();
        var product = await pos.CreateProductAsync(price: 1000, stock: 10);
        var primary = await CreateAsync(pos, tag, "Самал");
        var duplicate = await CreateAsync(pos, tag, "Самал");
        (await pos.SellAsync(pos.Owner, [Pos.Line(product, 2)], [Pos.Cash(2000)], customerId: CustomerId(primary))).EnsureOk();
        (await AccrueAsync(pos, CustomerId(primary), 100)).EnsureOk();
        (await pos.SellAsync(pos.Owner, [Pos.Line(product, 1)], [Debt(1000)], customerId: CustomerId(duplicate))).EnsureOk();
        (await AccrueAsync(pos, CustomerId(duplicate), 50)).EnsureOk();

        var dupes = (await pos.Get(pos.Owner, $"{C}/{Id(primary)}/duplicates")).Ok.AsArray();
        Assert.Contains(dupes, d => d!["id"]!.GetValue<Guid>() == Id(duplicate) && d["reason"]!.GetValue<string>() == "name");

        var a = (await CardAsync(pos, Id(primary)))["client"]!;
        var b = (await CardAsync(pos, Id(duplicate)))["client"]!;
        var bonus = a["bonus"]!.GetValue<int>() + b["bonus"]!.GetValue<int>();
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Post(pos.Owner, $"{C}/merge", new { primaryId = Id(primary), duplicateId = Id(primary) })).Status);

        var merged = (await pos.Post(pos.Owner, $"{C}/merge", new { primaryId = Id(primary), duplicateId = Id(duplicate) })).Ok["client"]!;
        Assert.Equal(2, merged["purchases"]!.GetValue<int>());
        Assert.Equal(3000m, merged["amount"]!.GetValue<decimal>());
        Assert.Equal(1000m, merged["debt"]!.GetValue<decimal>());
        Assert.Equal(bonus, merged["bonus"]!.GetValue<int>());
        Assert.Equal(bonus, await BonusModuleBalanceAsync(pos, CustomerId(primary)));
        Assert.Equal(1000m, (await pos.Get(pos.Owner, $"/api/pos/v1/finance/customer-debts/{CustomerId(primary)}")).Ok["total"]!.GetValue<decimal>());

        var dup = await CardAsync(pos, Id(duplicate));
        Assert.True(dup["client"]!["isArchived"]!.GetValue<bool>());
        Assert.Equal(Id(primary), dup["mergedIntoId"]!.GetValue<Guid>());
        Assert.Equal(0, dup["client"]!["purchases"]!.GetValue<int>());
        Assert.Equal(0m, dup["client"]!["debt"]!.GetValue<decimal>());
        Assert.Equal(1, await TotalAsync(pos, new { search = tag }));
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Post(pos.Owner, $"{C}/{Id(duplicate)}/restore", new { })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Post(pos.Owner, $"{C}/merge", new { primaryId = Id(primary), duplicateId = Id(duplicate) })).Status);
        var audit = (await HistoryAsync(pos, Id(primary))).First(h => h!["action"]!.GetValue<string>() == "client.merge")!;
        Assert.Contains(audit["changes"]!.AsArray(), x => x!["field"]!.GetValue<string>() == "sales" && x["new"]!.GetValue<string>() == "1");
    }

    // ---------- §26.14: хабар ----------

    [Fact]
    public async Task Message_needs_connected_channel_consent_and_app_and_is_logged_in_activity()
    {
        var pos = await api.NewDeviceAsync();
        var tag = Tag();
        var card = await CreateAsync(pos, tag);
        var id = Id(card);
        var sms = await pos.Post(pos.Owner, $"{C}/{id}/message", new { channel = "sms", text = "Скидка 10%" });
        Assert.Equal(HttpStatusCode.BadRequest, sms.Status);
        Assert.Contains("не подключён", sms.Body!["detail"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Post(pos.Owner, $"{C}/{id}/message", new { channel = "app", text = "Скидка 10%" })).Status); // келісім жоқ

        (await pos.Put(pos.Owner, $"{C}/{id}", new { firstName = "Айгерим", lastName = tag, consentApp = true })).EnsureOk();
        var noApp = await pos.Post(pos.Owner, $"{C}/{id}/message", new { channel = "app", text = "Скидка 10%" });
        Assert.Equal(HttpStatusCode.BadRequest, noApp.Status);
        Assert.Contains("приложения", noApp.Body!["detail"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Post(pos.Owner, $"{C}/{id}/message", new { channel = "app", text = "" })).Status);

        await GiveAppAsync(CustomerId(card));
        var sent = (await pos.Post(pos.Owner, $"{C}/{id}/message", new { channel = "app", text = "Скидка 10% до пятницы" })).Ok;
        Assert.Equal("delivered", sent["status"]!.GetValue<string>());
        var activity = (await pos.Get(pos.Owner, $"{C}/{id}/activity")).Ok;
        Assert.Contains(activity["communications"]!.AsArray(), m => m!["text"]!.GetValue<string>() == "Скидка 10% до пятницы");
        Assert.Contains(await HistoryAsync(pos, id), h => h!["action"]!.GetValue<string>() == "client.message");
    }

    // ---------- §13, §21: жазбалар мен баптаулар ----------

    [Fact]
    public async Task Notes_keep_author_and_settings_drive_validation_and_custom_fields()
    {
        var pos = await api.NewDeviceAsync();
        var tag = Tag();
        var card = await CreateAsync(pos, tag);
        var id = Id(card);
        var notes = (await pos.Post(pos.Owner, $"{C}/{id}/notes", new { text = "Любит зелёный чай", category = "preference" })).Ok.AsArray();
        var note = Assert.Single(notes)!;
        Assert.Equal("Preference", note["category"]!.GetValue<string>());
        Assert.True(note["canEdit"]!.GetValue<bool>());
        Assert.False(string.IsNullOrEmpty(note["author"]!.GetValue<string>()));
        var noteId = note["id"]!.GetValue<Guid>();
        var edited = (await pos.Put(pos.Owner, $"{C}/notes/{noteId}", new { text = "Любит чёрный чай" })).Ok.AsArray();
        Assert.Equal("Любит чёрный чай", edited[0]!["text"]!.GetValue<string>());
        Assert.NotNull(edited[0]!["updatedAt"]);
        Assert.Equal(1, (await CardAsync(pos, id))["notes"]!.GetValue<int>());
        Assert.Equal(HttpStatusCode.NoContent, (await pos.Delete(pos.Owner, $"{C}/notes/{noteId}")).Status);
        Assert.Empty((await pos.Get(pos.Owner, $"{C}/{id}/notes")).Ok.AsArray());
        var actions = (await HistoryAsync(pos, id)).Select(h => h!["action"]!.GetValue<string>()).ToList();
        Assert.Contains("client.note.add", actions);
        Assert.Contains("client.note.edit", actions);
        Assert.Contains("client.note.delete", actions);

        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Put(pos.Owner, $"{C}/settings", new { activeDays = 60, passiveDays = 30 })).Status);
        try
        {
            var settings = (await pos.Put(pos.Owner, $"{C}/settings", new
            {
                requireBirthDate = true,
                customFields = new[] { new { key = "size", label = "Размер одежды", type = "select", options = new[] { "S", "M", "L" } } },
            })).Ok;
            Assert.Equal("size", settings["customFields"]![0]!["key"]!.GetValue<string>());
            Assert.Equal(HttpStatusCode.BadRequest, (await pos.Post(pos.Owner, C, NewClient(Phone(), "Без", tag + "др", birthDate: null))).Status);
            Assert.Equal(HttpStatusCode.BadRequest, (await pos.Post(pos.Owner, C, NewClient(Phone(), "Размер", tag + "xl", customFields: new { size = "XL" }))).Status);
            var withField = (await pos.Post(pos.Owner, C, NewClient(Phone(), "Размер", tag + "m", customFields: new { size = "M", unknown = "x" }))).Ok;
            Assert.Equal("M", withField["client"]!["customFields"]!["size"]!.GetValue<string>());
            Assert.Null(withField["client"]!["customFields"]!["unknown"]);
            Assert.Equal("M", (await pos.Get(pos.Owner, $"{C}/meta")).Ok["settings"]!["customFields"]![0]!["options"]![1]!.GetValue<string>());
        }
        finally
        {
            (await pos.Put(pos.Owner, $"{C}/settings", new
            {
                requireLastName = true, requireBirthDate = false, activeDays = 30, passiveDays = 90, newDays = 30, dueSoonDays = 3, customFields = Array.Empty<object>(),
            })).EnsureOk();
        }
    }

    // ---------- §3, §14: шолу мен туған күндер ----------

    [Fact]
    public async Task Overview_and_birthdays_show_the_store_clients()
    {
        var pos = await api.NewDeviceAsync();
        var tag = Tag();
        var birthday = ApiFixture.StoreToday.AddYears(-30);
        var card = (await pos.Post(pos.Owner, C, NewClient(Phone(), "Именинник", tag, birthDate: birthday.ToString("yyyy-MM-dd")))).Ok;
        var birthdays = (await pos.Get(pos.Owner, $"{C}/birthdays?days=7")).Ok.AsArray();
        var mine = birthdays.First(b => b!["client"]!["id"]!.GetValue<Guid>() == Id(card))!;
        Assert.Equal(0, mine["daysLeft"]!.GetValue<int>());
        Assert.Equal(30, mine["age"]!.GetValue<int>());
        Assert.Equal(1, await TotalAsync(pos, new { search = tag, quick = "birthday" }));
        Assert.Equal(1, await TotalAsync(pos, new { search = tag, birthMonth = birthday.Month }));

        var overview = (await pos.Get(pos.Owner, $"{C}/overview")).Ok;
        Assert.True(overview["kpis"]!["total"]!.GetValue<int>() > 0);
        Assert.Equal(4, overview["activity"]!.AsArray().Count);
        Assert.Equal(12, overview["newByMonth"]!.AsArray().Count);
        Assert.True(overview["newByMonth"]![11]!["count"]!.GetValue<int>() >= 1);
        Assert.Equal(overview["kpis"]!["total"]!.GetValue<int>(), overview["activity"]!.AsArray().Sum(a => a!["count"]!.GetValue<int>()));
        var meta = (await pos.Get(pos.Owner, $"{C}/meta")).Ok;
        Assert.True(meta["can"]!["edit"]!.GetValue<bool>());
        Assert.Equal(HttpStatusCode.OK, (await pos.Raw(pos.Owner, $"{C}/import/template")).Status);
    }
}
