using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalemBonus.Application.Pos.Analytics;

namespace SalemBonus.Api.Controllers.Pos;

/// <summary>
/// «Статистика» (ТЗ «Статистика» v1.1 §26): шолу, «Динамика продаж», тоғыз детальды вкладка, экспорт.
/// Барлығы тек оқиды; дүкен — X-Store-Id не storeIds (сервер әр дүкенге құқықты тексереді).
/// </summary>
[ApiController]
[Route("api/pos/v1/analytics")]
[Authorize(Policy = StaffAuth.Policy)]
public class AnalyticsController(AnalyticsService analytics) : ControllerBase
{
    [HttpGet("overview")]
    public async Task<ActionResult<OverviewDto>> Overview([FromQuery] AnalyticsQuery q, CancellationToken ct) => Ok(await analytics.OverviewAsync(q, ct));

    [HttpGet("sales-trend")]
    public async Task<ActionResult<SalesTrendDto>> SalesTrend([FromQuery] AnalyticsQuery q, CancellationToken ct) => Ok(await analytics.SalesTrendAsync(q, ct));

    /// <summary>sales, products, inventory, suppliers, finance, bonus, customers, employees, registers.</summary>
    [HttpGet("tabs/{tab}")]
    public async Task<ActionResult<AnalyticsTabDto>> Tab(string tab, [FromQuery] AnalyticsQuery q, CancellationToken ct) =>
        Ok(await analytics.TabAsync(tab, q, false, ct));

    /// <summary>Excel (.xlsx) не CSV: белсенді вкладка ағымдағы сүзгілермен (§6).</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] string tab, [FromQuery] string? format, [FromQuery] AnalyticsQuery q, CancellationToken ct)
    {
        var (content, fileName, contentType) = await analytics.ExportAsync(tab, format, q, ct);
        return File(content, contentType, fileName);
    }
}
