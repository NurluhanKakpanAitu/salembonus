using SalemBonus.Domain.Entities;

namespace SalemBonus.Application.Common.Interfaces;

public interface IPushDeviceRepository
{
    Task<PushDevice?> GetByTokenAsync(string token, CancellationToken ct = default);
    Task<IReadOnlyList<PushDevice>> GetByCustomerAsync(Guid customerId, CancellationToken ct = default);
    void Add(PushDevice device);
    /// <summary>Токен тек осы клиенттікі болса өшіріледі. Нәтиже — өшкені бар ма.</summary>
    Task<bool> RemoveAsync(Guid customerId, string token, CancellationToken ct = default);
    Task RemoveByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
}
