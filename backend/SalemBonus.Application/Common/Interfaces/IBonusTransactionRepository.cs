using SalemBonus.Domain.Entities;

namespace SalemBonus.Application.Common.Interfaces;

public interface IBonusTransactionRepository
{
    Task<IReadOnlyList<BonusTransaction>> GetRecentByCustomerAsync(Guid customerId, int take, CancellationToken ct = default);
}
