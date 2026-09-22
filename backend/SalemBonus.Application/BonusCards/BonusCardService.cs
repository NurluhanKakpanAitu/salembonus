using SalemBonus.Application.BonusCards.Dtos;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Domain.Entities;

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
        var list = await transactions.GetRecentByCustomerAsync(currentUser.CustomerId, take, ct);
        return await MapAsync(list, ct);
    }

    public async Task<TransactionPageDto> GetMyTransactionsAsync(Guid? storeId, int skip, int take, CancellationToken ct = default)
    {
        var list = await transactions.GetByCustomerAsync(currentUser.CustomerId, storeId, skip, take + 1, ct);
        var hasMore = list.Count > take;
        return new TransactionPageDto(await MapAsync(list.Take(take).ToList(), ct), hasMore);
    }

    private async Task<IReadOnlyList<BonusTransactionDto>> MapAsync(IReadOnlyList<BonusTransaction> list, CancellationToken ct)
    {
        var myCards = await cards.GetByCustomerAsync(currentUser.CustomerId, ct);
        var storeNames = myCards.ToDictionary(c => c.Id, c => c.Store?.Name ?? string.Empty);
        return list.Select(t => new BonusTransactionDto(
            t.Id,
            storeNames.GetValueOrDefault(t.BonusCardId, string.Empty),
            t.Type.ToString(),
            t.Amount,
            t.PurchaseAmount,
            t.CreatedAt)).ToList();
    }

    public static BonusCardDto ToDto(BonusCard card) => new(
        card.StoreId,
        card.Store?.Name ?? string.Empty,
        card.Store?.Category ?? string.Empty,
        card.Store?.ThemeColor ?? "#111113",
        card.Store?.Icon ?? "store",
        card.Balance,
        CustomerLevels.Name(card.Level),
        card.Store?.CashbackPercent ?? 0,
        BonusRules.AmountToNextLevel(card));
}
