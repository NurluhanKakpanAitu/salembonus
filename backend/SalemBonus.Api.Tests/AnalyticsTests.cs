using System.Net;
using System.Text.Json.Nodes;
using static SalemBonus.Api.Tests.Pos;

namespace SalemBonus.Api.Tests;

/// <summary>
/// «Статистика» v1.1 (ТЗ §30 критерийлері): күнтізбелік бакеттер, формулалар сөздігі, аралас төлем бір рет,
/// жаңа/қайталама клиент компания тарихымен, тауар (сатылды/қайтарылды/таза), қойма, қаржы, экспорт, құқықтар.
/// Басқа тесттер де сатады — сондықтан көрсеткіштер «дейін/кейін» айырмасымен тексеріледі.
/// </summary>
[Collection(ApiCollection.Name)]
public class AnalyticsTests(ApiFixture api)
{
    private const string A = "/api/pos/v1/analytics";

    private static string Today() => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5)).ToString("yyyy-MM-dd"); // Asia/Almaty
    private static string Day() => $"from={Today()}&to={Today()}";

    private static JsonNode Kpi(JsonNode dto, string key) =>
        dto["kpis"]!.AsArray().First(k => k!["key"]!.GetValue<string>() == key)!;

    private static decimal Val(JsonNode dto, string key) => Kpi(dto, key)["value"]!.GetValue<decimal>();

    private static decimal Method(JsonNode overview, string method) =>
        overview["paymentMethods"]!["items"]!.AsArray().First(m => m!["method"]!.GetValue<string>() == method)!["amount"]!.GetValue<decimal>();

    private static object ReturnBody(JsonNode sale, decimal quantity) => new
    {
        clientRequestId = Guid.NewGuid(),
        items = new[] { new { saleItemId = sale["items"]![0]!["id"]!.GetValue<Guid>(), quantity } },
        refundMethod = "Cash",
        reasonCode = "NotSuitable",
    };

    // ---------- «Динамика продаж» (§9–10, §27) ----------

    [Fact]
    public async Task Trend_has_exactly_one_bucket_per_calendar_day_and_switches_granularity_by_length()
    {
        var pos = await api.NewDeviceAsync(activateRegister: false);

        var september = (await pos.Get(pos.Owner, $"{A}/sales-trend?from=2020-09-01&to=2020-09-30")).Ok;
        var data = september["data"]!.AsArray();
        Assert.Equal("day", september["granularity"]!.GetValue<string>());
        Assert.Equal(30, data.Count);
        var days = data.Select(d => DateOnly.Parse(d!["bucketStart"]!.GetValue<string>())).ToList();
        Assert.Equal(30, days.Distinct().Count());
        Assert.Equal(days.OrderBy(d => d), days); // тек хронологиялық
        Assert.Equal(new DateOnly(2020, 9, 1), days[0]);
        Assert.Equal(new DateOnly(2020, 9, 30), days[^1]);
        Assert.All(data, d => Assert.Equal(0m, d!["revenue"]!.GetValue<decimal>())); // сатылымсыз күндер — 0, түсіп қалмайды

        var quarter = (await pos.Get(pos.Owner, $"{A}/sales-trend?from=2020-01-01&to=2020-04-30")).Ok; // 121 күн
        Assert.Equal("week", quarter["granularity"]!.GetValue<string>());
        var first = quarter["data"]![0]!;
        Assert.Equal("2020-01-01", first["bucketStart"]!.GetValue<string>());
        Assert.Equal("2020-01-05", first["bucketEnd"]!.GetValue<string>()); // апта жексенбіде бітеді

        var year = (await pos.Get(pos.Owner, $"{A}/sales-trend?from=2020-01-01&to=2020-12-31")).Ok;
        Assert.Equal("month", year["granularity"]!.GetValue<string>());
        Assert.Equal(12, year["data"]!.AsArray().Count);
    }

    // ---------- KPI мен формулалар (§8, §11, §25) ----------

    [Fact]
    public async Task Overview_follows_the_formula_dictionary_and_counts_a_mixed_payment_once()
    {
        var pos = await api.NewDeviceAsync();
        var product = await pos.CreateProductAsync(price: 4000, stock: 20, purchase: 2500);
        var before = (await pos.Get(pos.Owner, $"{A}/overview?{Day()}")).Ok;
        var trendBefore = (await pos.Get(pos.Owner, $"{A}/sales-trend?{Day()}")).Ok["data"]![0]!;

        var cash = (await pos.SellAsync(pos.Owner, [Line(product, 3)], [Cash(12_000)])).Ok;
        (await pos.SellAsync(pos.Owner, [Line(product, 1)], [Card(1500), Cash(2500)])).EnsureOk(); // аралас
        (await pos.Post(pos.Owner, $"/api/pos/v1/sales/{cash["id"]}/returns", ReturnBody(cash, 1))).EnsureOk();

        var after = (await pos.Get(pos.Owner, $"{A}/overview?{Day()}")).Ok;
        var trendAfter = (await pos.Get(pos.Owner, $"{A}/sales-trend?{Day()}")).Ok["data"]![0]!;
        decimal D(string key) => Val(after, key) - Val(before, key);

        Assert.Equal(16_000m, D("revenue"));                  // қайтарымға дейін
        Assert.Equal(2m, D("salesCount"));
        Assert.Equal(1m, D("returns"));
        Assert.Equal(4000m, Kpi(after, "returns")["amount"]!.GetValue<decimal>() - Kpi(before, "returns")["amount"]!.GetValue<decimal>());
        Assert.Equal(12_000m, D("netSales"));                 // выручка − қайтарым
        var count = Val(after, "salesCount");
        Assert.Equal(Math.Round(Val(after, "revenue") / count, 0), Val(after, "averageCheck"));

        Assert.Equal(14_500m, Method(after, "Cash") - Method(before, "Cash"));
        Assert.Equal(1500m, Method(after, "Card") - Method(before, "Card"));
        // Аралас чек бөліктері бойынша: төлем түрлерінің қосындысы выручкаға тең, екі рет саналмайды.
        Assert.Equal(Val(after, "revenue"), after["paymentMethods"]!["total"]!.GetValue<decimal>());

        Assert.Equal(16_000m, trendAfter["revenue"]!.GetValue<decimal>() - trendBefore["revenue"]!.GetValue<decimal>());
        Assert.Equal(2, trendAfter["salesCount"]!.GetValue<int>() - trendBefore["salesCount"]!.GetValue<int>());
    }

    [Fact]
    public async Task New_and_repeat_customers_use_the_company_history_and_anonymous_receipts_are_separate()
    {
        var pos = await api.NewDeviceAsync();
        var product = await pos.CreateProductAsync(price: 1000, stock: 20);
        var regular = (await pos.CreateCustomerAsync())["id"]!.GetValue<Guid>();
        var newcomer = (await pos.CreateCustomerAsync())["id"]!.GetValue<Guid>();
        var old = (await pos.SellAsync(pos.Owner, [Line(product, 1)], [Card(1000)], customerId: regular)).Ok;
        await api.SqlAsync($"UPDATE pos.sales SET \"CreatedAt\" = now() - interval '40 days' WHERE \"Id\" = '{old["id"]}'");
        var before = (await pos.Get(pos.Owner, $"{A}/overview?{Day()}")).Ok;

        (await pos.SellAsync(pos.Owner, [Line(product, 1)], [Card(1000)], customerId: regular)).EnsureOk();
        (await pos.SellAsync(pos.Owner, [Line(product, 1)], [Card(1000)], customerId: newcomer)).EnsureOk();
        (await pos.SellAsync(pos.Owner, [Line(product, 1)], [Card(1000)])).EnsureOk(); // клиентсіз

        var after = (await pos.Get(pos.Owner, $"{A}/overview?{Day()}")).Ok;
        int C(JsonNode o, string key) => o["customers"]![key]!.GetValue<int>();
        Assert.Equal(1, C(after, "repeat") - C(before, "repeat"));
        Assert.Equal(1, C(after, "new") - C(before, "new"));
        Assert.Equal(2, C(after, "buyers") - C(before, "buyers"));
        Assert.Equal(1, C(after, "unidentifiedReceipts") - C(before, "unidentifiedReceipts"));

        var repeatList = (await pos.Get(pos.Owner, $"{A}/tabs/customers?{Day()}&segment=repeat&pageSize=100")).Ok["table"]!["rows"]!.AsArray();
        var newList = (await pos.Get(pos.Owner, $"{A}/tabs/customers?{Day()}&segment=new&pageSize=100")).Ok["table"]!["rows"]!.AsArray();
        Assert.Contains(repeatList, r => r!["customerId"]!.GetValue<Guid>() == regular);
        Assert.DoesNotContain(repeatList, r => r!["customerId"]!.GetValue<Guid>() == newcomer);
        Assert.Contains(newList, r => r!["customerId"]!.GetValue<Guid>() == newcomer);
    }

    // ---------- Вкладки (§16–19) ----------

    [Fact]
    public async Task Products_tab_separates_sold_returned_and_net_and_gives_profit_from_historical_cost()
    {
        var pos = await api.NewDeviceAsync();
        var tag = Guid.NewGuid().ToString("N")[..6];
        var product = await pos.CreateProductAsync(price: 3000, stock: 10, purchase: 1800, article: $"AN-{tag}");
        var sale = (await pos.SellAsync(pos.Owner, [Line(product, 3)], [Cash(9000)])).Ok;
        (await pos.Post(pos.Owner, $"/api/pos/v1/sales/{sale["id"]}/returns", ReturnBody(sale, 1))).EnsureOk();

        var tab = (await pos.Get(pos.Owner, $"{A}/tabs/products?{Day()}&search=AN-{tag}")).Ok;
        var row = Assert.Single(tab["table"]!["rows"]!.AsArray())!;
        Assert.Equal(3m, row["sold"]!.GetValue<decimal>());
        Assert.Equal(1m, row["returned"]!.GetValue<decimal>());
        Assert.Equal(2m, row["netQty"]!.GetValue<decimal>());
        Assert.Equal(6000m, row["amount"]!.GetValue<decimal>());
        Assert.Equal(3600m, row["cost"]!.GetValue<decimal>());
        Assert.Equal(2400m, row["profit"]!.GetValue<decimal>());
    }

    [Fact]
    public async Task Inventory_suppliers_and_finance_tabs_answer_and_expenses_are_unavailable_not_zero()
    {
        var pos = await api.NewDeviceAsync();
        var tag = Guid.NewGuid().ToString("N")[..6];
        await pos.CreateProductAsync(price: 500, stock: 7, purchase: 300, article: $"ST-{tag}");

        var inventory = (await pos.Get(pos.Owner, $"{A}/tabs/inventory?{Day()}&search=ST-{tag}")).Ok;
        var row = Assert.Single(inventory["table"]!["rows"]!.AsArray())!;
        Assert.Equal(7m, row["stock"]!.GetValue<decimal>());
        Assert.True(Val(inventory, "withStock") >= 1);

        (await pos.Get(pos.Owner, $"{A}/tabs/suppliers?{Day()}")).EnsureOk();
        var finance = (await pos.Get(pos.Owner, $"{A}/tabs/finance?{Day()}")).Ok;
        var expenses = Kpi(finance, "expenses");
        Assert.False(expenses["available"]!.GetValue<bool>());
        Assert.Null(expenses["value"]);
        Assert.Equal(Val(finance, "inflow") - Val(finance, "outflow"), Val(finance, "netFlow"));
        foreach (var t in new[] { "sales", "bonus", "customers", "employees", "registers" })
            (await pos.Get(pos.Owner, $"{A}/tabs/{t}?{Day()}")).EnsureOk();
    }

    // ---------- Экспорт және құқықтар (§6, §28) ----------

    [Fact]
    public async Task Export_gives_xlsx_and_csv_and_statistics_is_closed_to_a_cashier_and_foreign_stores()
    {
        var pos = await api.NewDeviceAsync(activateRegister: false);

        var xlsx = await pos.Raw(pos.Owner, $"{A}/export?tab=sales&format=xlsx&{Day()}");
        var csv = await pos.Raw(pos.Owner, $"{A}/export?tab=overview&format=csv&{Day()}");
        Assert.Equal(HttpStatusCode.OK, xlsx.Status);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", xlsx.ContentType);
        Assert.Equal(HttpStatusCode.OK, csv.Status);
        Assert.Equal("text/csv", csv.ContentType);

        Assert.Equal(HttpStatusCode.Forbidden, (await pos.Get(pos.Cashier, $"{A}/overview")).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await pos.Get(pos.Cashier, $"{A}/tabs/sales")).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await pos.Raw(pos.Cashier, $"{A}/export?tab=sales")).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await pos.Get(pos.Owner, $"{A}/overview?storeIds={Guid.NewGuid()}")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await pos.Get(pos.Owner, $"{A}/tabs/unknown")).Status);
    }
}
