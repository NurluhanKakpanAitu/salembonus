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
public class ProductsController(IProductService products) : ControllerBase
{
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
