using SalemBonus.Application.BonusCards;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Stores.Dtos;
using SalemBonus.Domain.Entities;
using SalemBonus.Domain.Enums;

namespace SalemBonus.Application.Stores;

public class StoreService(
    IStoreRepository stores,
    IBonusCardRepository cards,
    ICurrentUser currentUser) : IStoreService
{
    public async Task<IReadOnlyList<StoreListItemDto>> GetAllAsync(string? search, CancellationToken ct = default)
    {
        var all = await stores.GetActiveAsync(ct);
        var myCards = (await cards.GetByCustomerAsync(currentUser.CustomerId, ct)).ToDictionary(c => c.StoreId);

        var query = all.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(x =>
                x.Name.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                x.Category.Contains(s, StringComparison.OrdinalIgnoreCase));
        }

        return query
            .Select(store =>
            {
                myCards.TryGetValue(store.Id, out var card);
                return new StoreListItemDto(
                    store.Id, store.Name, store.Category, store.Description,
                    store.ThemeColor, store.Icon, store.CashbackPercent,
                    card is not null, card?.Balance ?? 0,
                    card is null ? null : CustomerLevels.Name(card.Level));
            })
            // Алдымен өз карталарым, сосын қалғаны
            .OrderByDescending(x => x.HasCard)
            .ThenByDescending(x => x.Balance)
            .ThenBy(x => x.Name)
            .ToList();
    }

    public async Task<StoreDetailDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var store = await stores.GetByIdAsync(id, ct);
        if (store is null || !store.IsActive) return null;

        var card = await cards.GetAsync(currentUser.CustomerId, id, ct);
        var currentLevel = card?.Level ?? CustomerLevel.New;

        var levels = new[]
        {
            new StoreLevelDto(CustomerLevels.Name(CustomerLevel.New), 0, currentLevel == CustomerLevel.New),
            new StoreLevelDto(CustomerLevels.Name(CustomerLevel.Regular), LevelThresholds.Regular, currentLevel == CustomerLevel.Regular),
            new StoreLevelDto(CustomerLevels.Name(CustomerLevel.Favorite), LevelThresholds.Favorite, currentLevel == CustomerLevel.Favorite),
            new StoreLevelDto(CustomerLevels.Name(CustomerLevel.Vip), LevelThresholds.Vip, currentLevel == CustomerLevel.Vip),
        };

        return new StoreDetailDto(
            store.Id, store.Name, store.Category, store.Description,
            store.ThemeColor, store.Icon, store.CashbackPercent, store.MaxRedeemPercent,
            card is not null, card?.Balance ?? 0,
            card is null ? null : CustomerLevels.Name(card.Level),
            card is null ? null : BonusRules.AmountToNextLevel(card),
            levels);
    }
}
