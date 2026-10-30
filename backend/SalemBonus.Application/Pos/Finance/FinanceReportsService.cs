using SalemBonus.Application.Common;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Application.Pos.Catalog;
using SalemBonus.Application.Pos.Sales;
using SalemBonus.Domain.Pos.Finance;
using SalemBonus.Domain.Pos.Inventory;
using SalemBonus.Domain.Pos.Sales;

namespace SalemBonus.Application.Pos.Finance;

/// <summary>
/// Есептер (ТЗ «Финансы» §4–6, §10): шолу, P&amp;L, Cash Flow, тауар пайдасы. Формулалар §3:
/// таза түсім = сатылым (жеңілдіктен кейін) − қайтарым; COGS — сатылған сәттегі кіріс бағасы; валовая = таза түсім − COGS;
/// операциялық шығын — P&amp;L-ге жіктелген қолмен операциялар; ақша ағыны ≠ пайда; аударым мен инкассация — кіріс те, шығыс та емес.
/// Күшіндегі операция — Posted және сторно емес (сторноланған түпнұсқа да, кері жазба да есепке кірмейді).
/// </summary>
public class FinanceReportsService(
    IFinanceRepository repo,
    IAnalyticsRepository analytics,
    FinanceOperationsService operations,
    FinanceLedger ledger,
    CatalogAccess access,
    IUnitOfWork unitOfWork)
{
    private AppLanguage Lang => access.Lang;

    private static readonly FinanceOperationType[] InTypes = [FinanceOperationType.Income, FinanceOperationType.SalePayment, FinanceOperationType.CustomerPayment];
    private static readonly FinanceOperationType[] OutTypes = [FinanceOperationType.Expense, FinanceOperationType.Refund, FinanceOperationType.SupplierPayment];

    internal static bool Effective(FinanceOperation o) => o.Status == FinanceOperationStatus.Posted && o.ReversalOfId is null;
    private static bool IsIn(FinanceOperation o) => InTypes.Contains(o.Type);
    private static bool IsOut(FinanceOperation o) => OutTypes.Contains(o.Type);

    private static decimal? Change(decimal value, decimal previous) => previous == 0 ? null : Math.Round((value - previous) / previous * 100, 1);

    private async Task<(IReadOnlyList<FinanceAccount> Accounts, IReadOnlyList<FinanceOperation> Ops)> LoadAsync(FinanceCtx c, CancellationToken ct)
    {
        var accounts = new List<FinanceAccount>();
        foreach (var sid in c.StoreIds) accounts.AddRange(await ledger.EnsureAccountsAsync(c.OrgId, sid, ct));
        await unitOfWork.SaveChangesAsync(ct);
        var ops = (await repo.PostedOperationsAsync(c.StoreIds, c.FromUtc, c.ToUtc, ct)).Where(Effective).ToList();
        return (accounts, ops);
    }

    /// <summary>Күндер бойынша: кіріс, шығыс, күн соңындағы қалдық (барлық белсенді шот бойынша).</summary>
    private async Task<List<FinanceDayDto>> DaysAsync(FinanceCtx c, IReadOnlyList<FinanceAccount> accounts, IReadOnlyList<FinanceOperation> ops, CancellationToken ct)
    {
        var ids = accounts.Where(a => a.IsActive).Select(a => a.Id).ToList();
        var opening = (await repo.BalancesBeforeAsync(ids, c.FromUtc, ct)).Values.Sum();
        var byDay = ops.GroupBy(o => c.LocalDate(o.Date)).ToDictionary(g => g.Key, g => (In: g.Where(IsIn).Sum(o => o.Amount), Out: g.Where(IsOut).Sum(o => o.Amount),
            Net: g.SelectMany(o => o.Entries).Where(e => ids.Contains(e.AccountId)).Sum(e => e.Amount)));
        var days = new List<FinanceDayDto>();
        var running = opening;
        for (var d = c.Start; d <= c.End; d = d.AddDays(1))
        {
            var v = byDay.GetValueOrDefault(d);
            running += v.Net;
            days.Add(new FinanceDayDto(d, v.In, v.Out, running));
        }
        return days;
    }

    // ---------- Шолу (§4) ----------

    public async Task<FinanceOverviewDto> OverviewAsync(string? storeIds, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var c = await operations.ContextAsync(storeIds, from, to, ct);
        var (accounts, ops) = await LoadAsync(c, ct);
        var length = c.ToUtc - c.FromUtc;
        var prevOps = (await repo.PostedOperationsAsync(c.StoreIds, c.FromUtc - length, c.FromUtc, ct)).Where(Effective).ToList();
        decimal In(IEnumerable<FinanceOperation> x) => x.Where(IsIn).Sum(o => o.Amount);
        decimal Out(IEnumerable<FinanceOperation> x) => x.Where(IsOut).Sum(o => o.Amount);
        var inflow = In(ops); var outflow = Out(ops);
        var pInflow = In(prevOps); var pOutflow = Out(prevOps);
        var pnl = await PnlAsync(storeIds, from, to, true, ct);
        var profitRow = pnl.Rows.First(r => r.Key == "netProfit");
        var cash = accounts.Where(a => a.IsActive).Sum(a => a.Balance);
        var kpis = new List<FinanceKpiDto>
        {
            new("cash", cash, null, null),
            new("inflow", inflow, pInflow, Change(inflow, pInflow)),
            new("outflow", outflow, pOutflow, Change(outflow, pOutflow)),
            new("netFlow", inflow - outflow, pInflow - pOutflow, Change(inflow - outflow, pInflow - pOutflow)),
            new("profit", profitRow.Amount, profitRow.Previous, profitRow.ChangePercent, pnl.CostComplete, pnl.CostComplete ? null : "costIncomplete"),
        };

        var categories = (await repo.ListCategoriesAsync(c.OrgId, ct)).ToDictionary(x => x.Id, x => x.Name);
        var expenses = ops.Where(IsOut).GroupBy(o => o.Type switch
            {
                FinanceOperationType.SupplierPayment => ("supplierPayments", (string?)null),
                FinanceOperationType.Refund => ("refunds", null),
                _ => (o.CategoryId?.ToString() ?? "uncategorized", o.CategoryId is { } cid ? categories.GetValueOrDefault(cid) : null),
            })
            .Select(g => (g.Key.Item1, g.Key.Item2, Sum: g.Sum(o => o.Amount))).Where(x => x.Sum > 0).OrderByDescending(x => x.Sum).ToList();
        var expShares = expenses.Select(x => new ExpenseShareDto(x.Item1, x.Item2 ?? x.Item1, x.Sum, outflow == 0 ? 0 : Math.Round(x.Sum / outflow * 100, 1))).ToList();

        var recent = (await repo.SearchOperationsAsync(new FinanceOperationFilter(c.StoreIds, null, null, null, null, null, null, null, null, null,
            null, null, null, null, null, false, "date", true, 0, 8), ct)).Items;
        var upcoming = await UpcomingAsync(c, ct);
        var debts = await DebtsSummaryAsync(c, ct);
        var storeNames = await repo.StoreNamesAsync(c.StoreIds, ct);
        return new FinanceOverviewDto(c.Start, c.End, c.NowUtc, kpis, await DaysAsync(c, accounts, ops, ct),
            accounts.Where(a => a.IsActive).Select(a => FinanceOperationsService.ToDto(a, storeNames, true)).ToList(),
            expShares, await operations.ToDtosAsync(c, recent, ct), upcoming, debts);
    }

    private async Task<List<UpcomingPaymentDto>> UpcomingAsync(FinanceCtx c, CancellationToken ct)
    {
        var today = c.Today;
        var horizon = today.AddDays(14);
        var list = new List<UpcomingPaymentDto>();
        var debts = (await repo.DebtsAsync(c.StoreIds, true, ct)).Where(d => d.DueDate <= horizon).ToList();
        var customers = await repo.CustomersAsync(debts.Select(d => d.CustomerId).Distinct().ToList(), ct);
        list.AddRange(debts.Select(d => new UpcomingPaymentDto("customer", d.CustomerId, customers.GetValueOrDefault(d.CustomerId).Name is { Length: > 0 } n ? n : "—",
            d.Remaining, d.DueDate, d.DueDate.DayNumber - today.DayNumber, d.DueDate < today ? "overdue" : "due")));
        var docs = (await repo.SupplierDocumentsAsync(c.StoreIds, ct)).Where(d => d.Remaining > 0 && d.PaymentDueDate is { } due && due <= horizon).ToList();
        var suppliers = await repo.SuppliersAsync(c.OrgId, ct);
        list.AddRange(docs.Select(d => new UpcomingPaymentDto("supplier", d.SupplierId!.Value, suppliers.GetValueOrDefault(d.SupplierId.Value).Name ?? "—",
            d.Remaining, d.PaymentDueDate, d.PaymentDueDate!.Value.DayNumber - today.DayNumber, d.PaymentDueDate < today ? "overdue" : "due")));
        return list.OrderBy(x => x.DueDate).Take(8).ToList();
    }

    internal async Task<DebtsSummaryDto> DebtsSummaryAsync(FinanceCtx c, CancellationToken ct)
    {
        var today = c.Today;
        var debts = await repo.DebtsAsync(c.StoreIds, true, ct);
        var docs = (await repo.SupplierDocumentsAsync(c.StoreIds, ct)).Where(d => d.Remaining > 0).ToList();
        return new DebtsSummaryDto(debts.Sum(d => d.Remaining), debts.Where(d => d.DueDate < today).Sum(d => d.Remaining), debts.Select(d => d.CustomerId).Distinct().Count(),
            docs.Sum(d => d.Remaining), docs.Where(d => d.PaymentDueDate < today).Sum(d => d.Remaining), docs.Count);
    }

    // ---------- P&L (§5) ----------

    public async Task<PnlDto> PnlAsync(string? storeIds, DateOnly? from, DateOnly? to, bool compare, CancellationToken ct = default)
    {
        var c = await operations.ContextAsync(storeIds, from, to, ct);
        var cur = await PnlPeriodAsync(c, c.FromUtc, c.ToUtc, ct);
        PnlPeriod? prev = null;
        DateOnly? prevStart = null, prevEnd = null;
        if (compare)
        {
            var days = c.End.DayNumber - c.Start.DayNumber + 1;
            prevStart = c.Start.AddDays(-days);
            prevEnd = c.Start.AddDays(-1);
            prev = await PnlPeriodAsync(c, StoreTime.StartOfDayUtc(prevStart.Value, c.Zone), c.FromUtc, ct);
        }
        var categories = (await repo.ListCategoriesAsync(c.OrgId, ct)).ToDictionary(x => x.Id);
        var rows = new List<PnlRowDto>();
        void Row(string key, int level, decimal amount, decimal? prevAmount, bool total = false, string? label = null, string? link = null, Guid? categoryId = null)
        {
            var pct = cur.Revenue == 0 ? (decimal?)null : Math.Round(amount / cur.Revenue * 100, 1);
            rows.Add(new PnlRowDto(key, level, label, amount, pct, prevAmount, prevAmount is { } p ? amount - p : null, prevAmount is { } pp ? Change(amount, pp) : null, total, link, categoryId));
        }
        Row("revenue", 0, cur.Revenue, prev?.Revenue, link: "/sales");
        Row("discounts", 0, -cur.Discounts, prev is null ? null : -prev.Discounts);
        Row("bonusRedeemed", 1, -cur.Bonus, prev is null ? null : -prev.Bonus);
        Row("returns", 0, -cur.Returns, prev is null ? null : -prev.Returns, link: "/sales?hasReturn=yes");
        Row("netRevenue", 0, cur.Net, prev?.Net, true);
        Row("cogs", 0, -cur.Cogs, prev is null ? null : -prev.Cogs, link: "/finance/product-profit");
        Row("grossProfit", 0, cur.Net - cur.Cogs, prev is null ? null : prev.Net - prev.Cogs, true);
        Group("operatingExpenses", PnlClass.OperatingExpense, true);
        Group("otherIncome", PnlClass.OtherIncome, false);
        Group("otherExpenses", PnlClass.OtherExpense, true);
        var beforeTax = cur.Net - cur.Cogs - cur.ByClass(PnlClass.OperatingExpense) + cur.ByClass(PnlClass.OtherIncome) - cur.ByClass(PnlClass.OtherExpense);
        var prevBeforeTax = prev is null ? (decimal?)null : prev.Net - prev.Cogs - prev.ByClass(PnlClass.OperatingExpense) + prev.ByClass(PnlClass.OtherIncome) - prev.ByClass(PnlClass.OtherExpense);
        Row("profitBeforeTax", 0, beforeTax, prevBeforeTax, true);
        var taxesTracked = cur.ByClass(PnlClass.Tax) != 0 || (prev?.ByClass(PnlClass.Tax) ?? 0) != 0;
        Group("taxes", PnlClass.Tax, true);
        Row("netProfit", 0, beforeTax - cur.ByClass(PnlClass.Tax), prevBeforeTax - prev?.ByClass(PnlClass.Tax), true);

        void Group(string key, PnlClass cls, bool expense)
        {
            var sign = expense ? -1 : 1;
            var items = cur.Ops.Where(o => o.Class == cls).GroupBy(o => o.CategoryId).ToList();
            Row(key, 0, sign * cur.ByClass(cls), prev is null ? null : sign * prev.ByClass(cls), link: $"/finance/operations?types={(expense ? "Expense" : "Income")}");
            foreach (var g in items.OrderByDescending(g => g.Sum(x => x.Amount)))
            {
                var cat = g.Key is { } id ? categories.GetValueOrDefault(id) : null;
                var prevSum = prev?.Ops.Where(o => o.CategoryId == g.Key).Sum(o => o.Amount);
                Row($"{key}.{g.Key}", 1, sign * g.Sum(x => x.Amount), prevSum is { } ps ? sign * ps : null, label: cat?.Name ?? "—",
                    link: g.Key is { } cid ? $"/finance/operations?categoryId={cid}" : null, categoryId: g.Key);
            }
        }

        var notes = new List<string> { "netRevenueOnce", "cogsHistorical", "flowIsNotProfit" };
        if (!cur.CostComplete) notes.Add("costIncomplete");
        if (!taxesTracked) notes.Add("taxesNotTracked");
        return new PnlDto(c.Start, c.End, prevStart, prevEnd, cur.CostComplete, taxesTracked, rows, notes);
    }

    private sealed record PnlOp(Guid? CategoryId, PnlClass Class, decimal Amount);

    private sealed record PnlPeriod(decimal Revenue, decimal Discounts, decimal Bonus, decimal Returns, decimal Net, decimal Cogs, bool CostComplete, List<PnlOp> Ops)
    {
        public decimal ByClass(PnlClass cls) => Ops.Where(o => o.Class == cls).Sum(o => o.Amount);
    }

    private async Task<PnlPeriod> PnlPeriodAsync(FinanceCtx c, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        var sales = await analytics.SalesAsync(c.StoreIds, fromUtc, toUtc, ct);
        var returns = await analytics.ReturnsAsync(c.StoreIds, fromUtc, toUtc, ct);
        var (cogs, complete) = Cogs(sales, returns);
        var ops = (await repo.PostedOperationsAsync(c.StoreIds, fromUtc, toUtc, ct)).Where(o => Effective(o) && o.IsManual).ToList();
        var categories = (await repo.ListCategoriesAsync(c.OrgId, ct)).ToDictionary(x => x.Id);
        var pnlOps = ops.Select(o => new PnlOp(o.CategoryId, o.CategoryId is { } cid && categories.TryGetValue(cid, out var cat) ? cat.PnlClass
                : o.Type == FinanceOperationType.Income ? PnlClass.OtherIncome : PnlClass.OtherExpense, o.Amount))
            .Where(o => o.Class != PnlClass.NotInPnl).ToList();
        var gross = sales.Sum(s => s.Subtotal);
        var discounts = sales.Sum(s => s.DiscountAmount);
        var bonus = sales.Sum(s => s.BonusRedeemed);
        var ret = returns.Sum(r => r.Amount);
        return new PnlPeriod(gross, discounts, bonus, ret, sales.Sum(s => s.Total) - ret, cogs, complete, pnlOps);
    }

    /// <summary>COGS — сатылған сәттегі кіріс бағасы (UnitCost); қайтарылған жолдың құны бастапқы сатылым жолынан (§3, §17).</summary>
    internal static (decimal Cogs, bool Complete) Cogs(IReadOnlyList<Sale> sales, IReadOnlyList<SaleReturn> returns)
    {
        var items = sales.SelectMany(s => s.Items).ToDictionary(i => i.Id);
        var complete = true;
        decimal cogs = 0;
        foreach (var i in items.Values) { if (i.UnitCost is { } u) cogs += u * i.Quantity; else complete = false; }
        foreach (var r in returns.SelectMany(x => x.Items))
        {
            if (items.TryGetValue(r.SaleItemId, out var si) && si.UnitCost is { } u) cogs -= u * r.Quantity; else complete = false;
        }
        return (Math.Round(cogs, 2), complete);
    }

    // ---------- Cash Flow (§6) ----------

    public async Task<CashFlowDto> CashFlowAsync(string? storeIds, DateOnly? from, DateOnly? to, Guid? accountId, CancellationToken ct = default)
    {
        var c = await operations.ContextAsync(storeIds, from, to, ct);
        var (accounts, allOps) = await LoadAsync(c, ct);
        var scope = accountId is { } aid ? accounts.Where(a => a.Id == aid).ToList() : accounts.Where(a => a.IsActive).ToList();
        var ids = scope.Select(a => a.Id).ToHashSet();
        var ops = allOps.Where(o => o.Entries.Any(e => ids.Contains(e.AccountId))).ToList();
        var opening = (await repo.BalancesBeforeAsync(ids.ToList(), c.FromUtc, ct)).Values.Sum();
        decimal Net(IEnumerable<FinanceOperation> x) => x.SelectMany(o => o.Entries).Where(e => ids.Contains(e.AccountId)).Sum(e => e.Amount);
        var inflow = ops.Where(IsIn).Sum(o => o.Amount);
        var outflow = ops.Where(IsOut).Sum(o => o.Amount);
        // Аударым: таңдалған шоттар ішінде — нөл, тек бір шот таңдалса — оның кірісі/шығысы бөлек топта.
        var transfers = Net(ops.Where(o => o.IsInternal));
        var adjustments = Net(ops.Where(o => o.Type == FinanceOperationType.Adjustment));
        var closing = opening + Net(ops);
        var byDay = ops.GroupBy(o => c.LocalDate(o.Date)).ToDictionary(g => g.Key, g => (In: g.Where(IsIn).Sum(o => o.Amount), Out: g.Where(IsOut).Sum(o => o.Amount), Net: Net(g)));
        var days = new List<FinanceDayDto>();
        var running = opening;
        for (var d = c.Start; d <= c.End; d = d.AddDays(1)) { var v = byDay.GetValueOrDefault(d); running += v.Net; days.Add(new FinanceDayDto(d, v.In, v.Out, running)); }
        static List<ExpenseShareDto> Shares(IEnumerable<FinanceOperation> x)
        {
            var list = x.GroupBy(o => o.Type.ToString()).Select(g => (g.Key, Sum: g.Sum(o => o.Amount))).Where(t => t.Sum > 0).OrderByDescending(t => t.Sum).ToList();
            var total = list.Sum(t => t.Sum);
            return list.Select(t => new ExpenseShareDto(t.Key, t.Key, t.Sum, total == 0 ? 0 : Math.Round(t.Sum / total * 100, 1))).ToList();
        }
        return new CashFlowDto(c.Start, c.End, opening, inflow, outflow, transfers, adjustments, closing, days, Shares(ops.Where(IsIn)), Shares(ops.Where(IsOut)));
    }

    // ---------- Тауар пайдасы (§10) ----------

    public async Task<ProductProfitDto> ProductProfitAsync(string? storeIds, DateOnly? from, DateOnly? to, string? search, Guid? nodeId, Guid? brandId,
        string? sort, string? dir, int page, int pageSize, CancellationToken ct = default)
    {
        var c = await operations.ContextAsync(storeIds, from, to, ct);
        var sales = await analytics.SalesAsync(c.StoreIds, c.FromUtc, c.ToUtc, ct);
        var returns = await analytics.ReturnsAsync(c.StoreIds, c.FromUtc, c.ToUtc, ct);
        var items = sales.SelectMany(s => s.Items).ToList();
        var byItem = items.ToDictionary(i => i.Id);
        var returnItems = returns.SelectMany(r => r.Items).ToList();
        var ids = items.Select(i => i.ProductId).Concat(returnItems.Select(i => i.ProductId)).Distinct().ToList();
        var products = (await analytics.ProductsAsync(c.OrgId, ids, ct)).ToDictionary(p => p.Id);
        var roots = await analytics.NodeRootsAsync(c.OrgId, ct);
        var soldBy = items.ToLookup(i => i.ProductId);
        var backBy = returnItems.ToLookup(i => i.ProductId);
        var rows = ids.Select(id =>
        {
            var sold = soldBy[id].ToList();
            var back = backBy[id].ToList();
            var p = products.GetValueOrDefault(id);
            var complete = sold.All(i => i.UnitCost != null) && back.All(r => byItem.TryGetValue(r.SaleItemId, out var si) && si.UnitCost != null);
            var soldQty = sold.Sum(i => i.Quantity);
            var retQty = back.Sum(r => r.Quantity);
            var net = soldQty - retQty;
            var revenue = sold.Sum(i => i.LineTotal - i.Discount) - back.Sum(r => r.Amount);
            decimal? cost = complete ? Math.Round(sold.Sum(i => i.UnitCost!.Value * i.Quantity) - back.Sum(r => byItem[r.SaleItemId].UnitCost!.Value * r.Quantity), 2) : null;
            decimal? profit = cost is { } cc ? revenue - cc : null;
            return new ProductProfitRowDto(id, p?.Name ?? sold.Select(i => i.Name).FirstOrDefault() ?? "—", p?.Article, null,
                p?.NodeId is { } n && roots.TryGetValue(n, out var r) ? r.RootName : null, p?.BrandName, p?.Unit,
                soldQty, retQty, net, revenue, cost, profit, profit is { } pr && revenue != 0 ? Math.Round(pr / revenue * 100, 1) : null,
                net > 0 ? Math.Round(revenue / net, 2) : null, complete);
        }).ToList();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var matching = await analytics.ProductIdsMatchingAsync(c.OrgId, search, ct);
            var tokens = ProductSearch.Tokens(search);
            rows = rows.Where(r => matching.Contains(r.ProductId) || ProductSearch.Matches(tokens, r.Name, r.Article)).ToList();
        }
        if (nodeId is { } node) rows = rows.Where(r => products.GetValueOrDefault(r.ProductId)?.NodeId is { } n && (n == node || roots.GetValueOrDefault(n).RootId == node)).ToList();
        if (brandId is { } brand) rows = rows.Where(r => products.GetValueOrDefault(r.ProductId)?.BrandId == brand).ToList();
        var desc = !string.Equals(dir, "asc", StringComparison.OrdinalIgnoreCase);
        Func<ProductProfitRowDto, decimal?> key = sort switch
        {
            "profit" => r => r.Profit, "margin" => r => r.Margin, "qty" => r => r.NetQty, "cost" => r => r.Cost, _ => r => r.NetRevenue,
        };
        rows = (desc ? rows.OrderByDescending(r => key(r) ?? decimal.MinValue) : rows.OrderBy(r => key(r) ?? decimal.MaxValue)).ToList();
        var total = rows.Count;
        var allComplete = rows.Count > 0 && rows.All(r => r.CostComplete);
        var revenueAll = rows.Sum(r => r.NetRevenue);
        decimal? costAll = allComplete ? rows.Sum(r => r.Cost!.Value) : null;
        var p1 = Math.Max(1, page);
        var size = Math.Clamp(pageSize, 1, 500);
        return new ProductProfitDto(total, p1, size, revenueAll, costAll, costAll is { } ca ? revenueAll - ca : null,
            costAll is { } cb && revenueAll != 0 ? Math.Round((revenueAll - cb) / revenueAll * 100, 1) : null, allComplete, rows.Count(r => !r.CostComplete),
            rows.Skip((p1 - 1) * size).Take(size).ToList());
    }
}
