using System.Net;
using static SalemBonus.Api.Tests.Pos;

namespace SalemBonus.Api.Tests;

/// <summary>Статистика (ТЗ «Статистика» §18, AC-4…6) және дүкен/құқық шекаралары (§21).</summary>
[Collection(ApiCollection.Name)]
public class DashboardAndAccessTests(ApiFixture api)
{
    private static decimal Kpi(ApiResult d, string key) =>
        d.Ok["kpi"]!.AsArray().First(k => k!["key"]!.GetValue<string>() == key)!["value"]!.GetValue<decimal>();

    [Fact]
    public async Task Revenue_is_sales_minus_returns_and_every_block_agrees_with_it()
    {
        var pos = await api.NewDeviceAsync();
        var product = await pos.CreateProductAsync(price: 4000, stock: 10, purchase: 2500);
        var before = await pos.Get(pos.Owner, "/api/pos/v1/dashboard");

        var sale = (await pos.SellAsync(pos.Owner, [Line(product, 3)], [Cash(12_000)])).Ok;
        (await pos.Post(pos.Owner, $"/api/pos/v1/sales/{sale["id"]}/returns", new
        {
            clientRequestId = Guid.NewGuid(),
            items = new[] { new { saleItemId = sale["items"]![0]!["id"]!.GetValue<Guid>(), quantity = 1m } },
            refundMethod = "Cash",
        })).EnsureOk();
        var after = await pos.Get(pos.Owner, "/api/pos/v1/dashboard");

        Assert.Equal(8000m, Kpi(after, "revenue") - Kpi(before, "revenue"));
        Assert.Equal(1m, Kpi(after, "sales") - Kpi(before, "sales"));
        Assert.Equal(Kpi(after, "revenue"), after.Dec("financial.revenue"));
        Assert.Equal(after.Dec("financial.revenue"), after.Ok["categories"]!.AsArray().Sum(c => c!["amount"]!.GetValue<decimal>()));
        // Екі дана сатылды деп есептеледі — өзіндік құн тек соларға.
        Assert.Equal(5000m, after.Dec("financial.cost") - before.Dec("financial.cost"));
        Assert.Equal(12_000m - 4000m, after.Dec("cash.expected") - before.Dec("cash.expected"));
    }

    [Fact]
    public async Task Dashboard_is_closed_to_a_cashier()
    {
        var pos = await api.NewDeviceAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await pos.Get(pos.Cashier, "/api/pos/v1/dashboard")).Status);
        Assert.Equal(HttpStatusCode.OK, (await pos.Get(pos.Owner, "/api/pos/v1/dashboard")).Status);
    }

    [Fact]
    public async Task Store_that_the_staff_member_does_not_belong_to_is_forbidden()
    {
        var pos = await api.NewDeviceAsync();

        var res = await pos.Send(HttpMethod.Get, pos.Owner, "/api/pos/v1/dashboard", null, storeId: Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.Forbidden, res.Status);
    }

    [Fact]
    public async Task Sale_is_impossible_on_a_device_that_is_not_a_register()
    {
        var registered = await api.NewDeviceAsync();
        var product = await registered.CreateProductAsync(price: 1000, stock: 3);
        var browser = await api.NewDeviceAsync(activateRegister: false);

        var res = await browser.SellAsync(browser.Owner, [Line(product, 1)], [Card(1000)]);

        Assert.NotEqual(HttpStatusCode.OK, res.Status);
        Assert.Equal(3m, await registered.StockAsync(product));
    }
}
