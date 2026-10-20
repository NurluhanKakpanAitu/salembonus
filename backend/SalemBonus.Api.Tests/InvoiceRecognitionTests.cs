using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using SalemBonus.Application.Common.Interfaces;

namespace SalemBonus.Api.Tests;

/// <summary>
/// Накладнаяны тану (ТЗ «Склад» §8): файл → жалған Claude → каталогпен сәйкестендіру → черновик ұсынысы.
/// Желіге шықпайды: танушы жалған, тест не қойса соны қайтарады; сервердің тексерулері шын.
/// </summary>
[Collection(ApiCollection.Name)]
public class InvoiceRecognitionTests(ApiFixture api)
{
    private const string Url = "/api/pos/v1/inventory/receipts/recognize";
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01];
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.4\n%fake\n");

    private static RecognizedLine Line(string? name, string? article = null, string? barcode = null, decimal? qty = 1, decimal? price = 100) =>
        new(name, article, barcode, qty, price, qty * price);

    private static async Task<Guid> CreateProductAsync(Pos pos, string name, string? article, string? barcode, decimal salePrice, decimal stock)
    {
        var units = (await pos.Get(pos.Owner, "/api/pos/v1/catalog/units")).Ok.AsArray();
        var res = await pos.Post(pos.Owner, "/api/pos/v1/products", new
        {
            name, article, unitId = units[0]!["id"]!.GetValue<Guid>(),
            barcodes = barcode is null ? [] : new[] { new { barcode, isPrimary = true } },
            opening = new { quantity = stock, purchasePrice = 70m, salePrice },
        });
        return res.Ok["id"]!.GetValue<Guid>();
    }

    private static JsonNode LineAt(JsonNode result, int index) => result["lines"]!.AsArray()[index]!;

    [Fact]
    public async Task Recognized_lines_are_matched_by_barcode_then_article_then_name_and_unknown_lines_stay_unmatched()
    {
        var pos = await api.NewDeviceAsync();
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var barcode = "48" + Random.Shared.NextInt64(10_000_000_000, 99_999_999_999);
        var byBarcode = await CreateProductAsync(pos, $"Сок яблочный {suffix}", $"ART-{suffix}-A", barcode, salePrice: 900, stock: 4);
        var byArticle = await CreateProductAsync(pos, $"Печенье овсяное {suffix}", $"ART-{suffix}-B", null, salePrice: 500, stock: 0);
        var byName = await CreateProductAsync(pos, $"Масло сливочное Крестьянское {suffix} 180г", null, null, salePrice: 1200, stock: 2);
        var supplierName = "ТОО Поставщик " + suffix;
        var supplierId = (await pos.Post(pos.Owner, "/api/pos/v1/inventory/suppliers", new { name = supplierName, type = "Company" })).Ok["id"]!.GetValue<Guid>();
        var warehouse = (await pos.Get(pos.Owner, "/api/pos/v1/inventory/warehouses")).Ok.AsArray().First(w => w!["isDefault"]!.GetValue<bool>())!["id"]!.GetValue<Guid>();

        api.Recognizer.Throw = null;
        api.Recognizer.IsConfigured = true;
        api.Recognizer.Next = new RecognizedInvoice(supplierName.ToUpperInvariant(), "Н-77", "2026-10-15",
        [
            Line("Сок (не совпадает по имени)", "WRONG", barcode, qty: 12, price: 450.5m),
            Line("Печенье", $"art {suffix} b", null, qty: 3, price: 300),
            Line($"МАСЛО СЛИВОЧНОЕ «КРЕСТЬЯНСКОЕ» {suffix}, 180 г", null, null, qty: 5, price: 800),
            Line("Неизвестный товар 4x40", null, "0000000000000", qty: -2, price: 10),
            Line(null, null, null),
        ]);

        var res = await pos.PostFile(pos.Owner, Url, Jpeg, "image/jpeg", "invoice.jpg", ("warehouseId", warehouse.ToString()));
        var r = res.Ok;

        Assert.Equal("image/jpeg", api.Recognizer.LastContentType);
        Assert.Equal(Jpeg.Length, api.Recognizer.LastLength);
        Assert.Equal(supplierId, r["supplierId"]!.GetValue<Guid>());
        Assert.Equal("Н-77", r["invoiceNumber"]!.GetValue<string>());
        Assert.Equal("2026-10-15", r["date"]!.GetValue<string>());
        Assert.Equal(4, r["lines"]!.AsArray().Count); // бос жол алынып тасталды
        Assert.Equal(3, r["matched"]!.GetValue<int>());
        Assert.Equal(0, r["review"]!.GetValue<int>());
        Assert.Equal(1, r["notFound"]!.GetValue<int>());

        var l1 = LineAt(r, 0);
        Assert.Equal("Matched", l1["status"]!.GetValue<string>());
        Assert.Equal("Barcode", l1["matchedBy"]!.GetValue<string>());
        Assert.Equal(byBarcode, l1["product"]!["id"]!.GetValue<Guid>());
        Assert.Equal(12m, l1["quantity"]!.GetValue<decimal>());
        Assert.Equal(450.5m, l1["price"]!.GetValue<decimal>());
        Assert.Equal(4m, l1["product"]!["stockNow"]!.GetValue<decimal>());
        Assert.Equal(900m, l1["product"]!["catalogRetailPrice"]!.GetValue<decimal>());
        Assert.Equal(70m, l1["product"]!["purchasePrice"]!.GetValue<decimal>());

        var l2 = LineAt(r, 1);
        Assert.Equal("Article", l2["matchedBy"]!.GetValue<string>());
        Assert.Equal(byArticle, l2["product"]!["id"]!.GetValue<Guid>());

        var l3 = LineAt(r, 2);
        Assert.Equal("Name", l3["matchedBy"]!.GetValue<string>());
        Assert.Equal("Matched", l3["status"]!.GetValue<string>());
        Assert.Equal(byName, l3["product"]!["id"]!.GetValue<Guid>());
        Assert.True(l3["confidence"]!.GetValue<decimal>() >= 0.85m);

        var l4 = LineAt(r, 3);
        Assert.Equal("NotFound", l4["status"]!.GetValue<string>());
        Assert.Null(l4["product"]);
        Assert.Null(l4["quantity"]); // теріс сан — ИИ қатесі, алынып тасталды
        Assert.Equal("Неизвестный товар 4x40", l4["name"]!.GetValue<string>());
    }

    [Fact]
    public async Task Recognition_validates_file_permission_configuration_and_the_recognizer_result()
    {
        var pos = await api.NewDeviceAsync();
        api.Recognizer.Throw = null;
        api.Recognizer.IsConfigured = true;
        api.Recognizer.Next = new RecognizedInvoice("X", null, null, [Line("Что-то")]);

        Assert.Equal(HttpStatusCode.Forbidden, (await pos.PostFile(pos.Cashier, Url, Jpeg, "image/jpeg", "a.jpg")).Status);

        var wrongType = await pos.PostFile(pos.Owner, Url, Encoding.UTF8.GetBytes("hello"), "text/plain", "a.txt");
        Assert.Equal(HttpStatusCode.BadRequest, wrongType.Status);
        Assert.Equal("file", wrongType.Body!["field"]!.GetValue<string>());

        // Кеңейтімі өзгертілген файл: түрі image/png делінген, іші PNG емес.
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.PostFile(pos.Owner, Url, Encoding.UTF8.GetBytes("not a png"), "image/png", "a.png")).Status);

        api.Recognizer.Next = new RecognizedInvoice(null, null, null, []);
        var empty = await pos.PostFile(pos.Owner, Url, Pdf, "application/pdf", "a.pdf");
        Assert.Equal(HttpStatusCode.BadRequest, empty.Status);
        Assert.Equal("application/pdf", api.Recognizer.LastContentType);

        api.Recognizer.Throw = new InvoiceRecognitionException("boom");
        var failed = await pos.PostFile(pos.Owner, Url, Jpeg, "image/jpeg", "a.jpg");
        Assert.Equal(HttpStatusCode.BadRequest, failed.Status);
        Assert.DoesNotContain("boom", failed.Body!.ToJsonString()); // ішкі себеп клиентке шықпайды
        api.Recognizer.Throw = null;

        api.Recognizer.IsConfigured = false;
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.PostFile(pos.Owner, Url, Jpeg, "image/jpeg", "a.jpg")).Status);
        api.Recognizer.IsConfigured = true;

        // Табылмаған тауар — ұсыныста «NotFound», ештеңе жасалмайды (§8: жаңа тауар расталмай жасалмайды).
        api.Recognizer.Next = new RecognizedInvoice(null, null, "вчера", [Line("Совершенно новый товар " + Guid.NewGuid().ToString("N"))]);
        var r = (await pos.PostFile(pos.Owner, Url, Jpeg, "image/jpeg", "a.jpg")).Ok;
        Assert.Null(r["date"]);
        Assert.Null(r["supplierId"]);
        Assert.Equal("NotFound", LineAt(r, 0)["status"]!.GetValue<string>());
        Assert.Equal(1, r["notFound"]!.GetValue<int>());
    }
}
