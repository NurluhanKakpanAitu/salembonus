using Microsoft.EntityFrameworkCore;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Domain.Entities;

namespace SalemBonus.Infrastructure.Persistence.Repositories;

public class StoreRepository(AppDbContext db) : IStoreRepository
{
    public Task<Store?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Stores.FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<Store?> GetByApiKeyAsync(string apiKey, CancellationToken ct = default) =>
        db.Stores.AsNoTracking().FirstOrDefaultAsync(s => s.ApiKey == apiKey, ct);

    public Task<Store?> GetByJoinCodeAsync(string joinCode, CancellationToken ct = default) =>
        db.Stores.FirstOrDefaultAsync(s => s.JoinCode == joinCode && s.IsActive, ct);

    public async Task<IReadOnlyList<Store>> GetActiveAsync(CancellationToken ct = default) =>
        await db.Stores.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync(ct);
}
