using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Application.Pos.Catalog;
using SalemBonus.Application.Pos.Sales;
using SalemBonus.Domain.Core;
using SalemBonus.Domain.Enums;
using SalemBonus.Domain.Pos.Inventory;
using SalemBonus.Domain.Pos.Sales;

namespace SalemBonus.Application.Pos.Analytics;

/// <summary>
/// «Статистика» v1.1 — аналитикалық витрина (ТЗ «Статистика»). Тек оқиды, ештеңе жасамайды (§1).
/// Барлық формула осында, бір сөздікпен (§25):
/// <list type="bullet">
/// <item>Выручка — аяқталған чектердің сомасы, жеңілдіктен кейін, қайтарымға дейін (қарызға сатылғаны да кіреді; бонус — кірмейді).</item>
/// <item>Возвраты — проведённый қайтару құжаттары мен сомасы, <b>қайтару күнімен</b>; чистые продажи = выручка − қайтарым.</item>
/// <item>Средний чек — выручка / чек саны (0-ге бөлу жоқ).</item>
/// <item>Жаңа клиент — компанияда бірінші сатып алуы кезеңге түскен; қайталама — кезеңге дейін де, кезеңде де сатып алған.
/// Клиентсіз чектер возвращаемостьқа кірмейді, бөлек саналады.</item>
/// <item>Поступления — нақты ақша (сатылымның қарыздан басқа төлемі + қарыз өтеуі); бұл выручка емес.
/// Денежный поток = поступления − выплаты (қайтарылған ақша, жеткізушіге төлем); бұл пайда емес.</item>
/// <item>Пайда мен қор құны — тек кіріс бағасы белгілі болса және «Финансы» құқығымен.</item>
/// </list>
/// Кезең шекаралары дүкеннің уақыт белдеуімен; бакеттер үздіксіз, әр күн бір рет (§9–10).
/// </summary>
public partial class AnalyticsService(
    IAnalyticsRepository repo,
    IStaffRepository staff,
    IStoreRepository stores,
    ISpreadsheet spreadsheet,
    CatalogAccess access)
{
    /// <summary>«Постоянный клиент» — бүкіл тарихта кемінде осынша сатып алу (дүкен баптауы әлі жоқ, ереже экранда жазылады, §13).</summary>
    public const int RegularThreshold = 3;
    private const int MaxDays = 366 * 3;
    private const int MaxPageSize = 100;
    private AppLanguage Lang => access.Lang;

    // ---------- Контекст ----------

    internal sealed record Ctx(
        Guid OrgId, IReadOnlyList<Guid> StoreIds, IReadOnlyList<AnalyticsStoreDto> Stores, TimeZoneInfo Zone,
        DateOnly Start, DateOnly End, DateOnly Today, DateTime FromUtc, DateTime ToUtc, DateTime NowUtc,
        string Granularity, string Compare, DateOnly? CmpStart, DateOnly? CmpEnd, DateTime? CmpFromUtc, DateTime? CmpToUtc,
        bool CanFinance, bool CanContacts, bool CanExport, IReadOnlyList<BucketDto> Buckets, Dictionary<DateOnly, int> DayIndex)
    {
        public bool HasCompare => CmpFromUtc is not null;
        public bool IncludesToday => End >= Today;

        public DateOnly LocalDate(DateTime utc) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, Zone));

        /// <summary>Сәт қай бакетке түседі (кезеңнен тыс болса — −1).</summary>
        public int BucketOf(DateTime utc) => DayIndex.TryGetValue(LocalDate(utc), out var i) ? i : -1;

        public AnalyticsMetaDto Meta() => new(Stores, Start, End, Zone.Id, Granularity, Compare, CmpStart, CmpEnd, IncludesToday, NowUtc,
            CanFinance, CanContacts, CanExport);
    }

    internal async Task<Ctx> ContextAsync(AnalyticsQuery q, CancellationToken ct)
    {
        var (orgId, current) = await access.RequireAsync(StaffPermissions.StatisticsView, ct);
        var memberships = new List<StoreMembership> { current };
        var requested = (q.StoreIds ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => Guid.TryParse(x, out var g) ? g : throw new ValidationException(Messages.AnalyticsStoreForbidden(Lang), "storeIds"))
            .Distinct().ToList();
        if (requested.Count > 0)
        {
            memberships.Clear();
            foreach (var id in requested)
            {
                // Тек рұқсат етілген дүкендер (§28): мүшелік, сол ұйым, statistics.view.
                var m = id == current.StoreId ? current : await staff.GetMembershipAsync(access.StaffUserId, id, ct);
                if (m is null || m.Store?.OrganizationId != orgId || !m.Has(StaffPermissions.StatisticsView))
                    throw new ForbiddenException(Messages.AnalyticsStoreForbidden(Lang));
                memberships.Add(m);
            }
        }

        var store = await stores.GetByIdAsync(current.StoreId, ct) ?? throw new NotFoundException(Messages.StoreNotFound(Lang));
        var zone = StoreTime.Zone(store);
        var today = StoreTime.Today(zone);
        var start = q.From ?? new DateOnly(today.Year, today.Month, 1);
        var end = q.To ?? today;
        if (end < start) (start, end) = (end, start);
        if (end.DayNumber - start.DayNumber + 1 > MaxDays) throw new ValidationException(Messages.AnalyticsRangeTooLong(Lang, MaxDays), "to");

        var now = DateTime.UtcNow;
        var fromUtc = StoreTime.StartOfDayUtc(start, zone);
        var toUtc = StoreTime.StartOfDayUtc(end.AddDays(1), zone);

        // Салыстыру (§5): алдыңғы тең кезең не өткен жылдың сол кезеңі. Бүгін аяқталмаса — тек өткен уақыт ұзындығы салыстырылады.
        var compare = q.Compare is "none" or "year" ? q.Compare : "prev";
        DateOnly? cmpStart = null, cmpEnd = null;
        DateTime? cmpFrom = null, cmpTo = null;
        if (compare != "none")
        {
            var days = end.DayNumber - start.DayNumber + 1;
            (cmpStart, cmpEnd) = compare == "year" ? (start.AddYears(-1), end.AddYears(-1)) : (start.AddDays(-days), start.AddDays(-1));
            cmpFrom = StoreTime.StartOfDayUtc(cmpStart.Value, zone);
            var elapsed = (toUtc < now ? toUtc : now) - fromUtc;
            var full = StoreTime.StartOfDayUtc(cmpEnd.Value.AddDays(1), zone);
            cmpTo = cmpFrom + elapsed < full ? cmpFrom + elapsed : full;
            if (cmpTo < cmpFrom) cmpTo = cmpFrom;
        }

        var (granularity, buckets) = Buckets(start, end);
        var dayIndex = new Dictionary<DateOnly, int>();
        for (var i = 0; i < buckets.Count; i++)
            for (var d = buckets[i].Start; d <= buckets[i].End; d = d.AddDays(1)) dayIndex[d] = i;

        bool All(string permission) => memberships.All(x => x.Has(permission));
        return new Ctx(orgId, memberships.Select(x => x.StoreId).ToList(),
            memberships.Select(x => new AnalyticsStoreDto(x.StoreId, x.Store?.Name ?? string.Empty)).ToList(),
            zone, start, end, today, fromUtc, toUtc, now, granularity, compare, cmpStart, cmpEnd, cmpFrom, cmpTo,
            All(StaffPermissions.FinanceView), All(StaffPermissions.CustomersContacts), All(StaffPermissions.StatisticsExport),
            buckets, dayIndex);
    }

    /// <summary>§9: 62 күнге дейін — күн, 63–180 — апта (дүйсенбіден), одан көп — ай. Шеттері кезеңмен кесіледі.</summary>
    internal static (string Granularity, List<BucketDto> Buckets) Buckets(DateOnly start, DateOnly end)
    {
        var days = end.DayNumber - start.DayNumber + 1;
        var list = new List<BucketDto>();
        if (days <= 62)
        {
            for (var d = start; d <= end; d = d.AddDays(1)) list.Add(new BucketDto(d, d));
            return ("day", list);
        }
        if (days <= 180)
        {
            for (var d = start; d <= end;)
            {
                var sunday = d.AddDays((7 - ((int)d.DayOfWeek + 6) % 7) - 1);
                var e = sunday < end ? sunday : end;
                list.Add(new BucketDto(d, e));
                d = e.AddDays(1);
            }
            return ("week", list);
        }
        for (var d = start; d <= end;)
        {
            var monthEnd = new DateOnly(d.Year, d.Month, 1).AddMonths(1).AddDays(-1);
            var e = monthEnd < end ? monthEnd : end;
            list.Add(new BucketDto(d, e));
            d = e.AddDays(1);
        }
        return ("month", list);
    }

    // ---------- Ортақ көмекшілер ----------

    internal static decimal? Change(decimal value, decimal? previous) =>
        previous is null or 0 ? null : Math.Round((value - previous.Value) / previous.Value * 100, 1);

    internal static KpiDto Kpi(string key, decimal value, decimal? previous, decimal? amount = null, decimal? previousAmount = null,
        decimal? numerator = null, decimal? denominator = null, string? note = null) =>
        new(key, value, previous, previous is null ? null : value - previous, Change(value, previous), amount, previousAmount, numerator, denominator,
            true, note);

    internal static KpiDto Unavailable(string key, string note) => new(key, null, null, null, null, Available: false, Note: note);

    internal static decimal Percent(decimal part, decimal total) => total == 0 ? 0 : Math.Round(part / total * 100, 1);

    internal static decimal? Rate(decimal part, decimal total) => total == 0 ? null : Math.Round(part / total * 100, 1);

    internal static readonly PaymentMethod[] Methods =
        [PaymentMethod.Cash, PaymentMethod.Card, PaymentMethod.Qr, PaymentMethod.Transfer, PaymentMethod.Debt];

    internal static bool IsAccrual(BonusTransactionType t) => t is BonusTransactionType.Accrual or BonusTransactionType.Birthday or BonusTransactionType.Promo;

    /// <summary>Телефон құқықсыз көрсетілмейді (§13): «+7 701 *** ** 67».</summary>
    internal static string? Phone(string? phone, bool canContacts)
    {
        if (string.IsNullOrEmpty(phone)) return null;
        if (canContacts) return phone;
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        return digits.Length < 6 ? "***" : $"+{digits[0]} {digits.Substring(1, 3)} *** ** {digits[^2..]}";
    }

    internal static decimal[] Zeros(Ctx c) => new decimal[c.Buckets.Count];

    // ---------- Динамика (§9–10, §27) ----------

    public async Task<SalesTrendDto> SalesTrendAsync(AnalyticsQuery q, CancellationToken ct = default)
    {
        var c = await ContextAsync(q, ct);
        var sales = Filter(await repo.SalesAsync(c.StoreIds, c.FromUtc, c.ToUtc, ct), q);
        var returns = FilterReturns(await repo.ReturnsAsync(c.StoreIds, c.FromUtc, c.ToUtc, ct), q);
        var revenue = Zeros(c);
        var count = new int[c.Buckets.Count];
        var ret = Zeros(c);
        foreach (var s in sales)
        {
            var i = c.BucketOf(s.CreatedAt);
            if (i < 0) continue;
            revenue[i] += s.Total;
            count[i]++;
        }
        foreach (var r in returns)
        {
            var i = c.BucketOf(r.CreatedAt);
            if (i >= 0) ret[i] += r.Amount;
        }
        var data = c.Buckets.Select((b, i) => new TrendPointDto(b.Start, b.End, revenue[i], count[i], ret[i], revenue[i] - ret[i])).ToList();
        // §10: бір бакет — бір жазба, хронологиялық, үздіксіз.
        if (data.Select(x => x.BucketStart).Distinct().Count() != data.Count) throw new InvalidOperationException("Duplicate trend bucket");
        return new SalesTrendDto(c.Granularity, c.Zone.Id, c.Start, c.End, c.IncludesToday, c.NowUtc, data);
    }

    /// <summary>Сатылым сүзгілері (касса, кассир, төлем түрі) — аралас чек әр түріне сай келеді.</summary>
    internal static IReadOnlyList<Sale> Filter(IReadOnlyList<Sale> sales, AnalyticsQuery q)
    {
        IEnumerable<Sale> x = sales;
        if (q.RegisterId is { } r) x = x.Where(s => s.RegisterId == r);
        if (q.StaffUserId is { } u) x = x.Where(s => s.StaffUserId == u);
        if (Enum.TryParse<PaymentMethod>(q.Method, true, out var m)) x = x.Where(s => s.Payments.Any(p => p.Method == m));
        return x as IReadOnlyList<Sale> ?? x.ToList();
    }

    internal static IReadOnlyList<SaleReturn> FilterReturns(IReadOnlyList<SaleReturn> returns, AnalyticsQuery q)
    {
        IEnumerable<SaleReturn> x = returns;
        if (q.RegisterId is { } r) x = x.Where(s => s.RegisterId == r);
        if (q.StaffUserId is { } u) x = x.Where(s => s.StaffUserId == u);
        return x as IReadOnlyList<SaleReturn> ?? x.ToList();
    }

    // ---------- Обзор (§8, §11–14) ----------

    public async Task<OverviewDto> OverviewAsync(AnalyticsQuery q, CancellationToken ct = default)
    {
        var c = await ContextAsync(q, ct);
        var sales = await repo.SalesAsync(c.StoreIds, c.FromUtc, c.ToUtc, ct);
        var returns = await repo.ReturnsAsync(c.StoreIds, c.FromUtc, c.ToUtc, ct);
        IReadOnlyList<Sale>? cmpSales = null;
        IReadOnlyList<SaleReturn>? cmpReturns = null;
        if (c.HasCompare)
        {
            cmpSales = await repo.SalesAsync(c.StoreIds, c.CmpFromUtc!.Value, c.CmpToUtc!.Value, ct);
            cmpReturns = await repo.ReturnsAsync(c.StoreIds, c.CmpFromUtc.Value, c.CmpToUtc.Value, ct);
        }

        // KPI ряд 1
        var revenue = sales.Sum(s => s.Total);
        var count = sales.Count;
        var returnsSum = returns.Sum(r => r.Amount);
        decimal? pRevenue = cmpSales?.Sum(s => s.Total);
        decimal? pCount = cmpSales?.Count;
        decimal? pReturnsSum = cmpReturns?.Sum(r => r.Amount);
        var avg = count == 0 ? 0 : Math.Round(revenue / count, 0);
        decimal? pAvg = cmpSales is null ? null : cmpSales.Count == 0 ? 0 : Math.Round(pRevenue!.Value / cmpSales.Count, 0);

        // Клиенттер: компания бойынша тарих (жаңа/қайталама)
        var buyers = sales.Where(s => s.CustomerId != null).Select(s => s.CustomerId!.Value).Distinct().ToList();
        var cmpBuyers = cmpSales?.Where(s => s.CustomerId != null).Select(s => s.CustomerId!.Value).Distinct().ToList();
        var history = await repo.CustomerHistoryAsync(c.OrgId, buyers.Concat(cmpBuyers ?? []).Distinct().ToList(), ct);
        var repeat = buyers.Count(b => history.TryGetValue(b, out var h) && h.FirstAt < c.FromUtc);
        int? pRepeat = cmpBuyers?.Count(b => history.TryGetValue(b, out var h) && h.FirstAt < c.CmpFromUtc);
        var newCount = buyers.Count - repeat;
        int? pNew = cmpBuyers is null ? null : cmpBuyers.Count - pRepeat!.Value;

        var bonus = await repo.BonusTransactionsAsync(c.StoreIds, c.FromUtc, c.ToUtc, ct);
        var redeemed = -bonus.Where(t => t.Type == BonusTransactionType.Redemption).Sum(t => t.Amount);
        int? pRedeemed = null;
        if (c.HasCompare)
            pRedeemed = -(await repo.BonusTransactionsAsync(c.StoreIds, c.CmpFromUtc!.Value, c.CmpToUtc!.Value, ct))
                .Where(t => t.Type == BonusTransactionType.Redemption).Sum(t => t.Amount);

        var inventory = await InventoryBlockAsync(c, ct);
        var (debtCount, debtRemaining) = await repo.OpenDebtsAsync(c.StoreIds, ct);
        var inBase = await repo.CustomersInBaseAsync(c.StoreIds, ct);

        var kpis = new List<KpiDto>
        {
            Kpi("revenue", revenue, pRevenue),
            Kpi("salesCount", count, pCount),
            Kpi("averageCheck", avg, pAvg, numerator: revenue, denominator: count),
            Kpi("returns", returns.Count, cmpReturns?.Count, returnsSum, pReturnsSum),
            Kpi("netSales", revenue - returnsSum, pRevenue - pReturnsSum, numerator: revenue, denominator: returnsSum),
            Kpi("customersInBase", inBase, null, numerator: buyers.Count),
            Kpi("repeatCustomers", repeat, pRepeat, numerator: repeat, denominator: buyers.Count),
            Kpi("bonusRedeemed", redeemed, pRedeemed),
            new KpiDto("stock", inventory.TotalUnits, null, null, null, Amount: inventory.Value, Numerator: inventory.WithStock,
                Note: inventory.Value is null ? (c.CanFinance ? "costIncomplete" : "noFinanceRight") : null),
            Kpi("customerDebt", debtRemaining, null, numerator: debtCount, note: "asOfNow"),
        };

        // Төлем түрлері (§11): аралас чектің әр бөлігі өз түрінде, бір рет.
        var payments = sales.SelectMany(s => s.Payments).ToList();
        var payTotal = payments.Sum(p => p.Amount);
        var methods = new PaymentMethodsDto(
            Methods.Select(m => new PaymentShareDto(m.ToString(), payments.Where(p => p.Method == m).Sum(p => p.Amount),
                Percent(payments.Where(p => p.Method == m).Sum(p => p.Amount), payTotal),
                sales.Count(s => s.Payments.Any(p => p.Method == m)))).ToList(),
            payTotal, sales.Count(s => s.Payments.Select(p => p.Method).Distinct().Count() > 1));

        var products = await ProductLinesAsync(c, sales, returns, ct);
        var top = TopProducts(products, c.CanFinance);
        var customers = await CustomersBlockAsync(c, sales, buyers, history, newCount, repeat, pNew, pRepeat, q.InactiveDays ?? 30, ct);
        var suppliers = await SuppliersBlockAsync(c, ct);
        var finance = c.CanFinance ? await FinanceBlockAsync(c, sales, returns, debtRemaining, ct) : null;
        return new OverviewDto(c.Meta(), kpis, methods, top, customers, inventory, suppliers, finance);
    }

    // ---------- Тауар жолдары (сатылған − қайтарылған) ----------

    internal sealed record ProductLine(Guid ProductId, string Name, string? Article, string? Image, string? Unit, Guid? NodeId, Guid? BrandId,
        string? Brand, decimal Sold, decimal Returned, decimal Amount, decimal ReturnedAmount, decimal Discount, decimal? Cost, bool CostComplete)
    {
        public decimal NetQty => Sold - Returned;
        public decimal NetAmount => Amount - ReturnedAmount;
        public decimal? Profit => CostComplete && Cost is { } c ? NetAmount - c : null;
    }

    /// <summary>
    /// Кезеңдегі сатылым жолдары мен кезеңдегі қайтарым жолдары (қайтарымның нақты сомасы). Өзіндік құн —
    /// сатылған сәттегі кіріс бағасы (UnitCost); белгісіз болса тауардың пайдасы есептелмейді (§12, §24).
    /// </summary>
    internal async Task<List<ProductLine>> ProductLinesAsync(Ctx c, IReadOnlyList<Sale> sales, IReadOnlyList<SaleReturn> returns, CancellationToken ct)
    {
        var items = sales.SelectMany(s => s.Items).ToList();
        var byItem = items.ToDictionary(i => i.Id);
        var returnItems = returns.SelectMany(r => r.Items).ToList();
        var ids = items.Select(i => i.ProductId).Concat(returnItems.Select(i => i.ProductId)).Distinct().ToList();
        var products = (await repo.ProductsAsync(c.OrgId, ids, ct)).ToDictionary(p => p.Id);
        // Қайтарылған жолдың құны — бастапқы сатылым жолынан; ол кезеңнен тыс болса, құны белгісіз (пайда есептелмейді).
        var soldBy = items.ToLookup(i => i.ProductId);
        var backBy = returnItems.ToLookup(i => i.ProductId);
        return ids.Select(id =>
        {
            var sold = soldBy[id].ToList();
            var back = backBy[id].ToList();
            var p = products.GetValueOrDefault(id);
            var costComplete = sold.All(i => i.UnitCost != null) && back.All(r => byItem.TryGetValue(r.SaleItemId, out var si) && si.UnitCost != null);
            decimal? cost = costComplete
                ? sold.Sum(i => i.UnitCost!.Value * i.Quantity) - back.Sum(r => byItem[r.SaleItemId].UnitCost!.Value * r.Quantity)
                : null;
            return new ProductLine(id, p?.Name ?? sold.Select(i => i.Name).Concat(back.Select(r => r.Name)).FirstOrDefault() ?? "—",
                p?.Article ?? sold.Select(i => i.Article).FirstOrDefault(), p?.ImageUrl, p?.Unit ?? sold.Select(i => i.UnitShortName).FirstOrDefault(),
                p?.NodeId, p?.BrandId, p?.BrandName,
                sold.Sum(i => i.Quantity), back.Sum(r => r.Quantity), sold.Sum(i => i.LineTotal - i.Discount), back.Sum(r => r.Amount),
                sold.Sum(i => i.Discount), cost is null ? null : Math.Round(cost.Value, 2), costComplete);
        }).ToList();
    }

    private static TopProductsDto TopProducts(List<ProductLine> lines, bool canFinance)
    {
        static TopProductDto Dto(ProductLine x, bool money) => new(x.ProductId, x.Name, x.Article, x.Image, x.Unit, x.NetQty, x.NetAmount,
            money ? x.Cost : null, money ? x.Profit : null);
        var sold = lines.Where(x => x.NetQty > 0).ToList();
        var withProfit = sold.Where(x => x.Profit != null).ToList();
        var profitAvailable = canFinance && withProfit.Count > 0;
        return new TopProductsDto(
            sold.OrderByDescending(x => x.NetQty).ThenByDescending(x => x.NetAmount).Take(5).Select(x => Dto(x, canFinance)).ToList(),
            sold.OrderByDescending(x => x.NetAmount).ThenByDescending(x => x.NetQty).Take(5).Select(x => Dto(x, canFinance)).ToList(),
            profitAvailable ? withProfit.OrderByDescending(x => x.Profit).Take(5).Select(x => Dto(x, true)).ToList() : null,
            profitAvailable);
    }

    // ---------- Клиенттер блогы (§13) ----------

    private async Task<CustomersBlockDto> CustomersBlockAsync(Ctx c, IReadOnlyList<Sale> sales, List<Guid> buyers,
        Dictionary<Guid, AnalyticsCustomerHistory> history, int newCount, int repeat, int? pNew, int? pRepeat, int inactiveDays, CancellationToken ct)
    {
        inactiveDays = inactiveDays is 60 or 90 ? inactiveDays : 30;
        var byCustomer = sales.Where(s => s.CustomerId != null).GroupBy(s => s.CustomerId!.Value)
            .ToDictionary(g => g.Key, g => (Count: g.Count(), Amount: g.Sum(s => s.Total), Last: g.Max(s => s.CreatedAt)));
        var frequency = new[] { ("1", 1, 1), ("2-3", 2, 3), ("4-5", 4, 5), ("6+", 6, int.MaxValue) }
            .Select(f =>
            {
                var n = byCustomer.Values.Count(v => v.Count >= f.Item2 && v.Count <= f.Item3);
                return new FrequencyDto(f.Item1, n, Percent(n, byCustomer.Count));
            }).ToList();

        var newIds = buyers.Where(b => history.TryGetValue(b, out var h) && h.FirstAt >= c.FromUtc).ToList();
        var regularIds = buyers.Where(b => history.TryGetValue(b, out var h) && h.Purchases >= RegularThreshold).ToList();
        var inactive = await repo.InactiveCustomersAsync(c.StoreIds, c.NowUtc.AddDays(-inactiveDays), 5, ct);
        var people = await repo.CustomersAsync(newIds.Concat(regularIds).Concat(inactive.Select(i => i.CustomerId)).Distinct().ToList(), ct);

        ActivityCustomerDto Dto(Guid id, int purchases, decimal amount, DateTime last)
        {
            var p = people.GetValueOrDefault(id);
            return new ActivityCustomerDto(id, string.IsNullOrWhiteSpace(p?.Name) ? "—" : p!.Name, Phone(p?.Phone, c.CanContacts), purchases, amount, last);
        }
        return new CustomersBlockDto(
            buyers.Count, newCount, repeat, Rate(repeat, buyers.Count), sales.Count(s => s.CustomerId == null), pNew, pRepeat,
            frequency, RegularThreshold,
            newIds.OrderByDescending(id => byCustomer[id].Amount).Take(5)
                .Select(id => Dto(id, byCustomer[id].Count, byCustomer[id].Amount, byCustomer[id].Last)).ToList(),
            regularIds.OrderByDescending(id => history[id].Amount).Take(5)
                .Select(id => Dto(id, history[id].Purchases, history[id].Amount, history[id].LastAt)).ToList(),
            inactive.Select(h => Dto(h.CustomerId, h.Purchases, h.Amount, h.LastAt)).ToList(),
            inactiveDays);
    }

    // ---------- Склад, жеткізушілер, қаржы блоктары (§14) ----------

    internal sealed record StockRow(Guid WarehouseId, Guid StoreId, Guid ProductId, decimal Quantity);

    internal async Task<(List<(Guid Id, Guid StoreId, string Name, bool IsActive)> Warehouses, List<StockRow> Rows, Dictionary<Guid, AnalyticsProduct> Products)>
        StockAsync(Ctx c, Guid? warehouseId, CancellationToken ct)
    {
        var warehouses = (await repo.WarehousesAsync(c.StoreIds, ct)).Where(w => w.IsActive && (warehouseId is null || w.Id == warehouseId)).ToList();
        var storeOf = warehouses.ToDictionary(w => w.Id, w => w.StoreId);
        var balances = await repo.BalancesAsync(warehouses.Select(w => w.Id).ToList(), ct);
        var products = (await repo.ProductsAsync(c.OrgId, null, ct)).Where(p => p.IsGoods && p.IsActive).ToDictionary(p => p.Id);
        var rows = balances.Where(b => products.ContainsKey(b.ProductId))
            .Select(b => new StockRow(b.WarehouseId, storeOf[b.WarehouseId], b.ProductId, b.Quantity)).ToList();
        return (warehouses, rows, products);
    }

    private async Task<InventoryBlockDto> InventoryBlockAsync(Ctx c, CancellationToken ct)
    {
        var (warehouses, rows, products) = await StockAsync(c, null, ct);
        var totals = products.Keys.ToDictionary(id => id, id => rows.Where(r => r.ProductId == id).Sum(r => r.Quantity));
        var movements = await repo.MovementsAsync(warehouses.Select(w => w.Id).ToList(), c.FromUtc, c.ToUtc, ct);
        var (value, complete) = c.CanFinance ? await StockValueAsync(c, rows, ct) : (null, false);
        return new InventoryBlockDto(
            totals.Count(t => t.Value > 0), totals.Count(t => t.Value <= 0),
            totals.Count(t => products[t.Key].MinStock is { } min && t.Value <= min),
            movements.Count, totals.Values.Where(v => v > 0).Sum(), value, complete);
    }

    /// <summary>Қор құны = қалдық × дүкеннің кіріс бағасы (§25). Қалдығы бар бір тауардың бағасы белгісіз болса — құн көрсетілмейді.</summary>
    internal async Task<(decimal? Value, bool Complete)> StockValueAsync(Ctx c, List<StockRow> rows, CancellationToken ct)
    {
        var prices = await repo.PurchasePricesAsync(c.StoreIds, ct);
        var positive = rows.Where(r => r.Quantity > 0).ToList();
        if (positive.Any(r => !prices.ContainsKey((r.StoreId, r.ProductId)))) return (null, false);
        return (Math.Round(positive.Sum(r => r.Quantity * prices[(r.StoreId, r.ProductId)]), 2), true);
    }

    private async Task<SuppliersBlockDto> SuppliersBlockAsync(Ctx c, CancellationToken ct)
    {
        var all = await repo.DocumentsAsync(c.StoreIds, null, null, ct);
        var posted = all.Where(d => d.Type == StockDocumentType.Receipt && d.Status == StockDocumentStatus.Posted).ToList();
        var period = posted.Where(d => d.Date >= c.FromUtc && d.Date < c.ToUtc).ToList();
        var payments = await repo.SupplierPaymentsAsync(c.StoreIds, c.FromUtc, c.ToUtc, ct);
        var withSupplier = posted.Where(d => d.SupplierId != null).ToList();
        return new SuppliersBlockDto(period.Sum(d => d.Total), period.Count, payments.Sum(p => p.Amount),
            withSupplier.Sum(d => d.Total - d.Paid), withSupplier.Count(d => d.Total - d.Paid > 0));
    }

    /// <summary>Нақты ақша қозғалысы (§19, §25): поступления — қарыздан басқа төлемдер мен қарыз өтеуі; выплаты — қайтарылған ақша мен жеткізушіге төлем.</summary>
    internal sealed record CashFlow(decimal SalePayments, decimal DebtRepaid, decimal Refunds, decimal SupplierPaid)
    {
        public decimal Inflow => SalePayments + DebtRepaid;
        public decimal Outflow => Refunds + SupplierPaid;
    }

    internal async Task<(CashFlow Flow, IReadOnlyList<AnalyticsDebtPayment> Repaid, IReadOnlyList<AnalyticsSupplierPayment> SupplierPayments)> CashFlowAsync(
        Ctx c, IReadOnlyList<Sale> sales, IReadOnlyList<SaleReturn> returns, CancellationToken ct)
    {
        var repaid = (await repo.DebtPaymentsAsync(c.StoreIds, c.FromUtc, c.ToUtc, ct)).Where(p => !p.IsReturn).ToList();
        var supplier = await repo.SupplierPaymentsAsync(c.StoreIds, c.FromUtc, c.ToUtc, ct);
        var flow = new CashFlow(
            sales.SelectMany(s => s.Payments).Where(p => p.Method != PaymentMethod.Debt).Sum(p => p.Amount),
            repaid.Sum(p => p.Amount), returns.Sum(r => r.Refunded), supplier.Sum(p => p.Amount));
        return (flow, repaid, supplier);
    }

    private async Task<FinanceBlockDto> FinanceBlockAsync(Ctx c, IReadOnlyList<Sale> sales, IReadOnlyList<SaleReturn> returns, decimal customerDebt, CancellationToken ct)
    {
        var (flow, _, _) = await CashFlowAsync(c, sales, returns, ct);
        return new FinanceBlockDto(flow.Inflow, flow.Outflow, flow.Inflow - flow.Outflow, customerDebt);
    }
}
