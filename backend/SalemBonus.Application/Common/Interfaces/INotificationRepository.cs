using SalemBonus.Domain.Entities;
using SalemBonus.Domain.Enums;

namespace SalemBonus.Application.Common.Interfaces;

public interface INotificationRepository
{
    Task<IReadOnlyList<Notification>> GetByCustomerAsync(
        Guid customerId, NotificationCategory? category, int skip, int take, CancellationToken ct = default);
    Task<int> CountUnreadAsync(Guid customerId, CancellationToken ct = default);
    Task<Notification?> GetAsync(Guid customerId, Guid id, CancellationToken ct = default);
    Task MarkReadAsync(Guid customerId, Guid? id, CancellationToken ct = default);
    void Add(Notification notification);
}
