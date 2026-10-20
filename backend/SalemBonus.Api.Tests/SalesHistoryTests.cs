using System.Net;
using System.Text.Json.Nodes;
using static SalemBonus.Api.Tests.Pos;

namespace SalemBonus.Api.Tests;

/// <summary>
/// «Продажи» (ТЗ «Продажи» v1.0): тарих, сүзгілер, KPI, чек тарихы, экспорт, қайтару ережелері, құқықтар.
/// Сатылымдар кассадан (регистр құрылғысы), тарих — кабинеттен (sales.view).
/// </summary>
[Collection(ApiCollection.Name)]
public class SalesHistoryTests(ApiFixture api)
{
    private const string Sales = "/api/pos/v1/sales";

    private static object Return(Guid saleItemId, decimal quantity, string? reasonCode = "NotSuitable", string? reason = null,
        string? condition = null, string? stockAction = null, string refundMethod = "Cash") => new
    {
        clientRequestId = Guid.NewGuid(),
        items = new[] { new { saleItemId, quantity } },
        refundMethod,
        reasonCode,
        reason,
        condition,
        stockAction,
    };

    private static Guid ItemId(JsonNode sale) => sale["items"]![0]!["id"]!.GetValue<Guid>();

    private static async Task<JsonArray> HistoryAsync(Pos pos, string query) =>
        (await pos.Get(pos.Owner, $"{Sales}/history?{query}")).Ok["items"]!.AsArray();

    private static string Today() => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5)).ToString("yyyy-MM-dd"); // Asia/Almaty

    // ---------- Тізім және сүзгілер (§6–7) ----------

    [Fact]
    public async Task History_finds_a_receipt_by_number_article_barcode_and_filters_by_method_and_cashier()
    {
        var pos = await api.NewDeviceAsync();
        var tag = Guid.NewGuid().ToString("N")[..6];
        var product = await pos.CreateProductAsync(price: 1200, stock: 10, article: $"ART-{tag}", barcode: $"29{tag}00");
        var cardSale = (await pos.SellAsync(pos.Owner, [Line(product, 2)], [Card(2400)])).Ok;
        var cashSale = (await pos.SellAsync(pos.Cashier, [Line(product, 1)], [Cash(1200)])).Ok;
        var number = cardSale["number"]!.GetValue<long>();
        var cashierId = (await pos.Get(pos.Cashier, "/api/staff/v1/auth/me")).Ok["id"]!.GetValue<Guid>();

        var byNumber = await HistoryAsync(pos, $"search=%E2%84%96{number}"); // «№1284»
        var byArticle = await HistoryAsync(pos, $"search=ART-{tag}");
        var byBarcode = await HistoryAsync(pos, $"search=29{tag}00");
        var byMethod = await HistoryAsync(pos, $"search=ART-{tag}&method=Card");
        var byCashier = await HistoryAsync(pos, $"search=ART-{tag}&staffUserId={cashierId}");

        Assert.Single(byNumber, s => s!["id"]!.GetValue<Guid>() == cardSale["id"]!.GetValue<Guid>());
        Assert.Equal(2, byArticle.Count);
        Assert.Equal(2, byBarcode.Count);
        Assert.Single(byMethod, s => s!["number"]!.GetValue<long>() == number);
        Assert.Single(byCashier, s => s!["id"]!.GetValue<Guid>() == cashSale["id"]!.GetValue<Guid>());
        var row = byNumber[0]!;
        Assert.Equal(2m, row["units"]!.GetValue<decimal>());
        Assert.Equal(2400m, row["total"]!.GetValue<decimal>());
        Assert.Equal(2400m, row["net"]!.GetValue<decimal>());
        Assert.Equal("Completed", row["status"]!.GetValue<string>());
        Assert.Contains("Card", row["methods"]!.AsArray().Select(m => m!.GetValue<string>()));
    }

    [Fact]
    public async Task Original_total_never_changes_after_a_return_and_status_filters_work()
    {
        var pos = await api.NewDeviceAsync();
        var tag = Guid.NewGuid().ToString("N")[..6];
        var product = await pos.CreateProductAsync(price: 1000, stock: 10, article: $"RET-{tag}");
        var sale = (await pos.SellAsync(pos.Owner, [Line(product, 3)], [Cash(3000)])).Ok;
        var saleId = sale["id"]!.GetValue<Guid>();
        (await pos.Post(pos.Owner, $"{Sales}/{saleId}/returns", Return(ItemId(sale), 1))).EnsureOk();

        var row = Assert.Single(await HistoryAsync(pos, $"search=RET-{tag}"))!;
        Assert.Equal(3000m, row["total"]!.GetValue<decimal>()); // тарихи сома өзгермейді
        Assert.Equal(1000m, row["returned"]!.GetValue<decimal>());
        Assert.Equal(2000m, row["net"]!.GetValue<decimal>());
        Assert.Equal("PartiallyReturned", row["status"]!.GetValue<string>());
        Assert.Single(await HistoryAsync(pos, $"search=RET-{tag}&hasReturn=yes"));
        Assert.Empty(await HistoryAsync(pos, $"search=RET-{tag}&hasReturn=no"));
        Assert.Single(await HistoryAsync(pos, $"search=RET-{tag}&status=PartiallyReturned"));
        Assert.Empty(await HistoryAsync(pos, $"search=RET-{tag}&status=Completed"));
    }

    [Fact]
    public async Task Debt_sale_is_shown_as_debt_until_it_is_repaid()
    {
        var pos = await api.NewDeviceAsync();
        var tag = Guid.NewGuid().ToString("N")[..6];
        var product = await pos.CreateProductAsync(price: 5000, stock: 5, article: $"DEBT-{tag}");
        var customer = await pos.CreateCustomerAsync();
        var sale = (await pos.SellAsync(pos.Owner, [Line(product, 1)], [Cash(1000), new
        {
            method = "Debt", amount = 4000m, dueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(7)),
        }], customerId: customer["id"]!.GetValue<Guid>())).Ok;

        var row = Assert.Single(await HistoryAsync(pos, $"search=DEBT-{tag}"))!;
        Assert.Equal("Debt", row["status"]!.GetValue<string>());
        Assert.True(row["hasOpenDebt"]!.GetValue<bool>());
        Assert.Equal(4000m, row["debtRemaining"]!.GetValue<decimal>());
        Assert.Single(await HistoryAsync(pos, $"search=DEBT-{tag}&status=Debt"));

        var debtId = sale["debt"]!["id"]!.GetValue<Guid>();
        (await pos.Post(pos.Owner, $"/api/pos/v1/debts/{debtId}/payments", new { amount = 4000m, method = "Cash" })).EnsureOk();
        row = Assert.Single(await HistoryAsync(pos, $"search=DEBT-{tag}"))!;
        Assert.Equal("Completed", row["status"]!.GetValue<string>());
        Assert.Empty(await HistoryAsync(pos, $"search=DEBT-{tag}&status=Debt"));
        var events = (await pos.Get(pos.Owner, $"{Sales}/{sale["id"]}/history")).Ok.AsArray().Select(e => e!["type"]!.GetValue<string>()).ToList();
        Assert.Contains("debt", events);
        Assert.Contains("debt.repaid", events);
    }

    // ---------- KPI (§5, §11) ----------

    [Fact]
    public async Task Summary_agrees_with_the_table_and_counts_returns_by_return_date()
    {
        var pos = await api.NewDeviceAsync();
        var product = await pos.CreateProductAsync(price: 2000, stock: 20);
        var customerId = (await pos.CreateCustomerAsync())["id"]!.GetValue<Guid>();
        var before = (await pos.Get(pos.Owner, $"{Sales}/summary?from={Today()}&to={Today()}")).Ok;

        var a = (await pos.SellAsync(pos.Owner, [Line(product, 2)], [Cash(4000)])).Ok;
        var b = (await pos.SellAsync(pos.Owner, [Line(product, 1)], [Card(1800)], discount: new { kind = "Amount", value = 200m })).Ok;
        (await pos.SellAsync(pos.Owner, [Line(product, 1)], [Card(2000)], customerId: customerId)).EnsureOk();
        (await pos.Post(pos.Owner, $"{Sales}/{a["id"]}/returns", Return(ItemId(a), 1))).EnsureOk();
        var after = (await pos.Get(pos.Owner, $"{Sales}/summary?from={Today()}&to={Today()}")).Ok;
        decimal Delta(string key) => after[key]!.GetValue<decimal>() - before[key]!.GetValue<decimal>();

        Assert.Equal(3m, Delta("salesCount"));
        Assert.Equal(4000m + 1800m + 2000m, Delta("salesTotal")); // тарихи сомалар, қайтарымсыз
        Assert.Equal(1m, Delta("returnsCount"));
        Assert.Equal(2000m, Delta("returnsTotal"));
        Assert.Equal(4m, Delta("units"));
        Assert.Equal(200m, Delta("discounts"));
        Assert.Equal(1m, Delta("discountCount"));
        Assert.Equal(1m, Delta("withReturn"));
        var count = after["salesCount"]!.GetValue<decimal>();
        Assert.Equal(Math.Round(after["salesTotal"]!.GetValue<decimal>() / count, 2), after["averageCheck"]!.GetValue<decimal>());
        _ = b;
    }

    // ---------- Қайтару ережелері (§10) ----------

    [Fact]
    public async Task Return_requires_a_reason_and_a_defect_is_written_off_instead_of_going_back_to_stock()
    {
        var pos = await api.NewDeviceAsync();
        var product = await pos.CreateProductAsync(price: 1000, stock: 5);
        var sale = (await pos.SellAsync(pos.Owner, [Line(product, 2)], [Cash(2000)])).Ok;
        var saleId = sale["id"]!.GetValue<Guid>();
        Assert.Equal(3m, await pos.StockAsync(product));

        var noReason = await pos.Post(pos.Owner, $"{Sales}/{saleId}/returns", Return(ItemId(sale), 1, reasonCode: null));
        var otherNoComment = await pos.Post(pos.Owner, $"{Sales}/{saleId}/returns", Return(ItemId(sale), 1, reasonCode: "Other"));
        var badAction = await pos.Post(pos.Owner, $"{Sales}/{saleId}/returns", Return(ItemId(sale), 1, condition: "Sellable", stockAction: "WriteOff"));
        Assert.Equal(HttpStatusCode.BadRequest, noReason.Status);
        Assert.Equal(HttpStatusCode.BadRequest, otherNoComment.Status);
        Assert.Equal(HttpStatusCode.BadRequest, badAction.Status);
        Assert.Equal(3m, await pos.StockAsync(product));

        var defect = (await pos.Post(pos.Owner, $"{Sales}/{saleId}/returns", Return(ItemId(sale), 1, reasonCode: "Defect", reason: "Трещина"))).Ok;
        var r = defect["returns"]![0]!;
        Assert.Equal("Defect", r["reasonCode"]!.GetValue<string>());
        Assert.Equal("Damaged", r["condition"]!.GetValue<string>());
        Assert.Equal("WriteOff", r["stockAction"]!.GetValue<string>());
        Assert.Equal(3m, await pos.StockAsync(product)); // бүлінген тауар сатылатын қалдыққа түспейді
        Assert.Equal(1000m, defect["returnedAmount"]!.GetValue<decimal>());
        var types = (await pos.Get(pos.Owner, $"/api/pos/v1/inventory/movements?productId={product}")).Ok["items"]!.AsArray()
            .Select(m => m!["type"]!.GetValue<string>()).ToList();
        Assert.Contains("Return", types);
        Assert.Contains("WriteOff", types);

        var good = (await pos.Post(pos.Owner, $"{Sales}/{saleId}/returns", Return(ItemId(sale), 1, reasonCode: "NotSuitable"))).Ok;
        Assert.Equal("Returned", good["status"]!.GetValue<string>());
        Assert.Equal(4m, await pos.StockAsync(product));
    }

    [Fact]
    public async Task Back_office_return_without_a_register_is_recorded_against_the_receipt_register()
    {
        var register = await api.NewDeviceAsync();
        var product = await register.CreateProductAsync(price: 1500, stock: 5);
        var sale = (await register.SellAsync(register.Owner, [Line(product, 1)], [Card(1500)])).Ok;
        var saleId = sale["id"]!.GetValue<Guid>();
        var office = await api.NewDeviceAsync(activateRegister: false);

        var cashierTry = await office.Post(office.Cashier, $"{Sales}/{saleId}/returns", Return(ItemId(sale), 1));
        var ownerReturn = (await office.Post(office.Owner, $"{Sales}/{saleId}/returns", Return(ItemId(sale), 1, reasonCode: "WrongSale", refundMethod: "Card"))).Ok;

        Assert.NotEqual(HttpStatusCode.OK, cashierTry.Status); // кассирде sales.view жоқ, кассасыз қайтара алмайды
        Assert.True(ownerReturn["returns"]![0]!["fromBackOffice"]!.GetValue<bool>());
        Assert.Equal("Returned", ownerReturn["status"]!.GetValue<string>());
        Assert.Equal(5m, await register.StockAsync(product));
        var events = (await office.Get(office.Owner, $"{Sales}/{saleId}/history")).Ok.AsArray();
        Assert.Contains(events, e => e!["type"]!.GetValue<string>() == "return" && e["returnId"] != null);
        Assert.Contains(events, e => e!["type"]!.GetValue<string>() == "sale");
    }

    // ---------- Экспорт және құқықтар (§12–13) ----------

    [Fact]
    public async Task Export_returns_an_excel_file_and_the_section_is_closed_to_a_cashier()
    {
        var pos = await api.NewDeviceAsync();
        var product = await pos.CreateProductAsync(price: 700, stock: 3);
        (await pos.SellAsync(pos.Owner, [Line(product, 1)], [Cash(700)])).EnsureOk();

        var export = await pos.Raw(pos.Owner, $"{Sales}/export?from={Today()}&to={Today()}");
        Assert.Equal(HttpStatusCode.OK, export.Status);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", export.ContentType);
        Assert.True(export.Length > 1000);

        Assert.Equal(HttpStatusCode.Forbidden, (await pos.Get(pos.Cashier, $"{Sales}/history")).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await pos.Get(pos.Cashier, $"{Sales}/summary")).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await pos.Raw(pos.Cashier, $"{Sales}/export")).Status);
        Assert.Equal(HttpStatusCode.OK, (await pos.Get(pos.Owner, $"{Sales}/filters")).Status);
    }
}
