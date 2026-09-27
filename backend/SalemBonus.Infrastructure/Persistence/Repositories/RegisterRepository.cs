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
}
