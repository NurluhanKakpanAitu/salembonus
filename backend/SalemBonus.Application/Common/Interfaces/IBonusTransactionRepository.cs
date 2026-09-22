using SalemBonus.Domain.Entities;

namespace SalemBonus.Application.Common.Interfaces;

public interface IBonusTransactionRepository
{
    Task<IReadOnlyList<BonusTransaction>> GetRecentByCustomerAsync(Guid customerId, int take, CancellationToken ct = default);
    Task<IReadOnlyList<BonusTransaction>> GetByCustomerAsync(Guid customerId, Guid? storeId, int skip, int take, CancellationToken ct = default);
    void Add(BonusTransaction transaction);
}
