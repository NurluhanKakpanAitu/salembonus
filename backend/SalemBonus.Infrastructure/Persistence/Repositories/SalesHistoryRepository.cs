using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SalemBonus.Application.Common;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Domain.Pos.Sales;

namespace SalemBonus.Infrastructure.Persistence.Repositories;

/// <summary>«Продажи» бөлімі: серверлік сүзгі, сұрыптау, беттеу және жиынтық — бәрі бір сұраныс құрылысынан.</summary>
public class SalesHistoryRepository(AppDbContext db) : ISalesHistoryRepository
{
    private static readonly Regex NumberLike = new(@"^\D{0,4}\d+$", RegexOptions.Compiled);

    private IQueryable<Sale> Query(SalesHistoryFilter f)
    {
        var q = db.Sales.AsNoTracking().Where(s => s.StoreId == f.StoreId);
        if (f.FromUtc is { } from) q = q.Where(s => s.CreatedAt >= from);
        if (f.ToUtc is { } to) q = q.Where(s => s.CreatedAt < to);
        if (f.RegisterId is { } register) q = q.Where(s => s.RegisterId == register);
        if (f.StaffUserId is { } staff) q = q.Where(s => s.StaffUserId == staff);
        if (f.Method is { } method) q = q.Where(s => s.Payments.Any(p => p.Method == method));
        if (f.CustomerId is { } customer) q = q.Where(s => s.CustomerId == customer);
        else if (f.NoCustomer) q = q.Where(s => s.CustomerId == null);
        if (f.HasReturn is { } hasReturn)
            q = hasReturn ? q.Where(s => s.Status != SaleStatus.Completed) : q.Where(s => s.Status == SaleStatus.Completed);
        switch (f.Status)
        {
            case "Completed":
                q = q.Where(s => s.Status == SaleStatus.Completed && !db.Debts.Any(d => d.SaleId == s.Id && d.Status == DebtStatus.Open));
                break;
            case "PartiallyReturned": q = q.Where(s => s.Status == SaleStatus.PartiallyReturned); break;
            case "Returned": q = q.Where(s => s.Status == SaleStatus.Returned); break;
            case "Debt": q = q.Where(s => db.Debts.Any(d => d.SaleId == s.Id && d.Status == DebtStatus.Open)); break;
        }
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var term = f.Search.Trim();
            var digits = new string(term.Where(char.IsDigit).ToArray());
            var like = $"%{term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
            // «1284», «№1284», «ЧК-001284» — чек нөмірі; тек сандардан тұратын 4+ сан — телефонның бөлігі де
            // (әріп аралас мәтіндегі сандар, мысалы артикул «ART-3917», телефонмен салыстырылмайды).
            var numeric = NumberLike.IsMatch(term.Replace(" ", ""));
            long.TryParse(digits, out var number);
            var phone = PhoneSearch.Parse(term, 4);
            var (phoneDigits, phonePrefix) = (phone?.Digits, phone?.Prefix);
            q = q.Where(s =>
                (numeric && s.Number == number)
                || db.Customers.Any(c => c.Id == s.CustomerId
                    && (EF.Functions.ILike(c.FirstName + " " + c.LastName, like, "\\")
                        || (phoneDigits != null && (c.Phone.Contains(phoneDigits) || (phonePrefix != null && c.Phone.StartsWith(phonePrefix))))))
                || db.SaleItems.Any(i => i.SaleId == s.Id
                    && (EF.Functions.ILike(i.Name, like, "\\")
                        || (i.Article != null && EF.Functions.ILike(i.Article, like, "\\"))
                        || db.ProductBarcodes.Any(b => b.ProductId == i.ProductId && b.Barcode == term))));
        }
        return q;
    }

    private static IOrderedQueryable<Sale> Sort(IQueryable<Sale> q, string? sort, bool desc) => (sort, desc) switch
    {
        ("number", true) => q.OrderByDescending(s => s.Number),
        ("number", false) => q.OrderBy(s => s.Number),
        ("total", true) => q.OrderByDescending(s => s.Total).ThenByDescending(s => s.Number),
        ("total", false) => q.OrderBy(s => s.Total).ThenBy(s => s.Number),
        ("returned", true) => q.OrderByDescending(s => s.ReturnedAmount).ThenByDescending(s => s.Number),
        ("returned", false) => q.OrderBy(s => s.ReturnedAmount).ThenBy(s => s.Number),
        ("net", true) => q.OrderByDescending(s => s.Total - s.ReturnedAmount).ThenByDescending(s => s.Number),
        ("net", false) => q.OrderBy(s => s.Total - s.ReturnedAmount).ThenBy(s => s.Number),
        ("status", true) => q.OrderByDescending(s => s.Status).ThenByDescending(s => s.Number),
        ("status", false) => q.OrderBy(s => s.Status).ThenBy(s => s.Number),
        (_, false) => q.OrderBy(s => s.CreatedAt).ThenBy(s => s.Number),
        _ => q.OrderByDescending(s => s.CreatedAt).ThenByDescending(s => s.Number),
    };

    public async Task<(IReadOnlyList<Sale> Items, int Total)> SearchAsync(SalesHistoryFilter f, CancellationToken ct = default)
    {
        var q = Query(f);
        var total = await q.CountAsync(ct);
        var items = await Sort(q, f.Sort, f.Desc).Skip(f.Skip).Take(f.Take).Include(s => s.Payments).ToListAsync(ct);
        return (items, total);
    }

    public async Task<SalesHistoryStats> StatsAsync(SalesHistoryFilter f, CancellationToken ct = default)
    {
        var q = Query(f);
        var ids = q.Select(s => s.Id);
        var agg = await q.GroupBy(_ => 1).Select(g => new
        {
            Count = g.Count(),
            Total = g.Sum(s => s.Total),
            Returned = g.Sum(s => s.ReturnedAmount),
            Discounts = g.Sum(s => s.DiscountAmount),
            DiscountCount = g.Sum(s => s.DiscountAmount > 0 ? 1 : 0),
            Bonus = g.Sum(s => s.BonusRedeemed),
            BonusCount = g.Sum(s => s.BonusRedeemed > 0 ? 1 : 0),
            WithReturn = g.Sum(s => s.Status != SaleStatus.Completed ? 1 : 0),
        }).FirstOrDefaultAsync(ct);
        if (agg is null) return new SalesHistoryStats(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        var units = await db.SaleItems.Where(i => ids.Contains(i.SaleId)).SumAsync(i => (decimal?)i.Quantity, ct) ?? 0;
        var debt = await db.Debts.Where(d => d.SaleId != null && ids.Contains(d.SaleId.Value)).GroupBy(_ => 1).Select(g => new
        {
            Count = g.Count(),
            Amount = g.Sum(d => d.Amount),
            Open = g.Sum(d => d.Status == DebtStatus.Open ? d.Amount - d.Paid : 0),
        }).FirstOrDefaultAsync(ct);
        return new SalesHistoryStats(agg.Count, agg.Total, agg.Returned, units, agg.Discounts, agg.DiscountCount, agg.Bonus, agg.BonusCount,
            agg.WithReturn, debt?.Count ?? 0, debt?.Amount ?? 0, debt?.Open ?? 0);
    }

    public async Task<ReturnsStats> ReturnsStatsAsync(Guid storeId, DateTime? fromUtc, DateTime? toUtc, Guid? registerId, Guid? staffUserId,
        CancellationToken ct = default)
    {
        var q = db.SaleReturns.AsNoTracking().Where(r => r.StoreId == storeId);
        if (fromUtc is { } from) q = q.Where(r => r.CreatedAt >= from);
        if (toUtc is { } to) q = q.Where(r => r.CreatedAt < to);
        if (registerId is { } register) q = q.Where(r => r.RegisterId == register);
        if (staffUserId is { } staff) q = q.Where(r => r.StaffUserId == staff);
        var agg = await q.GroupBy(_ => 1).Select(g => new { Count = g.Count(), Amount = g.Sum(r => r.Amount) }).FirstOrDefaultAsync(ct);
        return new ReturnsStats(agg?.Count ?? 0, agg?.Amount ?? 0);
    }

    public async Task<Dictionary<Guid, decimal>> UnitsBySaleAsync(IReadOnlyCollection<Guid> saleIds, CancellationToken ct = default) =>
        saleIds.Count == 0 ? [] : await db.SaleItems.Where(i => saleIds.Contains(i.SaleId))
            .GroupBy(i => i.SaleId).Select(g => new { g.Key, Units = g.Sum(i => i.Quantity) })
            .ToDictionaryAsync(x => x.Key, x => x.Units, ct);

    public async Task<Dictionary<Guid, OpenDebtInfo>> DebtsBySaleAsync(IReadOnlyCollection<Guid> saleIds, CancellationToken ct = default) =>
        saleIds.Count == 0 ? [] : await db.Debts.AsNoTracking().Where(d => d.SaleId != null && saleIds.Contains(d.SaleId.Value))
            .Select(d => new { SaleId = d.SaleId!.Value, d.Status, Remaining = d.Amount - d.Paid })
            .ToDictionaryAsync(x => x.SaleId, x => new OpenDebtInfo(x.Status, x.Remaining), ct);

    public async Task<IReadOnlyList<DebtPayment>> DebtPaymentsBySaleAsync(Guid saleId, CancellationToken ct = default) =>
        await db.DebtPayments.AsNoTracking()
            .Where(p => db.Debts.Any(d => d.Id == p.DebtId && d.SaleId == saleId))
            .OrderBy(p => p.CreatedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<(Guid Id, string Name)>> StoreStaffAsync(Guid storeId, CancellationToken ct = default) =>
        (await db.StoreMemberships.AsNoTracking()
            .Where(m => m.StoreId == storeId && m.IsActive)
            .Join(db.StaffUsers, m => m.StaffUserId, u => u.Id, (m, u) => new { u.Id, u.FirstName, u.LastName })
            .OrderBy(x => x.FirstName).ThenBy(x => x.LastName)
            .ToListAsync(ct))
        .Select(x => (x.Id, $"{x.FirstName} {x.LastName}".Trim())).ToList();
}
