using System.Net;
using System.Text.Json.Nodes;
using static SalemBonus.Api.Tests.Pos;

namespace SalemBonus.Api.Tests;

/// <summary>
/// Кассада жол бойынша баға мен жеңілдік: бағаны тек көтеруге болады, әр позицияға өз жеңілдігі (пайыз не сома), чек жеңілдігі
/// жол жеңілдіктерінен кейінгі сомадан. Жеңілдіктен кейін тауар өзіндік құнынан арзан сатылмайды (бонус — төлем, кірмейді).
/// Проведённая приёмка түзетіледі: қалдыққа тек айырма, сатылып кеткенді азайтуға, төленгеннен аз сомаға болмайды.
/// </summary>
[Collection(ApiCollection.Name)]
public class LinePricingTests(ApiFixture api)
{
    private const string Inv = "/api/pos/v1/inventory";

    private static object L(Guid productId, decimal quantity, decimal? price = null, string? kind = null, decimal value = 0) =>
        new { productId, quantity, price, discount = kind is null ? null : new { kind, value } };

    [Fact]
    public async Task Cashier_can_raise_a_price_but_not_lower_it()
    {
        var pos = await api.NewDeviceAsync();
        var product = await pos.CreateProductAsync(price: 1000, stock: 5, purchase: 600);

        var raised = (await pos.SellAsync(pos.Owner, [L(product, 2, price: 1200)], [Cash(2400)])).Ok;
        Assert.Equal(2400m, raised["total"]!.GetValue<decimal>());
        Assert.Equal(1200m, raised["items"]![0]!["price"]!.GetValue<decimal>());

        var lowered = await pos.SellAsync(pos.Owner, [L(product, 1, price: 900)], [Cash(900)]);
        Assert.Equal(HttpStatusCode.BadRequest, lowered.Status);
        Assert.Contains("1 000", lowered.Body!["detail"]!.GetValue<string>().Replace(' ', ' '));
        Assert.Equal(3m, await pos.StockAsync(product));
    }

    [Fact]
    public async Task Each_line_gets_its_own_discount_and_the_check_discount_applies_after_it()
    {
        var pos = await api.NewDeviceAsync();
        var a = await pos.CreateProductAsync(price: 1000, stock: 5, purchase: 500);
        var b = await pos.CreateProductAsync(price: 3000, stock: 5, purchase: 1000);

        // A: 2 × 1000 − 10% = 1800; B: 3000 − 300 ₸ = 2700; чекке 5% (4500-ден) = 225 → 4275.
        var sale = (await pos.SellAsync(pos.Owner, [L(a, 2, kind: "Percent", value: 10), L(b, 1, kind: "Amount", value: 300)], [Cash(4275)],
            discount: new { kind = "Percent", value = 5 })).Ok;
        Assert.Equal(5000m, sale["subtotal"]!.GetValue<decimal>());
        Assert.Equal(725m, sale["discountAmount"]!.GetValue<decimal>());
        Assert.Equal(4275m, sale["total"]!.GetValue<decimal>());
        var items = sale["items"]!.AsArray();
        Assert.Equal(200m, items.Single(i => i!["productId"]!.GetValue<Guid>() == a)!["lineDiscount"]!.GetValue<decimal>());
        Assert.Equal(300m, items.Single(i => i!["productId"]!.GetValue<Guid>() == b)!["lineDiscount"]!.GetValue<decimal>());
        // Қайтаруға: әр жолдың «төленбеген» бөлігі = өз жеңілдігі + чек жеңілдігінің үлесі.
        Assert.Equal(725m, items.Sum(i => i!["discount"]!.GetValue<decimal>()));

        var tooMuch = await pos.SellAsync(pos.Owner, [L(a, 1, kind: "Amount", value: 1500)], [Cash(0)]);
        Assert.Equal(HttpStatusCode.BadRequest, tooMuch.Status);
    }

    [Fact]
    public async Task A_discount_cannot_take_the_price_below_cost()
    {
        var pos = await api.NewDeviceAsync();
        var product = await pos.CreateProductAsync(price: 1000, stock: 10, purchase: 800);

        var below = await pos.SellAsync(pos.Owner, [L(product, 1, kind: "Percent", value: 25)], [Cash(750)]);
        Assert.Equal(HttpStatusCode.BadRequest, below.Status);
        Assert.Equal("items", below.Body!["field"]!.GetValue<string>());
        Assert.Contains("800", below.Body!["detail"]!.GetValue<string>());

        // Чек жеңілдігі де тексеріледі; өзіндік құнға дейін — болады.
        var check = await pos.SellAsync(pos.Owner, [L(product, 1)], [Cash(700)], discount: new { kind = "Amount", value = 300 });
        Assert.Equal(HttpStatusCode.BadRequest, check.Status);
        (await pos.SellAsync(pos.Owner, [L(product, 1, kind: "Percent", value: 20)], [Cash(800)])).EnsureOk();
        Assert.Equal(9m, await pos.StockAsync(product));
    }

    private static async Task<JsonNode> PostedReceiptAsync(Pos pos, Guid product, decimal qty, decimal price)
    {
        var doc = (await pos.Post(pos.Owner, $"{Inv}/receipts", new { supplierInvoiceNumber = "Н-9", lines = new[] { new { productId = product, quantity = qty, purchasePrice = price } } })).Ok;
        return (await pos.Post(pos.Owner, $"{Inv}/documents/{doc["id"]}/post", new { })).Ok;
    }

    [Fact]
    public async Task A_posted_receipt_can_be_edited_and_stock_moves_only_by_the_difference()
    {
        var pos = await api.NewDeviceAsync();
        var product = await pos.CreateProductAsync(price: 1500, stock: 0);
        var other = await pos.CreateProductAsync(price: 900, stock: 0);
        var doc = await PostedReceiptAsync(pos, product, 10, 1000);
        var id = doc["id"]!.GetValue<Guid>();
        Assert.Equal(10m, await pos.StockAsync(product));

        // 10 → 12 дана, баға 1000 → 1100, жаңа тауар қосылды.
        var edited = (await pos.Put(pos.Owner, $"{Inv}/receipts/{id}", new
        {
            supplierInvoiceNumber = "Н-9/1",
            lines = new object[] { new { productId = product, quantity = 12m, purchasePrice = 1100m }, new { productId = other, quantity = 3m, purchasePrice = 500m } },
        })).Ok;
        Assert.Equal("Posted", edited["status"]!.GetValue<string>());
        Assert.Equal(14700m, edited["total"]!.GetValue<decimal>());
        Assert.Equal(12m, await pos.StockAsync(product));
        Assert.Equal(3m, await pos.StockAsync(other));

        // Сатылғаннан кейін: қоймада 7 қалды — 12-ні 4-ке түсіру (−8) болмайды, 6-ға (−6) болады.
        (await pos.SellAsync(pos.Owner, [Line(product, 5)], [Cash(7500)])).EnsureOk();
        var tooFew = await pos.Put(pos.Owner, $"{Inv}/receipts/{id}", new { lines = new object[] { new { productId = product, quantity = 4m, purchasePrice = 1100m } } });
        Assert.Equal(HttpStatusCode.BadRequest, tooFew.Status);
        Assert.Equal(7m, await pos.StockAsync(product));
        (await pos.Put(pos.Owner, $"{Inv}/receipts/{id}", new { lines = new object[] { new { productId = product, quantity = 6m, purchasePrice = 1100m } } })).EnsureOk();
        Assert.Equal(1m, await pos.StockAsync(product));
        Assert.Equal(0m, await pos.StockAsync(other)); // жолдан алынған тауар қоймадан да алынды

        // Кассир (қойма құқығы жоқ) түзете алмайды.
        Assert.Equal(HttpStatusCode.Forbidden, (await pos.Put(pos.Cashier, $"{Inv}/receipts/{id}", new { lines = new object[] { new { productId = product, quantity = 7m, purchasePrice = 1100m } } })).Status);
    }
}
