using SalemBonus.Domain.Entities;

namespace SalemBonus.Application.Common.Interfaces;

public interface IStoreRepository
{
    Task<Store?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Store?> GetByApiKeyAsync(string apiKey, CancellationToken ct = default);
    Task<IReadOnlyList<Store>> GetActiveAsync(CancellationToken ct = default);
}
