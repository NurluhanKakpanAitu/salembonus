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

    public void Add(BonusTransaction transaction) => db.BonusTransactions.Add(transaction);
}
