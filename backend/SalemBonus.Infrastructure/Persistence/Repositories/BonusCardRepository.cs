using Microsoft.EntityFrameworkCore;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Domain.Entities;

namespace SalemBonus.Infrastructure.Persistence.Repositories;

public class BonusCardRepository(AppDbContext db) : IBonusCardRepository
{
    public async Task<IReadOnlyList<BonusCard>> GetByCustomerAsync(Guid customerId, CancellationToken ct = default) =>
        await db.BonusCards
            .AsNoTracking()
            .Include(c => c.Store)
            .Where(c => c.CustomerId == customerId)
            .OrderByDescending(c => c.Balance)
            .ToListAsync(ct);

    public Task<BonusCard?> GetAsync(Guid customerId, Guid storeId, CancellationToken ct = default) =>
        db.BonusCards
            .AsNoTracking()
            .Include(c => c.Store)
            .FirstOrDefaultAsync(c => c.CustomerId == customerId && c.StoreId == storeId, ct);
}
