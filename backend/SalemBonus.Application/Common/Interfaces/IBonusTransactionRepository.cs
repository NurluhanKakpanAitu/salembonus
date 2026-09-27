using SalemBonus.Domain.Entities;

namespace SalemBonus.Application.Common.Interfaces;

public interface IBonusTransactionRepository
{
    Task<IReadOnlyList<BonusTransaction>> GetByCustomerAsync(Guid customerId, Guid? storeId, int skip, int take, CancellationToken ct = default);
    /// <summary>Картаның жұмсалмаған партиялары, ең ескісінен бастап (жану күні бойынша).</summary>
    Task<IReadOnlyList<BonusTransaction>> GetOpenLotsAsync(Guid bonusCardId, CancellationToken ct = default);
    /// <summary>Мерзімі өткен, әлі өшірілмеген партиялар.</summary>
    Task<IReadOnlyList<BonusTransaction>> GetExpiredLotsAsync(DateTime now, int take, CancellationToken ct = default);
    /// <summary>Картаның ең жақын жанатын партиясы.</summary>
    Task<IReadOnlyList<BonusTransaction>> GetNextExpiringAsync(IReadOnlyCollection<Guid> cardIds, DateTime now, CancellationToken ct = default);
    /// <summary>Бір чекке жататын операциялар. Чек басқа клиенттің болса — бос тізім.</summary>
    Task<IReadOnlyList<BonusTransaction>> GetByReceiptAsync(Guid customerId, Guid receiptId, CancellationToken ct = default);
    /// <summary>Берілген уақыттан кейінгі операциялардың қосындысы — өткен баланстты есептеу үшін.</summary>
    Task<int> SumAmountAfterAsync(Guid bonusCardId, DateTime after, CancellationToken ct = default);
    void Add(BonusTransaction transaction);
}
