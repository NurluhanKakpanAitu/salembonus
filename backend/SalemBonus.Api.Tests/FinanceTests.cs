using System.Net;
using System.Text.Json.Nodes;

namespace SalemBonus.Api.Tests;

/// <summary>
/// Финансы (ТЗ «Финансы» v1.0, §19 FIN-001…FIN-012): сатылым → бір операция, приёмка ақшаны қозғамайды, жеткізушіге төлем қарыз бен шотты азайтады,
/// клиент төлемі түсім емес, аударым жалпы соманы сақтайды, овердрафт жабық, қайталанбау, сторно түпнұсқаны қалдырады, экспорт, бөтен дүкен 403, кассирге жабық.
/// Шоттар дүкенге ортақ (басқа тесттер де ақша қозғайды), сондықтан тексерулер айырма арқылы не өз жаңа шотында.
/// </summary>
[Collection(ApiCollection.Name)]
public class FinanceTests(ApiFixture api)
{
    private const string F = "/api/pos/v1/finance";
    private const string Inv = "/api/pos/v1/inventory";

    private static object Debt(decimal amount) => new { method = "Debt", amount, dueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(7)) };
    private static string Today() => ApiFixture.StoreToday.ToString("yyyy-MM-dd");
    private static string Period() => $"from=2020-01-01&to={Today()}";

    private static async Task<JsonNode> AccountsAsync(Pos pos) => (await pos.Get(pos.Owner, $"{F}/accounts")).Ok;

    private static JsonNode AccountByType(JsonNode accounts, string type) =>
        accounts["accounts"]!.AsArray().First(a => a!["type"]!.GetValue<string>() == type)!;

    private static async Task<decimal> BalanceAsync(Pos pos, Guid accountId) =>
        (await AccountsAsync(pos))["accounts"]!.AsArray().First(a => a!["id"]!.GetValue<Guid>() == accountId)!["balance"]!.GetValue<decimal>();

    /// <summary>Өз шоты: басқа тесттердің қалдығынан тәуелсіз. Бастапқы қалдық — Adjustment операциясымен түседі (§9).</summary>
    private static async Task<Guid> NewAccountAsync(Pos pos, decimal opening = 0, string type = "Bank")
    {
        var res = await pos.Post(pos.Owner, $"{F}/accounts", new
        {
            name = "Шот " + Guid.NewGuid().ToString("N")[..8], type, methods = Array.Empty<string>(), openingBalance = opening, openingDate = Today(),
        });
        return res.Ok["id"]!.GetValue<Guid>();
    }

    private static async Task<JsonNode> OperationBySourceAsync(Pos pos, Guid sourceDocumentId, string type)
    {
        var page = (await pos.Get(pos.Owner, $"{F}/operations?type={type}&pageSize=100&{Period()}")).Ok;
        return page["items"]!.AsArray().First(o => o!["sourceDocumentId"]?.GetValue<Guid>() == sourceDocumentId)!;
    }

    private static async Task<decimal> PnlRowAsync(Pos pos, string key) =>
        (await pos.Get(pos.Owner, $"{F}/reports/pnl?from={Today()}&to={Today()}")).Ok["rows"]!.AsArray().First(r => r!["key"]!.GetValue<string>() == key)!["amount"]!.GetValue<decimal>();

    private static async Task<Guid> CreateSupplierAsync(Pos pos) =>
        (await pos.Post(pos.Owner, $"{Inv}/suppliers", new { name = "Поставщик " + Guid.NewGuid().ToString("N")[..8], type = "Company", taxId = "123456789012" })).Ok["id"]!.GetValue<Guid>();

    private static async Task<Guid> PostedReceiptAsync(Pos pos, Guid supplier, Guid product, decimal qty, decimal price, string? dueDate = null)
    {
        var doc = (await pos.Post(pos.Owner, $"{Inv}/receipts", new { supplierId = supplier, supplierInvoiceNumber = "Н-7", paymentDueDate = dueDate, lines = new[] { new { productId = product, quantity = qty, purchasePrice = price } } })).Ok;
        var id = doc["id"]!.GetValue<Guid>();
        (await pos.Post(pos.Owner, $"{Inv}/documents/{id}/post", new { })).EnsureOk();
        return id;
    }

    // ---------- FIN-001, FIN-004, FIN-009: сатылым → бір операция; қарыз бөлігі ақша емес; бастапқы операция қаржыдан сторноланбайды ----------

    [Fact]
    public async Task Sale_posts_one_operation_with_a_line_per_method_and_without_the_debt_part()
    {
        var pos = await api.NewDeviceAsync();
        var product = await pos.CreateProductAsync(price: 1000, stock: 5);
        var customer = (await pos.CreateCustomerAsync())["id"]!.GetValue<Guid>();
        var before = await AccountsAsync(pos);
        var cashId = AccountByType(before, "Cash")["id"]!.GetValue<Guid>();
        var cashBefore = AccountByType(before, "Cash")["balance"]!.GetValue<decimal>();
        var totalBefore = before["total"]!.GetValue<decimal>();

        var sale = (await pos.SellAsync(pos.Owner, [Pos.Line(product, 3)], [Pos.Cash(1000), Pos.Card(1000), Debt(1000)], customerId: customer)).Ok;
        var saleId = sale["id"]!.GetValue<Guid>();

        var op = await OperationBySourceAsync(pos, saleId, "SalePayment");
        Assert.Equal("Posted", op["status"]!.GetValue<string>());
        Assert.Equal(2000m, op["amount"]!.GetValue<decimal>()); // қарызға берілген 1000 — ақша емес (§3.1)
        Assert.Equal(2, op["entries"]!.AsArray().Count);
        Assert.StartsWith("ФД-", op["number"]!.GetValue<string>());
        Assert.Equal("sales", op["sourceModule"]!.GetValue<string>());
        Assert.False(op["canReverse"]!.GetValue<bool>());
        Assert.Equal(cashBefore + 1000m, await BalanceAsync(pos, cashId));
        Assert.Equal(totalBefore + 2000m, (await AccountsAsync(pos))["total"]!.GetValue<decimal>());

        // Бастапқы модульдің операциясы қаржыдан сторноланбайды — тек қайтарым арқылы (§3.2, §8).
        var reverse = await pos.Post(pos.Owner, $"{F}/operations/{op["id"]}/reverse", new { reason = "Ошибка" });
        Assert.Equal(HttpStatusCode.BadRequest, reverse.Status);

        // Қайтарым → Refund операциясы, ақша шоттан кетеді.
        var itemId = sale["items"]![0]!["id"]!.GetValue<Guid>();
        (await pos.Post(pos.Owner, $"/api/pos/v1/sales/{saleId}/returns", new { clientRequestId = Guid.NewGuid(), items = new[] { new { saleItemId = itemId, quantity = 2m } }, refundMethod = "Cash", reasonCode = "NotSuitable" })).EnsureOk();
        var refund = (await pos.Get(pos.Owner, $"{F}/operations?type=Refund&pageSize=100&{Period()}")).Ok["items"]!.AsArray()
            .First(o => o!["sourceDocumentId"]?.GetValue<Guid>() == saleId)!;
        Assert.Equal("Refund", refund["type"]!.GetValue<string>());
        Assert.True(refund["amount"]!.GetValue<decimal>() > 0);
    }

    // ---------- FIN-002, FIN-003, FIN-007, FIN-010: приёмка ақшаны қозғамайды; төлем қарыз бен шотты азайтады; овердрафт жабық; сторно ----------

    [Fact]
    public async Task Supplier_payment_needs_funds_reduces_debt_and_account_and_is_reversible_from_inventory()
    {
        var pos = await api.NewDeviceAsync();
        var product = await pos.CreateProductAsync(price: 2000, stock: 0);
        var supplier = await CreateSupplierAsync(pos);
        var account = await NewAccountAsync(pos, opening: 0);
        var totalBefore = (await AccountsAsync(pos))["total"]!.GetValue<decimal>();

        var docId = await PostedReceiptAsync(pos, supplier, product, 10, 1000, dueDate: Today());
        Assert.Equal(totalBefore, (await AccountsAsync(pos))["total"]!.GetValue<decimal>()); // приёмка — қарыз, ақша емес (FIN-002)

        var noFunds = await pos.Post(pos.Owner, $"{Inv}/receipts/{docId}/payments", new { amount = 3000m, method = "Transfer", accountId = account });
        Assert.Equal(HttpStatusCode.BadRequest, noFunds.Status); // шотта 0 — овердрафт жабық (FIN-007)

        // Қаржыдан бастапқы қалдық салу (проводкасы жоқ шотқа) → Adjustment.
        (await pos.Put(pos.Owner, $"{F}/accounts/{account}", new { name = "Шот " + account.ToString("N")[..8], type = "Bank", methods = Array.Empty<string>(), openingBalance = 5000m, openingDate = Today() })).EnsureOk();
        Assert.Equal(5000m, await BalanceAsync(pos, account));

        var paid = (await pos.Post(pos.Owner, $"{Inv}/receipts/{docId}/payments", new { amount = 3000m, method = "Transfer", accountId = account, reference = "ПП-1" })).Ok;
        Assert.Equal(7000m, paid["remaining"]!.GetValue<decimal>());
        Assert.Equal(2000m, await BalanceAsync(pos, account));
        var op = await OperationBySourceAsync(pos, docId, "SupplierPayment");
        Assert.Equal(3000m, op["amount"]!.GetValue<decimal>());
        Assert.Equal("inventory", op["sourceModule"]!.GetValue<string>());
        Assert.Equal(account, op["fromAccountId"]!.GetValue<Guid>());

        var card = (await pos.Get(pos.Owner, $"{F}/supplier-debts/{supplier}")).Ok;
        Assert.Equal(7000m, card["debt"]!.GetValue<decimal>());
        var paymentRow = Assert.Single(card["payments"]!.AsArray());
        Assert.Equal(op["number"]!.GetValue<string>(), paymentRow!["operationNumber"]!.GetValue<string>());

        // Бастапқы қалдық проводкасы бар шотта өзгертілмейді (§9).
        var locked = await pos.Put(pos.Owner, $"{F}/accounts/{account}", new { name = "Шот " + account.ToString("N")[..8], type = "Bank", methods = Array.Empty<string>(), openingBalance = 9000m, openingDate = Today() });
        Assert.Equal(HttpStatusCode.BadRequest, locked.Status);

        // Қаржыдан төлем: бөлу нақты, құжат қалдығынан асырмайды (§12).
        var tooMuch = await pos.Post(pos.Owner, $"{F}/supplier-debts/payments", new { supplierId = supplier, accountId = account, method = "Transfer", allocations = new[] { new { documentId = docId, amount = 7001m } } });
        Assert.Equal(HttpStatusCode.BadRequest, tooMuch.Status);
        var fromFinance = (await pos.Post(pos.Owner, $"{F}/supplier-debts/payments", new { supplierId = supplier, accountId = account, method = "Transfer", allocations = new[] { new { documentId = docId, amount = 1500m } } })).Ok;
        Assert.Equal(5500m, fromFinance["debt"]!.GetValue<decimal>());
        Assert.Equal(500m, await BalanceAsync(pos, account));

        // Сторно (склад) → ақша шотқа қайтады, түпнұсқа Reversed, кері жазба бар (FIN-010).
        var paymentId = paid["payments"]![0]!["id"]!.GetValue<Guid>();
        Assert.Equal(HttpStatusCode.NoContent, (await pos.Post(pos.Owner, $"{Inv}/payments/{paymentId}/reverse", new { reason = "Дубль" })).Status);
        Assert.Equal(3500m, await BalanceAsync(pos, account));
        var original = (await pos.Get(pos.Owner, $"{F}/operations/{op["id"]}")).Ok;
        Assert.Equal("Reversed", original["status"]!.GetValue<string>());
        var reversal = (await pos.Get(pos.Owner, $"{F}/operations/{original["reversedById"]}")).Ok;
        Assert.Equal(op["id"]!.GetValue<Guid>(), reversal["reversalOfId"]!.GetValue<Guid>());
        Assert.Equal(8500m, (await pos.Get(pos.Owner, $"{F}/supplier-debts/{supplier}")).Ok["debt"]!.GetValue<decimal>());

        // Төлем мерзімі, салыстыру актісі, жеткізуші тізімі.
        Assert.Equal(HttpStatusCode.NoContent, (await pos.Put(pos.Owner, $"{F}/supplier-debts/documents/{docId}/due-date", new { dueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(10)) })).Status);
        var rec = (await pos.Get(pos.Owner, $"{F}/supplier-debts/{supplier}/reconciliation?{Period()}")).Ok;
        Assert.Equal(10000m, rec["debit"]!.GetValue<decimal>());
        Assert.Equal(1500m, rec["credit"]!.GetValue<decimal>()); // сторноланған төлем актіде жоқ
        var list = (await pos.Get(pos.Owner, $"{F}/supplier-debts?{Period()}")).Ok;
        Assert.Contains(list["rows"]!.AsArray(), r => r!["supplierId"]!.GetValue<Guid>() == supplier && r["debt"]!.GetValue<decimal>() == 8500m);
    }

    // ---------- FIN-004: клиент қарызын өтеу — ақша түседі, түсім өзгермейді ----------

    [Fact]
    public async Task Customer_payment_from_finance_adds_money_but_not_revenue()
    {
        var pos = await api.NewDeviceAsync();
        var product = await pos.CreateProductAsync(price: 5000, stock: 3);
        var customer = (await pos.CreateCustomerAsync())["id"]!.GetValue<Guid>();
        var revenueBefore = await PnlRowAsync(pos, "revenue");
        var sale = (await pos.SellAsync(pos.Owner, [Pos.Line(product, 1)], [Debt(5000)], customerId: customer)).Ok;
        var debtId = sale["debt"]!["id"]!.GetValue<Guid>();
        Assert.Equal(revenueBefore + 5000m, await PnlRowAsync(pos, "revenue")); // сатылым — түсім, төлем түрінен тәуелсіз

        var account = await NewAccountAsync(pos, opening: 0, type: "Cash");
        var card = (await pos.Get(pos.Owner, $"{F}/customer-debts/{customer}")).Ok;
        Assert.Equal(5000m, card["total"]!.GetValue<decimal>());
        var tooMuch = await pos.Post(pos.Owner, $"{F}/customer-debts/payments", new { customerId = customer, accountId = account, method = "Cash", allocations = new[] { new { debtId, amount = 5001m } } });
        Assert.Equal(HttpStatusCode.BadRequest, tooMuch.Status);
        var after = (await pos.Post(pos.Owner, $"{F}/customer-debts/payments", new { customerId = customer, accountId = account, method = "Cash", allocations = new[] { new { debtId, amount = 2000m } } })).Ok;
        Assert.Equal(3000m, after["total"]!.GetValue<decimal>());
        Assert.Equal(2000m, await BalanceAsync(pos, account));
        Assert.Equal(revenueBefore + 5000m, await PnlRowAsync(pos, "revenue")); // қарыз төлемі түсім емес (FIN-004)
        var payment = Assert.Single(after["payments"]!.AsArray());
        Assert.StartsWith("ФД-", payment!["operationNumber"]!.GetValue<string>());
        var rows = (await pos.Get(pos.Owner, $"{F}/customer-debts")).Ok["rows"]!.AsArray();
        Assert.Contains(rows, r => r!["customerId"]!.GetValue<Guid>() == customer && r["total"]!.GetValue<decimal>() == 3000m);
    }

    // ---------- FIN-005, FIN-006, FIN-008: аударым, инкассация, овердрафт, қайталанбау ----------

    [Fact]
    public async Task Transfer_keeps_the_total_blocks_overdraft_and_is_idempotent()
    {
        var pos = await api.NewDeviceAsync();
        var a = await NewAccountAsync(pos, opening: 10000, type: "Cash");
        var b = await NewAccountAsync(pos, opening: 0, type: "Safe");
        var transit = await NewAccountAsync(pos, opening: 0, type: "InTransit");
        var totalBefore = (await AccountsAsync(pos))["total"]!.GetValue<decimal>();

        var same = await pos.Post(pos.Owner, $"{F}/transfers", new { fromAccountId = a, toAccountId = a, amount = 100m });
        var over = await pos.Post(pos.Owner, $"{F}/transfers", new { fromAccountId = b, toAccountId = a, amount = 100m });
        Assert.Equal(HttpStatusCode.BadRequest, same.Status);
        Assert.Equal(HttpStatusCode.BadRequest, over.Status);

        var requestId = Guid.NewGuid();
        var first = (await pos.Post(pos.Owner, $"{F}/transfers", new { clientRequestId = requestId, fromAccountId = a, toAccountId = b, amount = 4000m })).Ok;
        var repeated = (await pos.Post(pos.Owner, $"{F}/transfers", new { clientRequestId = requestId, fromAccountId = a, toAccountId = b, amount = 4000m })).Ok;
        Assert.Equal(first["id"]!.GetValue<Guid>(), repeated["id"]!.GetValue<Guid>()); // FIN-008
        Assert.Equal("Transfer", first["type"]!.GetValue<string>());
        Assert.Equal(6000m, await BalanceAsync(pos, a));
        Assert.Equal(4000m, await BalanceAsync(pos, b));
        Assert.Equal(totalBefore, (await AccountsAsync(pos))["total"]!.GetValue<decimal>()); // FIN-005

        var collection = (await pos.Post(pos.Owner, $"{F}/transfers", new { fromAccountId = a, toAccountId = transit, amount = 1000m, collection = true })).Ok;
        Assert.Equal("Collection", collection["type"]!.GetValue<string>());
        Assert.Equal(1000m, await BalanceAsync(pos, transit));
        Assert.True((await AccountsAsync(pos))["inTransit"]!.GetValue<decimal>() >= 1000m);
        Assert.Equal(5000m, await BalanceAsync(pos, a));

        var statement = (await pos.Get(pos.Owner, $"{F}/accounts/{a}/statement?{Period()}")).Ok;
        Assert.Equal(5000m, statement["closing"]!.GetValue<decimal>());
        Assert.Contains(statement["rows"]!.AsArray(), r => r!["amount"]!.GetValue<decimal>() == -4000m);
        Assert.Contains(statement["rows"]!.AsArray(), r => r!["amount"]!.GetValue<decimal>() == 10000m); // бастапқы қалдық — Adjustment

        // Сторно (қолмен операция): аударым кері жазылады, түпнұсқа Reversed (FIN-010).
        var reversed = (await pos.Post(pos.Owner, $"{F}/operations/{first["id"]}/reverse", new { reason = "Ошибочно" })).Ok;
        Assert.Equal(first["id"]!.GetValue<Guid>(), reversed["reversalOfId"]!.GetValue<Guid>());
        Assert.Equal("Reversed", (await pos.Get(pos.Owner, $"{F}/operations/{first["id"]}")).Ok["status"]!.GetValue<string>());
        Assert.Equal(9000m, await BalanceAsync(pos, a));
        Assert.Equal(0m, await BalanceAsync(pos, b));
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Post(pos.Owner, $"{F}/operations/{first["id"]}/reverse", new { reason = "Ещё" })).Status);

        // Қалдығы бар шот өшірілмейді; нөлдік шот — өшіріледі де тізімнен кетеді (all=true-мен көрінеді).
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Post(pos.Owner, $"{F}/accounts/{a}/deactivate", new { })).Status);
        Assert.Equal(HttpStatusCode.NoContent, (await pos.Post(pos.Owner, $"{F}/accounts/{b}/deactivate", new { })).Status);
        Assert.DoesNotContain((await AccountsAsync(pos))["accounts"]!.AsArray(), x => x!["id"]!.GetValue<Guid>() == b);
        Assert.Contains((await pos.Get(pos.Owner, $"{F}/accounts?all=true")).Ok["accounts"]!.AsArray(), x => x!["id"]!.GetValue<Guid>() == b);
    }

    /// <summary>
    /// Инкассация кассаның «Финансы» экранында көрінеді: «Наличные в кассе» азаяды, инкассация жеке жолмен, «Сейчас в кассе» —
    /// касса шотының қалдығы. Сторно ақшаны кассаға қайтарады. Инкассация тек кассадан/сейфтен, Kaspi мен кассаға — жоқ.
    /// </summary>
    [Fact]
    public async Task Collection_takes_cash_out_of_the_drawer_shown_at_the_register()
    {
        var pos = await api.NewDeviceAsync();
        var accounts = await AccountsAsync(pos);
        var drawer = accounts["accounts"]!.AsArray().First(a => a!["methods"]!.AsArray().Any(m => m!.GetValue<string>() == "Cash"))!["id"]!.GetValue<Guid>();
        var transit = AccountByType(accounts, "InTransit")["id"]!.GetValue<Guid>();
        var bank = AccountByType(accounts, "Bank")["id"]!.GetValue<Guid>();
        var kaspi = AccountByType(accounts, "Kaspi")["id"]!.GetValue<Guid>();
        var before = (await pos.Get(pos.Owner, "/api/pos/v1/cashier/finance")).Ok;

        var product = await pos.CreateProductAsync(price: 3000, stock: 5);
        (await pos.SellAsync(pos.Owner, [Pos.Line(product, 1)], [Pos.Cash(3000)])).EnsureOk();
        var collection = (await pos.Post(pos.Owner, $"{F}/transfers", new { fromAccountId = drawer, toAccountId = transit, amount = 1200m, collection = true })).Ok;

        var after = (await pos.Get(pos.Owner, "/api/pos/v1/cashier/finance")).Ok;
        Assert.Equal(before["cashInDrawer"]!.GetValue<decimal>() + 3000m - 1200m, after["cashInDrawer"]!.GetValue<decimal>());
        Assert.Equal(before["collections"]!.GetValue<decimal>() + 1200m, after["collections"]!.GetValue<decimal>());
        // «Сейчас в кассе» — дүкеннің барлық «Касса» шоттарының (әр касса аппараты) қалдығы.
        var drawers = (await AccountsAsync(pos))["accounts"]!.AsArray().Where(a => a!["type"]!.GetValue<string>() == "Cash").Sum(a => a!["balance"]!.GetValue<decimal>());
        Assert.Equal(drawers, after["drawerBalance"]!.GetValue<decimal>());
        // Кассир тек өз сатылымын көреді — инкассация оған бөлінбейді, дүкен қалдығы көрсетілмейді.
        var cashier = (await pos.Get(pos.Cashier, "/api/pos/v1/cashier/finance")).Ok;
        Assert.False(cashier["wholeStore"]!.GetValue<bool>());
        Assert.Null(cashier["drawerBalance"]?.GetValue<decimal?>());

        (await pos.Post(pos.Owner, $"{F}/operations/{collection["id"]}/reverse", new { reason = "Ошибочно" })).EnsureOk();
        var reversed = (await pos.Get(pos.Owner, "/api/pos/v1/cashier/finance")).Ok;
        Assert.Equal(before["cashInDrawer"]!.GetValue<decimal>() + 3000m, reversed["cashInDrawer"]!.GetValue<decimal>());

        // Инкассация банктен не Kaspi-ге болмайды; белсенді емес шотқа аударым да жоқ.
        var fromBank = await pos.Post(pos.Owner, $"{F}/transfers", new { fromAccountId = bank, toAccountId = transit, amount = 1m, collection = true });
        Assert.Equal(HttpStatusCode.BadRequest, fromBank.Status);
        Assert.Equal("fromAccountId", fromBank.Body!["field"]!.GetValue<string>());
        var toKaspi = await pos.Post(pos.Owner, $"{F}/transfers", new { fromAccountId = drawer, toAccountId = kaspi, amount = 1m, collection = true });
        Assert.Equal(HttpStatusCode.BadRequest, toKaspi.Status);
        var closed = await NewAccountAsync(pos, type: "Safe");
        Assert.Equal(HttpStatusCode.NoContent, (await pos.Post(pos.Owner, $"{F}/accounts/{closed}/deactivate", new { })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Post(pos.Owner, $"{F}/transfers", new { fromAccountId = drawer, toAccountId = closed, amount = 1m })).Status);
    }

    // ---------- §7, §8: қолмен операция — черновик → проведение → сторно; валидация ----------

    [Fact]
    public async Task Manual_expense_goes_draft_post_reverse_and_lands_in_pnl()
    {
        var pos = await api.NewDeviceAsync();
        var account = await NewAccountAsync(pos, opening: 10000);
        var meta = (await pos.Get(pos.Owner, $"{F}/meta")).Ok;
        Assert.True(meta["canOperate"]!.GetValue<bool>());
        var categories = meta["categories"]!.AsArray();
        var expense = categories.First(c => c!["type"]!.GetValue<string>() == "Expense" && c["pnlClass"]!.GetValue<string>() == "OperatingExpense")!["id"]!.GetValue<Guid>();
        var income = categories.First(c => c!["type"]!.GetValue<string>() == "Income")!["id"]!.GetValue<Guid>();
        var opexBefore = await PnlRowAsync(pos, "operatingExpenses");

        var noCategory = await pos.Post(pos.Owner, $"{F}/operations", new { type = "Expense", amount = 1500m, accountId = account, description = "Аренда", post = true });
        var wrongCategory = await pos.Post(pos.Owner, $"{F}/operations", new { type = "Expense", amount = 1500m, accountId = account, categoryId = income, description = "Аренда", post = true });
        var zero = await pos.Post(pos.Owner, $"{F}/operations", new { type = "Expense", amount = 0m, accountId = account, categoryId = expense, description = "Аренда", post = true });
        var future = await pos.Post(pos.Owner, $"{F}/operations", new { type = "Expense", amount = 10m, accountId = account, categoryId = expense, description = "Аренда", date = DateTime.UtcNow.AddDays(2), post = true });
        var tooBig = await pos.Post(pos.Owner, $"{F}/operations", new { type = "Expense", amount = 10001m, accountId = account, categoryId = expense, description = "Аренда", post = true });
        Assert.All(new[] { noCategory, wrongCategory, zero, future, tooBig }, r => Assert.Equal(HttpStatusCode.BadRequest, r.Status));

        var draft = (await pos.Post(pos.Owner, $"{F}/operations", new { type = "Expense", amount = 1500m, accountId = account, categoryId = expense, description = "Аренда за октябрь", post = false })).Ok;
        Assert.Equal("Draft", draft["status"]!.GetValue<string>());
        Assert.Equal(10000m, await BalanceAsync(pos, account)); // черновик қалдықты қозғамайды
        var id = draft["id"]!.GetValue<Guid>();
        (await pos.Put(pos.Owner, $"{F}/operations/{id}", new { type = "Expense", amount = 1200m, accountId = account, categoryId = expense, description = "Аренда за октябрь", comment = "по договору" })).EnsureOk();
        var posted = (await pos.Post(pos.Owner, $"{F}/operations/{id}/post", new { })).Ok;
        Assert.Equal("Posted", posted["status"]!.GetValue<string>());
        Assert.Equal(1200m, posted["amount"]!.GetValue<decimal>());
        Assert.Equal(8800m, await BalanceAsync(pos, account));
        Assert.Equal(opexBefore - 1200m, await PnlRowAsync(pos, "operatingExpenses")); // шығыс P&L-де теріс
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Put(pos.Owner, $"{F}/operations/{id}", new { type = "Expense", amount = 1m, accountId = account, categoryId = expense, description = "Аренда" })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Post(pos.Owner, $"{F}/operations/{id}/reverse", new { })).Status); // себеп міндетті

        var reversal = (await pos.Post(pos.Owner, $"{F}/operations/{id}/reverse", new { reason = "Ошибка в сумме" })).Ok;
        Assert.Equal(id, reversal["reversalOfId"]!.GetValue<Guid>());
        Assert.Equal(10000m, await BalanceAsync(pos, account));
        Assert.Equal(opexBefore, await PnlRowAsync(pos, "operatingExpenses")); // сторноланған шығыс P&L-де жоқ
        var journal = (await pos.Get(pos.Owner, $"{F}/operations?accountId={account}&pageSize=50&{Period()}")).Ok;
        Assert.Contains(journal["items"]!.AsArray(), o => o!["id"]!.GetValue<Guid>() == id && o["status"]!.GetValue<string>() == "Reversed");
        Assert.Contains(journal["items"]!.AsArray(), o => o!["reversalOfId"]?.GetValue<Guid>() == id);

        var draft2 = (await pos.Post(pos.Owner, $"{F}/operations", new { type = "Income", amount = 300m, accountId = account, categoryId = income, description = "Прочее", post = false })).Ok;
        var deleted = await DeleteAsync(pos, $"{F}/operations/{draft2["id"]}");
        Assert.Equal(HttpStatusCode.NoContent, deleted);
        Assert.Equal(HttpStatusCode.NotFound, (await pos.Get(pos.Owner, $"{F}/operations/{draft2["id"]}")).Status);

        // Санаттар: қайталанған ат → 400; мұрағат → тізімнен кетеді.
        var catName = "Категория " + Guid.NewGuid().ToString("N")[..6];
        var cat = (await pos.Post(pos.Owner, $"{F}/categories", new { name = catName, type = "Expense", pnlClass = "OperatingExpense" })).Ok;
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Post(pos.Owner, $"{F}/categories", new { name = catName, type = "Expense", pnlClass = "OperatingExpense" })).Status);
        Assert.Equal(HttpStatusCode.NoContent, (await pos.Post(pos.Owner, $"{F}/categories/{cat["id"]}/archive", new { })).Status);
        Assert.Contains((await pos.Get(pos.Owner, $"{F}/categories")).Ok.AsArray(), c => c!["id"]!.GetValue<Guid>() == cat["id"]!.GetValue<Guid>() && !c["isActive"]!.GetValue<bool>());
    }

    // ---------- §4–6, §10, §13.5: есептер келісімді, экспорт файл береді (FIN-011) ----------

    [Fact]
    public async Task Reports_agree_with_accounts_and_export_returns_files()
    {
        var pos = await api.NewDeviceAsync();
        var product = await pos.CreateProductAsync(price: 3000, stock: 4, purchase: 1000);
        (await pos.SellAsync(pos.Owner, [Pos.Line(product, 2)], [Pos.Cash(6000)])).EnsureOk();

        var accounts = await AccountsAsync(pos);
        var cashFlow = (await pos.Get(pos.Owner, $"{F}/cash-flow?{Period()}")).Ok;
        Assert.Equal(accounts["total"]!.GetValue<decimal>(), cashFlow["closing"]!.GetValue<decimal>()); // Cash Flow соңы = шоттар қосындысы
        Assert.Equal(cashFlow["opening"]!.GetValue<decimal>() + cashFlow["inflow"]!.GetValue<decimal>() - cashFlow["outflow"]!.GetValue<decimal>() + cashFlow["adjustments"]!.GetValue<decimal>(),
            cashFlow["closing"]!.GetValue<decimal>());

        var overview = (await pos.Get(pos.Owner, $"{F}/overview?from={Today()}&to={Today()}")).Ok;
        var keys = overview["kpis"]!.AsArray().Select(k => k!["key"]!.GetValue<string>()).ToList();
        Assert.Equal(["cash", "inflow", "outflow", "netFlow", "profit"], keys);
        Assert.Equal(accounts["total"]!.GetValue<decimal>(), overview["kpis"]![0]!["value"]!.GetValue<decimal>());
        Assert.NotEmpty(overview["recent"]!.AsArray());

        var pnl = (await pos.Get(pos.Owner, $"{F}/reports/pnl?from={Today()}&to={Today()}&compare=true")).Ok;
        var rows = pnl["rows"]!.AsArray().ToDictionary(r => r!["key"]!.GetValue<string>(), r => r!["amount"]!.GetValue<decimal>());
        Assert.Equal(rows["netRevenue"] + rows["cogs"], rows["grossProfit"]); // шығыс жолдары теріс таңбамен
        Assert.Equal(rows["grossProfit"] + rows["operatingExpenses"] + rows["otherIncome"] + rows["otherExpenses"], rows["profitBeforeTax"]);
        Assert.Equal(rows["profitBeforeTax"] + rows["taxes"], rows["netProfit"]);
        Assert.True(rows["cogs"] <= -2000m);

        var profit = (await pos.Get(pos.Owner, $"{F}/reports/product-profit?from={Today()}&to={Today()}&pageSize=100")).Ok;
        var row = profit["rows"]!.AsArray().First(r => r!["productId"]!.GetValue<Guid>() == product)!;
        Assert.Equal(6000m, row["netRevenue"]!.GetValue<decimal>());
        Assert.Equal(2000m, row["cost"]!.GetValue<decimal>());
        Assert.Equal(4000m, row["profit"]!.GetValue<decimal>());

        const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        foreach (var report in new[] { "operations", "pnl", "cashflow", "product-profit", "customer-debts", "supplier-debts" })
        {
            var xlsx = await pos.Raw(pos.Owner, $"{F}/export?report={report}&{Period()}");
            Assert.Equal((HttpStatusCode.OK, Xlsx), (xlsx.Status, xlsx.ContentType));
            Assert.True(xlsx.Length > 0, report);
        }
        var csv = await pos.Raw(pos.Owner, $"{F}/export?report=operations&format=csv&{Period()}");
        Assert.Equal(HttpStatusCode.OK, csv.Status);
        Assert.StartsWith("text/csv", csv.ContentType);
        var cashId = AccountByType(accounts, "Cash")["id"]!.GetValue<Guid>();
        Assert.Equal(HttpStatusCode.OK, (await pos.Raw(pos.Owner, $"{F}/export?report=statement&id={cashId}&{Period()}")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await pos.Raw(pos.Owner, $"{F}/export?report=nope")).Status);
    }

    // ---------- FIN-012, §18: кассирге жабық, бөтен дүкен 403, тарихты жүктеу қайталанбайды ----------

    [Fact]
    public async Task Cashier_is_forbidden_foreign_store_is_forbidden_and_backfill_is_idempotent()
    {
        var pos = await api.NewDeviceAsync();
        foreach (var path in new[] { "overview", "accounts", "operations", "reports/pnl", "cash-flow", "customer-debts", "supplier-debts" })
            Assert.Equal(HttpStatusCode.Forbidden, (await pos.Get(pos.Cashier, $"{F}/{path}")).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await pos.Post(pos.Cashier, $"{F}/transfers", new { amount = 1m })).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await pos.Get(pos.Owner, $"{F}/overview?storeIds={Guid.NewGuid()}")).Status);

        var product = await pos.CreateProductAsync(price: 700, stock: 2);
        (await pos.SellAsync(pos.Owner, [Pos.Line(product, 1)], [Pos.Cash(700)])).EnsureOk();
        var first = (await pos.Post(pos.Owner, $"{F}/backfill", new { })).Ok;
        var second = (await pos.Post(pos.Owner, $"{F}/backfill", new { })).Ok;
        Assert.Equal(0, second["posted"]!.GetValue<int>()); // тірі операциялар қайта жазылмайды (FIN-008)
        Assert.True(second["skipped"]!.GetValue<int>() >= first["skipped"]!.GetValue<int>() + first["posted"]!.GetValue<int>());
    }

    private static async Task<HttpStatusCode> DeleteAsync(Pos pos, string path)
    {
        var res = await pos.Delete(pos.Owner, path);
        return res.Status;
    }
}
