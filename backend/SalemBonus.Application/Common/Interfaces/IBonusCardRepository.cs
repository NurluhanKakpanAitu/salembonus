using SalemBonus.Domain.Entities;

namespace SalemBonus.Application.Common.Interfaces;

public interface IBonusCardRepository
{
    Task<IReadOnlyList<BonusCard>> GetByCustomerAsync(Guid customerId, CancellationToken ct = default);
    Task<BonusCard?> GetAsync(Guid customerId, Guid storeId, CancellationToken ct = default);
}
