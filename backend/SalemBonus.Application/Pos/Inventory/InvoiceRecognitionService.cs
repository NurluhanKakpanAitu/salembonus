using Microsoft.Extensions.Logging;
using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Application.Pos.Catalog;
using SalemBonus.Domain.Core;
using SalemBonus.Domain.Pos.Catalog;

namespace SalemBonus.Application.Pos.Inventory;

// ---------- Накладнаяны тану (ТЗ «Склад» §8) ----------

/// <summary>Сәйкестендірілген каталог тауары — приёмка жолын толтыруға жететін дерек.</summary>
public record RecognizedProductDto(
    Guid Id, string Name, string? Article, string? Barcode, string? ImageUrl, string? Unit,
    decimal StockNow, decimal? PurchasePrice, decimal? CatalogRetailPrice);

/// <summary>Бір жол: накладнаядағы түпнұсқа мән + сәйкестендіру нәтижесі. Status: Matched / Review / NotFound.</summary>
public record RecognizedLineDto(
    int Index, string? Name, string? Article, string? Barcode, decimal? Quantity, decimal? Price, decimal? LineTotal,
    string Status, string MatchedBy, decimal Confidence, RecognizedProductDto? Product);

public record InvoiceRecognitionDto(
    string? SupplierName,
    /// <summary>Аты бойынша табылған жеткізуші (бар болса) — формада бірден таңдалады.</summary>
    Guid? SupplierId,
    string? InvoiceNumber, DateOnly? Date, IReadOnlyList<RecognizedLineDto> Lines, int Matched, int Review, int NotFound);

/// <summary>
/// Накладнаяның фотосы / PDF-і → приёмка черновигінің ұсынысы (ТЗ «Склад» §8). ИИ ешқашан проведение жасамайды:
/// нәтиже — тек ұсыныс, оны пайдаланушы тексеріп, формаға өзі қолданады. Танылған мәтін сенімсіз дерек:
/// тауар каталогтан қайта ізделеді, сандар тексеріледі, бұл жерде ештеңе сақталмайды.
/// </summary>
public class InvoiceRecognitionService(
    IInvoiceRecognizer recognizer,
    IProductRepository products,
    IInventoryRepository inventory,
    ICatalogRepository catalog,
    CatalogAccess access,
    ILogger<InvoiceRecognitionService> log)
{
    public const long MaxBytes = 10 * 1024 * 1024;
    public const int MaxLines = 200;

    public static readonly IReadOnlySet<string> AllowedTypes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/webp", "application/pdf" };

    private AppLanguage Lang => access.Lang;

    public async Task<InvoiceRecognitionDto> RecognizeAsync(Stream file, string? contentType, long length, Guid? warehouseId, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.InventoryReceive, ct);
        var type = contentType?.Split(';')[0].Trim().ToLowerInvariant() ?? string.Empty;
        if (!AllowedTypes.Contains(type)) throw new ValidationException(Messages.RecognitionFileTypeInvalid(Lang), "file");
        if (length <= 0) throw new ValidationException(Messages.ImportFileRequired(Lang), "file");
        if (length > MaxBytes) throw new ValidationException(Messages.UploadTooLarge(Lang, (int)(MaxBytes / 1024 / 1024)), "file");
        if (!recognizer.IsConfigured) throw new ValidationException(Messages.RecognitionNotConfigured(Lang));

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        if (bytes.Length > MaxBytes) throw new ValidationException(Messages.UploadTooLarge(Lang, (int)(MaxBytes / 1024 / 1024)), "file");
        if (!LooksLike(bytes, type)) throw new ValidationException(Messages.RecognitionFileTypeInvalid(Lang), "file");

        RecognizedInvoice recognized;
        try
        {
            recognized = await recognizer.RecognizeAsync(bytes, type, ct);
        }
        catch (InvoiceRecognitionException ex)
        {
            log.LogWarning(ex, "Накладнаяны тану сәтсіз (org {OrgId})", orgId);
            throw new ValidationException(Messages.RecognitionFailed(Lang));
        }

        var lines = recognized.Lines.Take(MaxLines).Select(Sanitize).Where(l => l.Name is not null || l.Barcode is not null || l.Article is not null).ToList();
        if (lines.Count == 0) throw new ValidationException(Messages.RecognitionNothingFound(Lang), "file");

        var all = await products.ListForExportAsync(orgId, new ProductFilter(null, CatalogStatus.Active, null, null, null, 0, 0), ct);
        var candidates = all.Select(p => new CatalogCandidate(p.Id, p.Name, p.Article, p.Barcodes.Select(b => b.Barcode).ToList())).ToList();
        var matches = InvoiceLineMatcher.Match(lines, candidates);

        var ids = matches.Where(x => x.ProductId is { }).Select(x => x.ProductId!.Value).Distinct().ToList();
        var full = (await inventory.ProductsAsync(orgId, ids, ct)).ToDictionary(p => p.Id);
        var units = (await catalog.ListUnitsAsync(orgId, ct)).ToDictionary(u => u.Id, u => u.ShortName);
        var prices = await inventory.PricesAsync(m.StoreId, ids, ct);
        var stock = warehouseId is { } wid && await inventory.GetWarehouseAsync(m.StoreId, wid, ct) is { } ? await inventory.BalancesAsync(wid, ids, ct) : [];

        var dtoLines = matches.Select((x, i) =>
        {
            var p = x.ProductId is { } pid ? full.GetValueOrDefault(pid) : null;
            var product = p is null ? null : new RecognizedProductDto(p.Id, p.Name, p.Article,
                p.Barcodes.FirstOrDefault(b => b.IsPrimary)?.Barcode ?? p.Barcodes.FirstOrDefault()?.Barcode,
                p.Images.FirstOrDefault(im => im.IsPrimary)?.Url ?? p.Images.OrderBy(im => im.SortOrder).FirstOrDefault()?.Url,
                units.GetValueOrDefault(p.UnitId), stock.GetValueOrDefault(p.Id), prices.GetValueOrDefault(p.Id)?.PurchasePrice,
                prices.GetValueOrDefault(p.Id)?.SalePrice);
            var status = product is null ? MatchStatus.NotFound : x.Status;
            return new RecognizedLineDto(i + 1, x.Line.Name, x.Line.Article, x.Line.Barcode, x.Line.Quantity, x.Line.Price, x.Line.LineTotal,
                status.ToString(), x.Kind.ToString(), x.Confidence, product);
        }).ToList();

        var supplierName = CatalogAccess.Optional(recognized.SupplierName, 200);
        var supplierId = supplierName is null ? null
            : (await inventory.ListSuppliersAsync(orgId, ct)).FirstOrDefault(s => s.Status == CatalogStatus.Active && SameSupplier(s.Name, supplierName))?.Id;

        log.LogInformation("Накладная танылды (org {OrgId}): {Lines} жол, {Matched} сәйкес, {Review} тексеруге, {NotFound} табылмады",
            orgId, dtoLines.Count, dtoLines.Count(l => l.Status == "Matched"), dtoLines.Count(l => l.Status == "Review"), dtoLines.Count(l => l.Status == "NotFound"));

        return new InvoiceRecognitionDto(supplierName, supplierId, CatalogAccess.Optional(recognized.InvoiceNumber, 64), ParseDate(recognized.Date), dtoLines,
            dtoLines.Count(l => l.Status == "Matched"), dtoLines.Count(l => l.Status == "Review"), dtoLines.Count(l => l.Status == "NotFound"));
    }

    /// <summary>Сандар мен мәтін шектеулі: теріс не орасан сандар, ұзын мәтін — ИИ қатесі, оны алып тастаймыз.</summary>
    private static RecognizedLine Sanitize(RecognizedLine l) => new(
        CatalogAccess.Optional(l.Name, 200), CatalogAccess.Optional(l.Article, 64), CatalogAccess.Optional(l.Barcode, 32),
        Clamp(l.Quantity, 3), Clamp(l.Price, 2), Clamp(l.LineTotal, 2));

    private static decimal? Clamp(decimal? v, int decimals) =>
        v is { } x && x > 0 && x <= 1_000_000_000 ? Math.Round(x, decimals) : null;

    private static bool SameSupplier(string a, string b) =>
        InvoiceLineMatcher.Normalize(a) == InvoiceLineMatcher.Normalize(b) || InvoiceLineMatcher.Similarity(a, b) >= 0.9m;

    private static DateOnly? ParseDate(string? raw) =>
        DateOnly.TryParseExact(raw?.Trim(), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d)
            && d.Year >= 2000 && d <= DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1) ? d : null;

    /// <summary>Файлдың басы жарияланған түріне сай ма (кеңейтімі өзгертілген басқа файл жіберілмесін).</summary>
    private static bool LooksLike(byte[] b, string type) => type switch
    {
        "image/jpeg" => b.Length > 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF,
        "image/png" => b.Length > 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47,
        "image/webp" => b.Length > 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P',
        "application/pdf" => b.Length > 4 && b[0] == '%' && b[1] == 'P' && b[2] == 'D' && b[3] == 'F',
        _ => false,
    };
}
