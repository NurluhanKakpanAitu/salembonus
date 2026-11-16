using System.Net;
using System.Text.Json.Nodes;
using static SalemBonus.Api.Tests.Pos;

namespace SalemBonus.Api.Tests;

/// <summary>
/// Кассаның жергілікті каталогы (/cashier/catalog/sync): толық жүктеу, минуттық өзгерістер (сатылым → қалдық, сатудан алу),
/// бренд атауы өзгерсе — қайта толық. Өзгеріс терезесін тексеру үшін тауардың уақыт белгілері SQL-мен бір сағатқа кейін жылжытылады.
/// </summary>
[Collection(ApiCollection.Name)]
public class CatalogSyncTests(ApiFixture api)
{
    private const string Sync = "/api/pos/v1/cashier/catalog/sync";

    private static string Q(JsonNode prev) =>
        $"?since={Uri.EscapeDataString(prev["serverTime"]!.GetValue<string>())}&version={Uri.EscapeDataString(prev["version"]!.GetValue<string>())}";

    private static JsonNode? Item(JsonNode sync, Guid id) => sync["items"]!.AsArray().FirstOrDefault(i => i!["id"]!.GetValue<Guid>() == id);

    /// <summary>Тауар «бұрыннан бар» болсын: барлық белгілері бір сағат бұрын.</summary>
    private Task AgeAsync(Guid id) => api.SqlAsync($"""
        UPDATE pos.products SET "UpdatedAt" = now() - interval '1 hour' WHERE "Id" = '{id}';
        UPDATE pos.product_prices SET "UpdatedAt" = now() - interval '1 hour' WHERE "ProductId" = '{id}';
        UPDATE pos.stock_balances SET "UpdatedAt" = now() - interval '1 hour' WHERE "ProductId" = '{id}';
        UPDATE pos.stock_movements SET "CreatedAt" = now() - interval '1 hour' WHERE "ProductId" = '{id}';
        """);

    [Fact]
    public async Task Full_then_only_changes_sales_update_stock_and_removed_products_leave()
    {
        var pos = await api.NewDeviceAsync();
        var barcode = "47" + Random.Shared.NextInt64(10_000_000_000, 99_999_999_999);
        var product = await pos.CreateProductAsync(price: 1500, stock: 10, article: "SYN-" + barcode[^4..], barcode: barcode);

        var full = (await pos.Get(pos.Cashier, Sync)).Ok;
        Assert.True(full["full"]!.GetValue<bool>());
        Assert.Equal(full["total"]!.GetValue<int>(), full["items"]!.AsArray().Count);
        var item = Item(full, product)!;
        Assert.Equal(pos.NameOf(product), item["name"]!.GetValue<string>());
        Assert.Equal(1500m, item["price"]!.GetValue<decimal>());
        Assert.Equal(10m, item["stock"]!.GetValue<decimal>());
        Assert.Contains(barcode, item["barcodes"]!.AsArray().Select(b => b!.GetValue<string>()));

        // Өзгеріс жоқ — тауар қайта келмейді.
        await AgeAsync(product);
        var quiet = (await pos.Get(pos.Cashier, Sync + Q(full))).Ok;
        Assert.False(quiet["full"]!.GetValue<bool>());
        Assert.Null(Item(quiet, product));

        // Сатылым: қалдық пен «популярлық» өзгереді.
        (await pos.SellAsync(pos.Cashier, [Line(product, 3)], [Cash(4500)])).EnsureOk();
        var afterSale = (await pos.Get(pos.Cashier, Sync + Q(quiet))).Ok;
        var changed = Item(afterSale, product)!;
        Assert.Equal(7m, changed["stock"]!.GetValue<decimal>());
        Assert.Equal(3m, changed["sold"]!.GetValue<decimal>());

        // Сатудан алынған тауар — Removed-та.
        await AgeAsync(product);
        Assert.Equal(HttpStatusCode.NoContent, (await pos.Post(pos.Owner, $"/api/pos/v1/products/{product}/archive", new { })).Status);
        var archived = (await pos.Get(pos.Cashier, Sync + Q(afterSale))).Ok;
        Assert.Contains(product, archived["removed"]!.AsArray().Select(r => r!.GetValue<Guid>()));
        Assert.Null(Item(archived, product));
    }

    [Fact]
    public async Task Brand_change_makes_the_next_sync_full()
    {
        var pos = await api.NewDeviceAsync();
        var first = (await pos.Get(pos.Cashier, Sync)).Ok;
        (await pos.Post(pos.Owner, "/api/pos/v1/catalog/brands", new { name = "Бренд " + Guid.NewGuid().ToString("N")[..6], status = "Active" })).EnsureOk();
        var next = (await pos.Get(pos.Cashier, Sync + Q(first))).Ok;
        Assert.True(next["full"]!.GetValue<bool>());
        Assert.NotEqual(first["version"]!.GetValue<string>(), next["version"]!.GetValue<string>());
    }
}
