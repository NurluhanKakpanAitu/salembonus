using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Application.Pos.Inventory;
using SalemBonus.Domain.Core;
using SalemBonus.Domain.Pos.Catalog;
using SalemBonus.Domain.Pos.Inventory;

namespace SalemBonus.Application.Pos.Catalog;

/// <summary>
/// Тауар карточкасы (ТЗ «Товар» §5–6, §14). Каталог бизнеске ортақ; баға мен қалдық — дүкен
/// мен қоймада. Барлық тексеріс осында: фронт тек батырманы жасырады.
/// </summary>
public partial class ProductService(
    IProductRepository repo,
    ICatalogRepository catalog,
    IInventoryRepository inventory,
    WarehouseService warehouses,
    IFileStorage storage,
    CatalogAccess access,
    IUnitOfWork unitOfWork) : IProductService
{
    public const int MaxImages = 10;
    private const int MaxPageSize = 100;

    private AppLanguage Lang => access.Lang;

    [GeneratedRegex(@"^[0-9A-Za-z\-\.]{3,64}$")]
    private static partial Regex BarcodePattern();

    public static string ImagePrefix(Guid orgId) => $"org/{orgId:N}/products/";

    // ---------- Оқу ----------

    public async Task<ProductPageDto> ListAsync(string? search, string? status, Guid? nodeId, Guid? brandId, Guid? unitId,
        int page, int pageSize, CancellationToken ct = default)
    {
        var (orgId, _) = await access.RequireAsync(StaffPermissions.ProductsView, ct);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var nodes = await catalog.ListNodesAsync(orgId, ct);
        string? nodePath = null;
        if (nodeId is { } nid)
            nodePath = nodes.FirstOrDefault(n => n.Id == nid)?.Path ?? throw new NotFoundException(Messages.CatalogNodeNotFound(Lang));

        CatalogStatus? statusFilter = string.IsNullOrWhiteSpace(status) ? null : access.Status(status, CatalogStatus.Active);
        var term = search?.Trim();
        var (items, total) = await repo.SearchAsync(orgId,
            new ProductFilter(string.IsNullOrEmpty(term) ? null : term, statusFilter, nodePath, brandId, unitId,
                (page - 1) * pageSize, pageSize), ct);

        var lookup = await LookupAsync(orgId, nodes, ct);
        return new ProductPageDto(items.Select(p => ToListItem(p, lookup)).ToList(), total, page, pageSize);
    }

    public async Task<ProductDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.ProductsView, ct);
        var product = await repo.GetAsync(orgId, id, ct) ?? throw new NotFoundException(Messages.ProductNotFound(Lang));
        return await ToDtoAsync(orgId, m, product, ct);
    }

    /// <summary>Кез келген байланған штрихкод бір тауарды табады (ТЗ §6.5).</summary>
    public async Task<ProductListItemDto> FindByBarcodeAsync(string barcode, CancellationToken ct = default)
    {
        var (orgId, _) = await access.RequireAsync(StaffPermissions.ProductsView, ct);
        var product = await repo.FindByBarcodeAsync(orgId, barcode.Trim(), ct) ?? throw new NotFoundException(Messages.ProductNotFound(Lang));
        return ToListItem(product, await LookupAsync(orgId, await catalog.ListNodesAsync(orgId, ct), ct));
    }

    public async Task<BarcodeCheckDto> CheckBarcodeAsync(string barcode, Guid? excludeProductId, CancellationToken ct = default)
    {
        var (orgId, _) = await access.RequireAsync(StaffPermissions.ProductsView, ct);
        var code = barcode.Trim();
        if (!BarcodePattern().IsMatch(code)) throw new ValidationException(Messages.BarcodeInvalid(Lang, code), "barcode");
        var owner = (await repo.BarcodeOwnersAsync(orgId, [code], excludeProductId, ct)).FirstOrDefault();
        return owner is null ? new BarcodeCheckDto(true, null, null) : new BarcodeCheckDto(false, owner.ProductId, owner.ProductName);
    }

    /// <summary>
    /// Ішкі EAN-13: «2» префиксі дүкен ішіндегі кодтарға арналған (GS1), сондықтан зауыттық
    /// штрихкодпен соқтығыспайды. Бизнесте бос екені тексеріледі.
    /// </summary>
    public async Task<GeneratedBarcodeDto> GenerateBarcodeAsync(CancellationToken ct = default)
    {
        var (orgId, _) = await access.RequireAnyAsync(ct, StaffPermissions.ProductsCreate, StaffPermissions.ProductsEdit);
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var body = "2" + string.Concat(Enumerable.Range(0, 11).Select(_ => RandomNumberGenerator.GetInt32(10)));
            var code = body + Ean13CheckDigit(body);
            if ((await repo.BarcodeOwnersAsync(orgId, [code], null, ct)).Count == 0) return new GeneratedBarcodeDto(code);
        }
        throw new ValidationException(Messages.BarcodeGenerateFailed(Lang), "barcode");
    }

    public static int Ean13CheckDigit(string twelveDigits)
    {
        var sum = 0;
        for (var i = 0; i < 12; i++) sum += (twelveDigits[i] - '0') * (i % 2 == 0 ? 1 : 3);
        return (10 - sum % 10) % 10;
    }

    // ---------- Жазу ----------

    public async Task<ProductDto> CreateAsync(SaveProductRequest request, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.ProductsCreate, ct);
        var product = new Product { Id = Guid.NewGuid(), OrganizationId = orgId };
        await ApplyFieldsAsync(orgId, product, request, isNew: true, ct);
        var barcodes = await ValidateBarcodesAsync(orgId, product.Id, request.Barcodes, ct);
        var images = ValidateImages(orgId, request.Images);
        var values = await ValidateCharacteristicsAsync(orgId, request.Characteristics, ct);

        repo.Add(product);
        SyncChildren(product, barcodes, images, values);
        var opening = await ApplyOpeningAsync(orgId, m, product, request.Opening, ct);

        access.Audit(orgId, m.StoreId, "catalog.product.create", "product", product.Id, null, Snapshot(product, barcodes, opening));
        await unitOfWork.SaveChangesAsync(ct);
        return await GetAsync(product.Id, ct);
    }

    public async Task<ProductDto> UpdateAsync(Guid id, SaveProductRequest request, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.ProductsEdit, ct);
        var product = await repo.GetForUpdateAsync(orgId, id, ct) ?? throw new NotFoundException(Messages.ProductNotFound(Lang));
        var before = Snapshot(product, product.Barcodes.Select(b => new ProductBarcodeDto(b.Barcode, b.IsPrimary)).ToList(), null);

        await ApplyFieldsAsync(orgId, product, request, isNew: false, ct);
        var barcodes = await ValidateBarcodesAsync(orgId, product.Id, request.Barcodes, ct);
        var images = ValidateImages(orgId, request.Images);
        var values = await ValidateCharacteristicsAsync(orgId, request.Characteristics, ct);
        SyncChildren(product, barcodes, images, values);
        var price = await ApplySalePriceAsync(product.Id, m.StoreId, request.SalePrice, ct);
        product.UpdatedAt = DateTime.UtcNow;

        access.Audit(orgId, m.StoreId, "catalog.product.update", "product", product.Id, before, Snapshot(product, barcodes, null));
        if (price is { } p)
            access.Audit(orgId, m.StoreId, "catalog.product.price", "product", product.Id, new { SalePrice = p.Old }, new { SalePrice = p.New });
        await unitOfWork.SaveChangesAsync(ct);
        return await GetAsync(product.Id, ct);
    }

    /// <summary>Классификацияны өзгерту (ТЗ §14): тауар сол объект болып қалады, тарихы сақталады.</summary>
    public async Task<ProductDto> ChangeClassificationAsync(Guid id, Guid? nodeId, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.ProductsEdit, ct);
        var product = await repo.GetForUpdateAsync(orgId, id, ct) ?? throw new NotFoundException(Messages.ProductNotFound(Lang));
        var oldNodeId = product.CatalogNodeId;
        if (oldNodeId == nodeId) return await GetAsync(id, ct);

        await EnsureNodeAsync(orgId, nodeId, ct);
        product.CatalogNodeId = nodeId;
        product.UpdatedAt = DateTime.UtcNow;
        access.Audit(orgId, m.StoreId, "catalog.product.move", "product", product.Id, new { NodeId = oldNodeId }, new { NodeId = nodeId });
        await unitOfWork.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task ArchiveAsync(Guid id, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.CatalogLifecycle, ct);
        var product = await repo.GetForUpdateAsync(orgId, id, ct) ?? throw new NotFoundException(Messages.ProductNotFound(Lang));
        if (product.Status == CatalogStatus.Archived) return;
        product.Status = CatalogStatus.Archived;
        product.UpdatedAt = DateTime.UtcNow;
        access.Audit(orgId, m.StoreId, "catalog.product.archive", "product", product.Id, null, new { Status = "Archived" });
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task RestoreAsync(Guid id, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.CatalogLifecycle, ct);
        var product = await repo.GetForUpdateAsync(orgId, id, ct) ?? throw new NotFoundException(Messages.ProductNotFound(Lang));
        if (product.Status == CatalogStatus.Active) return;
        if (product.CatalogNodeId is { } nodeId
            && (await catalog.GetNodeForUpdateAsync(orgId, nodeId, ct))?.Status == CatalogStatus.Archived)
            throw new ValidationException(Messages.ProductRestoreNodeArchived(Lang), "nodeId");
        product.Status = CatalogStatus.Active;
        product.UpdatedAt = DateTime.UtcNow;
        access.Audit(orgId, m.StoreId, "catalog.product.restore", "product", product.Id, null, new { Status = "Active" });
        await unitOfWork.SaveChangesAsync(ct);
    }

    // ---------- Тексеріс ----------

    private async Task ApplyFieldsAsync(Guid orgId, Product p, SaveProductRequest r, bool isNew, CancellationToken ct)
    {
        p.Name = access.Name(r.Name, 200);
        p.Article = CatalogAccess.Optional(r.Article, 64);
        p.Description = CatalogAccess.Optional(r.Description, 1000);
        p.Supplier = CatalogAccess.Optional(r.Supplier, 200);
        p.Manufacturer = CatalogAccess.Optional(r.Manufacturer, 200);
        p.Country = CatalogAccess.Optional(r.Country, 64);
        p.Notes = CatalogAccess.Optional(r.Notes, 1000);
        p.IsMarked = r.IsMarked;
        p.WarrantyMonths = InRange(r.WarrantyMonths, 0, 600, "warrantyMonths");
        p.ShelfLifeDays = InRange(r.ShelfLifeDays, 0, 36500, "shelfLifeDays");
        p.VatRate = r.VatRate is { } vat
            ? vat is >= 0 and <= 100 ? Math.Round(vat, 2) : throw new ValidationException(Messages.ValueOutOfRange(Lang, 0, 100), "vatRate")
            : null;

        // Архивтегі анықтамалық жаңа байланысқа ұсынылмайды, бірақ бұрынғы байланыс сақталады (ТЗ §16).
        var unitId = r.UnitId ?? throw new ValidationException(Messages.ProductUnitRequired(Lang), "unitId");
        if (isNew || unitId != p.UnitId)
        {
            var unit = await catalog.GetUnitForUpdateAsync(orgId, unitId, ct) ?? throw new ValidationException(Messages.UnitNotFound(Lang), "unitId");
            if (unit.Status == CatalogStatus.Archived) throw new ValidationException(Messages.ProductUnitArchived(Lang), "unitId");
            p.UnitId = unitId;
        }

        if (r.BrandId != p.BrandId || isNew)
        {
            if (r.BrandId is { } brandId)
            {
                var brand = await catalog.GetBrandForUpdateAsync(orgId, brandId, ct) ?? throw new ValidationException(Messages.BrandNotFound(Lang), "brandId");
                if (brand.Status == CatalogStatus.Archived) throw new ValidationException(Messages.ProductBrandArchived(Lang), "brandId");
            }
            p.BrandId = r.BrandId;
        }

        if (r.NodeId != p.CatalogNodeId || isNew)
        {
            await EnsureNodeAsync(orgId, r.NodeId, ct);
            p.CatalogNodeId = r.NodeId;
        }
    }

    private async Task EnsureNodeAsync(Guid orgId, Guid? nodeId, CancellationToken ct)
    {
        if (nodeId is not { } id) return;
        var node = await catalog.GetNodeForUpdateAsync(orgId, id, ct) ?? throw new ValidationException(Messages.CatalogNodeNotFound(Lang), "nodeId");
        if (node.Status == CatalogStatus.Archived) throw new ValidationException(Messages.ProductNodeArchived(Lang), "nodeId");
    }

    private int? InRange(int? value, int min, int max, string field) =>
        value is null || (value >= min && value <= max) ? value : throw new ValidationException(Messages.ValueOutOfRange(Lang, min, max), field);

    /// <summary>Бір бизнесте бір штрихкод тек бір тауарда (ТЗ §18). Негізгісі біреу ғана.</summary>
    private async Task<List<ProductBarcodeDto>> ValidateBarcodesAsync(Guid orgId, Guid productId,
        IReadOnlyList<ProductBarcodeDto>? input, CancellationToken ct)
    {
        var list = new List<ProductBarcodeDto>();
        foreach (var b in input ?? [])
        {
            var code = b.Barcode?.Trim() ?? string.Empty;
            if (code.Length == 0) continue;
            if (!BarcodePattern().IsMatch(code)) throw new ValidationException(Messages.BarcodeInvalid(Lang, code), "barcodes");
            if (list.Any(x => x.Barcode == code)) throw new ValidationException(Messages.BarcodeRepeated(Lang, code), "barcodes");
            list.Add(new ProductBarcodeDto(code, b.IsPrimary));
        }

        var owner = (await repo.BarcodeOwnersAsync(orgId, list.Select(x => x.Barcode).ToList(), productId, ct)).FirstOrDefault();
        if (owner is not null) throw new ValidationException(Messages.BarcodeTaken(Lang, owner.Barcode, owner.ProductName), "barcodes");

        var primary = Math.Max(0, list.FindIndex(x => x.IsPrimary));
        return list.Select((x, i) => x with { IsPrimary = i == primary }).ToList();
    }

    /// <summary>Фото тек біздің қоймадағы осы бизнестің бумасынан — бөтен сілтеме сақталмайды.</summary>
    private List<ProductImageDto> ValidateImages(Guid orgId, IReadOnlyList<ProductImageDto>? input)
    {
        var list = (input ?? []).Where(i => !string.IsNullOrWhiteSpace(i.Url))
            .Select(i => i with { Url = i.Url.Trim() }).DistinctBy(i => i.Url).ToList();
        if (list.Count > MaxImages) throw new ValidationException(Messages.ProductImagesTooMany(Lang, MaxImages), "images");
        if (list.Any(i => i.Url.Length > 500 || !storage.IsOwnUrl(i.Url, ImagePrefix(orgId))))
            throw new ValidationException(Messages.ProductImageInvalid(Lang), "images");
        var primary = Math.Max(0, list.FindIndex(x => x.IsPrimary));
        return list.Select((x, i) => x with { IsPrimary = i == primary }).ToList();
    }

    private async Task<List<ProductCharacteristicDto>> ValidateCharacteristicsAsync(Guid orgId,
        IReadOnlyList<ProductCharacteristicDto>? input, CancellationToken ct)
    {
        var defs = (await catalog.ListCharacteristicsAsync(orgId, ct)).ToDictionary(d => d.Id);
        var result = new List<ProductCharacteristicDto>();
        foreach (var v in input ?? [])
        {
            if (string.IsNullOrWhiteSpace(v.Value) || result.Any(x => x.DefinitionId == v.DefinitionId)) continue;
            var def = defs.GetValueOrDefault(v.DefinitionId) ?? throw new ValidationException(Messages.CharacteristicNotFound(Lang), "characteristics");
            var normalized = NormalizeValue(def, v.Value.Trim())
                ?? throw new ValidationException(Messages.CharacteristicValueInvalid(Lang, def.Name), "characteristics");
            result.Add(new ProductCharacteristicDto(def.Id, normalized));
        }

        var missing = defs.Values.Where(d => d.Status == CatalogStatus.Active && d.IsRequired)
            .OrderBy(d => d.SortOrder).FirstOrDefault(d => result.All(x => x.DefinitionId != d.Id));
        if (missing is not null) throw new ValidationException(Messages.CharacteristicRequired(Lang, missing.Name), "characteristics");
        return result;
    }

    /// <summary>Мәнді түріне қарай тексеріп, бір қалыпқа келтіреді. Қате болса — null.</summary>
    public static string? NormalizeValue(CharacteristicDefinition def, string raw)
    {
        switch (def.Type)
        {
            case CharacteristicType.Text:
                return raw.Length <= 200 ? raw : null;
            case CharacteristicType.Number:
                return ParseNumber(raw) is { } n ? FormatNumber(n) : null;
            case CharacteristicType.List:
                return def.Options.FirstOrDefault(o => string.Equals(o.Value, raw, StringComparison.OrdinalIgnoreCase))?.Value;
            case CharacteristicType.Boolean:
                return raw.ToLowerInvariant() is "true" or "false" ? raw.ToLowerInvariant() : null;
            case CharacteristicType.Date:
                return DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                    ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null;
            case CharacteristicType.Range:
                var parts = raw.Split("..");
                if (parts.Length != 2 || ParseNumber(parts[0]) is not { } from || ParseNumber(parts[1]) is not { } to || from > to) return null;
                return $"{FormatNumber(from)}..{FormatNumber(to)}";
            default:
                return null;
        }
    }

    private static decimal? ParseNumber(string raw) =>
        decimal.TryParse(raw.Trim().Replace(',', '.').Replace(" ", ""), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out var n) ? n : null;

    private static string FormatNumber(decimal n) => n.ToString("0.############", CultureInfo.InvariantCulture);

    /// <summary>
    /// Балаларды айырма бойынша жаңарту: барын өзгертеміз, жоғын өшіреміз, жаңасын нақты қосамыз.
    /// Штрихкодты өшіріп, сол кодты қайта қоссақ, бірегейлік индексі сақтау кезінде соқтығысар еді.
    /// </summary>
    private void SyncChildren(Product p, List<ProductBarcodeDto> barcodes, List<ProductImageDto> images, List<ProductCharacteristicDto> values)
    {
        foreach (var existing in p.Barcodes.ToList())
        {
            var match = barcodes.FirstOrDefault(b => b.Barcode == existing.Barcode);
            if (match is null) repo.RemoveBarcode(existing);
            else existing.IsPrimary = match.IsPrimary;
        }
        foreach (var b in barcodes.Where(b => p.Barcodes.All(x => x.Barcode != b.Barcode)))
            repo.AddBarcode(new ProductBarcode
            {
                Id = Guid.NewGuid(), ProductId = p.Id, OrganizationId = p.OrganizationId, Barcode = b.Barcode, IsPrimary = b.IsPrimary,
            });

        foreach (var existing in p.Images.ToList())
            if (images.All(i => i.Url != existing.Url)) repo.RemoveImage(existing);
        for (var i = 0; i < images.Count; i++)
        {
            var existing = p.Images.FirstOrDefault(x => x.Url == images[i].Url);
            if (existing is null)
                repo.AddImage(new ProductImage { Id = Guid.NewGuid(), ProductId = p.Id, Url = images[i].Url, IsPrimary = images[i].IsPrimary, SortOrder = i + 1 });
            else
            {
                existing.IsPrimary = images[i].IsPrimary;
                existing.SortOrder = i + 1;
            }
        }

        foreach (var existing in p.Characteristics.ToList())
        {
            var match = values.FirstOrDefault(v => v.DefinitionId == existing.DefinitionId);
            if (match is null) repo.RemoveCharacteristicValue(existing);
            else existing.Value = match.Value;
        }
        foreach (var v in values.Where(v => p.Characteristics.All(x => x.DefinitionId != v.DefinitionId)))
            repo.AddCharacteristicValue(new ProductCharacteristicValue { ProductId = p.Id, DefinitionId = v.DefinitionId, Value = v.Value });
    }

    /// <summary>
    /// Бастапқы қалдық пен баға (ТЗ §6.2): тауармен бірге бір транзакцияда бастапқы қоймалық жазба болып сақталады.
    /// Қойма — ағымдағы дүкендікі.
    /// </summary>
    private async Task<object?> ApplyOpeningAsync(Guid orgId, StoreMembership m, Product product, OpeningStockRequest? o, CancellationToken ct)
    {
        if (o is null || (o.Quantity is null or 0m && o.PurchasePrice is null && o.SalePrice is null)) return null;
        if (o.Quantity < 0) throw new ValidationException(Messages.ValueNegative(Lang), "opening.quantity");
        if (o.PurchasePrice < 0) throw new ValidationException(Messages.ValueNegative(Lang), "opening.purchasePrice");
        if (o.SalePrice < 0) throw new ValidationException(Messages.ValueNegative(Lang), "opening.salePrice");

        var warehouse = o.WarehouseId is { } wid
            ? await inventory.GetWarehouseAsync(m.StoreId, wid, ct) ?? throw new ValidationException(Messages.WarehouseNotFound(Lang), "opening.warehouseId")
            : await warehouses.EnsureDefaultAsync(orgId, m.StoreId, ct);

        var quantity = Math.Round(o.Quantity ?? 0, 3);
        var purchase = o.PurchasePrice is { } pp ? Math.Round(pp, 2) : (decimal?)null;
        var sale = o.SalePrice is { } sp ? Math.Round(sp, 2) : (decimal?)null;
        var now = DateTime.UtcNow;

        if (quantity > 0)
        {
            inventory.AddMovement(new StockMovement
            {
                Id = Guid.NewGuid(), OrganizationId = orgId, WarehouseId = warehouse.Id, ProductId = product.Id,
                Type = StockMovementType.Opening, Quantity = quantity, UnitCost = purchase, StaffUserId = access.StaffUserId, CreatedAt = now,
            });
            inventory.AddBalance(new StockBalance { WarehouseId = warehouse.Id, ProductId = product.Id, Quantity = quantity, UpdatedAt = now });
        }
        if (sale is not null || purchase is not null)
            inventory.AddPrice(new ProductPrice { ProductId = product.Id, StoreId = warehouse.StoreId, SalePrice = sale ?? 0, PurchasePrice = purchase, UpdatedAt = now });

        return new { WarehouseId = warehouse.Id, Quantity = quantity, PurchasePrice = purchase, SalePrice = sale };
    }

    /// <summary>
    /// Ағымдағы дүкеннің сату бағасын өзгерту (уақытша, «Склад» модулі келгенше). Өзгермесе — null.
    /// Кіріс бағасына тимейміз: ол қоймалық кіріспен ғана өзгереді.
    /// </summary>
    private async Task<(decimal? Old, decimal New)?> ApplySalePriceAsync(Guid productId, Guid storeId, decimal? salePrice, CancellationToken ct)
    {
        if (salePrice is not { } raw) return null;
        if (raw < 0) throw new ValidationException(Messages.ValueNegative(Lang), "salePrice");
        var value = Math.Round(raw, 2);

        var price = await inventory.GetPriceForUpdateAsync(productId, storeId, ct);
        if (price is null)
        {
            inventory.AddPrice(new ProductPrice { ProductId = productId, StoreId = storeId, SalePrice = value });
            return (null, value);
        }
        if (price.SalePrice == value) return null;
        var old = price.SalePrice;
        price.SalePrice = value;
        price.UpdatedAt = DateTime.UtcNow;
        return (old, value);
    }

    // ---------- Көрсету ----------

    private sealed record Lookup(
        IReadOnlyDictionary<Guid, IReadOnlyList<string>> Paths,
        IReadOnlyDictionary<Guid, Brand> Brands,
        IReadOnlyDictionary<Guid, MeasureUnit> Units);

    private async Task<Lookup> LookupAsync(Guid orgId, IReadOnlyList<CatalogNode> nodes, CancellationToken ct)
    {
        var names = nodes.ToDictionary(n => n.Id.ToString(), n => n.Name);
        var paths = nodes.ToDictionary(n => n.Id,
            n => (IReadOnlyList<string>)n.Path.Split('/').Select(id => names.GetValueOrDefault(id) ?? "?").ToList());
        var brands = (await catalog.ListBrandsAsync(orgId, ct)).ToDictionary(b => b.Id);
        var units = (await catalog.ListUnitsAsync(orgId, ct)).ToDictionary(u => u.Id);
        return new Lookup(paths, brands, units);
    }

    private static ProductListItemDto ToListItem(Product p, Lookup l) => new(
        p.Id,
        p.Name,
        p.Article,
        p.Barcodes.FirstOrDefault(b => b.IsPrimary)?.Barcode ?? p.Barcodes.FirstOrDefault()?.Barcode,
        p.Images.FirstOrDefault(i => i.IsPrimary)?.Url ?? p.Images.OrderBy(i => i.SortOrder).FirstOrDefault()?.Url,
        p.CatalogNodeId,
        p.CatalogNodeId is { } nid ? l.Paths.GetValueOrDefault(nid) ?? [] : [],
        p.BrandId,
        p.BrandId is { } bid ? l.Brands.GetValueOrDefault(bid)?.Name : null,
        p.UnitId,
        l.Units.GetValueOrDefault(p.UnitId)?.ShortName,
        p.Status.ToString(),
        p.UpdatedAt);

    private async Task<ProductDto> ToDtoAsync(Guid orgId, StoreMembership m, Product p, CancellationToken ct)
    {
        var lookup = await LookupAsync(orgId, await catalog.ListNodesAsync(orgId, ct), ct);
        var stock = await inventory.StockByProductAsync(orgId, p.Id, ct);
        var price = await inventory.GetPriceAsync(p.Id, m.StoreId, ct);
        // Кіріс бағасы — коммерциялық құпия: кассир көрмейді.
        var canSeeCost = m.Has(StaffPermissions.ProductsEdit) || m.Has(StaffPermissions.FinanceView);

        return new ProductDto(
            p.Id, p.Name, p.Article, p.UnitId, p.BrandId, p.CatalogNodeId,
            p.CatalogNodeId is { } nid ? lookup.Paths.GetValueOrDefault(nid) ?? [] : [],
            p.Status.ToString(), p.Description, p.Supplier, p.Manufacturer, p.Country,
            p.WarrantyMonths, p.ShelfLifeDays, p.VatRate, p.IsMarked, p.Notes,
            p.Barcodes.OrderByDescending(b => b.IsPrimary).ThenBy(b => b.Barcode).Select(b => new ProductBarcodeDto(b.Barcode, b.IsPrimary)).ToList(),
            p.Images.OrderBy(i => i.SortOrder).Select(i => new ProductImageDto(i.Url, i.IsPrimary)).ToList(),
            p.Characteristics.Select(c => new ProductCharacteristicDto(c.DefinitionId, c.Value)).ToList(),
            stock.Select(s => new ProductStockDto(s.WarehouseId, s.WarehouseName, s.StoreName, s.Quantity)).ToList(),
            price?.SalePrice,
            canSeeCost ? price?.PurchasePrice : null,
            p.CreatedAt, p.UpdatedAt);
    }

    private static object Snapshot(Product p, IReadOnlyList<ProductBarcodeDto> barcodes, object? opening) => new
    {
        p.Name, p.Article, p.UnitId, p.BrandId, NodeId = p.CatalogNodeId, Status = p.Status.ToString(), p.Description,
        p.Supplier, p.Manufacturer, p.Country, p.WarrantyMonths, p.ShelfLifeDays, p.VatRate, p.IsMarked, p.Notes,
        Barcodes = barcodes.Select(b => b.IsPrimary ? $"{b.Barcode}*" : b.Barcode).ToList(),
        Opening = opening,
    };
}
