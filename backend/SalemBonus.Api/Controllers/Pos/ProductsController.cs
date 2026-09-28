using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalemBonus.Application.Pos.Catalog;

namespace SalemBonus.Api.Controllers.Pos;

/// <summary>
/// Тауарлар (ТЗ «Товар» §5–6): тізім, карточка, штрихкод, классификацияны өзгерту, архив.
/// Каталог бүкіл бизнеске ортақ; баға мен қалдық — X-Store-Id дүкенінікі.
/// </summary>
[ApiController]
[Route("api/pos/v1/products")]
[Authorize(Policy = StaffAuth.Policy)]
public class ProductsController(IProductService products, ProductExchangeService exchange) : ControllerBase
{
    private const string XlsxType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>Excel-ге экспорт (тізімнің сүзгілерімен). template=true — тек тақырыптар, импортқа үлгі.</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] string? search, [FromQuery] string? status, [FromQuery] Guid? nodeId, [FromQuery] Guid? brandId,
        [FromQuery] Guid? unitId, [FromQuery] bool template = false, CancellationToken ct = default)
    {
        var (content, fileName) = await exchange.ExportAsync(search, status, nodeId, brandId, unitId, template, ct);
        return File(content, XlsxType, fileName);
    }

    /// <summary>Excel-ден импорт. dryRun=true — тек тексеру (ештеңе сақталмайды).</summary>
    [HttpPost("import")]
    [RequestSizeLimit(ProductExchangeService.MaxFileBytes + 64 * 1024)]
    public async Task<ActionResult<ImportResultDto>> Import(IFormFile? file, [FromForm] bool dryRun = true,
        [FromForm] bool createMissing = true, CancellationToken ct = default)
    {
        await using var stream = file?.OpenReadStream() ?? Stream.Null;
        return Ok(await exchange.ImportAsync(stream, file?.Length ?? 0, dryRun, createMissing, ct));
    }

    [HttpGet]
    public async Task<ActionResult<ProductPageDto>> List(
        [FromQuery] string? search, [FromQuery] string? status, [FromQuery] Guid? nodeId, [FromQuery] Guid? brandId,
        [FromQuery] Guid? unitId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(await products.ListAsync(search, status, nodeId, brandId, unitId, page, pageSize, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductDto>> Get(Guid id, CancellationToken ct) => Ok(await products.GetAsync(id, ct));

    [HttpGet("by-barcode/{barcode}")]
    public async Task<ActionResult<ProductListItemDto>> ByBarcode(string barcode, CancellationToken ct) =>
        Ok(await products.FindByBarcodeAsync(barcode, ct));

    [HttpGet("barcodes/check")]
    public async Task<ActionResult<BarcodeCheckDto>> CheckBarcode([FromQuery] string barcode, [FromQuery] Guid? excludeProductId, CancellationToken ct) =>
        Ok(await products.CheckBarcodeAsync(barcode, excludeProductId, ct));

    [HttpPost("barcodes/generate")]
    public async Task<ActionResult<GeneratedBarcodeDto>> GenerateBarcode(CancellationToken ct) =>
        Ok(await products.GenerateBarcodeAsync(ct));

    [HttpPost]
    public async Task<ActionResult<ProductDto>> Create([FromBody] SaveProductRequest request, CancellationToken ct) =>
        Ok(await products.CreateAsync(request, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProductDto>> Update(Guid id, [FromBody] SaveProductRequest request, CancellationToken ct) =>
        Ok(await products.UpdateAsync(id, request, ct));

    [HttpPut("{id:guid}/classification")]
    public async Task<ActionResult<ProductDto>> ChangeClassification(Guid id, [FromBody] ChangeClassificationRequest request, CancellationToken ct) =>
        Ok(await products.ChangeClassificationAsync(id, request.NodeId, ct));

    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        await products.ArchiveAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/restore")]
    public async Task<IActionResult> Restore(Guid id, CancellationToken ct)
    {
        await products.RestoreAsync(id, ct);
        return NoContent();
    }
}
