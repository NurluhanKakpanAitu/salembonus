using System.Text.Json.Nodes;
using SalemBonus.Application.Common;

namespace SalemBonus.Api.Tests;

/// <summary>
/// Тауар іздеуі касса, «Товары» және «Склад» тізімінде бірдей: сөздер кез келген ретпен, үлкен-кіші әріпке қарамай,
/// әр сөз кез келген өрісте — атауы, артикулы, штрихкодтың кез келген бөлігі, бренді, санаты (жоғарғысы да), елі, өндірушісі.
/// </summary>
[Collection(ApiCollection.Name)]
public class ProductSearchTests(ApiFixture api)
{
    private static string Esc(string s) => Uri.EscapeDataString(s);

    private static async Task<(Pos Pos, Guid Id, string Tag, string Barcode)> ProductAsync(ApiFixture api)
    {
        var pos = await api.NewDeviceAsync();
        var tag = "Пиш" + Guid.NewGuid().ToString("N")[..5];
        var units = (await pos.Get(pos.Owner, "/api/pos/v1/catalog/units")).Ok.AsArray();
        var brand = (await pos.Post(pos.Owner, "/api/pos/v1/catalog/brands", new { name = "Mobil" + tag, status = "Active" })).Ok["id"]!.GetValue<Guid>();
        var parent = (await pos.Post(pos.Owner, "/api/pos/v1/catalog/nodes", new { parentId = (Guid?)null, name = "Жидкости" + tag, icon = "droplet", status = "Active" })).Ok["id"]!.GetValue<Guid>();
        var node = (await pos.Post(pos.Owner, "/api/pos/v1/catalog/nodes", new { parentId = parent, name = "Моторные масла", status = "Active" })).Ok["id"]!.GetValue<Guid>();
        var barcode = "46" + Random.Shared.NextInt64(10_000_000_000, 99_999_999_999);
        var id = (await pos.Post(pos.Owner, "/api/pos/v1/products", new
        {
            name = $"Масло моторное синтетика 5W-40 {tag}", article = "MB-540" + tag[^3..], brandId = brand, nodeId = node,
            country = "Германия", manufacturer = "ExxonMobil",
            barcodes = new[] { new { barcode, isPrimary = true } },
            unitId = units[0]!["id"]!.GetValue<Guid>(),
            opening = new { quantity = 3, purchasePrice = 9000, salePrice = 12400 },
        })).Ok["id"]!.GetValue<Guid>();
        return (pos, id, tag, barcode);
    }

    private static async Task<bool> FoundAsync(Pos pos, Guid id, string q)
    {
        var lists = new[]
        {
            (await pos.Get(pos.Owner, $"/api/pos/v1/cashier/catalog?search={Esc(q)}&pageSize=100")).Ok["items"]!.AsArray(),
            (await pos.Get(pos.Owner, $"/api/pos/v1/products?search={Esc(q)}&pageSize=100")).Ok["items"]!.AsArray(),
            (await pos.Get(pos.Owner, $"/api/pos/v1/inventory/stock?search={Esc(q)}&pageSize=100")).Ok["items"]!.AsArray(),
        };
        var found = lists.Select(l => l.Any(x => (x!["id"] ?? x["productId"])!.GetValue<Guid>() == id)).ToList();
        Assert.True(found.Distinct().Count() == 1, $"«{q}»: касса/товары/склад әртүрлі нәтиже береді ({string.Join(", ", found)})");
        return found[0];
    }

    [Fact]
    public async Task Words_match_any_field_in_any_order_everywhere()
    {
        var (pos, id, tag, barcode) = await ProductAsync(api);
        string[] hits =
        [
            $"5W-40 масло {tag}",                 // сөздердің реті басқа
            $"{tag.ToLowerInvariant()} СИНТЕТИКА масло", // үлкен-кіші әріп
            barcode[3..10],                         // штрихкодтың ортасы
            $"mobil{tag.ToLowerInvariant()} MB-540",   // бренд + артикул
            $"жидкости{tag} 5w-40",                 // жоғарғы санат + атауы
            $"моторные масла {tag}",                // санат атауы
            $"Германия {tag}",                      // ел
            $"exxonmobil {tag}",                    // өндіруші
        ];
        foreach (var q in hits) Assert.True(await FoundAsync(pos, id, q), $"«{q}» бойынша табылмады");
        Assert.False(await FoundAsync(pos, id, $"масло {tag} дизельное"));   // бір сөз ешбір өрісте жоқ
    }

    [Theory]
    [InlineData("масло  5w-40", new[] { "масло", "5w-40" })]
    [InlineData(" A, b ;a ", new[] { "A", "b" })]
    [InlineData("   ", new string[0])]
    public void Query_splits_into_distinct_words(string text, string[] expected) =>
        Assert.Equal(expected, ProductSearch.Tokens(text));

    [Fact]
    public void Every_word_must_hit_some_field() =>
        Assert.True(ProductSearch.Matches(ProductSearch.Tokens("40 масло"), "Масло 5W-40", null)
                    && !ProductSearch.Matches(ProductSearch.Tokens("масло фильтр"), "Масло 5W-40", "MB-540"));
}
