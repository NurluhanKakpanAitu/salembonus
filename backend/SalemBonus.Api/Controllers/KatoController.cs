using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalemBonus.Application.Kato;
using SalemBonus.Application.Kato.Dtos;

namespace SalemBonus.Api.Controllers;

/// <summary>
/// ҚР әкімшілік-аумақтық объектілер жіктеуіші (КАТО). Анықтамалықты екі жақ та оқиды: SalemBonus клиенті (профиль)
/// және SalemPos қызметкері (клиент карточкасы, сүзгі, дүкен баптауы) — сондықтан екі токен де жарайды.
/// </summary>
[ApiController]
[Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},{StaffAuth.Scheme}")]
[Route("api/[controller]")]
public class KatoController(IKatoService service) : ControllerBase
{
    /// <summary>Тікелей бағынышты тармақтар. parent бос болса — облыстар мен республикалық қалалар.</summary>
    [HttpGet("children")]
    public async Task<ActionResult<IReadOnlyList<KatoNodeDto>>> Children(
        [FromQuery] string? parent, CancellationToken ct) =>
        Ok(await service.GetChildrenAsync(string.IsNullOrWhiteSpace(parent) ? null : parent.Trim(), ct));

    /// <summary>Аудан/қала ішіндегі елді мекендер (ауылдық округтерсіз).</summary>
    [HttpGet("settlements")]
    public async Task<ActionResult<IReadOnlyList<KatoNodeDto>>> Settlements(
        [FromQuery] string parent, CancellationToken ct) =>
        Ok(await service.GetSettlementsAsync(parent, ct));

    /// <summary>Атауы бойынша іздеу.</summary>
    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<KatoMatchDto>>> Search(
        [FromQuery] string q, [FromQuery] int take = 20, CancellationToken ct = default) =>
        Ok(await service.SearchAsync(q ?? string.Empty, Math.Clamp(take, 1, 50), ct));
}
