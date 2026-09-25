using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Notifications.Dtos;
using SalemBonus.Domain.Entities;
using SalemBonus.Domain.Enums;

namespace SalemBonus.Application.Notifications;

public class NotificationService(INotificationRepository notifications, ICurrentUser currentUser) : INotificationService
{
    public async Task<NotificationPageDto> GetMyAsync(
        NotificationCategory? category, Guid? storeId, bool systemOnly, int skip, int take, CancellationToken ct = default)
    {
        var list = await notifications.GetByCustomerAsync(currentUser.CustomerId, category, storeId, systemOnly, skip, take + 1, ct);
        var hasMore = list.Count > take;
        return new NotificationPageDto(list.Take(take).Select(ToDto).ToList(), hasMore);
    }

    public async Task<IReadOnlyList<NotificationStoreDto>> GetMyStoresAsync(CancellationToken ct = default)
    {
        var groups = await notifications.GetGroupsByCustomerAsync(currentUser.CustomerId, ct);
        return groups
            .Select(g => new NotificationStoreDto(
                g.Last.StoreId,
                g.Last.Store?.Name,
                g.Last.Store?.Icon,
                g.Last.Store?.ThemeColor,
                g.Total,
                g.Unread,
                ToDto(g.Last)))
            .ToList();
    }

    public Task<int> GetMyUnreadCountAsync(CancellationToken ct = default) =>
        notifications.CountUnreadAsync(currentUser.CustomerId, ct);

    public async Task<bool> MarkReadAsync(Guid id, CancellationToken ct = default)
    {
        var n = await notifications.GetAsync(currentUser.CustomerId, id, ct);
        if (n is null) return false;
        await notifications.MarkReadAsync(currentUser.CustomerId, id, ct);
        return true;
    }

    public Task MarkAllReadAsync(CancellationToken ct = default) =>
        notifications.MarkReadAsync(currentUser.CustomerId, null, ct);

    private static NotificationDto ToDto(Notification n) => new(
        n.Id,
        n.Type.ToString(),
        n.Category.ToString(),
        n.Title,
        n.Body,
        n.Detail,
        n.TemplateKey,
        n.Amount,
        n.PurchaseAmount,
        n.LevelKey,
        n.Store?.Name,
        n.Store?.Icon,
        n.Store?.ThemeColor,
        n.IsRead,
        n.CreatedAt);
}
