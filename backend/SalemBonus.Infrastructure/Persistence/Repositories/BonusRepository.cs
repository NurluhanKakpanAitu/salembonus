using Microsoft.EntityFrameworkCore;
using SalemBonus.Application.Common;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Domain.Entities;
using SalemBonus.Domain.Enums;
using SalemBonus.Domain.Pos.Bonus;
using SalemBonus.Domain.Pos.Sales;

namespace SalemBonus.Infrastructure.Persistence.Repositories;

public class BonusRepository(AppDbContext db) : IBonusRepository
{
    // EF позициялық конструктор арқылы проекциядан кейін сүзе алмайды — сүзу/сұрыптау осы кластарда, соңында record-қа.
    private sealed class CustomerQ { public BonusCard Card { get; init; } = null!; public string FirstName { get; init; } = ""; public string LastName { get; init; } = ""; public string Phone { get; init; } = ""; public DateOnly? BirthDate { get; init; } public string QrCode { get; init; } = ""; public DateTime RegisteredAt { get; init; } public DateTime? LastOperationAt { get; init; } public BonusCustomerRow Row() => new(Card, FirstName, LastName, Phone, BirthDate, QrCode, RegisteredAt, LastOperationAt); }
    private sealed class OpQ { public BonusTransaction Tx { get; init; } = null!; public Guid StoreId { get; init; } public Guid CustomerId { get; init; } public string FirstName { get; init; } = ""; public string LastName { get; init; } = ""; public string Phone { get; init; } = ""; public string QrCode { get; init; } = ""; public BonusOperationRow Row() => new(Tx, StoreId, CustomerId, FirstName, LastName, Phone, QrCode); }
    private sealed class LotQ { public BonusTransaction Lot { get; init; } = null!; public Guid StoreId { get; init; } public Guid CustomerId { get; init; } public string FirstName { get; init; } = ""; public string LastName { get; init; } = ""; public string Phone { get; init; } = ""; public Guid? StatusId { get; init; } public BonusLotRow Row() => new(Lot, StoreId, CustomerId, FirstName, LastName, Phone, StatusId); }

    /// <summary>Партия бола алатын (оң, қалдығы жүретін) операция түрлері.</summary>
    private static readonly BonusTransactionType[] LotTypes =
    [
        BonusTransactionType.Accrual, BonusTransactionType.Birthday, BonusTransactionType.Promo, BonusTransactionType.ReturnRestore,
        BonusTransactionType.Adjustment, BonusTransactionType.Reversal,
    ];

    // ---------- Мәртебелер мен баптаулар ----------

    public async Task<IReadOnlyList<LoyaltyStatus>> StatusesAsync(Guid storeId, CancellationToken ct = default) =>
        await db.LoyaltyStatuses.Where(s => s.StoreId == storeId).OrderBy(s => s.SortOrder).ThenBy(s => s.CreatedAt).ToListAsync(ct);

    public Task<LoyaltyStatus?> GetStatusAsync(Guid storeId, Guid id, CancellationToken ct = default) =>
        db.LoyaltyStatuses.FirstOrDefaultAsync(s => s.StoreId == storeId && s.Id == id, ct);

    public async Task<bool> StatusNameExistsAsync(Guid storeId, string name, Guid? excludeId, CancellationToken ct = default)
    {
        // Кириллицаны базаның локалі әрқашан кіші әріпке түсірмейді — салыстыру клиент жағында.
        var names = await db.LoyaltyStatuses.Where(s => s.StoreId == storeId && (excludeId == null || s.Id != excludeId)).Select(s => s.Name).ToListAsync(ct);
        return names.Any(n => string.Equals(n.Trim(), name.Trim(), StringComparison.CurrentCultureIgnoreCase));
    }

    public async Task<Dictionary<Guid, int>> StatusUsageAsync(Guid storeId, CancellationToken ct = default) =>
        await db.BonusCards.Where(c => c.StoreId == storeId && c.StatusId != null).GroupBy(c => c.StatusId!.Value)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);

    public void AddStatus(LoyaltyStatus status) => db.LoyaltyStatuses.Add(status);
    public void RemoveStatus(LoyaltyStatus status) => db.LoyaltyStatuses.Remove(status);

    public async Task AssignStatusByLevelAsync(Guid storeId, CustomerLevel level, Guid statusId, CancellationToken ct = default) =>
        await db.BonusCards.Where(c => c.StoreId == storeId && c.StatusId == null && c.Level == level)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.StatusId, statusId), ct);

    public Task<BonusSettings?> SettingsAsync(Guid storeId, CancellationToken ct = default) =>
        db.BonusSettings.FirstOrDefaultAsync(s => s.StoreId == storeId, ct);

    public void AddSettings(BonusSettings settings) => db.BonusSettings.Add(settings);

    public async Task<IReadOnlyList<BonusSettings>> AllSettingsAsync(CancellationToken ct = default) =>
        await db.BonusSettings.AsNoTracking().ToListAsync(ct);

    public async Task SyncCardLevelsAsync(Guid storeId, Guid statusId, CustomerLevel level, CancellationToken ct = default) =>
        await db.BonusCards.Where(c => c.StoreId == storeId && c.StatusId == statusId && c.Level != level).ExecuteUpdateAsync(s => s.SetProperty(c => c.Level, level), ct);

    public async Task ReplaceLadderAsync(Guid storeId, IReadOnlyList<StoreLevel> levels, CancellationToken ct = default)
    {
        foreach (var tracked in db.ChangeTracker.Entries<StoreLevel>().Where(e => e.Entity.StoreId == storeId).ToList()) tracked.State = EntityState.Detached;
        await db.StoreLevels.Where(l => l.StoreId == storeId).ExecuteDeleteAsync(ct);
        db.StoreLevels.AddRange(levels);
    }

    // ---------- Клиенттер ----------

    private IQueryable<CustomerQ> CustomerRows(Guid storeId) =>
        from c in db.BonusCards.AsNoTracking()
        join u in db.Customers.AsNoTracking() on c.CustomerId equals u.Id
        where c.StoreId == storeId
        select new CustomerQ
        {
            Card = c, FirstName = u.FirstName, LastName = u.LastName, Phone = u.Phone, BirthDate = u.BirthDate, QrCode = u.QrCode, RegisteredAt = c.CreatedAt,
            LastOperationAt = db.BonusTransactions.Where(t => t.BonusCardId == c.Id).Max(t => (DateTime?)t.CreatedAt),
        };

    public async Task<(IReadOnlyList<BonusCustomerRow> Rows, int Total)> CustomersAsync(BonusCustomerFilter f, CancellationToken ct = default)
    {
        var q = CustomerRows(f.StoreId);
        if (f.StatusId is { } sid) q = q.Where(r => r.Card.StatusId == sid);
        if (f.PositiveOnly) q = q.Where(r => r.Card.Balance > 0);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = f.Search.Trim();
            var phone = PhoneSearch.Parse(s);
            var (phoneDigits, phonePrefix) = (phone?.Digits, phone?.Prefix);
            var like = $"%{s}%";
            q = q.Where(r => EF.Functions.ILike(r.FirstName + " " + r.LastName, like) || EF.Functions.ILike(r.QrCode, like)
                || (phoneDigits != null && (r.Phone.Contains(phoneDigits) || (phonePrefix != null && r.Phone.StartsWith(phonePrefix)))));
        }
        var total = await q.CountAsync(ct);
        q = (f.Sort, f.Desc) switch
        {
            ("name", false) => q.OrderBy(r => r.FirstName).ThenBy(r => r.LastName),
            ("name", true) => q.OrderByDescending(r => r.FirstName).ThenByDescending(r => r.LastName),
            ("spent", false) => q.OrderBy(r => r.Card.TotalSpent),
            ("spent", true) => q.OrderByDescending(r => r.Card.TotalSpent),
            ("registered", false) => q.OrderBy(r => r.RegisteredAt),
            ("registered", true) => q.OrderByDescending(r => r.RegisteredAt),
            (_, false) => q.OrderBy(r => r.Card.Balance),
            _ => q.OrderByDescending(r => r.Card.Balance).ThenBy(r => r.FirstName),
        };
        return ((await q.Skip(f.Skip).Take(f.Take).ToListAsync(ct)).Select(x => x.Row()).ToList(), total);
    }

    public async Task<IReadOnlyList<BonusCustomerRow>> AllCustomersAsync(Guid storeId, CancellationToken ct = default) =>
        await (from c in db.BonusCards.AsNoTracking()
               join u in db.Customers.AsNoTracking() on c.CustomerId equals u.Id
               where c.StoreId == storeId
               select new BonusCustomerRow(c, u.FirstName, u.LastName, u.Phone, u.BirthDate, u.QrCode, c.CreatedAt, null)).ToListAsync(ct);

    public async Task<BonusCustomerRow?> CustomerAsync(Guid storeId, Guid customerId, CancellationToken ct = default) =>
        (await CustomerRows(storeId).FirstOrDefaultAsync(r => r.Card.CustomerId == customerId, ct))?.Row();

    /// <summary>Картаны бұғаттайды (транзакция ішінде): екі касса бір балансты екі рет жұмсай алмайды (§11.2).</summary>
    public async Task<BonusCard?> CardForUpdateAsync(Guid storeId, Guid customerId, CancellationToken ct = default)
    {
        await db.Database.ExecuteSqlAsync($"""SELECT 1 FROM "BonusCards" WHERE "StoreId" = {storeId} AND "CustomerId" = {customerId} FOR UPDATE""", ct);
        return await db.BonusCards.Include(c => c.Store).FirstOrDefaultAsync(c => c.StoreId == storeId && c.CustomerId == customerId, ct);
    }

    public async Task<BonusCard?> CardByIdForUpdateAsync(Guid cardId, CancellationToken ct = default)
    {
        await db.Database.ExecuteSqlAsync($"""SELECT 1 FROM "BonusCards" WHERE "Id" = {cardId} FOR UPDATE""", ct);
        return await db.BonusCards.Include(c => c.Store).FirstOrDefaultAsync(c => c.Id == cardId, ct);
    }

    public async Task<IReadOnlyList<BonusCard>> CardsDueForReviewAsync(DateTime now, int take, CancellationToken ct = default) =>
        await db.BonusCards.Include(c => c.Store)
            .Where(c => c.StatusId != null && !c.StatusManual && c.StatusExpiresAt != null && c.StatusExpiresAt <= now)
            .OrderBy(c => c.StatusExpiresAt).Take(take).ToListAsync(ct);

    private IQueryable<Sale> SalesOf(Guid storeId, DateTime? fromUtc)
    {
        var q = db.Sales.AsNoTracking().Where(s => s.StoreId == storeId && s.CustomerId != null);
        if (fromUtc is { } from) q = q.Where(s => s.CreatedAt >= from);
        return q;
    }

    public async Task<PurchaseStats> PurchaseStatsAsync(Guid storeId, Guid customerId, DateTime? fromUtc, CancellationToken ct = default) =>
        (await PurchaseStatsBulkAsync(storeId, [customerId], fromUtc, ct)).GetValueOrDefault(customerId) ?? PurchaseStats.Empty;

    public async Task<Dictionary<Guid, PurchaseStats>> PurchaseStatsBulkAsync(Guid storeId, IReadOnlyCollection<Guid> customerIds, DateTime? fromUtc, CancellationToken ct = default)
    {
        if (customerIds.Count == 0) return [];
        var rows = await SalesOf(storeId, fromUtc).Where(s => customerIds.Contains(s.CustomerId!.Value))
            .GroupBy(s => s.CustomerId!.Value)
            .Select(g => new
            {
                g.Key,
                Gross = g.Sum(s => s.Total),
                Returned = g.Sum(s => s.ReturnedAmount),
                Count = g.Count(),
                NotReturned = g.Count(s => s.Status != SaleStatus.Returned),
                Last = g.Max(s => s.CreatedAt),
            }).ToListAsync(ct);
        return rows.ToDictionary(r => r.Key, r => new PurchaseStats(r.Gross, r.Returned, r.Count, r.NotReturned, r.Last));
    }

    public Task<int> CustomersCountAsync(IReadOnlyCollection<Guid> storeIds, CancellationToken ct = default) =>
        db.BonusCards.CountAsync(c => storeIds.Contains(c.StoreId), ct);

    public async Task<IReadOnlyList<(Guid CardId, Guid CustomerId, int Balance, Guid? StatusId)>> BalancesAsync(IReadOnlyCollection<Guid> storeIds, CancellationToken ct = default) =>
        (await db.BonusCards.AsNoTracking().Where(c => storeIds.Contains(c.StoreId)).Select(c => new { c.Id, c.CustomerId, c.Balance, c.StatusId }).ToListAsync(ct))
        .Select(c => (c.Id, c.CustomerId, c.Balance, c.StatusId)).ToList();

    // ---------- Партиялар ----------

    public async Task<IReadOnlyList<BonusTransaction>> LotsAsync(Guid cardId, CancellationToken ct = default) =>
        await db.BonusTransactions.AsNoTracking().Where(t => t.BonusCardId == cardId && LotTypes.Contains(t.Type) && t.Amount > 0)
            .OrderByDescending(t => t.CreatedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<BonusTransaction>> OpenLotsForUpdateAsync(Guid cardId, CancellationToken ct = default) =>
        await db.BonusTransactions.Where(t => t.BonusCardId == cardId && t.Remaining > 0)
            .OrderBy(t => t.ExpiresAt == null).ThenBy(t => t.ExpiresAt).ThenBy(t => t.CreatedAt).ToListAsync(ct);

    public Task<BonusTransaction?> LotForUpdateAsync(Guid lotId, CancellationToken ct = default) =>
        db.BonusTransactions.FirstOrDefaultAsync(t => t.Id == lotId, ct);

    private IQueryable<LotQ> LotRows(IReadOnlyCollection<Guid> storeIds) =>
        from t in db.BonusTransactions.AsNoTracking()
        join c in db.BonusCards.AsNoTracking() on t.BonusCardId equals c.Id
        join u in db.Customers.AsNoTracking() on c.CustomerId equals u.Id
        where storeIds.Contains(c.StoreId) && t.Remaining > 0 && t.ExpiresAt != null
        select new LotQ { Lot = t, StoreId = c.StoreId, CustomerId = c.CustomerId, FirstName = u.FirstName, LastName = u.LastName, Phone = u.Phone, StatusId = c.StatusId };

    public async Task<(IReadOnlyList<BonusLotRow> Rows, int Total, int Sum)> ExpiringLotsAsync(IReadOnlyCollection<Guid> storeIds, DateTime fromUtc, DateTime toUtc,
        string? search, Guid? statusId, int skip, int take, CancellationToken ct = default)
    {
        var q = LotRows(storeIds).Where(r => r.Lot.ExpiresAt >= fromUtc && r.Lot.ExpiresAt < toUtc);
        if (statusId is { } sid) q = q.Where(r => r.StatusId == sid);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var like = $"%{search.Trim()}%";
            var phone = PhoneSearch.Parse(search);
            var (phoneDigits, phonePrefix) = (phone?.Digits, phone?.Prefix);
            q = q.Where(r => EF.Functions.ILike(r.FirstName + " " + r.LastName, like)
                || (phoneDigits != null && (r.Phone.Contains(phoneDigits) || (phonePrefix != null && r.Phone.StartsWith(phonePrefix)))));
        }
        var total = await q.CountAsync(ct);
        var sum = await q.SumAsync(r => (int?)r.Lot.Remaining, ct) ?? 0;
        return ((await q.OrderBy(r => r.Lot.ExpiresAt).Skip(skip).Take(take).ToListAsync(ct)).Select(x => x.Row()).ToList(), total, sum);
    }

    public async Task<int> ExpiringSumAsync(IReadOnlyCollection<Guid> storeIds, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default) =>
        await LotRows(storeIds).Where(r => r.Lot.ExpiresAt >= fromUtc && r.Lot.ExpiresAt < toUtc).SumAsync(r => (int?)r.Lot.Remaining, ct) ?? 0;

    public async Task<IReadOnlyList<BonusLotRow>> WarnableLotsAsync(Guid storeId, DateTime nowUtc, DateTime untilUtc, CancellationToken ct = default) =>
        (await LotRows([storeId]).Where(r => r.Lot.ExpiresAt > nowUtc && r.Lot.ExpiresAt <= untilUtc).ToListAsync(ct)).Select(x => x.Row()).ToList();

    public async Task<HashSet<(Guid LotId, int Days)>> WarningsAsync(IReadOnlyCollection<Guid> lotIds, CancellationToken ct = default) =>
        lotIds.Count == 0 ? [] : (await db.ExpiryWarnings.AsNoTracking().Where(w => lotIds.Contains(w.LotId)).Select(w => new { w.LotId, w.DaysBefore }).ToListAsync(ct))
            .Select(w => (w.LotId, w.DaysBefore)).ToHashSet();

    public void AddWarning(ExpiryWarning warning) => db.ExpiryWarnings.Add(warning);

    public async Task<IReadOnlyList<BonusTransaction>> OpenLotsOfStoreAsync(Guid storeId, CancellationToken ct = default) =>
        await (from t in db.BonusTransactions
               join c in db.BonusCards on t.BonusCardId equals c.Id
               where c.StoreId == storeId && t.Remaining > 0
               select t).ToListAsync(ct);

    // ---------- Операциялар ----------

    private IQueryable<OpQ> OperationRows() =>
        from t in db.BonusTransactions.AsNoTracking()
        join c in db.BonusCards.AsNoTracking() on t.BonusCardId equals c.Id
        join u in db.Customers.AsNoTracking() on c.CustomerId equals u.Id
        select new OpQ { Tx = t, StoreId = c.StoreId, CustomerId = c.CustomerId, FirstName = u.FirstName, LastName = u.LastName, Phone = u.Phone, QrCode = u.QrCode };

    public async Task<(IReadOnlyList<BonusOperationRow> Rows, int Total)> OperationsAsync(BonusOperationFilter f, CancellationToken ct = default)
    {
        var q = OperationRows().Where(r => f.StoreIds.Contains(r.StoreId));
        if (f.FromUtc is { } from) q = q.Where(r => r.Tx.CreatedAt >= from);
        if (f.ToUtc is { } to) q = q.Where(r => r.Tx.CreatedAt < to);
        if (f.Types is { Count: > 0 } types) q = q.Where(r => types.Contains(r.Tx.Type));
        if (f.Status == "reversed") q = q.Where(r => r.Tx.ReversedAt != null);
        else if (f.Status == "posted") q = q.Where(r => r.Tx.ReversedAt == null);
        if (f.StaffUserId is { } staff) q = q.Where(r => r.Tx.StaffUserId == staff);
        if (!string.IsNullOrWhiteSpace(f.Source)) q = q.Where(r => r.Tx.Source == f.Source);
        if (f.MinAmount is { } min) q = q.Where(r => (r.Tx.Amount >= 0 ? r.Tx.Amount : -r.Tx.Amount) >= min);
        if (f.MaxAmount is { } max) q = q.Where(r => (r.Tx.Amount >= 0 ? r.Tx.Amount : -r.Tx.Amount) <= max);
        if (f.CustomerId is { } cid) q = q.Where(r => r.CustomerId == cid);
        if (f.SourceId is { } srcId) q = q.Where(r => r.Tx.SourceId == srcId);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = f.Search.Trim().TrimStart('№', '#');
            if (Guid.TryParse(s, out var g)) q = q.Where(r => r.Tx.Id == g || r.Tx.ReceiptId == g);
            else
            {
                var digits = new string(s.Where(char.IsDigit).ToArray());
                var like = $"%{s}%";
                var phone = PhoneSearch.Parse(s, 4);
                var (phoneDigits, phonePrefix) = (phone?.Digits, phone?.Prefix);
                var saleIds = digits.Length is > 0 and <= 9 && long.TryParse(digits, out var number)
                    ? db.Sales.Where(x => f.StoreIds.Contains(x.StoreId) && x.Number == number).Select(x => (Guid?)x.Id)
                    : db.Sales.Where(_ => false).Select(x => (Guid?)x.Id);
                q = q.Where(r => EF.Functions.ILike(r.FirstName + " " + r.LastName, like) || EF.Functions.ILike(r.QrCode, like)
                    || (phoneDigits != null && (r.Phone.Contains(phoneDigits) || (phonePrefix != null && r.Phone.StartsWith(phonePrefix))))
                    || saleIds.Contains(r.Tx.ReceiptId));
            }
        }
        var total = await q.CountAsync(ct);
        return ((await q.OrderByDescending(r => r.Tx.CreatedAt).ThenByDescending(r => r.Tx.Id).Skip(f.Skip).Take(f.Take).ToListAsync(ct)).Select(x => x.Row()).ToList(), total);
    }

    public async Task<BonusOperationRow?> OperationAsync(Guid id, CancellationToken ct = default) =>
        (await OperationRows().FirstOrDefaultAsync(r => r.Tx.Id == id, ct))?.Row();

    public Task<BonusTransaction?> OperationForUpdateAsync(Guid id, CancellationToken ct = default) =>
        db.BonusTransactions.FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<IReadOnlyList<BonusTransaction>> RelatedAsync(Guid id, CancellationToken ct = default) =>
        await db.BonusTransactions.AsNoTracking().Where(t => t.SourceTransactionId == id).OrderBy(t => t.CreatedAt).ToListAsync(ct);

    public async Task<int> RestoredForRedemptionAsync(BonusTransaction redemption, CancellationToken ct = default) =>
        await db.BonusTransactions.Where(t => t.BonusCardId == redemption.BonusCardId && t.Type == BonusTransactionType.ReturnRestore
                && (t.SourceTransactionId == redemption.Id || (t.SourceTransactionId == null && redemption.ReceiptId != null && t.ReceiptId == redemption.ReceiptId)))
            .SumAsync(t => (int?)t.Amount, ct) ?? 0;

    public Task<BonusTransaction?> ByIdempotencyKeyAsync(Guid key, CancellationToken ct = default) =>
        db.BonusTransactions.AsNoTracking().FirstOrDefaultAsync(t => t.IdempotencyKey == key, ct);

    public async Task<IReadOnlyList<BonusFlowRow>> FlowsAsync(IReadOnlyCollection<Guid> storeIds, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default) =>
        await (from t in db.BonusTransactions.AsNoTracking()
               join c in db.BonusCards.AsNoTracking() on t.BonusCardId equals c.Id
               where storeIds.Contains(c.StoreId) && t.CreatedAt >= fromUtc && t.CreatedAt < toUtc
               select new BonusFlowRow(t.CreatedAt, t.Type, t.Amount, c.Id, c.CustomerId, c.StoreId, t.Source, t.SourceId)).ToListAsync(ct);

    public async Task<IReadOnlyList<BonusOperationRow>> RecentAsync(IReadOnlyCollection<Guid> storeIds, int take, CancellationToken ct = default) =>
        (await OperationRows().Where(r => storeIds.Contains(r.StoreId)).OrderByDescending(r => r.Tx.CreatedAt).Take(take).ToListAsync(ct)).Select(x => x.Row()).ToList();

    public async Task<Dictionary<Guid, DateTime>> LastOperationAsync(IReadOnlyCollection<Guid> cardIds, CancellationToken ct = default) =>
        cardIds.Count == 0 ? [] : await db.BonusTransactions.AsNoTracking().Where(t => cardIds.Contains(t.BonusCardId))
            .GroupBy(t => t.BonusCardId).Select(g => new { g.Key, Last = g.Max(t => t.CreatedAt) }).ToDictionaryAsync(x => x.Key, x => x.Last, ct);

    // ---------- Туған күн ----------

    public async Task<IReadOnlyList<BirthdayGrant>> GrantsAsync(Guid storeId, IReadOnlyCollection<int> years, CancellationToken ct = default) =>
        await db.BirthdayGrants.AsNoTracking().Where(g => g.StoreId == storeId && years.Contains(g.Year)).ToListAsync(ct);

    public Task<BirthdayGrant?> GrantForUpdateAsync(Guid storeId, Guid customerId, int year, CancellationToken ct = default) =>
        db.BirthdayGrants.FirstOrDefaultAsync(g => g.StoreId == storeId && g.CustomerId == customerId && g.Year == year, ct);

    public void AddGrant(BirthdayGrant grant) => db.BirthdayGrants.Add(grant);

    // ---------- Таратулар ----------

    public async Task<(IReadOnlyList<BonusCampaign> Items, int Total)> CampaignsAsync(Guid storeId, string? status, string? search, int skip, int take, CancellationToken ct = default)
    {
        var q = db.BonusCampaigns.AsNoTracking().Where(c => c.StoreId == storeId);
        if (Enum.TryParse<CampaignStatus>(status, true, out var st)) q = q.Where(c => c.Status == st);
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(c => EF.Functions.ILike(c.Name, $"%{search.Trim()}%"));
        var total = await q.CountAsync(ct);
        return (await q.OrderByDescending(c => c.CreatedAt).Skip(skip).Take(take).ToListAsync(ct), total);
    }

    public Task<BonusCampaign?> CampaignAsync(Guid storeId, Guid id, CancellationToken ct = default) =>
        db.BonusCampaigns.FirstOrDefaultAsync(c => c.StoreId == storeId && c.Id == id, ct);

    public async Task<BonusCampaign?> CampaignForUpdateAsync(Guid id, CancellationToken ct = default) =>
        (await db.BonusCampaigns.FromSql($"""SELECT * FROM pos.bonus_campaigns WHERE "Id" = {id} FOR UPDATE""").ToListAsync(ct)).FirstOrDefault();

    public void AddCampaign(BonusCampaign campaign) => db.BonusCampaigns.Add(campaign);

    public async Task<IReadOnlyList<Guid>> DueCampaignIdsAsync(DateTime now, CancellationToken ct = default) =>
        await db.BonusCampaigns.AsNoTracking().Where(c => c.Status == CampaignStatus.Scheduled && c.ScheduledAt != null && c.ScheduledAt <= now)
            .OrderBy(c => c.ScheduledAt).Select(c => c.Id).Take(20).ToListAsync(ct);

    public async Task<(IReadOnlyList<CampaignRecipient> Items, int Total)> RecipientsAsync(Guid campaignId, string? status, int skip, int take, CancellationToken ct = default)
    {
        var q = db.CampaignRecipients.AsNoTracking().Where(r => r.CampaignId == campaignId);
        if (Enum.TryParse<RecipientStatus>(status, true, out var st)) q = q.Where(r => r.Status == st);
        var total = await q.CountAsync(ct);
        return (await q.OrderBy(r => r.Status).ThenBy(r => r.Contact).Skip(skip).Take(take).ToListAsync(ct), total);
    }

    public async Task<IReadOnlyList<CampaignRecipient>> RecipientsForUpdateAsync(Guid campaignId, RecipientStatus status, CancellationToken ct = default) =>
        await db.CampaignRecipients.Where(r => r.CampaignId == campaignId && r.Status == status).ToListAsync(ct);

    public Task<bool> HasRecipientsAsync(Guid campaignId, CancellationToken ct = default) =>
        db.CampaignRecipients.AnyAsync(r => r.CampaignId == campaignId, ct);

    public void AddRecipient(CampaignRecipient recipient) => db.CampaignRecipients.Add(recipient);

    public Task<int> ReadCountAsync(Guid campaignId, CancellationToken ct = default) =>
        (from r in db.CampaignRecipients
         join n in db.Notifications on r.NotificationId equals n.Id
         where r.CampaignId == campaignId && n.IsRead
         select r.Id).CountAsync(ct);

    public async Task<HashSet<Guid>> ReadNotificationsAsync(IReadOnlyCollection<Guid> notificationIds, CancellationToken ct = default) =>
        notificationIds.Count == 0 ? [] : (await db.Notifications.AsNoTracking().Where(n => notificationIds.Contains(n.Id) && n.IsRead).Select(n => n.Id).ToListAsync(ct)).ToHashSet();

    // ---------- Ережелер ----------

    public async Task<IReadOnlyList<BonusTerms>> TermsAsync(Guid storeId, CancellationToken ct = default) =>
        await db.BonusTerms.AsNoTracking().Where(t => t.StoreId == storeId).OrderByDescending(t => t.Version).ThenByDescending(t => t.UpdatedAt).ToListAsync(ct);

    public Task<BonusTerms?> TermsVersionAsync(Guid storeId, Guid id, CancellationToken ct = default) =>
        db.BonusTerms.AsNoTracking().FirstOrDefaultAsync(t => t.StoreId == storeId && t.Id == id, ct);

    public Task<BonusTerms?> DraftForUpdateAsync(Guid storeId, CancellationToken ct = default) =>
        db.BonusTerms.FirstOrDefaultAsync(t => t.StoreId == storeId && t.Status == TermsStatus.Draft, ct);

    public Task<BonusTerms?> PublishedForUpdateAsync(Guid storeId, CancellationToken ct = default) =>
        db.BonusTerms.FirstOrDefaultAsync(t => t.StoreId == storeId && t.Status == TermsStatus.Published, ct);

    public async Task<int> MaxTermsVersionAsync(Guid storeId, CancellationToken ct = default) =>
        await db.BonusTerms.Where(t => t.StoreId == storeId).MaxAsync(t => (int?)t.Version, ct) ?? 0;

    public void AddTerms(BonusTerms terms) => db.BonusTerms.Add(terms);

    // ---------- Атаулар ----------

    public async Task<Dictionary<Guid, string>> StaffNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
        ids.Count == 0 ? [] : await db.StaffUsers.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => (u.FirstName + " " + u.LastName).Trim(), ct);

    public async Task<Dictionary<Guid, long>> SaleNumbersAsync(IReadOnlyCollection<Guid> saleIds, CancellationToken ct = default) =>
        saleIds.Count == 0 ? [] : await db.Sales.AsNoTracking().Where(s => saleIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Number, ct);

    public async Task<Dictionary<Guid, string>> CampaignNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
        ids.Count == 0 ? [] : await db.BonusCampaigns.AsNoTracking().Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, ct);

    public async Task<Dictionary<Guid, string>> StoreNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
        ids.Count == 0 ? [] : await db.Stores.AsNoTracking().Where(s => ids.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, ct);

    public async Task<Guid?> SaleIdByNumberAsync(Guid storeId, long number, CancellationToken ct = default) =>
        await db.Sales.AsNoTracking().Where(s => s.StoreId == storeId && s.Number == number).Select(s => (Guid?)s.Id).FirstOrDefaultAsync(ct);
}
