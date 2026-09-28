using System.Net;
using static SalemBonus.Api.Tests.Pos;

namespace SalemBonus.Api.Tests;

/// <summary>Қайтару мен қарыз (ТЗ «Касса» §11–13).</summary>
[Collection(ApiCollection.Name)]
public class ReturnsAndDebtsTests(ApiFixture api)
{
    /// <summary>Қарыз бөлігі: мерзімі міндетті (ТЗ §11.3).</summary>
    private static object Debt(decimal amount) =>
        new { method = "Debt", amount, dueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(7)) };

    private static object ReturnBody(Guid saleItemId, decimal quantity, Guid? requestId = null) => new
    {
        clientRequestId = requestId ?? Guid.NewGuid(),
        items = new[] { new { saleItemId, quantity } },
        refundMethod = "Cash",
        reason = "Тест",
    };

    [Fact]
    public async Task Partial_return_refunds_the_line_restores_stock_and_cannot_exceed_what_was_sold()
    {
        var pos = await api.NewDeviceAsync();
        var product = await pos.CreateProductAsync(price: 2500, stock: 10);
        var sale = (await pos.SellAsync(pos.Owner, [Line(product, 3)], [Cash(7500)])).Ok;
        var saleId = sale["id"]!.GetValue<Guid>();
        var itemId = sale["items"]![0]!["id"]!.GetValue<Guid>();
        var requestId = Guid.NewGuid();

        var first = await pos.Post(pos.Owner, $"/api/pos/v1/sales/{saleId}/returns", ReturnBody(itemId, 1, requestId));
        var repeated = await pos.Post(pos.Owner, $"/api/pos/v1/sales/{saleId}/returns", ReturnBody(itemId, 1, requestId));
        var tooMany = await pos.Post(pos.Owner, $"/api/pos/v1/sales/{saleId}/returns", ReturnBody(itemId, 3));

        first.EnsureOk();
        repeated.EnsureOk();
        Assert.Equal(HttpStatusCode.BadRequest, tooMany.Status);
        var receipt = (await pos.Get(pos.Owner, $"/api/pos/v1/sales/{saleId}")).Ok;
        Assert.Equal(2500m, receipt["returnedAmount"]!.GetValue<decimal>());
        Assert.Single(receipt["returns"]!.AsArray());
        Assert.Equal(1m, receipt["items"]![0]!["returnedQuantity"]!.GetValue<decimal>());
        Assert.Equal(8m, await pos.StockAsync(product)); // 10 − 3 + 1
    }

    [Fact]
    public async Task Cashier_without_the_return_permission_cannot_return()
    {
        var pos = await api.NewDeviceAsync();
        var product = await pos.CreateProductAsync(price: 1000, stock: 5);
        var sale = (await pos.SellAsync(pos.Cashier, [Line(product, 1)], [Card(1000)])).Ok;

        var res = await pos.Post(pos.Cashier, $"/api/pos/v1/sales/{sale["id"]}/returns",
            ReturnBody(sale["items"]![0]!["id"]!.GetValue<Guid>(), 1));

        Assert.Equal(HttpStatusCode.Forbidden, res.Status);
        Assert.Equal(4m, await pos.StockAsync(product));
    }

    [Fact]
    public async Task Debt_needs_a_customer_and_is_closed_only_by_exact_repayments()
    {
        var pos = await api.NewDeviceAsync();
        var product = await pos.CreateProductAsync(price: 6000, stock: 5);
        var customerId = (await pos.CreateCustomerAsync())["id"]!.GetValue<Guid>();

        var anonymous = await pos.SellAsync(pos.Owner, [Line(product, 1)], [Debt(6000)]);
        Assert.Equal(HttpStatusCode.BadRequest, anonymous.Status);

        var sale = await pos.SellAsync(pos.Owner, [Line(product, 1)], [Card(1000), Debt(5000)], customerId: customerId);
        var debtId = sale.Node("debt.id")!.GetValue<Guid>();
        Assert.Equal(5000m, sale.Dec("debt.remaining"));
        Assert.Equal(5000m, (await pos.Get(pos.Owner, $"/api/pos/v1/cashier/customers/{customerId}")).Dec("debtTotal"));

        var over = await pos.Post(pos.Owner, $"/api/pos/v1/debts/{debtId}/payments", new { amount = 5001m, method = "Cash" });
        var part = await pos.Post(pos.Owner, $"/api/pos/v1/debts/{debtId}/payments", new { amount = 2000m, method = "Cash" });
        var rest = await pos.Post(pos.Owner, $"/api/pos/v1/debts/{debtId}/payments", new { amount = 3000m, method = "Card" });
        var afterClosed = await pos.Post(pos.Owner, $"/api/pos/v1/debts/{debtId}/payments", new { amount = 1m, method = "Cash" });

        Assert.Equal(HttpStatusCode.BadRequest, over.Status);
        Assert.Equal(3000m, part.Dec("remaining"));
        Assert.Equal(0m, rest.Dec("remaining"));
        Assert.NotEqual(HttpStatusCode.OK, afterClosed.Status);
        Assert.Equal(0m, (await pos.Get(pos.Owner, $"/api/pos/v1/cashier/customers/{customerId}")).Dec("debtTotal"));
    }

    [Fact]
    public async Task Returning_a_debt_sale_reduces_the_debt_before_giving_money_back()
    {
        var pos = await api.NewDeviceAsync();
        var product = await pos.CreateProductAsync(price: 3000, stock: 5);
        var customerId = (await pos.CreateCustomerAsync())["id"]!.GetValue<Guid>();
        var sale = (await pos.SellAsync(pos.Owner, [Line(product, 2)], [Debt(6000)],
            customerId: customerId)).Ok;

        var ret = await pos.Post(pos.Owner, $"/api/pos/v1/sales/{sale["id"]}/returns",
            ReturnBody(sale["items"]![0]!["id"]!.GetValue<Guid>(), 1));

        ret.EnsureOk();
        var receipt = (await pos.Get(pos.Owner, $"/api/pos/v1/sales/{sale["id"]}")).Ok;
        var returned = receipt["returns"]![0]!;
        Assert.Equal(3000m, returned["debtReduced"]!.GetValue<decimal>());
        Assert.Equal(0m, returned["refunded"]!.GetValue<decimal>());
        Assert.Equal(3000m, receipt["debt"]!["remaining"]!.GetValue<decimal>());
    }
}
