using Microsoft.EntityFrameworkCore;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Domain.Entities;

namespace SalemBonus.Infrastructure.Persistence.Repositories;

public class BonusCardRepository(AppDbContext db) : IBonusCardRepository
{
    public async Task<IReadOnlyList<BonusCard>> GetByCustomerAsync(Guid customerId, CancellationToken ct = default) =>
        await db.BonusCards
            .AsNoTracking()
            .Include(c => c.Store).ThenInclude(s => s!.Levels)
            .Where(c => c.CustomerId == customerId)
            .OrderByDescending(c => c.Balance)
            .ToListAsync(ct);

    public Task<BonusCard?> GetAsync(Guid customerId, Guid storeId, CancellationToken ct = default) =>
        db.BonusCards
            .AsNoTracking()
            .Include(c => c.Store).ThenInclude(s => s!.Levels)
            .FirstOrDefaultAsync(c => c.CustomerId == customerId && c.StoreId == storeId, ct);

    /// <summary>Карта жолы бұғатталады (транзакция ішінде): екі касса бір балансты екі рет жұмсай алмайды (ТЗ «Бонусы» §11.2).</summary>
    public async Task<BonusCard?> GetForUpdateAsync(Guid customerId, Guid storeId, CancellationToken ct = default)
    {
        await db.Database.ExecuteSqlAsync($"""SELECT 1 FROM "BonusCards" WHERE "CustomerId" = {customerId} AND "StoreId" = {storeId} FOR UPDATE""", ct);
        return await db.BonusCards
            .Include(c => c.Store).ThenInclude(s => s!.Levels)
            .FirstOrDefaultAsync(c => c.CustomerId == customerId && c.StoreId == storeId, ct);
    }

    public async Task<BonusCard?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default)
    {
        await db.Database.ExecuteSqlAsync($"""SELECT 1 FROM "BonusCards" WHERE "Id" = {id} FOR UPDATE""", ct);
        return await db.BonusCards
            .Include(c => c.Store)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public void Add(BonusCard card) => db.BonusCards.Add(card);
}
