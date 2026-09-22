using SalemBonus.Application.BonusCards.Dtos;

namespace SalemBonus.Application.BonusCards;

public interface IBonusCardService
{
    Task<IReadOnlyList<BonusCardDto>> GetMyCardsAsync(CancellationToken ct = default);
    Task<BonusCardDto?> GetMyCardAsync(Guid storeId, CancellationToken ct = default);
    Task<IReadOnlyList<BonusTransactionDto>> GetMyRecentTransactionsAsync(int take = 20, CancellationToken ct = default);
}
