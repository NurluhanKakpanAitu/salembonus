using Microsoft.AspNetCore.Mvc;
using SalemBonus.Application.BonusCards;
using SalemBonus.Application.BonusCards.Dtos;

namespace SalemBonus.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TransactionsController(IBonusCardService service) : ControllerBase
{
    [HttpGet("recent")]
    public async Task<ActionResult<IReadOnlyList<BonusTransactionDto>>> GetRecent(
        [FromQuery] int take = 20, CancellationToken ct = default) =>
        Ok(await service.GetMyRecentTransactionsAsync(Math.Clamp(take, 1, 100), ct));
}
