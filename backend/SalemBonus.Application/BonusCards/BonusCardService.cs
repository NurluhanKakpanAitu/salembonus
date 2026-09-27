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

    public async Task<IReadOnlyList<BonusTransactionDto>> GetMyRecentTransactionsAsync(
        int take = 20, Guid? storeId = null, CancellationToken ct = default)
    {
        var list = await transactions.GetByCustomerAsync(currentUser.CustomerId, storeId, 0, take, ct);
        return await MapAsync(list, ct);
    }

    public async Task<TransactionPageDto> GetMyTransactionsAsync(Guid? storeId, int skip, int take, CancellationToken ct = default)
    {
        var list = await transactions.GetByCustomerAsync(currentUser.CustomerId, storeId, skip, take + 1, ct);
        var hasMore = list.Count > take;
        return new TransactionPageDto(await MapAsync(list.Take(take).ToList(), ct), hasMore);
    }

    public async Task<ReceiptDto?> GetMyReceiptAsync(Guid receiptId, CancellationToken ct = default)
    {
        var list = await transactions.GetByReceiptAsync(currentUser.CustomerId, receiptId, ct);
        if (list.Count == 0) return null;

        var card = (await cards.GetByCustomerAsync(currentUser.CustomerId, ct))
            .FirstOrDefault(c => c.Id == list[0].BonusCardId);
        if (card is null) return null;

        var redeemed = -list.Where(t => t.Amount < 0).Sum(t => t.Amount);
        var accrual = list.FirstOrDefault(t => t.Amount > 0);
        var accrued = accrual?.Amount ?? 0;
        var purchase = list.Max(t => t.PurchaseAmount) ?? 0m;
        var paid = purchase - redeemed;
        // Чектен кейінгі операцияларды ағымдағы балансттан шегергенде сол кездегі баланс шығады.
        var last = list.Max(t => t.CreatedAt);
        var balanceAfter = card.Balance - await transactions.SumAmountAfterAsync(card.Id, last, ct);

        return new ReceiptDto(
            receiptId,
            receiptId.ToString("N")[..8].ToUpperInvariant(),
            card.StoreId,
            card.Store?.Name ?? string.Empty,
            card.Store?.ThemeColor ?? "#111113",
            list[0].CreatedAt,
            purchase,
            redeemed,
            accrued,
            paid,
            paid > 0 ? Math.Round(accrued / paid * 100, 1) : 0m,
            balanceAfter,
            accrual?.ExpiresAt,
            list.Select(t => t.Comment).FirstOrDefault(c => !string.IsNullOrWhiteSpace(c)));
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
            t.CreatedAt,
            t.ReceiptId)).ToList();
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
