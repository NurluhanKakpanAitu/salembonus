using SalemBonus.Application.Notifications.Dtos;
using SalemBonus.Domain.Enums;

namespace SalemBonus.Application.Notifications;

public interface INotificationService
{
    Task<NotificationPageDto> GetMyAsync(NotificationCategory? category, int skip, int take, CancellationToken ct = default);
    Task<int> GetMyUnreadCountAsync(CancellationToken ct = default);
    Task<bool> MarkReadAsync(Guid id, CancellationToken ct = default);
    Task MarkAllReadAsync(CancellationToken ct = default);
}
