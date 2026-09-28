using SalemBonus.Application.Common.Interfaces;

namespace SalemBonus.Infrastructure.Persistence;

public class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);

    public async Task<T> InTransactionAsync<T>(Func<Task<T>> action, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var result = await action();
        await tx.CommitAsync(ct);
        return result;
    }
}
