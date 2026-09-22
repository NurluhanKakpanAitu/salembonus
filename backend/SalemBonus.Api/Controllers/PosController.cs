using Microsoft.AspNetCore.Mvc;
using SalemBonus.Application.Pos;
using SalemBonus.Application.Pos.Dtos;

namespace SalemBonus.Api.Controllers;

/// <summary>
/// Дүкен (SalemPos / касса) жағының API-ы. Әр сұраныста X-Store-Api-Key тақырыбы болуы керек.
/// </summary>
[ApiController]
[Route("api/pos")]
public class PosController(IPosService pos) : ControllerBase
{
    public const string ApiKeyHeader = "X-Store-Api-Key";

    /// <summary>Кілт бойынша дүкен мәліметі. Кілтті тексеру үшін де қолданылады.</summary>
    [HttpGet("store")]
    public async Task<ActionResult<PosStoreDto>> GetStore([FromHeader(Name = ApiKeyHeader)] string? apiKey, CancellationToken ct) =>
        Ok(await pos.AuthenticateAsync(apiKey, ct));

    /// <summary>Тұтынушыны QR ("SB:XXXXXXXXXX") немесе телефон ("+7701...") бойынша табу.</summary>
    [HttpGet("customers/{code}")]
    public async Task<ActionResult<PosCustomerDto>> LookupCustomer(
        [FromHeader(Name = ApiKeyHeader)] string? apiKey, string code, CancellationToken ct)
    {
        var store = await pos.AuthenticateAsync(apiKey, ct);
        return Ok(await pos.LookupCustomerAsync(store.Id, code, ct));
    }

    /// <summary>Сатып алуды тіркеу: RedeemAmount бонусын шегеру, қалғанына бонус есептеу.</summary>
    [HttpPost("purchases")]
    public async Task<ActionResult<PurchaseResultDto>> RegisterPurchase(
        [FromHeader(Name = ApiKeyHeader)] string? apiKey, [FromBody] PurchaseRequest request, CancellationToken ct)
    {
        var store = await pos.AuthenticateAsync(apiKey, ct);
        return Ok(await pos.RegisterPurchaseAsync(store.Id, request, ct));
    }
}
