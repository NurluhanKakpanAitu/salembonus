using Microsoft.EntityFrameworkCore;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Domain.Entities;
using SalemBonus.Domain.Enums;

namespace SalemBonus.Infrastructure.Persistence.Repositories;

public class NotificationRepository(AppDbContext db) : INotificationRepository
{
    private static readonly Dictionary<NotificationCategory, NotificationType[]> TypesByCategory = new()
    {
        [NotificationCategory.Bonus] = [NotificationType.BonusAccrued, NotificationType.BonusRedeemed, NotificationType.Birthday],
        [NotificationCategory.Promo] = [NotificationType.Promo],
        [NotificationCategory.Store] = [NotificationType.StoreAdded],
        [NotificationCategory.System] = [NotificationType.ProfileUpdated, NotificationType.System],
    };

    public async Task<IReadOnlyList<Notification>> GetByCustomerAsync(
        Guid customerId, NotificationCategory? category, Guid? storeId, bool systemOnly, int skip, int take,
        CancellationToken ct = default)
    {
        var q = db.Notifications.AsNoTracking().Include(n => n.Store).Where(n => n.CustomerId == customerId);
        if (category is { } c)
        {
            var types = TypesByCategory[c];
            q = q.Where(n => types.Contains(n.Type));
        }
        if (storeId is { } s) q = q.Where(n => n.StoreId == s);
        else if (systemOnly) q = q.Where(n => n.StoreId == null);
        return await q.OrderByDescending(n => n.CreatedAt).Skip(skip).Take(take).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<(Notification Last, int Total, int Unread)>> GetGroupsByCustomerAsync(
        Guid customerId, CancellationToken ct = default)
    {
        var groups = await db.Notifications.AsNoTracking()
            .Where(n => n.CustomerId == customerId)
            .GroupBy(n => n.StoreId)
            .Select(g => new
            {
                Total = g.Count(),
                Unread = g.Count(x => !x.IsRead),
                LastId = g.OrderByDescending(x => x.CreatedAt).Select(x => x.Id).First(),
                LastAt = g.Max(x => x.CreatedAt),
            })
            .OrderByDescending(x => x.LastAt)
            .ToListAsync(ct);

        var ids = groups.Select(g => g.LastId).ToList();
        var last = await db.Notifications.AsNoTracking().Include(n => n.Store)
            .Where(n => ids.Contains(n.Id))
            .ToDictionaryAsync(n => n.Id, ct);

        return groups
            .Where(g => last.ContainsKey(g.LastId))
            .Select(g => (last[g.LastId], g.Total, g.Unread))
            .ToList();
    }

    public Task<int> CountUnreadAsync(Guid customerId, CancellationToken ct = default) =>
        db.Notifications.CountAsync(n => n.CustomerId == customerId && !n.IsRead, ct);

    public Task<Notification?> GetAsync(Guid customerId, Guid id, CancellationToken ct = default) =>
        db.Notifications.AsNoTracking().FirstOrDefaultAsync(n => n.CustomerId == customerId && n.Id == id, ct);

    public Task MarkReadAsync(Guid customerId, Guid? id, CancellationToken ct = default)
    {
        var q = db.Notifications.Where(n => n.CustomerId == customerId && !n.IsRead);
        if (id is { } i) q = q.Where(n => n.Id == i);
        return q.ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), ct);
    }

    public void Add(Notification notification) => db.Notifications.Add(notification);
}
