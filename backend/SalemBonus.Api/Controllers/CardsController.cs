using Microsoft.AspNetCore.Mvc;
using SalemBonus.Application.BonusCards;
using SalemBonus.Application.BonusCards.Dtos;

namespace SalemBonus.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CardsController(IBonusCardService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BonusCardDto>>> GetAll(CancellationToken ct) =>
        Ok(await service.GetMyCardsAsync(ct));

    [HttpGet("{storeId:guid}")]
    public async Task<ActionResult<BonusCardDto>> Get(Guid storeId, CancellationToken ct)
    {
        var card = await service.GetMyCardAsync(storeId, ct);
        return card is null ? NotFound() : Ok(card);
    }
}
