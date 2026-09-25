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
        var expiring = await NextExpiringAsync(list, ct);
        return list.Select(c => ToDto(c, expiring)).ToList();
    }

    /// <summary>Әр картаның ең жақын жанатын партиясы.</summary>
    private async Task<Dictionary<Guid, BonusTransaction>> NextExpiringAsync(
        IReadOnlyList<BonusCard> list, CancellationToken ct)
    {
        if (list.Count == 0) return [];
        var lots = await transactions.GetNextExpiringAsync(list.Select(c => c.Id).ToList(), DateTime.UtcNow, ct);
        return lots.ToDictionary(x => x.BonusCardId);
    }

    public async Task<BonusCardDto?> GetMyCardAsync(Guid storeId, CancellationToken ct = default)
    {
        var card = await cards.GetAsync(currentUser.CustomerId, storeId, ct);
        if (card is null) return null;
        return ToDto(card, await NextExpiringAsync([card], ct));
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

    private static BonusCardDto ToDto(BonusCard card, IReadOnlyDictionary<Guid, BonusTransaction> expiring)
    {
        var ladder = BonusRules.LadderOf(card.Store);
        var next = ladder.Where(l => l.FromAmount > card.TotalSpent).OrderBy(l => l.FromAmount).FirstOrDefault();
        expiring.TryGetValue(card.Id, out var lot);
        return Build(card, ladder, next, lot);
    }

    private static BonusCardDto Build(
        BonusCard card, IReadOnlyList<StoreLevel> ladder, StoreLevel? next, BonusTransaction? expiring) => new(
        card.StoreId,
        card.Store?.Name ?? string.Empty,
        card.Store?.Category ?? string.Empty,
        card.Store?.ThemeColor ?? "#111113",
        card.Store?.Icon ?? "store",
        card.Balance,
        CustomerLevels.Key(card.Level),
        BonusRules.PercentFor(card.Level, ladder),
        BonusRules.AmountToNextLevel(card.TotalSpent, ladder),
        card.TotalSpent,
        next is null ? null : CustomerLevels.Key(next.Level),
        next?.FromAmount,
        expiring?.Remaining,
        expiring?.ExpiresAt);
}
