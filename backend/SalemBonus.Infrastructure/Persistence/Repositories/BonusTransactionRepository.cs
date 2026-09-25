using Microsoft.EntityFrameworkCore;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Domain.Entities;

namespace SalemBonus.Infrastructure.Persistence.Repositories;

public class BonusTransactionRepository(AppDbContext db) : IBonusTransactionRepository
{
    public Task<IReadOnlyList<BonusTransaction>> GetRecentByCustomerAsync(Guid customerId, int take, CancellationToken ct = default) =>
        GetByCustomerAsync(customerId, null, 0, take, ct);

    public async Task<IReadOnlyList<BonusTransaction>> GetByCustomerAsync(Guid customerId, Guid? storeId, int skip, int take, CancellationToken ct = default)
    {
        var cardIds = db.BonusCards.Where(c => c.CustomerId == customerId);
        if (storeId is { } s) cardIds = cardIds.Where(c => c.StoreId == s);
        var ids = cardIds.Select(c => c.Id);
        return await db.BonusTransactions
            .AsNoTracking()
            .Where(t => ids.Contains(t.BonusCardId))
            .OrderByDescending(t => t.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<BonusTransaction>> GetOpenLotsAsync(Guid bonusCardId, CancellationToken ct = default) =>
        await db.BonusTransactions
            .Where(t => t.BonusCardId == bonusCardId && t.Remaining > 0)
            // Ең ерте жанатыны бірінші жұмсалады, мерзімсіздері соңында.
            .OrderBy(t => t.ExpiresAt == null)
            .ThenBy(t => t.ExpiresAt)
            .ThenBy(t => t.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<BonusTransaction>> GetExpiredLotsAsync(DateTime now, int take, CancellationToken ct = default) =>
        await db.BonusTransactions
            .Where(t => t.Remaining > 0 && t.ExpiresAt != null && t.ExpiresAt <= now)
            .OrderBy(t => t.ExpiresAt)
            .Take(take)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<BonusTransaction>> GetNextExpiringAsync(
        IReadOnlyCollection<Guid> cardIds, DateTime now, CancellationToken ct = default) =>
        await db.BonusTransactions.AsNoTracking()
            .Where(t => cardIds.Contains(t.BonusCardId) && t.Remaining > 0 && t.ExpiresAt != null && t.ExpiresAt > now)
            .GroupBy(t => t.BonusCardId)
            .Select(g => g.OrderBy(x => x.ExpiresAt).First())
            .ToListAsync(ct);

    public void Add(BonusTransaction transaction) => db.BonusTransactions.Add(transaction);
}
