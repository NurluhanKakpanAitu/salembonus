using SalemBonus.Application.BonusCards.Dtos;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Domain.Entities;
using SalemBonus.Domain.Enums;

namespace SalemBonus.Application.BonusCards;

public class BonusCardService(
    IBonusCardRepository cards,
    IBonusTransactionRepository transactions,
    ICurrentUser currentUser) : IBonusCardService
{
    public async Task<IReadOnlyList<BonusCardDto>> GetMyCardsAsync(CancellationToken ct = default)
    {
        var list = await cards.GetByCustomerAsync(currentUser.CustomerId, ct);
        return list.Select(ToDto).ToList();
    }

    public async Task<BonusCardDto?> GetMyCardAsync(Guid storeId, CancellationToken ct = default)
    {
        var card = await cards.GetAsync(currentUser.CustomerId, storeId, ct);
        return card is null ? null : ToDto(card);
    }

    public async Task<IReadOnlyList<BonusTransactionDto>> GetMyRecentTransactionsAsync(int take = 20, CancellationToken ct = default)
    {
        var myCards = await cards.GetByCustomerAsync(currentUser.CustomerId, ct);
        var storeNames = myCards.ToDictionary(c => c.Id, c => c.Store?.Name ?? string.Empty);
        var list = await transactions.GetRecentByCustomerAsync(currentUser.CustomerId, take, ct);
        return list.Select(t => new BonusTransactionDto(
            t.Id,
            storeNames.GetValueOrDefault(t.BonusCardId, string.Empty),
            t.Type.ToString(),
            t.Amount,
            t.PurchaseAmount,
            t.CreatedAt)).ToList();
    }

    private static BonusCardDto ToDto(BonusCard card) => new(
        card.StoreId,
        card.Store?.Name ?? string.Empty,
        card.Store?.Category ?? string.Empty,
        card.Store?.ThemeColor ?? "#111113",
        card.Store?.Icon ?? "store",
        card.Balance,
        LevelName(card.Level),
        card.Store?.CashbackPercent ?? 0,
        AmountToNextLevel(card));

    private static string LevelName(CustomerLevel level) => level switch
    {
        CustomerLevel.Vip => "VIP клиент",
        CustomerLevel.Favorite => "Сүйікті клиент",
        CustomerLevel.Regular => "Тұрақты клиент",
        _ => "Жаңа клиент",
    };

    private static decimal AmountToNextLevel(BonusCard card)
    {
        var threshold = card.Level switch
        {
            CustomerLevel.New => LevelThresholds.Regular,
            CustomerLevel.Regular => LevelThresholds.Favorite,
            CustomerLevel.Favorite => LevelThresholds.Vip,
            _ => LevelThresholds.Vip,
        };
        return Math.Max(0, threshold - card.TotalSpent);
    }
}
