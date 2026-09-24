using SalemBonus.Application.BonusCards;
using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Stores.Dtos;
using SalemBonus.Domain.Entities;
using SalemBonus.Domain.Enums;

namespace SalemBonus.Application.Stores;

public class StoreService(
    IStoreRepository stores,
    IBonusCardRepository cards,
    INotificationRepository notifications,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser) : IStoreService
{
    /// <summary>Дүкен QR кодының префиксі: "SBS:MKMAUTO".</summary>
    public const string QrPrefix = "SBS:";

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
        return ToDetail(store, card);
    }

    public async Task<JoinStoreResultDto> JoinAsync(string rawCode, CancellationToken ct = default)
    {
        var store = await FindStoreAsync(rawCode, ct)
            ?? throw new NotFoundException("Дүкен табылмады. QR кодты тексеріп, қайта сканерлеңіз.");

        var existing = await cards.GetAsync(currentUser.CustomerId, store.Id, ct);
        if (existing is not null)
            return new JoinStoreResultDto(true, ToDetail(store, existing));

        var now = DateTime.UtcNow;
        var card = new BonusCard
        {
            Id = Guid.NewGuid(),
            CustomerId = currentUser.CustomerId,
            StoreId = store.Id,
            Store = store,
            CreatedAt = now,
        };
        cards.Add(card);

        notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            CustomerId = currentUser.CustomerId,
            StoreId = store.Id,
            Type = NotificationType.StoreAdded,
            Title = "Жаңа дүкен қосылды",
            Body = $"{store.Name} дүкені сіздің карталарыңызға қосылды.",
            Detail = $"Енді осы дүкенде {store.CashbackPercent}% бонус жинай аласыз!",
            CreatedAt = now,
        });

        await unitOfWork.SaveChangesAsync(ct);
        return new JoinStoreResultDto(false, ToDetail(store, card));
    }

    /// <summary>QR мәтіні "SBS:KOD", жай "KOD" немесе дүкен идентификаторы болуы мүмкін.</summary>
    private async Task<Store?> FindStoreAsync(string rawCode, CancellationToken ct)
    {
        var code = (rawCode ?? string.Empty).Trim();
        if (code.Length == 0) throw new ValidationException("QR коды бос");

        if (code.StartsWith(QrPrefix, StringComparison.OrdinalIgnoreCase))
            code = code[QrPrefix.Length..].Trim();

        if (Guid.TryParse(code, out var id))
        {
            var byId = await stores.GetByIdAsync(id, ct);
            return byId is { IsActive: true } ? byId : null;
        }

        return await stores.GetByJoinCodeAsync(code.ToUpperInvariant(), ct);
    }

    private static StoreDetailDto ToDetail(Store store, BonusCard? card)
    {
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
