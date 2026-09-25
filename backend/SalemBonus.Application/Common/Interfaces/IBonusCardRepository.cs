using SalemBonus.Domain.Entities;

namespace SalemBonus.Application.Common.Interfaces;

public interface IBonusCardRepository
{
    Task<IReadOnlyList<BonusCard>> GetByCustomerAsync(Guid customerId, CancellationToken ct = default);
    Task<BonusCard?> GetAsync(Guid customerId, Guid storeId, CancellationToken ct = default);
    /// <summary>Өзгерту үшін бақыланатын (tracked) нұсқасын қайтарады.</summary>
    Task<BonusCard?> GetForUpdateAsync(Guid customerId, Guid storeId, CancellationToken ct = default);
    Task<BonusCard?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default);
    void Add(BonusCard card);
}
