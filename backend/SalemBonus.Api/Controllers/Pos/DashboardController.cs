using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalemBonus.Application.Pos.Dashboard;

namespace SalemBonus.Api.Controllers.Pos;

/// <summary>
/// «Статистика» (ТЗ «Статистика» §17): бір агрегат endpoint — бір snapshot. Дүкен X-Store-Id арқылы,
/// сервер оған рұқсатты тексереді; from/to — дүкеннің жергілікті күндері (әдепкіде бүгін).
/// </summary>
[ApiController]
[Route("api/pos/v1/dashboard")]
[Authorize(Policy = StaffAuth.Policy)]
public class DashboardController(DashboardService dashboard) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DashboardDto>> Get([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) =>
        Ok(await dashboard.GetAsync(from, to, ct));
}
