using SalemBonus.Application.Common.Interfaces;

namespace SalemBonus.Infrastructure.Persistence;

public class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
