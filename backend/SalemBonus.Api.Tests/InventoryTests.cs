using System.Net;
using System.Text.Json.Nodes;

namespace SalemBonus.Api.Tests;

/// <summary>
/// Склад (ТЗ «Склад» v1.0): приёмка, төлем, сторно, списание, перемещение, қоймалар, кезең сүзгісі, құқықтар.
/// Әр тест өз тауарын жасайды — басқа тесттің қалдығына тәуелді емес.
/// </summary>
[Collection(ApiCollection.Name)]
public class InventoryTests(ApiFixture api)
{
    private const string Inv = "/api/pos/v1/inventory";

    private static object ReceiptLine(Guid productId, decimal quantity, decimal purchasePrice, decimal? retailPrice = null) =>
        new { productId, quantity, purchasePrice, retailPrice };

    private static async Task<Guid> CreateSupplierAsync(Pos pos)
    {
        var res = await pos.Post(pos.Owner, $"{Inv}/suppliers", new
        {
            name = "Поставщик " + Guid.NewGuid().ToString("N")[..8], type = "Company", taxId = "123456789012", phone = "+77010000000",
        });
        return res.Ok["id"]!.GetValue<Guid>();
    }

    private static Task<ApiResult> CreateReceiptAsync(Pos pos, Guid? supplierId, params object[] lines) =>
        pos.Post(pos.Owner, $"{Inv}/receipts", new { supplierId, supplierInvoiceNumber = "Н-1", lines });

    private static async Task<JsonNode> StockRowAsync(Pos pos, Guid productId)
    {
        var res = await pos.Get(pos.Owner, $"{Inv}/stock?search=" + Uri.EscapeDataString(pos.NameOf(productId)));
        return res.Ok["items"]!.AsArray().First(i => i!["productId"]!.GetValue<Guid>() == productId)!;
    }

    private static decimal StockIn(JsonNode row, Guid warehouseId) =>
        row["byWarehouse"]!.AsArray().FirstOrDefault(w => w!["warehouseId"]!.GetValue<Guid>() == warehouseId)?["quantity"]?.GetValue<decimal>() ?? 0;

    private static async Task<JsonArray> MovementsAsync(Pos pos, Guid productId)
    {
        var res = await pos.Get(pos.Owner, $"{Inv}/movements?productId={productId}&pageSize=50");
        return res.Ok["items"]!.AsArray();
    }

    private static async Task<(Guid Id, Guid StoreId)> DefaultWarehouseAsync(Pos pos)
    {
        var w = (await pos.Get(pos.Owner, $"{Inv}/warehouses")).Ok.AsArray().First(x => x!["isDefault"]!.GetValue<bool>())!;
        return (w["id"]!.GetValue<Guid>(), w["storeId"]!.GetValue<Guid>());
    }

    private static async Task<Guid> CreateWarehouseAsync(Pos pos)
    {
        var res = await pos.Post(pos.Owner, $"{Inv}/warehouses", new { name = "Склад " + Guid.NewGuid().ToString("N")[..8], type = "Storage" });
        return res.Ok["id"]!.GetValue<Guid>();
    }

    private static int Seq(JsonNode doc) => int.Parse(doc["number"]!.GetValue<string>().Split('-')[1]);

    private static async Task<bool> IsActiveAsync(Pos pos, Guid warehouseId) =>
        (await pos.Get(pos.Owner, $"{Inv}/warehouses")).Ok.AsArray().First(w => w!["id"]!.GetValue<Guid>() == warehouseId)!["isActive"]!.GetValue<bool>();

    // ---------- Приёмка (§7, §9) ----------

    [Fact]
    public async Task Receipt_draft_keeps_stock_posting_is_idempotent_updates_prices_and_numbers_documents_in_order()
    {
        var pos = await api.NewDeviceAsync();
        var product = await pos.CreateProductAsync(price: 1000, stock: 0, purchase: 500);
        var supplier = await CreateSupplierAsync(pos);

        var draft = (await CreateReceiptAsync(pos, supplier, ReceiptLine(product, 10, purchasePrice: 700, retailPrice: 1200))).Ok;
        var id = draft["id"]!.GetValue<Guid>();
        Assert.Equal("Draft", draft["status"]!.GetValue<string>());
        Assert.StartsWith("ПР-", draft["number"]!.GetValue<string>());
        Assert.Equal(7000m, draft["total"]!.GetValue<decimal>());
        Assert.Equal(0m, await pos.StockAsync(product)); // черновик қалдыққа тимейді

        var posted = (await pos.Post(pos.Owner, $"{Inv}/documents/{id}/post", new { })).Ok;
        var again = (await pos.Post(pos.Owner, $"{Inv}/documents/{id}/post", new { })).Ok;

        Assert.Equal("Posted", posted["status"]!.GetValue<string>());
        Assert.Equal(posted["postedAt"]!.GetValue<DateTime>(), again["postedAt"]!.GetValue<DateTime>());
        Assert.Equal(10m, await pos.StockAsync(product));
        var card = (await pos.Get(pos.Owner, $"/api/pos/v1/products/{product}")).Ok;
        Assert.Equal(700m, card["purchasePrice"]!.GetValue<decimal>());
        Assert.Equal(1200m, card["salePrice"]!.GetValue<decimal>());
        var movement = Assert.Single(await MovementsAsync(pos, product), m => m!["type"]!.GetValue<string>() == "Receipt")!;
        Assert.Equal(10m, movement["quantity"]!.GetValue<decimal>());
        Assert.Equal(10m, movement["balanceAfter"]!.GetValue<decimal>());
        Assert.Equal(draft["number"]!.GetValue<string>(), movement["documentNumber"]!.GetValue<string>());

        var next = (await CreateReceiptAsync(pos, supplier, ReceiptLine(product, 1, 700))).Ok;
        Assert.Equal(Seq(draft) + 1, Seq(next));
    }

    [Fact]
    public async Task Retail_price_changes_only_when_the_line_says_so_and_posted_receipt_is_frozen()
    {
        var pos = await api.NewDeviceAsync();
        var product = await pos.CreateProductAsync(price: 1500, stock: 2, purchase: 900);

        var doc = (await CreateReceiptAsync(pos, null, ReceiptLine(product, 3, purchasePrice: 1000))).Ok;
        var id = doc["id"]!.GetValue<Guid>();
        (await pos.Post(pos.Owner, $"{Inv}/documents/{id}/post", new { })).EnsureOk();

        var card = (await pos.Get(pos.Owner, $"/api/pos/v1/products/{product}")).Ok;
        Assert.Equal(1000m, card["purchasePrice"]!.GetValue<decimal>());
        Assert.Equal(1500m, card["salePrice"]!.GetValue<decimal>()); // розница көрсетілмеді — өзгермейді
        Assert.Equal(5m, await pos.StockAsync(product));

        var edit = await pos.Put(pos.Owner, $"{Inv}/receipts/{id}", new { lines = new[] { ReceiptLine(product, 30, 1000) } });
        var cancel = await pos.Post(pos.Owner, $"{Inv}/documents/{id}/cancel", new { });
        Assert.Equal(HttpStatusCode.BadRequest, edit.Status);
        Assert.Equal(HttpStatusCode.BadRequest, cancel.Status);
        Assert.Equal(5m, await pos.StockAsync(product));
    }

    [Fact]
    public async Task Empty_draft_cannot_be_posted_but_can_be_cancelled()
    {
        var pos = await api.NewDeviceAsync();
        var doc = (await pos.Post(pos.Owner, $"{Inv}/receipts", new { isQuick = true })).Ok;
        var id = doc["id"]!.GetValue<Guid>();

        var post = await pos.Post(pos.Owner, $"{Inv}/documents/{id}/post", new { });
        var cancel = await pos.Post(pos.Owner, $"{Inv}/documents/{id}/cancel", new { });

        Assert.Equal(HttpStatusCode.BadRequest, post.Status);
        Assert.Equal(HttpStatusCode.NoContent, cancel.Status);
        Assert.Equal("Cancelled", (await pos.Get(pos.Owner, $"{Inv}/documents/{id}")).Ok["status"]!.GetValue<string>());
    }

    // ---------- Жеткізушіге төлем (§12–13) ----------

    [Fact]
    public async Task Supplier_payment_is_limited_by_debt_idempotent_and_reversible()
    {
        var pos = await api.NewDeviceAsync();
        var product = await pos.CreateProductAsync(price: 2000, stock: 0);
        var supplier = await CreateSupplierAsync(pos);
        var doc = (await CreateReceiptAsync(pos, supplier, ReceiptLine(product, 10, 1000))).Ok;
        var id = doc["id"]!.GetValue<Guid>();

        var beforePosting = await pos.Post(pos.Owner, $"{Inv}/receipts/{id}/payments", new { amount = 1000m, method = "Cash" });
        Assert.Equal(HttpStatusCode.BadRequest, beforePosting.Status); // тек проведённыйға
        (await pos.Post(pos.Owner, $"{Inv}/documents/{id}/post", new { })).EnsureOk();
        // Финанс (ТЗ «Финансы» §17): жеткізушіге төлем шоттағы ақшадан — алдымен картамен сатылым, банк шотына 4000 түседі.
        (await pos.SellAsync(pos.Owner, [Pos.Line(product, 2)], [Pos.Card(4000)])).EnsureOk();

        var requestId = Guid.NewGuid();
        var first = (await pos.Post(pos.Owner, $"{Inv}/receipts/{id}/payments", new { clientRequestId = requestId, amount = 3000m, method = "Transfer", reference = "№1" })).Ok;
        var repeated = (await pos.Post(pos.Owner, $"{Inv}/receipts/{id}/payments", new { clientRequestId = requestId, amount = 3000m, method = "Transfer", reference = "№1" })).Ok;
        var tooMuch = await pos.Post(pos.Owner, $"{Inv}/receipts/{id}/payments", new { amount = 7001m, method = "Cash" });
        var zero = await pos.Post(pos.Owner, $"{Inv}/receipts/{id}/payments", new { amount = 0m, method = "Cash" });

        Assert.Equal(3000m, first["paid"]!.GetValue<decimal>());
        Assert.Equal(3000m, repeated["paid"]!.GetValue<decimal>());
        Assert.Equal(7000m, repeated["remaining"]!.GetValue<decimal>());
        Assert.Single(repeated["payments"]!.AsArray());
        Assert.Equal(HttpStatusCode.BadRequest, tooMuch.Status);
        Assert.Equal(HttpStatusCode.BadRequest, zero.Status);

        var supplierCard = (await pos.Get(pos.Owner, $"{Inv}/suppliers/{supplier}")).Ok;
        Assert.Equal(7000m, supplierCard["supplier"]!["debt"]!.GetValue<decimal>());

        // Төлемі бар приёмка сторноланбайды; төлем сторносына себеп керек.
        var reverseDoc = await pos.Post(pos.Owner, $"{Inv}/documents/{id}/reverse", new { reason = "Ошибка" });
        Assert.Equal(HttpStatusCode.BadRequest, reverseDoc.Status);
        var paymentId = first["payments"]![0]!["id"]!.GetValue<Guid>();
        var noReason = await pos.Post(pos.Owner, $"{Inv}/payments/{paymentId}/reverse", new { });
        var reversed = await pos.Post(pos.Owner, $"{Inv}/payments/{paymentId}/reverse", new { reason = "Дубль" });
        Assert.Equal(HttpStatusCode.BadRequest, noReason.Status);
        Assert.Equal(HttpStatusCode.NoContent, reversed.Status);
        var after = (await pos.Get(pos.Owner, $"{Inv}/documents/{id}")).Ok;
        Assert.Equal(0m, after["paid"]!.GetValue<decimal>());
        Assert.Equal("Reversed", after["payments"]![0]!["status"]!.GetValue<string>()); // жол өшірілмейді
    }

    // ---------- Сторно (§1, §20) ----------

    [Fact]
    public async Task Reversal_needs_a_reason_returns_stock_and_keeps_both_documents_in_history()
    {
        var pos = await api.NewDeviceAsync();
        var product = await pos.CreateProductAsync(price: 1000, stock: 4);
        var doc = (await CreateReceiptAsync(pos, null, ReceiptLine(product, 6, 500))).Ok;
        var id = doc["id"]!.GetValue<Guid>();
        (await pos.Post(pos.Owner, $"{Inv}/documents/{id}/post", new { })).EnsureOk();
        Assert.Equal(10m, await pos.StockAsync(product));

        var noReason = await pos.Post(pos.Owner, $"{Inv}/documents/{id}/reverse", new { });
        var reversed = await pos.Post(pos.Owner, $"{Inv}/documents/{id}/reverse", new { reason = "Ошиблись в количестве" });
        var twice = await pos.Post(pos.Owner, $"{Inv}/documents/{id}/reverse", new { reason = "Ещё раз" }); // идемпотентті: екінші рет қалдық өзгермейді

        Assert.Equal(HttpStatusCode.BadRequest, noReason.Status);
        Assert.Equal("Reversed", reversed.Ok["status"]!.GetValue<string>());
        Assert.Equal("Reversed", twice.Ok["status"]!.GetValue<string>());
        Assert.Equal(4m, await pos.StockAsync(product));
        var movements = await MovementsAsync(pos, product);
        var reversal = Assert.Single(movements, m => m!["type"]!.GetValue<string>() == "Reversal")!;
        Assert.Equal(-6m, reversal["quantity"]!.GetValue<decimal>());
        Assert.Equal(4m, reversal["balanceAfter"]!.GetValue<decimal>());
        Assert.Contains(movements, m => m!["type"]!.GetValue<string>() == "Receipt"); // тарих қалады
    }

    // ---------- Списание (§11) ----------

    [Fact]
    public async Task Write_off_needs_a_reason_cannot_exceed_stock_and_posts_with_the_reason_in_history()
    {
        var pos = await api.NewDeviceAsync();
        var product = await pos.CreateProductAsync(price: 1000, stock: 5);
        object[] Lines(decimal qty) => [new { productId = product, quantity = qty }];

        var tooMany = (await pos.Post(pos.Owner, $"{Inv}/write-offs", new { reason = "Damage", lines = Lines(6) })).Ok;
        var tooManyPost = await pos.Post(pos.Owner, $"{Inv}/documents/{tooMany["id"]}/post", new { });
        var other = (await pos.Post(pos.Owner, $"{Inv}/write-offs", new { reason = "Other", lines = Lines(1) })).Ok;
        var otherPost = await pos.Post(pos.Owner, $"{Inv}/documents/{other["id"]}/post", new { });
        var badReason = await pos.Post(pos.Owner, $"{Inv}/write-offs", new { reason = "Stolen", lines = Lines(1) });

        Assert.Equal(HttpStatusCode.BadRequest, tooManyPost.Status);
        Assert.Equal(HttpStatusCode.BadRequest, otherPost.Status); // «Другое» — коммент міндетті
        Assert.Equal(HttpStatusCode.BadRequest, badReason.Status);
        Assert.Equal(5m, await pos.StockAsync(product));

        var doc = (await pos.Post(pos.Owner, $"{Inv}/write-offs", new { reason = "Damage", comment = "Упала полка", lines = Lines(2) })).Ok;
        Assert.StartsWith("СП-", doc["number"]!.GetValue<string>());
        var posted = (await pos.Post(pos.Owner, $"{Inv}/documents/{doc["id"]}/post", new { })).Ok;

        Assert.Equal("Posted", posted["status"]!.GetValue<string>());
        Assert.Equal(3m, await pos.StockAsync(product));
        var movement = Assert.Single(await MovementsAsync(pos, product), m => m!["type"]!.GetValue<string>() == "WriteOff")!;
        Assert.Equal(-2m, movement["quantity"]!.GetValue<decimal>());
        Assert.Equal(3m, movement["balanceAfter"]!.GetValue<decimal>());
        Assert.StartsWith("Damage", movement["comment"]!.GetValue<string>());
    }

    // ---------- Перемещение (§10) ----------

    [Fact]
    public async Task Transfer_moves_stock_on_dispatch_and_records_what_was_actually_received()
    {
        var pos = await api.NewDeviceAsync();
        var product = await pos.CreateProductAsync(price: 1000, stock: 10);
        var (source, storeId) = await DefaultWarehouseAsync(pos);
        var destination = await CreateWarehouseAsync(pos);

        var same = await pos.Post(pos.Owner, $"{Inv}/transfers", new
        {
            sourceWarehouseId = source, destinationStoreId = storeId, destinationWarehouseId = source,
            lines = new[] { new { productId = product, quantity = 3m } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, same.Status);

        var draft = (await pos.Post(pos.Owner, $"{Inv}/transfers", new
        {
            sourceWarehouseId = source, destinationStoreId = storeId, destinationWarehouseId = destination,
            lines = new[] { new { productId = product, quantity = 3m } },
        })).Ok;
        var id = draft["id"]!.GetValue<Guid>();
        Assert.StartsWith("ПМ-", draft["number"]!.GetValue<string>());
        Assert.Equal(10m, StockIn(await StockRowAsync(pos, product), source));

        var sent = (await pos.Post(pos.Owner, $"{Inv}/transfers/{id}/dispatch", new { })).Ok;
        Assert.Equal("InTransit", sent["status"]!.GetValue<string>());
        var row = await StockRowAsync(pos, product);
        Assert.Equal(7m, StockIn(row, source));
        Assert.Equal(0m, StockIn(row, destination));

        var tooMuch = await pos.Post(pos.Owner, $"{Inv}/transfers/{id}/receive", new { lines = new[] { new { productId = product, quantityReceived = 4m } } });
        var noReason = await pos.Post(pos.Owner, $"{Inv}/transfers/{id}/receive", new { lines = new[] { new { productId = product, quantityReceived = 2m } } });
        var received = (await pos.Post(pos.Owner, $"{Inv}/transfers/{id}/receive", new
        {
            lines = new[] { new { productId = product, quantityReceived = 2m, discrepancyReason = "1 шт повреждена" } },
        })).Ok;
        var againAfterDone = await pos.Post(pos.Owner, $"{Inv}/transfers/{id}/dispatch", new { });

        Assert.Equal(HttpStatusCode.BadRequest, tooMuch.Status);
        Assert.Equal(HttpStatusCode.BadRequest, noReason.Status);
        Assert.Equal("Completed", received["status"]!.GetValue<string>());
        Assert.Equal(2m, received["totalReceived"]!.GetValue<decimal>());
        Assert.Equal(HttpStatusCode.BadRequest, againAfterDone.Status);
        row = await StockRowAsync(pos, product);
        Assert.Equal(7m, StockIn(row, source));
        Assert.Equal(2m, StockIn(row, destination)); // айырмашылық құжатта, автосписание жоқ
        Assert.Equal(9m, row["total"]!.GetValue<decimal>());
        var types = (await MovementsAsync(pos, product)).Select(m => m!["type"]!.GetValue<string>()).ToList();
        Assert.Contains("TransferOut", types);
        Assert.Contains("TransferIn", types);

        // Қалдығы бар қойма архивтелмейді.
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Post(pos.Owner, $"{Inv}/warehouses/{destination}/archive", new { })).Status);
    }

    [Fact]
    public async Task Cancelling_a_transfer_in_transit_needs_a_reason_and_returns_stock_to_the_source()
    {
        var pos = await api.NewDeviceAsync();
        var product = await pos.CreateProductAsync(price: 1000, stock: 5);
        var (source, storeId) = await DefaultWarehouseAsync(pos);
        var destination = await CreateWarehouseAsync(pos);
        var t = (await pos.Post(pos.Owner, $"{Inv}/transfers", new
        {
            sourceWarehouseId = source, destinationStoreId = storeId, destinationWarehouseId = destination,
            lines = new[] { new { productId = product, quantity = 5m } },
        })).Ok;
        var id = t["id"]!.GetValue<Guid>();
        (await pos.Post(pos.Owner, $"{Inv}/transfers/{id}/dispatch", new { })).EnsureOk();
        Assert.Equal(0m, StockIn(await StockRowAsync(pos, product), source));

        var noReason = await pos.Post(pos.Owner, $"{Inv}/transfers/{id}/cancel", new { });
        var cancelled = await pos.Post(pos.Owner, $"{Inv}/transfers/{id}/cancel", new { reason = "Машина не поехала" });

        Assert.Equal(HttpStatusCode.BadRequest, noReason.Status);
        Assert.Equal("Cancelled", cancelled.Ok["status"]!.GetValue<string>());
        Assert.Equal(5m, StockIn(await StockRowAsync(pos, product), source));
    }

    // ---------- Қоймалар (§6) ----------

    [Fact]
    public async Task Default_warehouse_cannot_be_archived_and_an_empty_one_can_be_archived_restored_and_made_default()
    {
        var pos = await api.NewDeviceAsync();
        var (original, _) = await DefaultWarehouseAsync(pos);
        var extra = await CreateWarehouseAsync(pos);

        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Post(pos.Owner, $"{Inv}/warehouses/{original}/archive", new { })).Status);
        Assert.Equal(HttpStatusCode.NoContent, (await pos.Post(pos.Owner, $"{Inv}/warehouses/{extra}/archive", new { })).Status);
        Assert.False(await IsActiveAsync(pos, extra)); // тізімде қалады — «(архив)» белгісімен, тарихы үшін
        Assert.Equal(HttpStatusCode.BadRequest, (await pos.Post(pos.Owner, $"{Inv}/warehouses/{extra}/make-default", new { })).Status); // архивтегі

        Assert.Equal(HttpStatusCode.NoContent, (await pos.Post(pos.Owner, $"{Inv}/warehouses/{extra}/restore", new { })).Status);
        Assert.True(await IsActiveAsync(pos, extra));
        Assert.Equal(HttpStatusCode.NoContent, (await pos.Post(pos.Owner, $"{Inv}/warehouses/{extra}/make-default", new { })).Status);
        var (nowDefault, _) = await DefaultWarehouseAsync(pos);
        Assert.Equal(extra, nowDefault);

        Assert.Equal(HttpStatusCode.NoContent, (await pos.Post(pos.Owner, $"{Inv}/warehouses/{original}/make-default", new { })).Status); // тесттерге бұрынғы әдепкі
        Assert.Equal(original, (await DefaultWarehouseAsync(pos)).Id);
    }

    // ---------- Кезең сүзгісі — дүкен уақытымен ----------

    [Fact]
    public async Task Period_filters_are_store_dates_and_find_todays_documents()
    {
        var pos = await api.NewDeviceAsync();
        var product = await pos.CreateProductAsync(price: 1000, stock: 0);
        var doc = (await CreateReceiptAsync(pos, null, ReceiptLine(product, 1, 100))).Ok;
        var number = doc["number"]!.GetValue<string>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5)); // Asia/Almaty — демо дүкеннің белдеуі
        var ymd = today.ToString("yyyy-MM-dd");
        var tomorrow = today.AddDays(1).ToString("yyyy-MM-dd");

        var inRange = await pos.Get(pos.Owner, $"{Inv}/receipts?from={ymd}&to={ymd}&search={Uri.EscapeDataString(number)}");
        var outOfRange = await pos.Get(pos.Owner, $"{Inv}/receipts?from={tomorrow}&to={tomorrow}&search={Uri.EscapeDataString(number)}");
        var movements = await pos.Get(pos.Owner, $"{Inv}/movements?from={ymd}&to={ymd}&productId={product}");
        var transfers = await pos.Get(pos.Owner, $"{Inv}/transfers?from={ymd}&to={ymd}");
        var writeOffs = await pos.Get(pos.Owner, $"{Inv}/write-offs?from={ymd}&to={ymd}");

        Assert.Contains(inRange.Ok["items"]!.AsArray(), d => d!["number"]!.GetValue<string>() == number);
        Assert.Equal(1, inRange.Ok["stats"]!["total"]!.GetValue<int>());
        Assert.Empty(outOfRange.Ok["items"]!.AsArray());
        movements.EnsureOk();
        transfers.EnsureOk();
        writeOffs.EnsureOk();
    }

    // ---------- Құқықтар (§16) ----------

    [Fact]
    public async Task Cashier_sees_stock_and_history_but_cannot_touch_documents_suppliers_or_warehouses()
    {
        var pos = await api.NewDeviceAsync();
        var product = await pos.CreateProductAsync(price: 1000, stock: 3);

        Assert.Equal(HttpStatusCode.OK, (await pos.Get(pos.Cashier, $"{Inv}/stock")).Status);
        Assert.Equal(HttpStatusCode.OK, (await pos.Get(pos.Cashier, $"{Inv}/kpi")).Status);
        Assert.Equal(HttpStatusCode.OK, (await pos.Get(pos.Cashier, $"{Inv}/movements?productId={product}")).Status);
        Assert.Equal(HttpStatusCode.OK, (await pos.Get(pos.Cashier, $"{Inv}/warehouses")).Status);

        Assert.Equal(HttpStatusCode.Forbidden, (await pos.Post(pos.Cashier, $"{Inv}/receipts", new { isQuick = true, lines = new[] { ReceiptLine(product, 1, 100) } })).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await pos.Get(pos.Cashier, $"{Inv}/receipts")).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await pos.Post(pos.Cashier, $"{Inv}/write-offs", new { reason = "Damage", lines = new[] { new { productId = product, quantity = 1m } } })).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await pos.Post(pos.Cashier, $"{Inv}/transfers", new { })).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await pos.Get(pos.Cashier, $"{Inv}/suppliers")).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await pos.Post(pos.Cashier, $"{Inv}/warehouses", new { name = "Х" })).Status);
        Assert.Equal(3m, await pos.StockAsync(product));
    }
}
