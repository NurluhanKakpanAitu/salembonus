using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SalemBonus.Application.Platform;

namespace SalemBonus.Api.Controllers;

/// <summary>
/// Лендинг (salemtech.kz) үшін ашық API: белсенді тарифтер және өтінім формасы. Кіру талап етілмейді; өтінімге IP бойынша жиілік шегі.
/// </summary>
[ApiController]
[Route("api/public/v1")]
[AllowAnonymous]
public class PublicController(PlatformLeadsService leads) : ControllerBase
{
    public const string FormRateLimit = "public-form";

    [HttpGet("plans")]
    [ResponseCache(Duration = 300)]
    public async Task<ActionResult<IReadOnlyList<PublicPlanDto>>> Plans(CancellationToken ct) => Ok(await leads.PublicPlansAsync(ct));

    [HttpPost("leads")]
    [EnableRateLimiting(FormRateLimit)]
    public async Task<IActionResult> Lead([FromBody] LeadRequest r, CancellationToken ct)
    {
        await leads.SubmitAsync(r, ct);
        return Ok(new { ok = true });
    }
}
