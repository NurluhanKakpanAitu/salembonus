using SalemBonus.Application.BonusCards.Dtos;

namespace SalemBonus.Application.BonusCards;

public interface IBonusCardService
{
    Task<IReadOnlyList<BonusCardDto>> GetMyCardsAsync(CancellationToken ct = default);
    Task<BonusCardDto?> GetMyCardAsync(Guid storeId, CancellationToken ct = default);
    /// <summary>Соңғы операциялар. storeId берілсе тек сол дүкен бойынша.</summary>
    Task<IReadOnlyList<BonusTransactionDto>> GetMyRecentTransactionsAsync(int take = 20, Guid? storeId = null, CancellationToken ct = default);
    Task<TransactionPageDto> GetMyTransactionsAsync(Guid? storeId, int skip, int take, CancellationToken ct = default);
    /// <summary>Бір сатып алудың чегі. Табылмаса немесе басқа клиенттің болса — бос.</summary>
    Task<ReceiptDto?> GetMyReceiptAsync(Guid receiptId, CancellationToken ct = default);
}
