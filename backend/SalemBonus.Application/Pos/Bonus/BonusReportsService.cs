using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Domain.Core;
using SalemBonus.Domain.Enums;
using SalemBonus.Domain.Pos.Bonus;

namespace SalemBonus.Application.Pos.Bonus;

/// <summary>
/// «Обзор» (ТЗ «Бонусы» §2) және «Статистика» (§7): тек оқиды. Ағын (кезеңдегі есептелді/жұмсалды/қайтарылды/жанды) мен қалдық
/// («на момент обновления») бөлек; әр түр жеке көрсетіледі. Сандар нақты операциялардан — журналмен келіседі (§18).
/// </summary>
public class BonusReportsService(
    IBonusRepository repo,
    BonusAccess access,
    BonusMapper mapper,
    BonusSettingsService settingsService)
{
    private AppLanguage Lang => access.Lang;

    public static bool IsAccrued(BonusTransactionType t, int amount) =>
        t is BonusTransactionType.Accrual or BonusTransactionType.Birthday or BonusTransactionType.Promo || (t == BonusTransactionType.Adjustment && amount > 0);
    public static bool IsUsed(BonusTransactionType t) => t == BonusTransactionType.Redemption;
    public static bool IsReturned(BonusTransactionType t) => t == BonusTransactionType.ReturnRestore;
    public static bool IsExpired(BonusTransactionType t) => t == BonusTransactionType.Expiration;

    private record Totals(int Accrued, int Used, int Returned, int Expired, int Corrections);

    private static Totals Sum(IEnumerable<BonusFlowRow> rows)
    {
        int a = 0, u = 0, r = 0, e = 0, c = 0;
        foreach (var x in rows)
        {
            if (IsAccrued(x.Type, x.Amount)) a += x.Amount;
            else if (IsUsed(x.Type)) u -= x.Amount;
            else if (IsReturned(x.Type)) r += x.Amount;
            else if (IsExpired(x.Type)) e -= x.Amount;
            else c += x.Amount;
        }
        return new Totals(a, u, r, e, c);
    }

    private static decimal? Change(decimal cur, decimal? prev) => prev is null or 0 ? null : Math.Round((cur - prev.Value) / Math.Abs(prev.Value) * 100, 1);

    private static List<BonusDayDto> Days(BonusCtx c, IReadOnlyList<BonusFlowRow> flows)
    {
        var byDay = flows.GroupBy(f => c.LocalDate(f.CreatedAt)).ToDictionary(g => g.Key, g => Sum(g));
        var list = new List<BonusDayDto>();
        for (var d = c.Start; d <= c.End; d = d.AddDays(1))
        {
            var t = byDay.GetValueOrDefault(d) ?? new Totals(0, 0, 0, 0, 0);
            list.Add(new BonusDayDto(d, t.Accrued, t.Used, t.Returned, t.Expired));
        }
        return list;
    }

    private async Task<List<StatusShareDto>> StatusesAsync(BonusCtx c, IReadOnlyList<LoyaltyStatus> statuses, IReadOnlyList<(Guid CardId, Guid CustomerId, int Balance, Guid? StatusId)> cards, CancellationToken ct)
    {
        var total = cards.Count;
        var groups = cards.GroupBy(x => x.StatusId).Select(g =>
        {
            var s = g.Key is { } id ? statuses.FirstOrDefault(x => x.Id == id) : null;
            return new StatusShareDto(g.Key, s?.Name ?? (Lang == AppLanguage.Ru ? "Без статуса" : "Мәртебесіз"), s?.Color ?? "#94A3B8", g.Count(),
                total == 0 ? 0 : Math.Round(g.Count() * 100m / total, 1));
        }).ToList();
        foreach (var s in statuses.Where(s => s.IsActive && groups.All(g => g.StatusId != s.Id))) groups.Add(new StatusShareDto(s.Id, s.Name, s.Color, 0, 0));
        await Task.CompletedTask;
        return groups.OrderBy(g => statuses.FirstOrDefault(s => s.Id == g.StatusId)?.SortOrder ?? 99).ToList();
    }

    public async Task<BonusOverviewDto> OverviewAsync(string? storeIds, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.BonusView, ct, storeIds, from, to);
        var (statuses, settings) = await access.ProgramAsync(c, ct);
        var flows = await repo.FlowsAsync(c.StoreIds, c.FromUtc, c.ToUtc, ct);
        var length = c.ToUtc - c.FromUtc;
        var prev = Sum(await repo.FlowsAsync(c.StoreIds, c.FromUtc - length, c.FromUtc, ct));
        var cur = Sum(flows);
        var cards = await repo.BalancesAsync(c.StoreIds, ct);
        var withBonus = cards.Count(x => x.Balance > 0);
        var balance = cards.Sum(x => x.Balance);
        var kpis = new List<BonusKpiDto>
        {
            new("customers", cards.Count, null, null, true),
            new("withBonus", withBonus, null, null, true),
            new("accrued", cur.Accrued, prev.Accrued, Change(cur.Accrued, prev.Accrued), false),
            new("used", cur.Used, prev.Used, Change(cur.Used, prev.Used), false),
            new("returned", cur.Returned, prev.Returned, Change(cur.Returned, prev.Returned), false),
            new("expired", cur.Expired, prev.Expired, Change(cur.Expired, prev.Expired), false),
            new("balance", balance, null, null, true),
        };
        var expiring = await settingsService.ExpiringAsync(30, null, null, null, null, storeIds, 1, 8, ct);
        var (birthdays, _) = await settingsService.UpcomingAsync(c, statuses, settings, 7, ct);
        var recent = await mapper.OperationsAsync(await repo.RecentAsync(c.StoreIds, 8, ct), c.Can.Contacts, ct);
        return new BonusOverviewDto(c.Start, c.End, DateTime.UtcNow, kpis, Days(c, flows), await StatusesAsync(c, statuses, cards, ct), expiring.Items,
            settings.ExpiryEnabled, birthdays.Take(8).Select(b => b.Dto).ToList(), BonusBirthdays.Enabled(settings), recent);
    }

    public async Task<BonusStatisticsDto> StatisticsAsync(string? storeIds, DateOnly? from, DateOnly? to, Guid? statusId, string? type, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.BonusView, ct, storeIds, from, to);
        var (statuses, _) = await access.ProgramAsync(c, ct);
        var cards = await repo.BalancesAsync(c.StoreIds, ct);
        var cardStatus = cards.ToDictionary(x => x.CardId, x => x.StatusId);
        var types = BonusOperationsService.ParseTypes(type, null);
        bool Keep(BonusFlowRow f) => (statusId is null || cardStatus.GetValueOrDefault(f.CardId) == statusId) && (types is null || types.Contains(f.Type));
        var flows = (await repo.FlowsAsync(c.StoreIds, c.FromUtc, c.ToUtc, ct)).Where(Keep).ToList();
        var length = c.ToUtc - c.FromUtc;
        var prevRows = (await repo.FlowsAsync(c.StoreIds, c.FromUtc - length, c.FromUtc, ct)).Where(Keep).ToList();
        var cur = Sum(flows);
        var prev = Sum(prevRows);
        var scoped = statusId is null ? cards : cards.Where(x => x.StatusId == statusId).ToList();
        var positive = scoped.Where(x => x.Balance > 0).ToList();
        var balance = positive.Sum(x => x.Balance);
        // Қатысушы — кемінде бір бонус операциясы бар карта (әдістемесі экранда жазылған).
        var participants = (await repo.LastOperationAsync(scoped.Select(x => x.CardId).ToList(), ct)).Keys.ToHashSet();
        var kpis = new List<BonusKpiDto>
        {
            new("accrued", cur.Accrued, prev.Accrued, Change(cur.Accrued, prev.Accrued), false),
            new("used", cur.Used, prev.Used, Change(cur.Used, prev.Used), false),
            new("returned", cur.Returned, prev.Returned, Change(cur.Returned, prev.Returned), false),
            new("expired", cur.Expired, prev.Expired, Change(cur.Expired, prev.Expired), false),
            new("balance", balance, null, null, true),
            new("withBonus", positive.Count, null, null, true),
            new("averageBalance", positive.Count == 0 ? 0 : Math.Round((decimal)balance / positive.Count, 0), null, null, true),
            new("usageRate", cur.Accrued == 0 ? null : Math.Round(cur.Used * 100m / cur.Accrued, 1), prev.Accrued == 0 ? null : Math.Round(prev.Used * 100m / prev.Accrued, 1), null, false,
                cur.Accrued == 0 ? "noAccrual" : null),
            new("participation", scoped.Count == 0 ? 0 : Math.Round(scoped.Count(x => participants.Contains(x.CardId)) * 100m / scoped.Count, 1), null, null, true, "participationMethod"),
        };
        var typeTotals = flows.GroupBy(f => f.Type == BonusTransactionType.Adjustment ? (f.Amount > 0 ? "AdjustmentPlus" : "AdjustmentMinus") : f.Type.ToString())
            .Select(g => new TypeTotalDto(g.Key, g.Sum(x => x.Amount), g.Count())).OrderByDescending(t => Math.Abs(t.Amount)).ToList();

        var customers = new Dictionary<Guid, BonusCustomerRow>();
        foreach (var id in c.StoreIds) foreach (var r in await repo.AllCustomersAsync(id, ct)) customers[r.Card.Id] = r;
        var last = await repo.LastOperationAsync(participants.ToList(), ct);
        TopCustomerDto Top(int place, Guid cardId, int value, int returned)
        {
            var row = customers.GetValueOrDefault(cardId);
            var (name, color) = row is null ? ("—", "#94A3B8") : BonusAccess.StatusOf(row.Card.StatusId, row.Card.Level, statuses, Lang);
            return new TopCustomerDto(place, row?.Card.CustomerId ?? Guid.Empty, row is null ? "—" : BonusAccess.FullName(row.FirstName, row.LastName), name, color, value, returned,
                last.TryGetValue(cardId, out var l) ? l : null);
        }
        var topBalance = scoped.Where(x => x.Balance > 0).OrderByDescending(x => x.Balance).Take(10).Select((x, i) => Top(i + 1, x.CardId, x.Balance, 0)).ToList();
        var usage = flows.Where(f => f.Type is BonusTransactionType.Redemption or BonusTransactionType.ReturnRestore).GroupBy(f => f.CardId)
            .Select(g => (Card: g.Key, Used: -g.Where(x => x.Type == BonusTransactionType.Redemption).Sum(x => x.Amount), Returned: g.Where(x => x.Type == BonusTransactionType.ReturnRestore).Sum(x => x.Amount)))
            .Where(x => x.Used > 0).OrderByDescending(x => x.Used).Take(10).ToList();
        var topUsage = usage.Select((x, i) => Top(i + 1, x.Card, x.Used, x.Returned)).ToList();

        var (campaigns, _) = await repo.CampaignsAsync(c.Store.Id, null, null, 0, 200, ct);
        var effects = new List<CampaignEffectDto>();
        foreach (var k in campaigns.Where(k => k.StartedAt is { } s && s >= c.FromUtc && s < c.ToUtc))
        {
            var read = await repo.ReadCountAsync(k.Id, ct);
            var bonus = flows.Where(f => f.SourceId == k.Id && f.Type == BonusTransactionType.Promo).Sum(f => f.Amount);
            effects.Add(new CampaignEffectDto(k.Id, k.Name, k.StartedAt, k.RecipientsCount, k.SentCount, read, k.SentCount == 0 ? null : Math.Round(read * 100m / k.SentCount, 1), bonus));
        }
        var now = DateTime.UtcNow;
        return new BonusStatisticsDto(c.Start, c.End, now, kpis, Days(c, flows), typeTotals, await StatusesAsync(c, statuses, scoped, ct), topBalance, topUsage, effects,
            cur.Expired, await repo.ExpiringSumAsync(c.StoreIds, now, now.AddDays(30), ct), await repo.ExpiringSumAsync(c.StoreIds, now, now.AddDays(60), ct),
            await repo.ExpiringSumAsync(c.StoreIds, now, now.AddDays(90), ct));
    }
}
