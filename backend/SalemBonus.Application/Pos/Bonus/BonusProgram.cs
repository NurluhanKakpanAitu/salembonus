using SalemBonus.Application.BonusCards;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Pos.Sales;
using SalemBonus.Domain.Entities;
using SalemBonus.Domain.Enums;
using SalemBonus.Domain.Pos.Bonus;

namespace SalemBonus.Application.Pos.Bonus;

/// <summary>
/// Бонус бағдарламасының ережелері (ТЗ «Бонусы» §3, §4, §11): мәртебелер реті, клиент мәртебесін бағалау, есептеу пайызы,
/// партияның жану күні. Бағдарлама — дүкен бойынша; мәртебелері жоқ дүкенде ескі SalemBonus баспалдағы әрекет етеді.
/// Сұраныс ішінде мәртебелер мен баптаулар бір рет жүктеледі.
/// </summary>
public class BonusProgram(IBonusRepository repo, ICurrentLanguage language)
{
    private readonly Dictionary<Guid, IReadOnlyList<LoyaltyStatus>> statuses = [];
    private readonly Dictionary<Guid, BonusSettings?> settings = [];

    public async Task<IReadOnlyList<LoyaltyStatus>> StatusesAsync(Guid storeId, CancellationToken ct)
    {
        if (!statuses.TryGetValue(storeId, out var list)) statuses[storeId] = list = await repo.StatusesAsync(storeId, ct);
        return list;
    }

    public async Task<BonusSettings?> SettingsAsync(Guid storeId, CancellationToken ct)
    {
        if (!settings.TryGetValue(storeId, out var s)) settings[storeId] = s = await repo.SettingsAsync(storeId, ct);
        return s;
    }

    public void Forget(Guid storeId)
    {
        statuses.Remove(storeId);
        settings.Remove(storeId);
    }

    /// <summary>
    /// Бағдарламаны іске қосу (модуль бірінші ашылғанда): мәртебелер бұрынғы баспалдақтан (шегі мен пайызы сақталады),
    /// баптаулар дүкеннің бұрынғы жану мерзімінен; бар карталар өз деңгейіне сәйкес мәртебе алады. Шақырушы сақтайды.
    /// </summary>
    public async Task<(IReadOnlyList<LoyaltyStatus> Statuses, BonusSettings Settings)> EnsureAsync(Store store, Guid? staffId, CancellationToken ct)
    {
        var list = await StatusesAsync(store.Id, ct);
        if (list.Count == 0)
        {
            var created = new List<LoyaltyStatus>();
            foreach (var l in BonusRules.LadderOf(store))
                created.Add(new LoyaltyStatus
                {
                    Id = Guid.NewGuid(), StoreId = store.Id, Name = CustomerLevels.Name(l.Level, language.Value),
                    Rule = l.Level == CustomerLevel.New ? StatusRule.Registration : StatusRule.PurchaseAmount,
                    ThresholdAmount = l.Level == CustomerLevel.New ? null : l.FromAmount, AccrualPercent = l.CashbackPercent,
                    Icon = DefaultIcon(l.Level), Color = DefaultColor(l.Level), Level = l.Level, CreatedBy = staffId,
                });
            Rank(created);
            foreach (var s in created) repo.AddStatus(s);
            foreach (var s in created) await repo.AssignStatusByLevelAsync(store.Id, s.Level, s.Id, ct);
            statuses[store.Id] = list = created;
        }
        var cfg = await SettingsAsync(store.Id, ct);
        if (cfg is null)
        {
            cfg = new BonusSettings
            {
                StoreId = store.Id, ExpiryEnabled = store.BonusLifetimeDays is not null, ExpiryValue = store.BonusLifetimeDays ?? 12,
                ExpiryUnit = store.BonusLifetimeDays is null ? ExpiryUnit.Months : ExpiryUnit.Days,
                BirthdayAmount = 0, BirthdayMessage = DefaultBirthdayMessage(language.Value), UpdatedBy = staffId,
            };
            repo.AddSettings(cfg);
            settings[store.Id] = cfg;
        }
        return (list, cfg);
    }

    public static string DefaultBirthdayMessage(AppLanguage lang) => lang == AppLanguage.Ru
        ? "{{Имя}}, с днём рождения! Дарим {{Бонусы}} бонусов — они действуют до {{ДатаСгорания}}."
        : "{{Имя}}, туған күніңізбен! Сізге {{Бонусы}} бонус сыйлаймыз — {{ДатаСгорания}} дейін жарамды.";

    private static string DefaultIcon(CustomerLevel l) => l switch { CustomerLevel.New => "sprout", CustomerLevel.Regular => "crown", CustomerLevel.Favorite => "heart", _ => "gem" };
    private static string DefaultColor(CustomerLevel l) => l switch { CustomerLevel.New => "#16A34A", CustomerLevel.Regular => "#1769FF", CustomerLevel.Favorite => "#DB2777", _ => "#7C3AED" };

    /// <summary>
    /// Деңгей реті: тіркелу мәртебесі — бірінші, қалғаны есептеу пайызы бойынша (жоғары пайыз — жоғары мәртебе), тең болса — шегі бойынша.
    /// <see cref="LoyaltyStatus.Level"/> — қосымшадағы төрт деңгейдің біреуі (белгі мен түс үшін).
    /// </summary>
    public static void Rank(List<LoyaltyStatus> list)
    {
        var ordered = list.OrderBy(s => s.Rule == StatusRule.Registration ? 0 : 1).ThenBy(s => s.AccrualPercent)
            .ThenBy(s => s.ThresholdAmount ?? 0).ThenBy(s => s.ThresholdCount ?? 0).ThenBy(s => s.CreatedAt).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i].SortOrder = i;
            ordered[i].Level = (CustomerLevel)Math.Min(i, (int)CustomerLevel.Vip);
        }
    }

    /// <summary>Ескі баспалдақты мәртебелерден жаңарту: қосымша мен терминал API-ы дұрыс пайыз бен шекті көрсетеді.</summary>
    public async Task SyncLadderAsync(Store store, IReadOnlyList<LoyaltyStatus> list, CancellationToken ct)
    {
        foreach (var s in list) await repo.SyncCardLevelsAsync(store.Id, s.Id, s.Level, ct);
        // Қосымша баспалдағы сомамен өлшенеді: тіркелу мен сома шартты мәртебелер ғана (қолмен және сан бойынша — баспалдақта жоқ).
        var levels = list.Where(s => s.IsActive && s.Rule is StatusRule.Registration or StatusRule.PurchaseAmount).GroupBy(s => s.Level).Select(g => g.OrderBy(s => s.SortOrder).First())
            .Select(s => new StoreLevel
            {
                Id = Guid.NewGuid(), StoreId = store.Id, Level = s.Level, FromAmount = s.Rule == StatusRule.PurchaseAmount ? s.ThresholdAmount ?? 0 : 0,
                CashbackPercent = s.AccrualPercent,
            }).ToList();
        if (levels.Count == 0) return;
        await repo.ReplaceLadderAsync(store.Id, levels, ct);
    }

    /// <summary>Картаның ағымдағы мәртебесі: жазылған id бойынша, жоқ болса — деңгейі бойынша.</summary>
    public static LoyaltyStatus? Current(BonusCard? card, IReadOnlyList<LoyaltyStatus> list)
    {
        if (list.Count == 0) return null;
        if (card?.StatusId is { } id && list.FirstOrDefault(s => s.Id == id) is { } byId) return byId;
        var level = card?.Level ?? CustomerLevel.New;
        return list.Where(s => s.IsActive && s.Level == level).OrderBy(s => s.SortOrder).FirstOrDefault() ?? Registration(list);
    }

    public static LoyaltyStatus? Registration(IReadOnlyList<LoyaltyStatus> list) =>
        list.Where(s => s.IsActive && s.Rule == StatusRule.Registration).OrderBy(s => s.SortOrder).FirstOrDefault()
        ?? list.Where(s => s.IsActive && s.Rule != StatusRule.Manual).OrderBy(s => s.SortOrder).FirstOrDefault();

    /// <summary>Есептеу пайызы (§11.1): мәртебе бойынша; бағдарлама қосылмаған дүкенде — ескі баспалдақ.</summary>
    public async Task<decimal> PercentForAsync(BonusCard? card, Store store, CancellationToken ct)
    {
        var list = await StatusesAsync(store.Id, ct);
        if (list.Count == 0) return BonusRules.PercentFor(card?.Level ?? CustomerLevel.New, BonusRules.LadderOf(store));
        return Current(card, list)?.AccrualPercent ?? 0;
    }

    public async Task<string> StatusNameAsync(BonusCard? card, Store store, CancellationToken ct)
    {
        var list = await StatusesAsync(store.Id, ct);
        return Current(card, list)?.Name ?? CustomerLevels.Name(card?.Level ?? CustomerLevel.New, language.Value);
    }

    /// <summary>
    /// Партияның жану күні (§4.1): баптау болмаса — дүкеннің ескі мерзімі; өшірулі, ерекшелік түрі не мәртебе тізімде жоқ болса — жанбайды.
    /// Мерзім әр партияға жеке беріледі, бүкіл балансқа емес.
    /// </summary>
    public static DateTime? LotExpiresAt(BonusSettings? s, Store store, BonusCard card, BonusTransactionType type, DateTime from)
    {
        if (s is null) return store.BonusLifetimeDays is { } days ? from.AddDays(days) : null;
        if (!s.ExpiryEnabled || s.ExpiryValue <= 0) return null;
        if (s.ExpiryExcludedTypes.Contains(type.ToString())) return null;
        if (s.ExpiryStatusIds.Count > 0 && (card.StatusId is not { } sid || !s.ExpiryStatusIds.Contains(sid))) return null;
        return s.ExpiryUnit == ExpiryUnit.Months ? from.AddMonths(s.ExpiryValue) : from.AddDays(s.ExpiryValue);
    }

    public async Task<DateTime?> LotExpiresAtAsync(Store store, BonusCard card, BonusTransactionType type, DateTime from, CancellationToken ct) =>
        LotExpiresAt(await SettingsAsync(store.Id, ct), store, card, type, from);

    public static void Assign(BonusCard card, LoyaltyStatus status, DateTime now, bool manual, string? reason)
    {
        card.StatusId = status.Id;
        card.Level = status.Level;
        card.StatusManual = manual;
        card.StatusReason = reason;
        card.StatusAssignedAt = now;
        card.StatusExpiresAt = status.ValidityDays is { } d && d > 0 ? now.AddDays(d) : null;
    }

    /// <summary>
    /// Мәртебені шарттар бойынша бағалау (§3, §11): тек белсенді, автоматты ережелер; қолмен берілген мәртебеге тиіспейді.
    /// Сатып алуда тек көтеріледі (артқа санап бонус есептелмейді); <paramref name="allowDowngrade"/> — мерзімі біткенде қайта тексеру.
    /// «Барлық уақыт» сомасы — картаның жинақталған сомасы (терминал сатылымдарын да қамтиды), кезеңдер — SalemPos чектері бойынша.
    /// </summary>
    public async Task<(LoyaltyStatus? Status, bool Upgraded)> EvaluateAsync(BonusCard card, Store store, DateTime now, bool allowDowngrade,
        decimal pendingAmount, int pendingCount, CancellationToken ct)
    {
        var list = await StatusesAsync(store.Id, ct);
        if (list.Count == 0 || card.StatusManual) return (null, false);
        var current = Current(card, list);
        var zone = StoreTime.Zone(store);
        var cache = new Dictionary<StatusPeriod, PurchaseStats>();
        async Task<PurchaseStats> Stats(StatusPeriod p)
        {
            if (cache.TryGetValue(p, out var s)) return s;
            DateTime? from = p switch
            {
                StatusPeriod.Last12Months => now.AddMonths(-12),
                StatusPeriod.CalendarYear => StoreTime.StartOfDayUtc(new DateOnly(TimeZoneInfo.ConvertTimeFromUtc(now, zone).Year, 1, 1), zone),
                _ => null,
            };
            return cache[p] = await repo.PurchaseStatsAsync(store.Id, card.CustomerId, from, ct);
        }

        LoyaltyStatus? best = null;
        foreach (var s in list.Where(x => x.IsActive && x.Rule != StatusRule.Manual).OrderBy(x => x.SortOrder))
        {
            var met = s.Rule switch
            {
                StatusRule.Registration => true,
                StatusRule.PurchaseAmount => (s.Period == StatusPeriod.AllTime && s.CountReturns
                    ? card.TotalSpent
                    : (await Stats(s.Period)).Amount(s.CountReturns) + pendingAmount) >= (s.ThresholdAmount ?? 0),
                StatusRule.PurchaseCount => (await Stats(s.Period)).Purchases(s.CountReturns) + pendingCount >= (s.ThresholdCount ?? 1),
                _ => false,
            };
            if (met) best = s;
        }
        if (best is null) return (null, false);
        if (current is not null && best.Id == current.Id)
        {
            if (allowDowngrade) card.StatusExpiresAt = best.ValidityDays is { } d && d > 0 ? now.AddDays(d) : null;
            if (card.StatusId is null) card.StatusId = best.Id;
            return (null, false);
        }
        if (current is not null && !allowDowngrade && current.SortOrder > best.SortOrder) return (null, false);
        var upgraded = current is null || best.SortOrder > current.SortOrder;
        Assign(card, best, now, false, null);
        return (best, upgraded);
    }
}
