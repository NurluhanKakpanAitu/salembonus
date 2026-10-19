using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SalemBonus.Application.Core.Staff;
using SalemBonus.Application.Core.Staff.Dtos;

namespace SalemBonus.Api.Controllers.Staff;

/// <summary>
/// Кіру жауабы. Кабинет сеансында refresh токені мұнда жоқ — ол httpOnly cookie-де. Касса терминалының
/// сеансында (PIN-мен кіру, X-Refresh-Token арқылы жаңарту) ол осында беріледі, cookie қойылмайды.
/// </summary>
public record StaffSessionResponse(string AccessToken, DateTime AccessTokenExpiresAt, StaffMeDto Me, string? RefreshToken = null);

/// <summary>
/// Қызметкердің кіруі (ТЗ «Авторизация»). Барлық өнімге ортақ: SalemPos, кейін SalemZapis.
/// </summary>
[ApiController]
[Route("api/staff/v1/auth")]
public class StaffAuthController(
    IStaffAuthService auth,
    IStaffPasswordResetService reset,
    IStaffCredentialsService credentials,
    IWebHostEnvironment env) : ControllerBase
{
    /// <summary>Телефон + құпиясөз.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(StaffAuth.LoginRateLimit)]
    public async Task<ActionResult<StaffSessionResponse>> Login([FromBody] StaffLoginRequest request, CancellationToken ct) =>
        Ok(Respond(await auth.LoginAsync(request, ct)));

    /// <summary>
    /// Жаңа access токен. Refresh токені — X-Refresh-Token тақырыбында (касса терминалы) немесе cookie-де (кабинет).
    /// Ескі refresh жабылады, жаңасы сол жолмен қайтады.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<StaffSessionResponse>> Refresh(CancellationToken ct)
    {
        var terminal = TerminalRefreshToken is not null;
        try
        {
            var session = await auth.RefreshAsync(TerminalRefreshToken ?? Request.Cookies[StaffAuth.RefreshCookie], ct);
            return Ok(IssueSession(Response, env, session, terminal));
        }
        catch
        {
            if (!terminal) ClearCookie();
            throw;
        }
    }

    /// <summary>Шығу: терминал сеансы (тақырып) не кабинет сеансы (cookie) — біреуі ғана жабылады.</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        if (TerminalRefreshToken is { } terminal)
        {
            await auth.LogoutAsync(terminal, ct);
            return NoContent();
        }
        await auth.LogoutAsync(Request.Cookies[StaffAuth.RefreshCookie], ct);
        ClearCookie();
        return NoContent();
    }

    private string? TerminalRefreshToken =>
        Request.Headers[StaffAuth.RefreshHeader].ToString() is { Length: > 0 } token ? token : null;

    [HttpGet("me")]
    [Authorize(Policy = StaffAuth.Policy)]
    public async Task<ActionResult<StaffMeDto>> Me(CancellationToken ct) =>
        Ok(await auth.GetMeAsync(ct));

    /// <summary>Интерфейс тілін сақтау — келесі кіргенде сол тілде ашылады (ТЗ §15).</summary>
    [HttpPut("me/language")]
    [Authorize(Policy = StaffAuth.Policy)]
    public async Task<ActionResult<StaffMeDto>> SetLanguage([FromBody] StaffLanguageRequest request, CancellationToken ct) =>
        Ok(await auth.SetLanguageAsync(request.Language, ct));

    /// <summary>PIN қою не ауыстыру — қазіргі құпиясөзбен (ТЗ «Касса» §17.8).</summary>
    [HttpPut("me/pin")]
    [Authorize(Policy = StaffAuth.Policy)]
    public async Task<ActionResult<StaffMeDto>> SetPin([FromBody] SetPinRequest request, CancellationToken ct) =>
        Ok(await credentials.SetPinAsync(request, ct));

    /// <summary>Құпиясөзді ауыстыру — қазіргісін растап (ТЗ «Касса» §17.7). Басқа сеанстар жабылады.</summary>
    [HttpPut("me/password")]
    [Authorize(Policy = StaffAuth.Policy)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        await credentials.ChangePasswordAsync(request, ct);
        return NoContent();
    }

    /// <summary>Құпиясөз талаптары — фронт жаңа құпиясөзді алдын ала тексеру үшін.</summary>
    [HttpGet("password-policy")]
    [AllowAnonymous]
    public ActionResult<PasswordPolicy> PasswordPolicy() => Ok(reset.Policy);

    /// <summary>Қалпына келтіру, 1-қадам: тіркелген нөмірге WhatsApp арқылы 4 таңбалы код.</summary>
    [HttpPost("password-reset/request")]
    [AllowAnonymous]
    [EnableRateLimiting(StaffAuth.ResetRateLimit)]
    public async Task<ActionResult<PasswordResetRequested>> RequestReset([FromBody] PasswordResetRequest request, CancellationToken ct) =>
        Ok(await reset.RequestAsync(request, ct));

    /// <summary>2-қадам: кодты тексеру. Дұрыс болса — жаңа құпиясөз қоюға рұқсат токені.</summary>
    [HttpPost("password-reset/verify")]
    [AllowAnonymous]
    [EnableRateLimiting(StaffAuth.ResetRateLimit)]
    public async Task<ActionResult<PasswordResetVerified>> VerifyReset([FromBody] PasswordResetVerify request, CancellationToken ct) =>
        Ok(await reset.VerifyAsync(request, ct));

    /// <summary>3-қадам: жаңа құпиясөз. Барлық ескі сеанс жабылады.</summary>
    [HttpPost("password-reset/complete")]
    [AllowAnonymous]
    [EnableRateLimiting(StaffAuth.ResetRateLimit)]
    public async Task<IActionResult> CompleteReset([FromBody] PasswordResetComplete request, CancellationToken ct)
    {
        await reset.CompleteAsync(request, ct);
        return NoContent();
    }

    private StaffSessionResponse Respond(StaffSession session) => IssueSession(Response, env, session);

    private void ClearCookie() =>
        Response.Cookies.Delete(StaffAuth.RefreshCookie, CookieOptions(env, null));

    /// <summary>
    /// Сеансты жауапқа жазу. Кабинет: refresh — httpOnly cookie-ге, қалғаны — JSON-ға. Терминал (касса):
    /// бәрі JSON-ға, cookie қозғалмайды — сол браузердегі кабинет сеансы сақталады.
    /// </summary>
    internal static StaffSessionResponse IssueSession(HttpResponse response, IWebHostEnvironment env, StaffSession session, bool terminal = false)
    {
        if (terminal)
            return new StaffSessionResponse(session.AccessToken, session.AccessTokenExpiresAt, session.Me, session.RefreshToken);
        response.Cookies.Append(StaffAuth.RefreshCookie, session.RefreshToken, CookieOptions(env, session.RefreshTokenExpiresAt));
        return new StaffSessionResponse(session.AccessToken, session.AccessTokenExpiresAt, session.Me);
    }

    private static CookieOptions CookieOptions(IWebHostEnvironment env, DateTime? expires) => new()
    {
        HttpOnly = true,
        // Локалда http, продакшнда тек https.
        Secure = !env.IsDevelopment(),
        SameSite = SameSiteMode.Strict,
        Path = StaffAuth.RefreshCookiePath,
        Expires = expires,
    };
}
