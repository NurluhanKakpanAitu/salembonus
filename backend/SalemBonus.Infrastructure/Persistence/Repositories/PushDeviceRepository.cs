using Microsoft.EntityFrameworkCore;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Domain.Entities;

namespace SalemBonus.Infrastructure.Persistence.Repositories;

public class PushDeviceRepository(AppDbContext db) : IPushDeviceRepository
{
    public Task<PushDevice?> GetByTokenAsync(string token, CancellationToken ct = default) =>
        db.PushDevices.FirstOrDefaultAsync(d => d.Token == token, ct);

    public async Task<IReadOnlyList<PushDevice>> GetByCustomerAsync(Guid customerId, CancellationToken ct = default) =>
        await db.PushDevices.AsNoTracking().Where(d => d.CustomerId == customerId).ToListAsync(ct);

    public void Add(PushDevice device) => db.PushDevices.Add(device);

    public async Task<bool> RemoveAsync(Guid customerId, string token, CancellationToken ct = default) =>
        await db.PushDevices.Where(d => d.CustomerId == customerId && d.Token == token).ExecuteDeleteAsync(ct) > 0;

    public Task RemoveByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
        db.PushDevices.Where(d => ids.Contains(d.Id)).ExecuteDeleteAsync(ct);
}
