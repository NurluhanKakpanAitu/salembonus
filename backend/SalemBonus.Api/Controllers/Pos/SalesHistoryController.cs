using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalemBonus.Application.Pos.Sales;

namespace SalemBonus.Api.Controllers.Pos;

/// <summary>
/// «Продажи» бөлімі (ТЗ «Продажи» §14): тарих, KPI, сүзгі анықтамалары, чек тарихы, экспорт.
/// Жаңа сатылым мен қайтару — <see cref="SalesController"/>-де (касса мен кабинетке ортақ).
/// </summary>
[ApiController]
[Route("api/pos/v1/sales")]
[Authorize(Policy = StaffAuth.Policy)]
public class SalesHistoryController(SalesHistoryService history) : ControllerBase
{
    private const string XlsxType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    [HttpGet("history")]
    public async Task<ActionResult<SalesHistoryPageDto>> List([FromQuery] SalesHistoryQuery q, CancellationToken ct) =>
        Ok(await history.ListAsync(q, ct));

    [HttpGet("summary")]
    public async Task<ActionResult<SalesSummaryDto>> Summary([FromQuery] SalesHistoryQuery q, CancellationToken ct) =>
        Ok(await history.SummaryAsync(q, ct));

    [HttpGet("filters")]
    public async Task<ActionResult<SalesFiltersDto>> Filters(CancellationToken ct) => Ok(await history.FiltersAsync(ct));

    /// <summary>Чек карточкасы «Продажи» бөлімі үшін (sales.view; кассадағы «Чеки» — sales.create арқылы).</summary>
    [HttpGet("{id:guid}/details")]
    public async Task<ActionResult<ReceiptDto>> Details(Guid id, CancellationToken ct) => Ok(await history.GetAsync(id, ct));

    [HttpGet("{id:guid}/history")]
    public async Task<ActionResult<IReadOnlyList<SaleEventDto>>> History(Guid id, CancellationToken ct) =>
        Ok(await history.HistoryAsync(id, ct));

    /// <summary>Excel: ағымдағы таңдау сол сүзгілермен (ТЗ §12).</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] SalesHistoryQuery q, CancellationToken ct)
    {
        var (content, fileName) = await history.ExportAsync(q, ct);
        return File(content, XlsxType, fileName);
    }
}
