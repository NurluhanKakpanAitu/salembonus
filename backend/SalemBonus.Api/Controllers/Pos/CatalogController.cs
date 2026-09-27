using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalemBonus.Application.Pos.Catalog;

namespace SalemBonus.Api.Controllers.Pos;

/// <summary>
/// Каталог: классификация (санат → топ → топша…), брендтер, өлшем бірліктері, сипаттамалар
/// (ТЗ «Товар»). Каталог бүкіл бизнеске ортақ; рұқсаттар әр әрекетте серверде тексеріледі.
/// </summary>
[ApiController]
[Route("api/pos/v1/catalog")]
[Authorize(Policy = StaffAuth.Policy)]
public class CatalogController(ICatalogNodeService nodes, ICatalogDictionaryService dictionaries) : ControllerBase
{
    // ---------- Классификация ----------

    [HttpGet("nodes")]
    public async Task<ActionResult<IReadOnlyList<CatalogNodeDto>>> Nodes(CancellationToken ct) =>
        Ok(await nodes.ListAsync(ct));

    [HttpPost("nodes")]
    public async Task<ActionResult<CatalogNodeDto>> CreateNode([FromBody] SaveCatalogNodeRequest request, CancellationToken ct) =>
        Ok(await nodes.CreateAsync(request, ct));

    [HttpPut("nodes/{id:guid}")]
    public async Task<ActionResult<CatalogNodeDto>> UpdateNode(Guid id, [FromBody] SaveCatalogNodeRequest request, CancellationToken ct) =>
        Ok(await nodes.UpdateAsync(id, request, ct));

    [HttpPost("nodes/{id:guid}/reorder")]
    public async Task<IActionResult> ReorderNode(Guid id, [FromBody] ReorderRequest request, CancellationToken ct)
    {
        await nodes.ReorderAsync(id, request.Direction, ct);
        return NoContent();
    }

    [HttpPost("nodes/{id:guid}/archive")]
    public async Task<IActionResult> ArchiveNode(Guid id, CancellationToken ct)
    {
        await nodes.ArchiveAsync(id, ct);
        return NoContent();
    }

    [HttpPost("nodes/{id:guid}/restore")]
    public async Task<IActionResult> RestoreNode(Guid id, CancellationToken ct)
    {
        await nodes.RestoreAsync(id, ct);
        return NoContent();
    }

    /// <summary>Тек бос түйін өшіріледі; әйтпесе — тасымалдау не архив.</summary>
    [HttpDelete("nodes/{id:guid}")]
    public async Task<IActionResult> DeleteNode(Guid id, CancellationToken ct)
    {
        await nodes.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("nodes/{id:guid}/move-content")]
    public async Task<IActionResult> MoveContent(Guid id, [FromBody] MoveContentRequest request, CancellationToken ct)
    {
        await nodes.MoveContentAsync(id, request.TargetId, ct);
        return NoContent();
    }

    // ---------- Брендтер ----------

    [HttpGet("brands")]
    public async Task<ActionResult<IReadOnlyList<BrandDto>>> Brands(CancellationToken ct) =>
        Ok(await dictionaries.ListBrandsAsync(ct));

    [HttpPost("brands")]
    public async Task<ActionResult<BrandDto>> CreateBrand([FromBody] SaveBrandRequest request, CancellationToken ct) =>
        Ok(await dictionaries.CreateBrandAsync(request, ct));

    [HttpPut("brands/{id:guid}")]
    public async Task<ActionResult<BrandDto>> UpdateBrand(Guid id, [FromBody] SaveBrandRequest request, CancellationToken ct) =>
        Ok(await dictionaries.UpdateBrandAsync(id, request, ct));

    [HttpPost("brands/{id:guid}/reorder")]
    public async Task<IActionResult> ReorderBrand(Guid id, [FromBody] ReorderRequest request, CancellationToken ct)
    {
        await dictionaries.ReorderBrandAsync(id, request.Direction, ct);
        return NoContent();
    }

    // ---------- Өлшем бірліктері ----------

    [HttpGet("units")]
    public async Task<ActionResult<IReadOnlyList<UnitDto>>> Units(CancellationToken ct) =>
        Ok(await dictionaries.ListUnitsAsync(ct));

    [HttpPost("units")]
    public async Task<ActionResult<UnitDto>> CreateUnit([FromBody] SaveUnitRequest request, CancellationToken ct) =>
        Ok(await dictionaries.CreateUnitAsync(request, ct));

    [HttpPut("units/{id:guid}")]
    public async Task<ActionResult<UnitDto>> UpdateUnit(Guid id, [FromBody] SaveUnitRequest request, CancellationToken ct) =>
        Ok(await dictionaries.UpdateUnitAsync(id, request, ct));

    // ---------- Сипаттамалар ----------

    [HttpGet("characteristics")]
    public async Task<ActionResult<IReadOnlyList<CharacteristicDto>>> Characteristics(CancellationToken ct) =>
        Ok(await dictionaries.ListCharacteristicsAsync(ct));

    [HttpPost("characteristics")]
    public async Task<ActionResult<CharacteristicDto>> CreateCharacteristic([FromBody] SaveCharacteristicRequest request, CancellationToken ct) =>
        Ok(await dictionaries.CreateCharacteristicAsync(request, ct));

    [HttpPut("characteristics/{id:guid}")]
    public async Task<ActionResult<CharacteristicDto>> UpdateCharacteristic(Guid id, [FromBody] SaveCharacteristicRequest request, CancellationToken ct) =>
        Ok(await dictionaries.UpdateCharacteristicAsync(id, request, ct));

    [HttpPost("characteristics/{id:guid}/reorder")]
    public async Task<IActionResult> ReorderCharacteristic(Guid id, [FromBody] ReorderRequest request, CancellationToken ct)
    {
        await dictionaries.ReorderCharacteristicAsync(id, request.Direction, ct);
        return NoContent();
    }
}
