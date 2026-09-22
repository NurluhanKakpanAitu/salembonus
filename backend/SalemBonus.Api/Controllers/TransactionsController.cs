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

    /// <summary>Барлық операциялар, беттеп. storeId берілсе тек сол дүкен бойынша.</summary>
    [HttpGet]
    public async Task<ActionResult<TransactionPageDto>> Get(
        [FromQuery] Guid? storeId, [FromQuery] int skip = 0, [FromQuery] int take = 20, CancellationToken ct = default) =>
        Ok(await service.GetMyTransactionsAsync(storeId, Math.Max(0, skip), Math.Clamp(take, 1, 100), ct));
}
