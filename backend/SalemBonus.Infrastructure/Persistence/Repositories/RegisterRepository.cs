using Microsoft.EntityFrameworkCore;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Domain.Core;
using SalemBonus.Domain.Pos;

namespace SalemBonus.Infrastructure.Persistence.Repositories;

public class RegisterRepository(AppDbContext db) : IRegisterRepository
{
    public async Task<IReadOnlyList<Register>> ListByStoreAsync(Guid storeId, CancellationToken ct = default) =>
        await db.Registers.AsNoTracking().Include(r => r.Store)
            .Where(r => r.StoreId == storeId && r.IsActive)
            .OrderBy(r => r.Name)
            .ToListAsync(ct);

    public Task<Register?> GetForUpdateAsync(Guid storeId, Guid registerId, CancellationToken ct = default) =>
        db.Registers.Include(r => r.Store)
            .FirstOrDefaultAsync(r => r.Id == registerId && r.StoreId == storeId && r.IsActive, ct);

    public Task<Register?> GetByDeviceHashAsync(string deviceTokenHash, bool forUpdate = false, CancellationToken ct = default)
    {
        var query = db.Registers.Include(r => r.Store).Where(r => r.DeviceTokenHash == deviceTokenHash);
        return (forUpdate ? query : query.AsNoTracking()).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<StoreMembership>> ListSellersAsync(Guid storeId, CancellationToken ct = default) =>
        await db.StoreMemberships.AsNoTracking()
            .Include(m => m.StaffUser)
            .Where(m => m.StoreId == storeId && m.IsActive && m.StaffUser!.IsActive)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Register>> ListAllByStoreAsync(Guid storeId, CancellationToken ct = default) =>
        await db.Registers.AsNoTracking().Include(r => r.Store).Where(r => r.StoreId == storeId)
            .OrderByDescending(r => r.IsActive).ThenBy(r => r.CreatedAt).ToListAsync(ct);

    public Task<Register?> GetAnyForUpdateAsync(Guid storeId, Guid registerId, CancellationToken ct = default) =>
        db.Registers.Include(r => r.Store).FirstOrDefaultAsync(r => r.Id == registerId && r.StoreId == storeId, ct);

    public void Add(Register register) => db.Registers.Add(register);

    public async Task<Dictionary<Guid, (int Sales30, DateTime? LastSaleAt)>> SalesStatsAsync(Guid storeId, DateTime sinceUtc, CancellationToken ct = default)
    {
        var recent = await db.Sales.AsNoTracking().Where(s => s.StoreId == storeId && s.CreatedAt >= sinceUtc)
            .GroupBy(s => s.RegisterId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var last = await db.Sales.AsNoTracking().Where(s => s.StoreId == storeId)
            .GroupBy(s => s.RegisterId).Select(g => new { g.Key, Last = g.Max(s => s.CreatedAt) }).ToDictionaryAsync(x => x.Key, x => x.Last, ct);
        return recent.Keys.Union(last.Keys).ToDictionary(id => id, id => (recent.GetValueOrDefault(id), last.TryGetValue(id, out var l) ? (DateTime?)l : null));
    }
}
