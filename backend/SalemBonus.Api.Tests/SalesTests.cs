using System.Net;
using SalemBonus.Infrastructure.Persistence;
using static SalemBonus.Api.Tests.Pos;

namespace SalemBonus.Api.Tests;

/// <summary>Сатылым (ТЗ «Касса»): сомалар, қалдық, идемпотенттік, төлем тексерісі, бонус, жеңілдік шегі.</summary>
[Collection(ApiCollection.Name)]
public class SalesTests(ApiFixture api)
{
    [Fact]
    public async Task Cash_sale_writes_totals_decreases_stock_and_numbers_receipts_in_order()
    {
        var pos = await api.NewDeviceAsync();
        var product = await pos.CreateProductAsync(price: 1500, stock: 10);

        var first = await pos.SellAsync(pos.Owner, [Line(product, 2)], [Cash(3000, received: 5000)]);
        var second = await pos.SellAsync(pos.Owner, [Line(product, 1)], [Cash(1500)]);

        Assert.Equal(3000m, first.Dec("total"));
        Assert.Equal(2000m, first.Ok["payments"]![0]!["change"]!.GetValue<decimal>());
        Assert.Equal(first.Dec("number") + 1, second.Dec("number"));
        Assert.Equal(7m, await pos.StockAsync(product));
    }

    [Fact]
    public async Task Repeated_request_with_same_id_returns_the_same_receipt_and_sells_once()
    {
        var pos = await api.NewDeviceAsync();
        var product = await pos.CreateProductAsync(price: 800, stock: 5);
        var requestId = Guid.NewGuid();

        var a = await pos.SellAsync(pos.Owner, [Line(product, 2)], [Card(1600)], requestId: requestId);
        var b = await pos.SellAsync(pos.Owner, [Line(product, 2)], [Card(1600)], requestId: requestId);

        Assert.Equal(a.Ok["id"]!.GetValue<Guid>(), b.Ok["id"]!.GetValue<Guid>());
        Assert.Equal(3m, await pos.StockAsync(product));
    }

    [Fact]
    public async Task Payments_must_cover_the_total_exactly()
    {
        var pos = await api.NewDeviceAsync();
        var product = await pos.CreateProductAsync(price: 1000, stock: 5);

        var underpaid = await pos.SellAsync(pos.Owner, [Line(product, 1)], [Card(900)]);
        var overpaid = await pos.SellAsync(pos.Owner, [Line(product, 1)], [Card(1000), new { method = "Qr", amount = 100m }]);
        var cashShort = await pos.SellAsync(pos.Owner, [Line(product, 1)], [Cash(1000, received: 500)]);
        var mixed = await pos.SellAsync(pos.Owner, [Line(product, 1)], [Card(400), Cash(600)]);

        Assert.Equal(HttpStatusCode.BadRequest, underpaid.Status);
        Assert.Equal(HttpStatusCode.BadRequest, overpaid.Status);
        Assert.Equal(HttpStatusCode.BadRequest, cashShort.Status);
        Assert.Equal(2, mixed.Ok["payments"]!.AsArray().Count);
        Assert.Equal(4m, await pos.StockAsync(product));
    }

    [Fact]
    public async Task Cashier_cannot_sell_more_than_in_stock()
    {
        var pos = await api.NewDeviceAsync();
        var product = await pos.CreateProductAsync(price: 500, stock: 2);

        var res = await pos.SellAsync(pos.Cashier, [Line(product, 3)], [Card(1500)]);

        Assert.Equal(HttpStatusCode.BadRequest, res.Status);
        Assert.Equal(2m, await pos.StockAsync(product));
    }

    [Fact]
    public async Task Discount_above_cashier_limit_needs_a_valid_pin_of_an_approver()
    {
        var pos = await api.NewDeviceAsync();
        var product = await pos.CreateProductAsync(price: 10_000, stock: 5);
        var context = (await pos.Get(pos.Cashier, "/api/pos/v1/cashier/context")).Ok;
        Assert.True(context["maxDiscountPercent"]!.GetValue<decimal>() < 10);
        var ownerId = context["approvers"]!.AsArray()[0]!["id"]!.GetValue<Guid>();
        var discount = new { kind = "Percent", value = 10m };

        var noApproval = await pos.SellAsync(pos.Cashier, [Line(product, 1)], [Card(9000)], discount: discount);
        var wrongPin = await pos.SellAsync(pos.Cashier, [Line(product, 1)], [Card(9000)], discount: discount,
            approval: new { staffUserId = ownerId, pin = "0000" });
        var approved = await pos.SellAsync(pos.Cashier, [Line(product, 1)], [Card(9000)], discount: discount,
            approval: new { staffUserId = ownerId, pin = StaffSeeder.OwnerPin });

        Assert.False(noApproval.Status == HttpStatusCode.OK);
        Assert.False(wrongPin.Status == HttpStatusCode.OK);
        Assert.Equal(1000m, approved.Dec("discountAmount"));
        Assert.Equal(9000m, approved.Dec("total"));
        Assert.Equal(4m, await pos.StockAsync(product));
    }

    [Fact]
    public async Task Bonus_is_accrued_to_the_customer_and_redemption_is_limited_by_balance()
    {
        var pos = await api.NewDeviceAsync();
        var product = await pos.CreateProductAsync(price: 20_000, stock: 10);
        var customer = await pos.CreateCustomerAsync();
        var customerId = customer["id"]!.GetValue<Guid>();

        var sale = await pos.SellAsync(pos.Owner, [Line(product, 1)], [Card(20_000)], customerId: customerId);
        var accrued = (int)sale.Dec("bonusAccrued");
        var balance = (int)sale.Dec("customer.bonusBalance");
        Assert.True(accrued > 0);
        Assert.Equal(customer["balance"]!.GetValue<int>() + accrued, balance);

        var tooMuch = await pos.SellAsync(pos.Owner, [Line(product, 1)], [Card(20_000 - balance - 1)],
            customerId: customerId, bonusRedeem: balance + 1);
        var withoutCustomer = await pos.SellAsync(pos.Owner, [Line(product, 1)], [Card(19_990)], bonusRedeem: 10);
        Assert.Equal(HttpStatusCode.BadRequest, tooMuch.Status);
        Assert.Equal(HttpStatusCode.BadRequest, withoutCustomer.Status);

        var redeemed = await pos.SellAsync(pos.Owner, [Line(product, 1)], [Card(20_000 - balance)],
            customerId: customerId, bonusRedeem: balance);
        Assert.Equal(20_000m - balance, redeemed.Dec("total"));
        Assert.Equal(balance, (int)redeemed.Dec("bonusRedeemed"));
        // Бүкіл баланс жұмсалды: қалғаны — тек осы чекке есептелген бонус.
        Assert.Equal((int)redeemed.Dec("bonusAccrued"), (int)redeemed.Dec("customer.bonusBalance"));
    }
}
