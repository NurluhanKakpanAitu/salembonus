using Microsoft.AspNetCore.Mvc;
using SalemBonus.Application.Stores;
using SalemBonus.Application.Stores.Dtos;

namespace SalemBonus.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StoresController(IStoreService service) : ControllerBase
{
    /// <summary>Серіктес дүкендер каталогы.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<StoreListItemDto>>> GetAll(
        [FromQuery] string? search, CancellationToken ct) =>
        Ok(await service.GetAllAsync(search, ct));

    /// <summary>QR кодты сканерлеп дүкен қосу.</summary>
    [HttpPost("join")]
    public async Task<ActionResult<JoinStoreResultDto>> Join([FromBody] JoinStoreRequest request, CancellationToken ct) =>
        Ok(await service.JoinAsync(request.Code, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StoreDetailDto>> Get(Guid id, CancellationToken ct)
    {
        var store = await service.GetAsync(id, ct);
        return store is null ? NotFound() : Ok(store);
    }
}
