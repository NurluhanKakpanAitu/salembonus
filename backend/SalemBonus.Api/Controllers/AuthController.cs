using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalemBonus.Application.Auth;
using SalemBonus.Application.Auth.Dtos;

namespace SalemBonus.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class AuthController(IAuthService auth) : ControllerBase
{
    /// <summary>Телефонға SMS код жіберу.</summary>
    [HttpPost("request-code")]
    public async Task<ActionResult<RequestCodeResult>> RequestCode([FromBody] RequestCodeRequest request, CancellationToken ct) =>
        Ok(await auth.RequestCodeAsync(request.Phone, ct));

    /// <summary>Кодты тексеру, токендер алу. Жаңа нөмір болса тұтынушы автоматты тіркеледі.</summary>
    [HttpPost("verify")]
    public async Task<ActionResult<AuthTokens>> Verify([FromBody] VerifyCodeRequest request, CancellationToken ct) =>
        Ok(await auth.VerifyCodeAsync(request, ct));

    /// <summary>Refresh токенмен жаңа жұп алу (ескісі жабылады).</summary>
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthTokens>> Refresh([FromBody] RefreshRequest request, CancellationToken ct) =>
        Ok(await auth.RefreshAsync(request.RefreshToken, ct));

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request, CancellationToken ct)
    {
        await auth.LogoutAsync(request.RefreshToken, ct);
        return NoContent();
    }
}
