using System.Globalization;
using System.Text.RegularExpressions;
using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Application.Pos.Inventory;
using SalemBonus.Domain.Core;
using SalemBonus.Domain.Pos.Catalog;
using SalemBonus.Domain.Pos.Inventory;

namespace SalemBonus.Application.Pos.Catalog;

public record ImportErrorDto(int Row, string Message);

public record ImportResultDto(
    bool Applied, int Total, int Created, int Updated, int Skipped, int CreatedCategories, int CreatedBrands,
    IReadOnlyList<ImportErrorDto> Errors);

/// <summary>
/// Каталогты Excel арқылы импорттау мен экспорттау (ТЗ «Товар» §5: «Импорт / Экспорт — согласно правам»).
///
/// Импорт екі кезеңмен: алдымен әр жол толық тексеріледі (ештеңе өзгермейді), сосын ғана қолданылады —
/// қате жол жартылай сақталып қалмайды. «Тексеру» (dryRun) дәл сол кодпен жүреді, тек сақталмайды.
/// Файлда жоқ баған тауарда өзгермейді; баған бар, ұяшық бос болса — мән тазаланады.
/// </summary>
public partial class ProductExchangeService(
    IProductRepository products,
    ICatalogRepository catalog,
    IInventoryRepository inventory,
    WarehouseService warehouses,
    ISpreadsheet spreadsheet,
    CatalogAccess access,
    IUnitOfWork unitOfWork)
{
    public const int MaxRows = 5000;
    public const long MaxFileBytes = 5 * 1024 * 1024;
    private const int MaxReportedErrors = 300;

    private AppLanguage Lang => access.Lang;

    private sealed record Column(string Key, string Ru, string Kk);

    private static readonly Column[] Columns =
    [
        new("id", "ID", "ID"),
        new("name", "Название", "Атауы"),
        new("article", "Артикул", "Артикул"),
        new("barcode", "Штрихкод", "Штрихкод"),
        new("extraBarcodes", "Доп. штрихкоды", "Қосымша штрихкодтар"),
        new("category", "Категория", "Санат"),
        new("brand", "Бренд", "Бренд"),
        new("unit", "Единица", "Бірлік"),
        new("salePrice", "Продажная цена", "Сату бағасы"),
        new("quantity", "Начальный остаток", "Бастапқы қалдық"),
        new("purchasePrice", "Закупочная цена", "Кіріс бағасы"),
        new("status", "Статус", "Күйі"),
        new("description", "Описание", "Сипаттама"),
        new("supplier", "Поставщик", "Жеткізуші"),
        new("manufacturer", "Производитель", "Өндіруші"),
        new("country", "Страна", "Елі"),
        new("warranty", "Гарантия, мес", "Кепілдік, ай"),
        new("shelfLife", "Срок годности, дн", "Жарамдылық, күн"),
        new("vat", "НДС, %", "ҚҚС, %"),
        new("marked", "Маркировка", "Таңбалау"),
        new("notes", "Заметки", "Жазбалар"),
    ];

    /// <summary>Сипаттама бағанының алғы жұрнағы: «Производитель» сипаттамасы өндіруші бағанымен шатаспасын.</summary>
    private static readonly string[] CharacteristicPrefixes = ["хар.:", "хар:", "сип.:", "сип:"];

    [GeneratedRegex(@"^[0-9A-Za-z\-\.]{3,64}$")]
    private static partial Regex BarcodePattern();

    [GeneratedRegex(@"\s*(?:/|→|>)\s*")]
    private static partial Regex PathSeparator();

    [GeneratedRegex(@"[\s,;]+")]
    private static partial Regex ListSeparator();

    [GeneratedRegex(@"(?<=\d)\s*(?:–|—|\.\.|-)\s*(?=-?\d)")]
    private static partial Regex RangeSeparator();

    private string Title(Column c) => Lang == AppLanguage.Ru ? c.Ru : c.Kk;
    private string CharacteristicTitle(string name) => (Lang == AppLanguage.Ru ? "Хар.: " : "Сип.: ") + name;

    // =====================================================================
    // Экспорт
    // =====================================================================

    public async Task<(byte[] Content, string FileName)> ExportAsync(string? search, string? status, Guid? nodeId, Guid? brandId,
        Guid? unitId, bool template, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.ProductsView, ct);
        var defs = (await catalog.ListCharacteristicsAsync(orgId, ct)).Where(d => d.Status == CatalogStatus.Active).ToList();
        var headers = Columns.Select(Title).Concat(defs.Select(d => CharacteristicTitle(d.Name))).ToList();
        var sheetName = Lang == AppLanguage.Ru ? "Товары" : "Тауарлар";

        if (template)
            return (spreadsheet.Write(sheetName, headers, []), "salempos-products-template.xlsx");

        var nodes = await catalog.ListNodesAsync(orgId, ct);
        string? nodePath = null;
        if (nodeId is { } nid)
            nodePath = nodes.FirstOrDefault(n => n.Id == nid)?.Path ?? throw new NotFoundException(Messages.CatalogNodeNotFound(Lang));
        CatalogStatus? statusFilter = string.IsNullOrWhiteSpace(status) ? null : access.Status(status, CatalogStatus.Active);
        var term = search?.Trim();

        var list = await products.ListForExportAsync(orgId,
            new ProductFilter(string.IsNullOrEmpty(term) ? null : term, statusFilter, nodePath, brandId, unitId, 0, 0), ct);
        var names = nodes.ToDictionary(n => n.Id.ToString(), n => n.Name);
        var paths = nodes.ToDictionary(n => n.Id, n => string.Join(" / ", n.Path.Split('/').Select(id => names.GetValueOrDefault(id) ?? "?")));
        var brands = (await catalog.ListBrandsAsync(orgId, ct)).ToDictionary(b => b.Id, b => b.Name);
        var units = (await catalog.ListUnitsAsync(orgId, ct)).ToDictionary(u => u.Id, u => u.ShortName);
        var prices = (await inventory.ListPricesForUpdateAsync(m.StoreId, ct)).ToDictionary(p => p.ProductId, p => p.SalePrice);
        var yes = Lang == AppLanguage.Ru ? "Да" : "Иә";
        var no = Lang == AppLanguage.Ru ? "Нет" : "Жоқ";

        var rows = list.Select(p =>
        {
            var primary = p.Barcodes.FirstOrDefault(b => b.IsPrimary)?.Barcode ?? p.Barcodes.FirstOrDefault()?.Barcode;
            var values = p.Characteristics.ToDictionary(c => c.DefinitionId, c => c.Value);
            IReadOnlyList<object?> row =
            [
                p.Id.ToString(), p.Name, p.Article, primary,
                string.Join(", ", p.Barcodes.Select(b => b.Barcode).Where(b => b != primary)),
                p.CatalogNodeId is { } n ? paths.GetValueOrDefault(n) : null,
                p.BrandId is { } b ? brands.GetValueOrDefault(b) : null,
                units.GetValueOrDefault(p.UnitId),
                prices.TryGetValue(p.Id, out var price) ? price : null,
                // Қалдық пен кіріс бағасы тек жаңа тауар импортында қолданылады — экспортта бос.
                null, null,
                p.Status == CatalogStatus.Active ? Messages.StatusActive(Lang) : Messages.StatusArchived(Lang),
                p.Description, p.Supplier, p.Manufacturer, p.Country,
                p.WarrantyMonths, p.ShelfLifeDays, p.VatRate,
                p.IsMarked ? yes : no,
                p.Notes,
                .. defs.Select(d => (object?)FormatValue(d, values.GetValueOrDefault(d.Id), yes, no)),
            ];
            return row;
        });

        var file = spreadsheet.Write(sheetName, headers, rows);
        return (file, $"salempos-products-{DateTime.UtcNow:yyyy-MM-dd}.xlsx");
    }

    private static string? FormatValue(CharacteristicDefinition def, string? value, string yes, string no) =>
        value is null ? null
        : def.Type == CharacteristicType.Boolean ? (value == "true" ? yes : no)
        : value;

    // =====================================================================
    // Импорт
    // =====================================================================

    /// <summary>Бір жолдан шыққан, толық тексерілген жоспар. Қолдану кезеңінде ғана өзгеріс жасалады.</summary>
    private sealed class RowPlan
    {
        public int Row;
        public Product? Existing;
        public string Name = string.Empty;
        public Guid? UnitId;
        public bool HasBrand;
        public Guid? BrandId;
        public string? NewBrandName;
        public bool HasCategory;
        public Guid? NodeId;
        public List<string>? NewPath;
        public CatalogNode? NewPathParent;
        public List<string>? Barcodes;
        public Dictionary<Guid, string?> Characteristics = [];
        public decimal? SalePrice;
        public decimal? Quantity;
        public decimal? PurchasePrice;
        public Dictionary<string, string?> Text = [];
        public Dictionary<string, int?> Ints = [];
        public bool HasVat;
        public decimal? Vat;
        public bool? Marked;
    }

    private sealed class RowException(string message) : Exception(message);

    public async Task<ImportResultDto> ImportAsync(Stream file, long length, bool dryRun, bool createMissing, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAnyAsync(ct, StaffPermissions.ProductsCreate, StaffPermissions.ProductsEdit);
        if (length == 0) throw new ValidationException(Messages.ImportFileRequired(Lang), "file");
        if (length > MaxFileBytes) throw new ValidationException(Messages.UploadTooLarge(Lang, (int)(MaxFileBytes / 1024 / 1024)), "file");

        SpreadsheetData data;
        try
        {
            data = spreadsheet.Read(file);
        }
        catch (InvalidDataException)
        {
            throw new ValidationException(Messages.ImportFileInvalid(Lang), "file");
        }

        // Тақырыптар екі тілде де танылады: қазақша экспортты орысша интерфейсте де жүктеуге болады.
        var index = new Dictionary<string, int>();
        var defs = await catalog.ListCharacteristicsAsync(orgId, ct);
        var defIndex = new Dictionary<Guid, int>();
        for (var i = 0; i < data.Headers.Count; i++)
        {
            var h = data.Headers[i].Trim();
            var col = Columns.FirstOrDefault(c => Same(c.Ru, h) || Same(c.Kk, h));
            if (col is not null) { index.TryAdd(col.Key, i); continue; }
            var prefix = CharacteristicPrefixes.FirstOrDefault(p => h.StartsWith(p, StringComparison.OrdinalIgnoreCase));
            if (prefix is null) continue;
            var def = defs.FirstOrDefault(d => Same(d.Name, h[prefix.Length..].Trim()));
            if (def is not null) defIndex.TryAdd(def.Id, i);
        }
        foreach (var required in new[] { "name", "unit" })
            if (!index.ContainsKey(required))
                throw new ValidationException(Messages.ImportMissingColumn(Lang, Title(Columns.First(c => c.Key == required))), "file");
        if (data.Rows.Count > MaxRows) throw new ValidationException(Messages.ImportTooManyRows(Lang, MaxRows), "file");

        // Бәрі бір рет жүктеледі: мыңдаған жолды әр жолға сұраныспен тексеру Neon-да минуттарға созылар еді.
        var all = await products.ListAllForUpdateAsync(orgId, ct);
        var byId = all.ToDictionary(p => p.Id);
        var barcodeOwner = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var p in all) foreach (var b in p.Barcodes) barcodeOwner[b.Barcode] = p.Id;
        var nodes = (await catalog.ListNodesAsync(orgId, ct)).ToList();
        var brands = (await catalog.ListBrandsAsync(orgId, ct)).ToList();
        var units = await catalog.ListUnitsAsync(orgId, ct);
        var defsById = defs.ToDictionary(d => d.Id);

        var ctx = new ImportContext(m, createMissing, index, defIndex, defsById, byId, barcodeOwner, nodes, brands, units);
        var plans = new List<RowPlan>();
        var errors = new List<ImportErrorDto>();
        var total = 0;
        for (var i = 0; i < data.Rows.Count; i++)
        {
            var row = data.Rows[i];
            if (row.All(string.IsNullOrWhiteSpace)) continue;
            total++;
            try
            {
                plans.Add(Plan(ctx, row, i + 2));
            }
            catch (RowException ex)
            {
                errors.Add(new ImportErrorDto(i + 2, ex.Message));
            }
        }

        // Тексеруде де қолданамыз (жаңа санат пен бренд санын дәл көрсету үшін), тек сақтамаймыз —
        // сұраныс аяқталғанда өзгерістер контекстімен бірге жойылады.
        var result = new ImportStats();
        await ApplyAsync(ctx, plans, orgId, result, ct);

        if (!dryRun && plans.Count > 0)
        {
            access.Audit(orgId, m.StoreId, "catalog.product.import", "product", Guid.Empty, null,
                new { total, result.Created, result.Updated, Skipped = errors.Count, result.CreatedNodes, result.CreatedBrands });
            await unitOfWork.SaveChangesAsync(ct);
        }

        return new ImportResultDto(!dryRun, total, result.Created, result.Updated, errors.Count, result.CreatedNodes, result.CreatedBrands,
            errors.Take(MaxReportedErrors).ToList());
    }

    private sealed class ImportContext(
        StoreMembership membership, bool createMissing, Dictionary<string, int> index, Dictionary<Guid, int> defIndex,
        Dictionary<Guid, CharacteristicDefinition> defs, Dictionary<Guid, Product> byId, Dictionary<string, Guid> barcodeOwner,
        List<CatalogNode> nodes, List<Brand> brands, IReadOnlyList<MeasureUnit> units)
    {
        public StoreMembership M = membership;
        public bool CreateMissing = createMissing;
        public Dictionary<string, int> Index = index;
        public Dictionary<Guid, int> DefIndex = defIndex;
        public Dictionary<Guid, CharacteristicDefinition> Defs = defs;
        public Dictionary<Guid, Product> ById = byId;
        public Dictionary<string, Guid> BarcodeOwner = barcodeOwner;
        public List<CatalogNode> Nodes = nodes;
        public List<Brand> Brands = brands;
        public IReadOnlyList<MeasureUnit> Units = units;
        /// <summary>Файл ішінде бір тауар мен бір штрихкод екі рет кездеспеуі үшін: мән — жол нөмірі.</summary>
        public Dictionary<Guid, int> SeenProducts = [];
        public Dictionary<string, int> SeenBarcodes = new(StringComparer.Ordinal);
        /// <summary>Жаңа тауарлардың уақытша кілті (штрихкод иесін тексеру үшін).</summary>
        public Dictionary<int, Guid> NewIds = [];
    }

    private sealed class ImportStats
    {
        public int Created, Updated, CreatedNodes, CreatedBrands;
    }

    private RowPlan Plan(ImportContext ctx, IReadOnlyList<string> row, int rowNo)
    {
        string? Cell(string key) => ctx.Index.TryGetValue(key, out var i) && i < row.Count ? row[i].Trim() : null;
        bool Has(string key) => ctx.Index.ContainsKey(key);
        string ColTitle(string key) => Title(Columns.First(c => c.Key == key));

        var plan = new RowPlan { Row = rowNo };

        // Тауарды табу: ID → кез келген штрихкоды → артикул → жаңа.
        if (Cell("id") is { Length: > 0 } rawId)
        {
            if (!Guid.TryParse(rawId, out var id) || !ctx.ById.TryGetValue(id, out var byId))
                throw new RowException(Messages.ImportIdNotFound(Lang, rawId));
            plan.Existing = byId;
        }

        var primary = Cell("barcode");
        var extras = Cell("extraBarcodes") is { Length: > 0 } e ? ListSeparator().Split(e).Where(x => x.Length > 0).ToList() : [];
        if (plan.Existing is null)
            foreach (var code in extras.Prepend(primary ?? string.Empty))
                if (code.Length > 0 && ctx.BarcodeOwner.TryGetValue(code, out var owner) && ctx.ById.TryGetValue(owner, out var found))
                {
                    plan.Existing = found;
                    break;
                }

        // Штрихкоды жоқ тауар қайта импортта екі еселенбесін: артикул бизнесте біреу ғана болса — сол тауар.
        if (plan.Existing is null && Cell("article") is { Length: > 0 } art)
        {
            var sameArticle = ctx.ById.Values.Where(p => p.Article is not null && Same(p.Article, art)).Take(2).ToList();
            if (sameArticle.Count == 1) plan.Existing = sameArticle[0];
        }

        if (plan.Existing is { } ex)
        {
            if (!ctx.M.Has(StaffPermissions.ProductsEdit)) throw new RowException(Messages.ImportNoEditPermission(Lang));
            if (ctx.SeenProducts.TryGetValue(ex.Id, out var other)) throw new RowException(Messages.ImportDuplicateProduct(Lang, other));
        }
        else if (!ctx.M.Has(StaffPermissions.ProductsCreate)) throw new RowException(Messages.ImportNoCreatePermission(Lang));

        // Атауы мен бірлігі — міндетті.
        var name = Cell("name") ?? string.Empty;
        if (name.Length == 0) throw new RowException(Messages.CatalogNameRequired(Lang));
        plan.Name = name.Length > 200 ? name[..200] : name;

        var unitRaw = Cell("unit") ?? string.Empty;
        if (unitRaw.Length == 0) throw new RowException(Messages.ProductUnitRequired(Lang));
        var unit = ctx.Units.FirstOrDefault(u => Same(u.ShortName, unitRaw) || Same(u.Name, unitRaw))
            ?? throw new RowException(Messages.ImportUnitNotFound(Lang, unitRaw));
        if (unit.Status == CatalogStatus.Archived && unit.Id != plan.Existing?.UnitId)
            throw new RowException(Messages.ProductUnitArchived(Lang));
        plan.UnitId = unit.Id;

        // Бренд: табылмаса — рұқсат пен баптау болса жасалады.
        if (Has("brand"))
        {
            plan.HasBrand = true;
            if (Cell("brand") is { Length: > 0 } brandName)
            {
                var brand = ctx.Brands.FirstOrDefault(b => Same(b.Name, brandName));
                if (brand is null)
                {
                    if (!ctx.CreateMissing || !ctx.M.Has(StaffPermissions.CatalogDictionaries))
                        throw new RowException(Messages.ImportBrandNotFound(Lang, brandName));
                    plan.NewBrandName = brandName.Length > 100 ? brandName[..100] : brandName;
                }
                else if (brand.Status == CatalogStatus.Archived && brand.Id != plan.Existing?.BrandId)
                    throw new RowException(Messages.ProductBrandArchived(Lang));
                else plan.BrandId = brand.Id;
            }
        }

        // Санат: «Автозапчасти / Тормозная система / Тормозные колодки». Жетпейтін деңгейлер жасалуы мүмкін.
        if (Has("category"))
        {
            plan.HasCategory = true;
            if (Cell("category") is { Length: > 0 } rawPath)
            {
                var parts = PathSeparator().Split(rawPath).Where(x => x.Length > 0).ToList();
                if (parts.Count > CatalogNode.MaxDepth) throw new RowException(Messages.CatalogTooDeep(Lang, CatalogNode.MaxDepth));
                CatalogNode? parent = null;
                var depth = 0;
                for (; depth < parts.Count; depth++)
                {
                    var next = ctx.Nodes.FirstOrDefault(n => n.ParentId == parent?.Id && Same(n.Name, parts[depth]));
                    if (next is null) break;
                    parent = next;
                }
                if (depth < parts.Count)
                {
                    if (!ctx.CreateMissing || !ctx.M.Has(StaffPermissions.CatalogStructure))
                        throw new RowException(Messages.ImportCategoryNotFound(Lang, rawPath));
                    if (parent is { Status: CatalogStatus.Archived }) throw new RowException(Messages.ProductNodeArchived(Lang));
                    plan.NewPathParent = parent;
                    plan.NewPath = parts.Skip(depth).Select(x => x.Length > 150 ? x[..150] : x).ToList();
                }
                else if (parent!.Status == CatalogStatus.Archived && parent.Id != plan.Existing?.CatalogNodeId)
                    throw new RowException(Messages.ProductNodeArchived(Lang));
                else plan.NodeId = parent.Id;
            }
        }

        // Штрихкодтар: бағаны жоқ болса — тауардағысы сақталады.
        if (Has("barcode") || Has("extraBarcodes"))
        {
            var existingPrimary = plan.Existing?.Barcodes.FirstOrDefault(b => b.IsPrimary)?.Barcode;
            var existingExtras = plan.Existing?.Barcodes.Where(b => !b.IsPrimary).Select(b => b.Barcode).ToList() ?? [];
            var newPrimary = Has("barcode") ? primary : existingPrimary;
            var newExtras = Has("extraBarcodes") ? extras : existingExtras;
            var list = new List<string>();
            foreach (var code in newExtras.Prepend(newPrimary ?? string.Empty).Where(c => c.Length > 0))
            {
                if (!BarcodePattern().IsMatch(code)) throw new RowException(Messages.BarcodeInvalid(Lang, code));
                if (list.Contains(code)) throw new RowException(Messages.BarcodeRepeated(Lang, code));
                if (ctx.SeenBarcodes.TryGetValue(code, out var otherRow)) throw new RowException(Messages.ImportBarcodeInRow(Lang, code, otherRow));
                if (ctx.BarcodeOwner.TryGetValue(code, out var owner) && owner != plan.Existing?.Id)
                    throw new RowException(Messages.BarcodeTaken(Lang, code, ctx.ById.GetValueOrDefault(owner)?.Name ?? "?"));
                list.Add(code);
            }
            plan.Barcodes = list;
        }

        // Сипаттамалар.
        var yesNo = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["да"] = "true", ["иә"] = "true", ["yes"] = "true", ["1"] = "true", ["true"] = "true",
            ["нет"] = "false", ["жоқ"] = "false", ["no"] = "false", ["0"] = "false", ["false"] = "false",
        };
        foreach (var (defId, col) in ctx.DefIndex)
        {
            var def = ctx.Defs[defId];
            var raw = col < row.Count ? row[col].Trim() : string.Empty;
            if (raw.Length == 0) { plan.Characteristics[defId] = null; continue; }
            if (def.Type == CharacteristicType.Boolean) raw = yesNo.GetValueOrDefault(raw, raw);
            // «10-20», «10 – 20» → «10..20» (минус таңбасына тимейміз: алдында сан болуы керек).
            if (def.Type == CharacteristicType.Range) raw = RangeSeparator().Replace(raw, "..");
            plan.Characteristics[defId] = ProductService.NormalizeValue(def, raw)
                ?? throw new RowException(Messages.CharacteristicValueInvalid(Lang, def.Name));
        }
        foreach (var def in ctx.Defs.Values.Where(d => d.Status == CatalogStatus.Active && d.IsRequired))
        {
            var has = plan.Characteristics.TryGetValue(def.Id, out var v)
                ? v is not null
                : plan.Existing?.Characteristics.Any(c => c.DefinitionId == def.Id) == true;
            if (!has) throw new RowException(Messages.CharacteristicRequired(Lang, def.Name));
        }

        // Сандар.
        decimal? Number(string key, int decimals)
        {
            var raw = Cell(key);
            if (string.IsNullOrEmpty(raw)) return null;
            if (!decimal.TryParse(raw.Replace(" ", "").Replace(',', '.'), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture, out var n))
                throw new RowException(Messages.ImportNumberInvalid(Lang, ColTitle(key)));
            if (n < 0) throw new RowException(Messages.ImportNumberInvalid(Lang, ColTitle(key)));
            return Math.Round(n, decimals);
        }
        plan.SalePrice = Number("salePrice", 2);
        if (plan.Existing is null)
        {
            plan.Quantity = Number("quantity", 3);
            plan.PurchasePrice = Number("purchasePrice", 2);
        }

        int? Int(string key, int max)
        {
            var n = Number(key, 0);
            if (n is null) return null;
            if (n > max) throw new RowException(Messages.ImportNumberInvalid(Lang, ColTitle(key)));
            return (int)n;
        }
        foreach (var (key, max) in new[] { ("warranty", 600), ("shelfLife", 36500) })
            if (Has(key)) plan.Ints[key] = Int(key, max);
        if (Has("vat"))
        {
            plan.HasVat = true;
            plan.Vat = Number("vat", 2);
            if (plan.Vat > 100) throw new RowException(Messages.ImportNumberInvalid(Lang, ColTitle("vat")));
        }
        if (Has("marked"))
            plan.Marked = Cell("marked") is { Length: > 0 } mk
                ? yesNo.TryGetValue(mk, out var b) ? b == "true" : throw new RowException(Messages.ImportNumberInvalid(Lang, ColTitle("marked")))
                : false;

        foreach (var (key, max) in new[] { ("article", 64), ("description", 1000), ("supplier", 200), ("manufacturer", 200), ("country", 64), ("notes", 1000) })
            if (Has(key)) plan.Text[key] = CatalogAccess.Optional(Cell(key), max);

        // Жол дұрыс — файл ішіндегі бірегейлікке тіркейміз.
        var key2 = plan.Existing?.Id ?? Guid.NewGuid();
        ctx.NewIds[rowNo] = key2;
        ctx.SeenProducts[key2] = rowNo;
        foreach (var code in plan.Barcodes ?? []) ctx.SeenBarcodes[code] = rowNo;
        return plan;
    }

    private async Task ApplyAsync(ImportContext ctx, List<RowPlan> plans, Guid orgId, ImportStats stats, CancellationToken ct)
    {
        var storeId = ctx.M.StoreId;
        var prices = (await inventory.ListPricesForUpdateAsync(storeId, ct)).ToDictionary(p => p.ProductId);
        Warehouse? warehouse = null;
        var now = DateTime.UtcNow;
        var nextBrandOrder = ctx.Brands.Count == 0 ? 1 : ctx.Brands.Max(b => b.SortOrder) + 1;

        foreach (var plan in plans)
        {
            var isNew = plan.Existing is null;
            var p = plan.Existing ?? new Product { Id = ctx.NewIds[plan.Row], OrganizationId = orgId, CreatedAt = now };

            if (plan.NewBrandName is { } brandName)
            {
                // Алдыңғы жол осы брендті жасап қойған болуы мүмкін.
                var brand = ctx.Brands.FirstOrDefault(b => Same(b.Name, brandName));
                if (brand is null)
                {
                    brand = new Brand { Id = Guid.NewGuid(), OrganizationId = orgId, Name = brandName, SortOrder = nextBrandOrder++ };
                    catalog.AddBrand(brand);
                    ctx.Brands.Add(brand);
                    stats.CreatedBrands++;
                }
                plan.BrandId = brand.Id;
            }

            if (plan.NewPath is { } path)
            {
                var parent = plan.NewPathParent;
                foreach (var part in path)
                {
                    var node = ctx.Nodes.FirstOrDefault(n => n.ParentId == parent?.Id && Same(n.Name, part));
                    if (node is null)
                    {
                        var id = Guid.NewGuid();
                        node = new CatalogNode
                        {
                            Id = id, OrganizationId = orgId, ParentId = parent?.Id, Name = part,
                            Icon = parent is null ? "package" : null,
                            SortOrder = ctx.Nodes.Where(n => n.ParentId == parent?.Id).Select(n => n.SortOrder).DefaultIfEmpty(0).Max() + 1,
                            Path = parent is null ? id.ToString() : $"{parent.Path}/{id}",
                            Depth = parent is null ? 0 : parent.Depth + 1,
                        };
                        catalog.AddNode(node);
                        ctx.Nodes.Add(node);
                        stats.CreatedNodes++;
                    }
                    parent = node;
                }
                plan.NodeId = parent!.Id;
            }

            p.Name = plan.Name;
            p.UnitId = plan.UnitId!.Value;
            if (plan.HasBrand) p.BrandId = plan.BrandId;
            if (plan.HasCategory) p.CatalogNodeId = plan.NodeId;
            if (plan.Text.TryGetValue("article", out var article)) p.Article = article;
            if (plan.Text.TryGetValue("description", out var description)) p.Description = description;
            if (plan.Text.TryGetValue("supplier", out var supplier)) p.Supplier = supplier;
            if (plan.Text.TryGetValue("manufacturer", out var manufacturer)) p.Manufacturer = manufacturer;
            if (plan.Text.TryGetValue("country", out var country)) p.Country = country;
            if (plan.Text.TryGetValue("notes", out var notes)) p.Notes = notes;
            if (plan.Ints.TryGetValue("warranty", out var warranty)) p.WarrantyMonths = warranty;
            if (plan.Ints.TryGetValue("shelfLife", out var shelf)) p.ShelfLifeDays = shelf;
            if (plan.HasVat) p.VatRate = plan.Vat;
            if (plan.Marked is { } marked) p.IsMarked = marked;
            p.UpdatedAt = now;

            if (isNew)
            {
                products.Add(p);
                stats.Created++;
            }
            else stats.Updated++;

            if (plan.Barcodes is { } codes)
            {
                foreach (var old in p.Barcodes.ToList())
                {
                    var idx = codes.IndexOf(old.Barcode);
                    if (idx < 0) products.RemoveBarcode(old);
                    else old.IsPrimary = idx == 0;
                }
                for (var i = 0; i < codes.Count; i++)
                    if (p.Barcodes.All(b => b.Barcode != codes[i]))
                        products.AddBarcode(new ProductBarcode
                        {
                            Id = Guid.NewGuid(), ProductId = p.Id, OrganizationId = orgId, Barcode = codes[i], IsPrimary = i == 0,
                        });
                foreach (var code in codes) ctx.BarcodeOwner[code] = p.Id;
            }

            foreach (var (defId, value) in plan.Characteristics)
            {
                var existing = p.Characteristics.FirstOrDefault(c => c.DefinitionId == defId);
                if (value is null) { if (existing is not null) products.RemoveCharacteristicValue(existing); }
                else if (existing is null) products.AddCharacteristicValue(new ProductCharacteristicValue { ProductId = p.Id, DefinitionId = defId, Value = value });
                else existing.Value = value;
            }

            if (plan.SalePrice is not null || plan.PurchasePrice is not null)
            {
                if (!prices.TryGetValue(p.Id, out var price))
                {
                    price = new ProductPrice { ProductId = p.Id, StoreId = storeId };
                    inventory.AddPrice(price);
                    prices[p.Id] = price;
                }
                if (plan.SalePrice is { } sale) price.SalePrice = sale;
                if (plan.PurchasePrice is { } cost) price.PurchasePrice = cost;
                price.UpdatedAt = now;
            }

            if (plan.Quantity is > 0)
            {
                warehouse ??= await warehouses.EnsureDefaultAsync(orgId, storeId, ct);
                inventory.AddMovement(new StockMovement
                {
                    Id = Guid.NewGuid(), OrganizationId = orgId, WarehouseId = warehouse.Id, ProductId = p.Id,
                    Type = StockMovementType.Opening, Quantity = plan.Quantity.Value, UnitCost = plan.PurchasePrice,
                    StaffUserId = access.StaffUserId, CreatedAt = now,
                });
                inventory.AddBalance(new StockBalance { WarehouseId = warehouse.Id, ProductId = p.Id, Quantity = plan.Quantity.Value, UpdatedAt = now });
            }
        }
    }

    private static bool Same(string a, string b) => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
}
