using Microsoft.EntityFrameworkCore;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Domain.Entities;

namespace SalemBonus.Infrastructure.Persistence.Repositories;

public class BonusTransactionRepository(AppDbContext db) : IBonusTransactionRepository
{
    public async Task<IReadOnlyList<BonusTransaction>> GetRecentByCustomerAsync(Guid customerId, int take, CancellationToken ct = default)
    {
        var cardIds = db.BonusCards.Where(c => c.CustomerId == customerId).Select(c => c.Id);
        return await db.BonusTransactions
            .AsNoTracking()
            .Where(t => cardIds.Contains(t.BonusCardId))
            .OrderByDescending(t => t.CreatedAt)
            .Take(take)
            .ToListAsync(ct);
    }
}
