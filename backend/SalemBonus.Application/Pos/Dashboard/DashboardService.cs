using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Application.Pos.Catalog;
using SalemBonus.Application.Pos.Inventory;
using SalemBonus.Application.Pos.Sales;
using SalemBonus.Domain.Core;
using SalemBonus.Domain.Enums;
using SalemBonus.Domain.Pos.Sales;

namespace SalemBonus.Application.Pos.Dashboard;

public record DashboardMetaDto(string StoreName, DateOnly From, DateOnly To, DateTime GeneratedAt, string TimeZone, string Granularity);

/// <summary>KPI: мәні, алдыңғы салыстырмалы кезеңдегі мәні, өзгеріс пайызы (алдыңғысы 0 болса — null, «—»).</summary>
public record KpiDto(string Key, decimal Value, decimal Previous, decimal? ChangePercent);

public record ChartPointDto(DateTime Start, string Label, decimal Revenue, int Count);

public record ShareDto(string Key, string Label, decimal Amount, decimal Percent);

/// <summary>
/// Қаржы итогы (ТЗ §10). Шығын модулі әлі жоқ — <see cref="Expenses"/> null («деректер жоқ»).
/// <see cref="CostComplete"/> false — кей тауардың кіріс бағасы белгісіз, өзіндік құн толық емес.
/// </summary>
public record FinancialSummaryDto(decimal Revenue, decimal Cost, bool CostComplete, decimal? Expenses, decimal Profit);

/// <summary>
/// Қолма-қол (ТЗ §11). Кассалық ауысым мен санау әлі жоқ — нақты қалдық null; «кассада болуы керек»
/// = кезеңдегі түсім − берілгені (бастапқы қалдықсыз).
/// </summary>
public record CashSummaryDto(decimal? ActualBalance, decimal Received, decimal Paid, decimal Expected);

public record TopProductDto(Guid ProductId, string Name, string? ImageUrl, decimal Quantity, string? Unit, decimal Revenue);

public record CustomersSummaryDto(int Total, int New, int Regular, int Vip, decimal? TotalChange, decimal? NewChange);

public record BonusSummaryDto(int Accrued, int Redeemed, int Balance, decimal? AccruedChange, decimal? RedeemedChange);

public record AttentionDto(string Type, int Count, decimal? Amount);

public record DashboardDto(
    DashboardMetaDto Meta,
    IReadOnlyList<KpiDto> Kpi,
    IReadOnlyList<ChartPointDto> Chart,
    IReadOnlyList<ShareDto> PaymentMethods,
    IReadOnlyList<ShareDto> Categories,
    FinancialSummaryDto Financial,
    CashSummaryDto Cash,
    IReadOnlyList<TopProductDto> TopProducts,
    CustomersSummaryDto Customers,
    BonusSummaryDto Bonuses,
    IReadOnlyList<AttentionDto> Attention);

/// <summary>
/// Иесінің бас экраны (ТЗ «Статистика»): бір сұраныс — бір snapshot (§17). Барлық формула осында,
/// фронт ештеңе қайта есептемейді (§26). Кезең шекаралары дүкеннің уақыт белдеуімен; салыстыру —
/// ұзындығы бірдей алдыңғы кезеңмен (бүгін 00:00–14:30 ↔ кеше 00:00–14:30).
/// </summary>
public class DashboardService(
    IDashboardRepository dashboard,
    ISalesRepository sales,
    IStoreRepository stores,
    ICatalogRepository catalog,
    WarehouseService warehouses,
    CatalogAccess access)
{
    private const int TopCategories = 5;

    public async Task<DashboardDto> GetAsync(DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.StatisticsView, ct);
        var store = await stores.GetByIdAsync(m.StoreId, ct) ?? throw new NotFoundException(Messages.StoreNotFound(access.Lang));
        var zone = StoreTime.Zone(store);
        var today = StoreTime.Today(zone);
        var start = from ?? today;
        var end = to ?? start;
        if (end < start) (start, end) = (end, start);

        var now = DateTime.UtcNow;
        var fromUtc = StoreTime.StartOfDayUtc(start, zone);
        var toUtc = StoreTime.StartOfDayUtc(end.AddDays(1), zone);
        if (toUtc > now) toUtc = now;
        if (toUtc < fromUtc) toUtc = fromUtc;
        var length = toUtc - fromUtc;
        var prevFrom = fromUtc - length;

        var all = await dashboard.SalesWithItemsAsync(m.StoreId, prevFrom, toUtc, ct);
        var cur = all.Where(s => s.CreatedAt >= fromUtc).ToList();
        var prev = all.Where(s => s.CreatedAt < fromUtc).ToList();
        var returns = await sales.ListReturnsInRangeAsync(m.StoreId, prevFrom, toUtc, null, ct);
        var curReturns = returns.Where(r => r.CreatedAt >= fromUtc).ToList();
        var prevReturns = returns.Where(r => r.CreatedAt < fromUtc).ToList();

        // ---------- KPI (§6) ----------
        decimal Revenue(IEnumerable<Sale> s, IEnumerable<SaleReturn> r) => s.Sum(x => x.Total) - r.Sum(x => x.Amount);
        var revenue = Revenue(cur, curReturns);
        var prevRevenue = Revenue(prev, prevReturns);
        var avg = cur.Count == 0 ? 0 : Math.Round(revenue / cur.Count, 0);
        var prevAvg = prev.Count == 0 ? 0 : Math.Round(prevRevenue / prev.Count, 0);
        var buyers = cur.Where(s => s.CustomerId != null).Select(s => s.CustomerId!.Value).Distinct().ToList();
        var prevBuyers = prev.Where(s => s.CustomerId != null).Select(s => s.CustomerId!.Value).Distinct().Count();
        var newCustomers = await dashboard.NewCustomersAsync(m.StoreId, fromUtc, toUtc, ct);
        var prevNewCustomers = await dashboard.NewCustomersAsync(m.StoreId, prevFrom, fromUtc, ct);

        var bonusTx = await dashboard.BonusTransactionsAsync(m.StoreId, prevFrom, toUtc, ct);
        static bool IsAccrual(BonusTransactionType t) => t is BonusTransactionType.Accrual or BonusTransactionType.Birthday or BonusTransactionType.Promo;
        int Accrued(IEnumerable<DashboardBonusTx> x) => x.Where(t => IsAccrual(t.Type)).Sum(t => t.Amount);
        int Redeemed(IEnumerable<DashboardBonusTx> x) => -x.Where(t => t.Type == BonusTransactionType.Redemption).Sum(t => t.Amount);
        var curBonus = bonusTx.Where(t => t.CreatedAt >= fromUtc).ToList();
        var prevBonus = bonusTx.Where(t => t.CreatedAt < fromUtc).ToList();

        var kpi = new List<KpiDto>
        {
            Kpi("revenue", revenue, prevRevenue),
            Kpi("sales", cur.Count, prev.Count),
            Kpi("average", avg, prevAvg),
            Kpi("customers", buyers.Count, prevBuyers),
            Kpi("newCustomers", newCustomers, prevNewCustomers),
            Kpi("bonusAccrued", Accrued(curBonus), Accrued(prevBonus)),
        };

        // ---------- Уақыт бойынша (§7): қысқа кезеңде сағат, ұзынында күн ----------
        var hourly = end == start;
        var chart = new List<ChartPointDto>();
        DateTime Local(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(utc, zone);
        if (hourly)
        {
            var lastHour = toUtc >= StoreTime.StartOfDayUtc(start.AddDays(1), zone) ? 23 : Local(toUtc).Hour;
            var byHour = cur.GroupBy(s => Local(s.CreatedAt).Hour).ToDictionary(g => g.Key, g => (Sum: g.Sum(x => x.Total), Count: g.Count()));
            var retByHour = curReturns.GroupBy(r => Local(r.CreatedAt).Hour).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
            var firstHour = byHour.Count == 0 ? Math.Min(9, lastHour) : Math.Min(byHour.Keys.Min(), 9);
            for (var h = firstHour; h <= lastHour; h++)
            {
                var v = byHour.GetValueOrDefault(h);
                chart.Add(new ChartPointDto(fromUtc.AddHours(h), $"{h:00}:00", v.Sum - retByHour.GetValueOrDefault(h), v.Count));
            }
        }
        else
        {
            var byDay = cur.GroupBy(s => DateOnly.FromDateTime(Local(s.CreatedAt))).ToDictionary(g => g.Key, g => (Sum: g.Sum(x => x.Total), Count: g.Count()));
            var retByDay = curReturns.GroupBy(r => DateOnly.FromDateTime(Local(r.CreatedAt))).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
            var lastDay = DateOnly.FromDateTime(Local(toUtc.AddTicks(-1)));
            for (var d = start; d <= lastDay; d = d.AddDays(1))
            {
                var v = byDay.GetValueOrDefault(d);
                chart.Add(new ChartPointDto(StoreTime.StartOfDayUtc(d, zone), d.ToString("dd.MM"), v.Sum - retByDay.GetValueOrDefault(d), v.Count));
            }
        }

        // ---------- Төлем түрлері (§8): аралас төлемнің әр бөлігі өз түрінде, қайталанбайды ----------
        var payments = cur.SelectMany(s => s.Payments).ToList();
        var paidTotal = payments.Sum(p => p.Amount);
        var methods = new[] { PaymentMethod.Cash, PaymentMethod.Card, PaymentMethod.Qr, PaymentMethod.Transfer, PaymentMethod.Debt }
            .Select(x =>
            {
                var sum = payments.Where(p => p.Method == x).Sum(p => p.Amount);
                return new ShareDto(x.ToString(), x.ToString(), sum, Percent(sum, paidTotal));
            }).ToList();

        // ---------- Санаттар (§9), топ тауарлар (§12), өзіндік құн (§10) ----------
        var items = cur.SelectMany(s => s.Items).ToList();
        var returnItems = curReturns.SelectMany(r => r.Items).ToList();
        var productIds = items.Select(i => i.ProductId).Concat(returnItems.Select(i => i.ProductId)).Distinct().ToList();
        var products = (await dashboard.ProductsAsync(productIds, ct)).ToDictionary(p => p.Id);
        var nodes = (await catalog.ListNodesAsync(orgId, ct)).ToDictionary(n => n.Id);
        var currentCosts = await sales.PurchasePricesAsync(m.StoreId, productIds, ct);
        var saleItems = items.ToDictionary(i => i.Id);

        // Выручкамен бірдей модель: кезеңде сатылған жолдар минус кезеңде қайтарылған жолдар (қайтарымның
        // нақты сомасымен). Сонда санаттар мен топ тауарлардың қосындысы выручкаға тиынына дейін тең.
        var lines = items
            .Select(i => new Line(i.ProductId, i.Name, i.UnitShortName, i.Quantity, i.LineTotal - i.Discount, i.UnitCost))
            .Concat(returnItems.Select(r =>
            {
                var sold = saleItems.GetValueOrDefault(r.SaleItemId);
                return new Line(r.ProductId, r.Name, sold?.UnitShortName, -r.Quantity, -r.Amount, sold?.UnitCost);
            }))
            .ToList();

        string RootName(Guid productId)
        {
            if (!products.TryGetValue(productId, out var p) || p.NodeId is not { } nid || !nodes.TryGetValue(nid, out var node)) return "—";
            var rootId = Guid.Parse(node.Path.Split('/')[0]);
            return nodes.TryGetValue(rootId, out var root) ? root.Name : node.Name;
        }
        var byCategory = lines.GroupBy(x => RootName(x.ProductId)).Select(g => (Name: g.Key, Sum: g.Sum(x => x.Amount)))
            .Where(x => x.Sum > 0).OrderByDescending(x => x.Sum).ToList();
        var netTotal = byCategory.Sum(x => x.Sum);
        var categories = byCategory.Take(TopCategories).Select(x => new ShareDto(x.Name, x.Name, x.Sum, Percent(x.Sum, netTotal))).ToList();
        if (byCategory.Count > TopCategories)
        {
            var rest = byCategory.Skip(TopCategories).Sum(x => x.Sum);
            categories.Add(new ShareDto("other", "other", rest, Percent(rest, netTotal)));
        }

        var top = lines.GroupBy(x => x.ProductId)
            .Select(g => new TopProductDto(g.Key, products.GetValueOrDefault(g.Key)?.Name ?? g.First().Name,
                products.GetValueOrDefault(g.Key)?.ImageUrl, g.Sum(x => x.Quantity), g.Select(x => x.Unit).FirstOrDefault(u => u != null), g.Sum(x => x.Amount)))
            .Where(x => x.Quantity > 0 && x.Revenue > 0)
            .OrderByDescending(x => x.Revenue).Take(5).ToList();

        // Тарихи құн: сатылған сәттегі кіріс бағасы; ескі чектерде жоқ болса — ағымдағысы.
        // Қайтарылған тауар қоймаға оралады — оның құны да шегеріледі.
        var costComplete = true;
        var cost = lines.Sum(x =>
        {
            var unitCost = x.UnitCost ?? (currentCosts.TryGetValue(x.ProductId, out var c) ? c : (decimal?)null);
            if (unitCost is null) costComplete = false;
            return (unitCost ?? 0) * x.Quantity;
        });
        var financial = new FinancialSummaryDto(revenue, Math.Round(cost, 2), costComplete, null, Math.Round(revenue - cost, 2));

        // ---------- Қолма-қол (§11) ----------
        var repaid = await sales.ListDebtPaymentsInRangeAsync(m.StoreId, fromUtc, toUtc, null, ct);
        var received = payments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount)
                       + repaid.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount);
        var paidOut = curReturns.Where(r => r.RefundMethod == PaymentMethod.Cash).Sum(r => r.Refunded);
        var cash = new CashSummaryDto(null, received, paidOut, received - paidOut);

        // ---------- Клиенттер (§13) және бонус (§14) ----------
        var levels = await dashboard.LevelsAsync(m.StoreId, buyers, ct);
        var customers = new CustomersSummaryDto(
            buyers.Count, newCustomers,
            buyers.Count(b => levels.GetValueOrDefault(b) is CustomerLevel.Regular or CustomerLevel.Favorite),
            buyers.Count(b => levels.GetValueOrDefault(b) == CustomerLevel.Vip),
            Change(buyers.Count, prevBuyers), Change(newCustomers, prevNewCustomers));
        var bonuses = new BonusSummaryDto(
            Accrued(curBonus), Redeemed(curBonus), await dashboard.TotalBonusBalanceAsync(m.StoreId, ct),
            Change(Accrued(curBonus), Accrued(prevBonus)), Change(Redeemed(curBonus), Redeemed(prevBonus)));

        // ---------- Назар аударыңыз (§15): ағымдағы күй, кезеңге тәуелсіз ----------
        var settings = await sales.GetSettingsAsync(m.StoreId, ct) ?? new StoreCashierSettings { StoreId = m.StoreId };
        var warehouse = await warehouses.EnsureDefaultAsync(orgId, m.StoreId, ct);
        var (low, @out) = await dashboard.StockAlertsAsync(orgId, warehouse.Id, settings.LowStockThreshold, ct);
        var (debtCount, debtSum, overdue) = await dashboard.OpenDebtsAsync(m.StoreId, today, ct);
        var attention = new List<AttentionDto>();
        if (low > 0) attention.Add(new AttentionDto("lowStock", low, null));
        if (@out > 0) attention.Add(new AttentionDto("outOfStock", @out, null));
        if (debtCount > 0) attention.Add(new AttentionDto("debts", debtCount, debtSum));
        if (overdue > 0) attention.Add(new AttentionDto("overdueDebts", overdue, null));

        return new DashboardDto(
            new DashboardMetaDto(store.Name, start, end, now, zone.Id, hourly ? "hour" : "day"),
            kpi, chart, methods, categories, financial, cash, top, customers, bonuses, attention);
    }

    private static KpiDto Kpi(string key, decimal value, decimal previous) => new(key, value, previous, Change(value, previous));

    /// <summary>Алдыңғы мән 0 болса — пайыз жасанды болады, сондықтан null (ТЗ §6, §18).</summary>
    private static decimal? Change(decimal value, decimal previous) =>
        previous == 0 ? null : Math.Round((value - previous) / previous * 100, 1);

    private static decimal Percent(decimal part, decimal total) => total == 0 ? 0 : Math.Round(part / total * 100, 1);
}

/// <summary>Сатылым не қайтарым жолы (қайтарымда сан мен сома теріс).</summary>
internal sealed record Line(Guid ProductId, string Name, string? Unit, decimal Quantity, decimal Amount, decimal? UnitCost);
