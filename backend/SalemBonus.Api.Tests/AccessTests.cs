using System.Net;
using static SalemBonus.Api.Tests.Pos;

namespace SalemBonus.Api.Tests;

/// <summary>Дүкен мен құқық шекаралары: статистика кассирге жабық, бөтен дүкен — 403, кассасыз құрылғыда сату жоқ.</summary>
[Collection(ApiCollection.Name)]
public class AccessTests(ApiFixture api)
{
    [Fact]
    public async Task Statistics_is_closed_to_a_cashier()
    {
        var pos = await api.NewDeviceAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await pos.Get(pos.Cashier, "/api/pos/v1/analytics/overview")).Status);
        Assert.Equal(HttpStatusCode.OK, (await pos.Get(pos.Owner, "/api/pos/v1/analytics/overview")).Status);
    }

    [Fact]
    public async Task Store_that_the_staff_member_does_not_belong_to_is_forbidden()
    {
        var pos = await api.NewDeviceAsync();

        var res = await pos.Send(HttpMethod.Get, pos.Owner, "/api/pos/v1/analytics/overview", null, storeId: Guid.NewGuid().ToString());

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
