using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SalemBonus.Api.Controllers.Staff;
using SalemBonus.Application.Pos.Registers;

namespace SalemBonus.Api.Controllers.Pos;

/// <summary>
/// Касса (құрылғы): тіркеу, кассирлер тізімі, PIN-мен кіру, бұғаттау (ТЗ «Касса» §15, §17, §18).
/// Құрылғы кілті тек httpOnly cookie-де жүреді.
/// </summary>
[ApiController]
[Route("api/pos/v1/registers")]
public class RegistersController(IRegisterService registers, IWebHostEnvironment env) : ControllerBase
{
    private string? DeviceToken => Request.Cookies[StaffAuth.RegisterCookie];

    /// <summary>Дүкеннің кассалары (иесі не әкімші).</summary>
    [HttpGet]
    [Authorize(Policy = StaffAuth.Policy)]
    public async Task<ActionResult<IReadOnlyList<RegisterDto>>> List(CancellationToken ct) =>
        Ok(await registers.ListAsync(ct));

    /// <summary>Осы құрылғыны кассаға тіркеу.</summary>
    [HttpPost("{id:guid}/activate")]
    [Authorize(Policy = StaffAuth.Policy)]
    public async Task<ActionResult<RegisterDto>> Activate(Guid id, CancellationToken ct)
    {
        var result = await registers.ActivateAsync(id, ct);
        Response.Cookies.Append(StaffAuth.RegisterCookie, result.DeviceToken, DeviceCookie(DateTime.UtcNow.AddYears(1)));
        return Ok(result.Register);
    }

    /// <summary>Осы құрылғыны кассадан ажырату.</summary>
    [HttpPost("current/deactivate")]
    [Authorize(Policy = StaffAuth.Policy)]
    public async Task<IActionResult> Deactivate(CancellationToken ct)
    {
        await registers.DeactivateAsync(DeviceToken, ct);
        Response.Cookies.Delete(StaffAuth.RegisterCookie, DeviceCookie(null));
        return NoContent();
    }

    /// <summary>Бұл құрылғы касса ма. Кірмей тұрып та сұралады — кіру бетінде кассирлерді көрсету үшін.</summary>
    [HttpGet("current")]
    [AllowAnonymous]
    public async Task<ActionResult<RegisterDto>> Current(CancellationToken ct) =>
        await registers.GetCurrentAsync(DeviceToken, ct) is { } register ? Ok(register) : NoContent();

    [HttpGet("current/cashiers")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<RegisterCashierDto>>> Cashiers(CancellationToken ct) =>
        Ok(await registers.GetCashiersAsync(DeviceToken, ct));

    /// <summary>Кассир ауысуы: тізімнен таңдап, PIN-мен кіру.</summary>
    [HttpPost("current/pin-login")]
    [AllowAnonymous]
    [EnableRateLimiting(StaffAuth.PinRateLimit)]
    public async Task<ActionResult<StaffSessionResponse>> PinLogin([FromBody] PinLoginRequest request, CancellationToken ct)
    {
        var session = await registers.PinLoginAsync(DeviceToken, request, Request.Headers.UserAgent.ToString(), ct);
        return Ok(StaffAuthController.IssueSession(Response, env, session));
    }

    /// <summary>Бұғатталған кассаны қазіргі кассирдің PIN-імен ашу. Сеанс сақталады.</summary>
    [HttpPost("current/unlock")]
    [Authorize(Policy = StaffAuth.Policy)]
    [EnableRateLimiting(StaffAuth.PinRateLimit)]
    public async Task<IActionResult> Unlock([FromBody] PinRequest request, CancellationToken ct)
    {
        await registers.UnlockAsync(DeviceToken, request.Pin, ct);
        return NoContent();
    }

    /// <summary>Касса бұғатталды — аудит үшін.</summary>
    [HttpPost("current/lock")]
    [Authorize(Policy = StaffAuth.Policy)]
    public async Task<IActionResult> Lock(CancellationToken ct)
    {
        await registers.LockAsync(DeviceToken, ct);
        return NoContent();
    }

    private CookieOptions DeviceCookie(DateTime? expires) => new()
    {
        HttpOnly = true,
        Secure = !env.IsDevelopment(),
        SameSite = SameSiteMode.Strict,
        Path = StaffAuth.RegisterCookiePath,
        Expires = expires,
    };
}
