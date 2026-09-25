using Microsoft.AspNetCore.Mvc;
using SalemBonus.Application.Notifications;
using SalemBonus.Application.Notifications.Dtos;
using SalemBonus.Domain.Enums;

namespace SalemBonus.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificationsController(INotificationService service) : ControllerBase
{
    /// <summary>Хабарламалар тізімі. storeId берілсе сол дүкендікі, system=true болса дүкенсіздері.</summary>
    [HttpGet]
    public async Task<ActionResult<NotificationPageDto>> Get(
        [FromQuery] NotificationCategory? category,
        [FromQuery] Guid? storeId,
        [FromQuery] bool system = false,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        CancellationToken ct = default) =>
        Ok(await service.GetMyAsync(category, storeId, system, Math.Max(0, skip), Math.Clamp(take, 1, 100), ct));

    /// <summary>Дүкендер бойынша топтастырылған хабарламалар.</summary>
    [HttpGet("stores")]
    public async Task<ActionResult<IReadOnlyList<NotificationStoreDto>>> Stores(CancellationToken ct) =>
        Ok(await service.GetMyStoresAsync(ct));

    [HttpGet("unread-count")]
    public async Task<ActionResult<int>> UnreadCount(CancellationToken ct) =>
        Ok(await service.GetMyUnreadCountAsync(ct));

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct) =>
        await service.MarkReadAsync(id, ct) ? NoContent() : NotFound();

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        await service.MarkAllReadAsync(ct);
        return NoContent();
    }
}
