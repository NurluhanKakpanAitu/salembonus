namespace SalemBonus.Application.Common.Interfaces;

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct = default);
}
