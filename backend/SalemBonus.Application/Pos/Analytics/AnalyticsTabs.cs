using SalemBonus.Application.Common;
using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Domain.Enums;
using SalemBonus.Domain.Pos.Inventory;
using SalemBonus.Domain.Pos.Sales;

namespace SalemBonus.Application.Pos.Analytics;

using Row = Dictionary<string, object?>;

/// <summary>Детальные вкладки (ТЗ «Статистика» §15–23): бір пішін — KPI, сериялар, бөлінулер, серверлік кесте.</summary>
public partial class AnalyticsService
{
    public static readonly string[] Tabs = ["sales", "products", "inventory", "suppliers", "finance", "bonus", "customers", "employees", "registers"];

    /// <param name="all">Экспорт үшін: кесте беттелмейді.</param>
    public async Task<AnalyticsTabDto> TabAsync(string tab, AnalyticsQuery q, bool all = false, CancellationToken ct = default)
    {
        var c = await ContextAsync(q, ct);
        return tab switch
        {
            "sales" => await SalesTabAsync(c, q, all, ct),
            "products" => await ProductsTabAsync(c, q, all, ct),
            "inventory" => await InventoryTabAsync(c, q, all, ct),
            "suppliers" => await SuppliersTabAsync(c, q, all, ct),
            "finance" => c.CanFinance ? await FinanceTabAsync(c, q, all, ct) : throw new ForbiddenException(Messages.StaffPermissionDenied(Lang)),
            "bonus" => await BonusTabAsync(c, q, all, ct),
            "customers" => await CustomersTabAsync(c, q, all, ct),
            "employees" => await EmployeesTabAsync(c, q, all, ct),
            "registers" => await RegistersTabAsync(c, q, all, ct),
            _ => throw new NotFoundException(Messages.AnalyticsTabUnknown(Lang)),
        };
    }

    // ---------- Кесте: серверлік сұрыптау мен беттеу (§24) ----------

    private static TableDto Table(IReadOnlyList<ColumnDto> columns, List<Row> rows, AnalyticsQuery q, string defaultSort, bool defaultDesc, bool all)
    {
        var sort = columns.Any(x => x.Sortable && x.Key == q.Sort) ? q.Sort! : defaultSort;
        var desc = q.Dir is null ? defaultDesc : !string.Equals(q.Dir, "asc", StringComparison.OrdinalIgnoreCase);
        int Compare(Row a, Row b)
        {
            var x = a.GetValueOrDefault(sort);
            var y = b.GetValueOrDefault(sort);
            if (x is null && y is null) return 0;
            if (x is null) return 1;   // бос мәндер әрқашан соңында
            if (y is null) return -1;
            var r = x is string sx ? string.Compare(sx, y as string, StringComparison.CurrentCultureIgnoreCase) : Comparer<object>.Default.Compare(x, y);
            return desc ? -r : r;
        }
        rows.Sort(Compare);
        var page = Math.Max(1, q.Page);
        var size = all ? Math.Max(rows.Count, 1) : Math.Clamp(q.PageSize, 1, MaxPageSize);
        var paged = all ? rows : rows.Skip((page - 1) * size).Take(size).ToList();
        return new TableDto(columns, paged, rows.Count, all ? 1 : page, size, sort, desc ? "desc" : "asc");
    }

    private static ColumnDto Col(string key, string kind, bool sortable = true, string? link = null) => new(key, kind, sortable, link);

    private static ShareDto[] Shares(IEnumerable<(string Key, string? Label, decimal Value, int? Count)> items)
    {
        var list = items.ToList();
        var total = list.Sum(x => x.Value);
        return list.Select(x => new ShareDto(x.Key, x.Label, x.Value, Percent(x.Value, total), x.Count)).ToArray();
    }

    private static BreakdownDto Breakdown(string key, string kind, IReadOnlyList<ShareDto> items) => new(key, kind, items, items.Sum(x => x.Value));

    private static IReadOnlyList<ShareDto> PaymentShares(IReadOnlyList<Sale> sales)
    {
        var payments = sales.SelectMany(s => s.Payments).ToList();
        return Shares(Methods.Select(m => (m.ToString(), (string?)null, payments.Where(p => p.Method == m).Sum(p => p.Amount),
            (int?)sales.Count(s => s.Payments.Any(p => p.Method == m)))));
    }

    // ---------- Продажи (§15) ----------

    private async Task<AnalyticsTabDto> SalesTabAsync(Ctx c, AnalyticsQuery q, bool all, CancellationToken ct)
    {
        var sales = Filter(await repo.SalesAsync(c.StoreIds, c.FromUtc, c.ToUtc, ct), q);
        var returns = FilterReturns(await repo.ReturnsAsync(c.StoreIds, c.FromUtc, c.ToUtc, ct), q);
        IReadOnlyList<Sale>? ps = null;
        IReadOnlyList<SaleReturn>? pr = null;
        if (c.HasCompare)
        {
            ps = Filter(await repo.SalesAsync(c.StoreIds, c.CmpFromUtc!.Value, c.CmpToUtc!.Value, ct), q);
            pr = FilterReturns(await repo.ReturnsAsync(c.StoreIds, c.CmpFromUtc.Value, c.CmpToUtc.Value, ct), q);
        }
        decimal Rev(IReadOnlyList<Sale> s) => s.Sum(x => x.Total);
        decimal Avg(IReadOnlyList<Sale> s) => s.Count == 0 ? 0 : Math.Round(Rev(s) / s.Count, 0);

        var kpis = new List<KpiDto>
        {
            Kpi("revenue", Rev(sales), ps is null ? null : Rev(ps)),
            Kpi("salesCount", sales.Count, ps?.Count),
            Kpi("averageCheck", Avg(sales), ps is null ? null : Avg(ps), numerator: Rev(sales), denominator: sales.Count),
            Kpi("returns", returns.Count, pr?.Count, returns.Sum(r => r.Amount), pr?.Sum(r => r.Amount)),
            Kpi("netSales", Rev(sales) - returns.Sum(r => r.Amount), ps is null ? null : Rev(ps) - pr!.Sum(r => r.Amount)),
            Kpi("discounts", sales.Sum(s => s.DiscountAmount), ps?.Sum(s => s.DiscountAmount), numerator: sales.Count(s => s.DiscountAmount > 0)),
        };

        var revenue = Zeros(c); var count = Zeros(c); var ret = Zeros(c); var disc = Zeros(c);
        foreach (var s in sales) { var i = c.BucketOf(s.CreatedAt); if (i < 0) continue; revenue[i] += s.Total; count[i]++; disc[i] += s.DiscountAmount; }
        foreach (var r in returns) { var i = c.BucketOf(r.CreatedAt); if (i >= 0) ret[i] += r.Amount; }

        var rows = c.Buckets.Select((b, i) => new Row
        {
            ["bucketStart"] = b.Start, ["bucketEnd"] = b.End, ["receipts"] = (int)count[i], ["revenue"] = revenue[i], ["returns"] = ret[i],
            ["net"] = revenue[i] - ret[i], ["average"] = count[i] == 0 ? null : Math.Round(revenue[i] / count[i], 0), ["discounts"] = disc[i],
        }).ToList();
        var columns = new[]
        {
            Col("bucketStart", "period", link: "/sales?period=custom&from={bucketStart}&to={bucketEnd}"), Col("receipts", "int"), Col("revenue", "money"),
            Col("returns", "money"), Col("net", "money"), Col("average", "money"), Col("discounts", "money"),
        };
        return new AnalyticsTabDto(c.Meta(), kpis, c.Buckets,
            [new SeriesDto("revenue", "money", "bar", revenue), new SeriesDto("salesCount", "count", "line", count), new SeriesDto("returns", "money", "line", ret)],
            [Breakdown("paymentMethods", "money", PaymentShares(sales))],
            Table(columns, rows, q, "bucketStart", false, all),
            ["returnsByReturnDate", "revenueBeforeReturns"]);
    }

    // ---------- Товары (§16) ----------

    private async Task<AnalyticsTabDto> ProductsTabAsync(Ctx c, AnalyticsQuery q, bool all, CancellationToken ct)
    {
        var sales = Filter(await repo.SalesAsync(c.StoreIds, c.FromUtc, c.ToUtc, ct), q);
        var returns = FilterReturns(await repo.ReturnsAsync(c.StoreIds, c.FromUtc, c.ToUtc, ct), q);
        var lines = await ProductLinesAsync(c, sales, returns, ct);
        var roots = await repo.NodeRootsAsync(c.OrgId, ct);
        (Guid? Id, string Name) Root(ProductLine l) => l.NodeId is { } n && roots.TryGetValue(n, out var r) ? (r.RootId, r.RootName) : (null, "—");

        IEnumerable<ProductLine> filtered = lines;
        if (q.NodeId is { } node) filtered = filtered.Where(l => Root(l).Id == node || l.NodeId == node);
        if (q.BrandId is { } brand) filtered = filtered.Where(l => l.BrandId == brand);
        if (q.HasReturns is { } hr) filtered = filtered.Where(l => hr ? l.Returned > 0 : l.Returned == 0);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            // Каталогтағы тауар — барлық өрісі бойынша; чектегі атауы (тауар кейін өзгерсе) — атауы мен артикулы бойынша.
            var matching = await repo.ProductIdsMatchingAsync(c.OrgId, q.Search, ct);
            var tokens = ProductSearch.Tokens(q.Search);
            filtered = filtered.Where(l => matching.Contains(l.ProductId) || ProductSearch.Matches(tokens, l.Name, l.Article));
        }
        var list = filtered.ToList();
        var profitComplete = list.Count > 0 && list.All(l => l.CostComplete);

        var kpis = new List<KpiDto>
        {
            Kpi("productKinds", list.Count(l => l.NetQty > 0), null),
            Kpi("units", list.Sum(l => l.NetQty), null, numerator: list.Sum(l => l.Sold), denominator: list.Sum(l => l.Returned)),
            Kpi("productRevenue", list.Sum(l => l.NetAmount), null),
            Kpi("returnedUnits", list.Sum(l => l.Returned), null, list.Sum(l => l.ReturnedAmount)),
            Kpi("discounts", list.Sum(l => l.Discount), null),
            !c.CanFinance ? Unavailable("grossProfit", "noFinanceRight")
                : !profitComplete ? Unavailable("grossProfit", "costIncomplete")
                : Kpi("grossProfit", list.Sum(l => l.Profit!.Value), null, numerator: list.Sum(l => l.NetAmount), denominator: list.Sum(l => l.Cost!.Value)),
        };

        var categories = Shares(list.GroupBy(l => Root(l)).Select(g => (g.Key.Id?.ToString() ?? "none", (string?)g.Key.Name, g.Sum(l => l.NetAmount), (int?)null))
            .Where(x => x.Item3 > 0).OrderByDescending(x => x.Item3));
        var top = Shares(list.Where(l => l.NetAmount > 0).OrderByDescending(l => l.NetAmount).Take(10)
            .Select(l => (l.ProductId.ToString(), (string?)l.Name, l.NetAmount, (int?)null)));

        var rows = list.Select(l => new Row
        {
            ["productId"] = l.ProductId, ["name"] = l.Name, ["article"] = l.Article, ["category"] = Root(l).Name, ["brand"] = l.Brand, ["unit"] = l.Unit,
            ["sold"] = l.Sold, ["returned"] = l.Returned, ["netQty"] = l.NetQty, ["amount"] = l.NetAmount, ["discount"] = l.Discount,
            ["cost"] = c.CanFinance ? l.Cost : null, ["profit"] = c.CanFinance ? l.Profit : null,
        }).ToList();
        var columns = new List<ColumnDto>
        {
            Col("name", "text", link: "/products/{productId}"), Col("article", "text"), Col("category", "text"), Col("brand", "text"),
            Col("sold", "qty"), Col("returned", "qty"), Col("netQty", "qty"), Col("amount", "money"), Col("discount", "money"),
        };
        if (c.CanFinance) columns.AddRange([Col("cost", "money"), Col("profit", "money")]);
        return new AnalyticsTabDto(c.Meta(), kpis, c.Buckets, [],
            [Breakdown("categories", "money", categories), Breakdown("topProducts", "money", top)],
            Table(columns, rows, q, "amount", true, all),
            ["netOfReturns", profitComplete ? "profitByHistoricalCost" : "profitNeedsCost"]);
    }

    // ---------- Склад (§17) ----------

    private async Task<AnalyticsTabDto> InventoryTabAsync(Ctx c, AnalyticsQuery q, bool all, CancellationToken ct)
    {
        var (warehouses, stock, products) = await StockAsync(c, q.WarehouseId, ct);
        var wIds = warehouses.Select(w => w.Id).ToList();
        var wNames = warehouses.ToDictionary(w => w.Id, w => w.Name);
        var movements = await repo.MovementsAsync(wIds, c.FromUtc, c.ToUtc, ct);
        var last = await repo.LastMovementsAsync(wIds, ct);
        var docs = (await repo.DocumentsAsync(c.StoreIds, c.FromUtc, c.ToUtc, ct))
            .Where(d => d.Status is StockDocumentStatus.Posted or StockDocumentStatus.Reversed && wIds.Contains(d.WarehouseId)).ToList();
        var transfers = await repo.TransfersAsync(wIds, c.FromUtc, c.ToUtc, ct);

        var totals = products.Keys.ToDictionary(id => id, id => stock.Where(r => r.ProductId == id).Sum(r => r.Quantity));
        var (value, complete) = c.CanFinance ? await StockValueAsync(c, stock, ct) : (null, false);
        var kpis = new List<KpiDto>
        {
            Kpi("withStock", totals.Count(t => t.Value > 0), null, numerator: totals.Values.Where(v => v > 0).Sum()),
            Kpi("withoutStock", totals.Count(t => t.Value <= 0), null),
            Kpi("belowMin", totals.Count(t => products[t.Key].MinStock is { } min && t.Value <= min), null),
            Kpi("receiptDocs", docs.Count(d => d.Type == StockDocumentType.Receipt), null),
            Kpi("transfers", transfers.Count, null),
            Kpi("writeOffDocs", docs.Count(d => d.Type == StockDocumentType.WriteOff), null),
            !c.CanFinance ? Unavailable("stockValue", "noFinanceRight") : !complete ? Unavailable("stockValue", "costIncomplete") : Kpi("stockValue", value!.Value, null),
        };

        var incoming = Zeros(c); var outgoing = Zeros(c);
        foreach (var m in movements)
        {
            var i = c.BucketOf(m.CreatedAt);
            if (i < 0) continue;
            if (m.Quantity > 0) incoming[i] += m.Quantity; else outgoing[i] += -m.Quantity;
        }
        var byType = Shares(movements.GroupBy(m => m.Type).Select(g => (g.Key.ToString(), (string?)null, g.Sum(m => Math.Abs(m.Quantity)), (int?)g.Count()))
            .OrderByDescending(x => x.Item3));

        // Кесте: қойма × тауар — қалдығы бар не кезеңде қозғалғандар.
        var mv = movements.GroupBy(m => (m.WarehouseId, m.ProductId)).ToDictionary(g => g.Key, g => g.ToList());
        var keys = stock.Select(r => (r.WarehouseId, r.ProductId)).Concat(mv.Keys).Where(k => products.ContainsKey(k.ProductId)).Distinct();
        decimal Sum(List<AnalyticsMovement>? list, params StockMovementType[] types) => list?.Where(m => types.Contains(m.Type)).Sum(m => m.Quantity) ?? 0;
        var rows = keys.Select(k =>
        {
            var p = products[k.ProductId];
            var qty = stock.FirstOrDefault(r => r.WarehouseId == k.WarehouseId && r.ProductId == k.ProductId)?.Quantity ?? 0;
            var list = mv.GetValueOrDefault(k);
            return new Row
            {
                ["productId"] = p.Id, ["warehouse"] = wNames.GetValueOrDefault(k.WarehouseId), ["name"] = p.Name, ["article"] = p.Article, ["unit"] = p.Unit,
                ["stock"] = qty, ["minStock"] = p.MinStock,
                ["received"] = Sum(list, StockMovementType.Receipt, StockMovementType.Opening),
                ["transferIn"] = Sum(list, StockMovementType.TransferIn), ["transferOut"] = -Sum(list, StockMovementType.TransferOut),
                ["writeOff"] = -Sum(list, StockMovementType.WriteOff), ["returnsIn"] = Sum(list, StockMovementType.Return),
                ["sold"] = -Sum(list, StockMovementType.Sale),
                ["lastMovement"] = last.TryGetValue(k, out var lm) ? lm : null,
            };
        });
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var matching = await repo.ProductIdsMatchingAsync(c.OrgId, q.Search, ct);
            var tokens = ProductSearch.Tokens(q.Search);
            rows = rows.Where(r => matching.Contains((Guid)r["productId"]!) || ProductSearch.Matches(tokens, (string)r["name"]!, r["article"] as string));
        }
        rows = q.Segment switch
        {
            "in" => rows.Where(r => (decimal)r["stock"]! > 0),
            "out" => rows.Where(r => (decimal)r["stock"]! <= 0),
            "belowMin" => rows.Where(r => r["minStock"] is decimal min && (decimal)r["stock"]! <= min),
            _ => rows,
        };
        var columns = new[]
        {
            Col("warehouse", "text"), Col("name", "text", link: "/products/{productId}"), Col("article", "text"), Col("stock", "qty"), Col("unit", "text", false),
            Col("received", "qty"), Col("transferIn", "qty"), Col("transferOut", "qty"), Col("writeOff", "qty"), Col("returnsIn", "qty"), Col("sold", "qty"),
            Col("lastMovement", "datetime"),
        };
        return new AnalyticsTabDto(c.Meta(), kpis, c.Buckets,
            [new SeriesDto("incoming", "qty", "bar", incoming), new SeriesDto("outgoing", "qty", "bar", outgoing)],
            [Breakdown("movementTypes", "qty", byType)],
            Table(columns, rows.ToList(), q, "name", false, all),
            ["stockNow", c.CanFinance ? (complete ? "valueByPurchasePrice" : "valueNeedsCost") : "valueNeedsFinance"]);
    }

    // ---------- Поставщики (§18) ----------

    private async Task<AnalyticsTabDto> SuppliersTabAsync(Ctx c, AnalyticsQuery q, bool all, CancellationToken ct)
    {
        var allDocs = (await repo.DocumentsAsync(c.StoreIds, null, null, ct))
            .Where(d => d.Type == StockDocumentType.Receipt && d.Status == StockDocumentStatus.Posted && (q.SupplierId is null || d.SupplierId == q.SupplierId)).ToList();
        var period = allDocs.Where(d => d.Date >= c.FromUtc && d.Date < c.ToUtc).ToList();
        var payments = (await repo.SupplierPaymentsAsync(c.StoreIds, c.FromUtc, c.ToUtc, ct)).Where(p => q.SupplierId is null || p.SupplierId == q.SupplierId).ToList();
        var names = await repo.SupplierNamesAsync(c.OrgId, ct);
        var debt = allDocs.Where(d => d.SupplierId != null).Sum(d => d.Total - d.Paid);

        var kpis = new List<KpiDto>
        {
            Kpi("purchases", period.Sum(d => d.Total), null, numerator: period.Count),
            Kpi("receiptDocs", period.Count, null),
            Kpi("supplierPaid", payments.Sum(p => p.Amount), null, numerator: payments.Count),
            Kpi("supplierDebt", debt, null, note: "asOfNow"),
            Kpi("activeSuppliers", period.Where(d => d.SupplierId != null).Select(d => d.SupplierId).Distinct().Count(), null),
        };
        var purchases = Zeros(c); var paid = Zeros(c);
        foreach (var d in period) { var i = c.BucketOf(d.Date); if (i >= 0) purchases[i] += d.Total; }
        foreach (var p in payments) { var i = c.BucketOf(p.CreatedAt); if (i >= 0) paid[i] += p.Amount; }

        var supplied = period.Where(d => d.SupplierId != null).ToList();
        var status = Shares(new[]
        {
            ("paid", (string?)null, (decimal)supplied.Count(d => d.Paid >= d.Total), (int?)null),
            ("partial", null, supplied.Count(d => d.Paid > 0 && d.Paid < d.Total), null),
            ("unpaid", null, supplied.Count(d => d.Paid == 0 && d.Total > 0), null),
        });
        var top = Shares(period.GroupBy(d => d.SupplierId).Select(g => (g.Key?.ToString() ?? "none",
                (string?)(g.Key is { } id ? names.GetValueOrDefault(id) : null), g.Sum(d => d.Total), (int?)g.Count()))
            .OrderByDescending(x => x.Item3).Take(10));

        var ids = allDocs.Select(d => d.SupplierId).Concat(payments.Select(p => (Guid?)p.SupplierId)).Distinct();
        var rows = ids.Select(id =>
        {
            var docsOf = allDocs.Where(d => d.SupplierId == id).ToList();
            var inPeriod = docsOf.Where(d => d.Date >= c.FromUtc && d.Date < c.ToUtc).ToList();
            return new Row
            {
                ["supplierId"] = id, ["supplier"] = id is { } x ? names.GetValueOrDefault(x) : null,
                ["documents"] = inPeriod.Count, ["purchases"] = inPeriod.Sum(d => d.Total),
                ["paid"] = payments.Where(p => p.SupplierId == id).Sum(p => p.Amount),
                ["debt"] = id is null ? null : docsOf.Sum(d => d.Total - d.Paid),
                ["lastReceipt"] = docsOf.Count == 0 ? null : docsOf.Max(d => d.Date),
            };
        }).Where(r => (int)r["documents"]! > 0 || (decimal)r["paid"]! > 0 || r["debt"] is decimal dd && dd > 0).ToList();
        var columns = new[]
        {
            Col("supplier", "text", link: "/warehouse/suppliers/{supplierId}"), Col("documents", "int"), Col("purchases", "money"),
            Col("paid", "money"), Col("debt", "money"), Col("lastReceipt", "datetime"),
        };
        return new AnalyticsTabDto(c.Meta(), kpis, c.Buckets,
            [new SeriesDto("purchases", "money", "bar", purchases), new SeriesDto("supplierPaid", "money", "line", paid)],
            [Breakdown("paymentStatus", "count", status), Breakdown("topSuppliers", "money", top)],
            Table(columns, rows, q, "purchases", true, all),
            ["purchasesVsDebt"]);
    }

    // ---------- Финансы (§19) ----------

    private async Task<AnalyticsTabDto> FinanceTabAsync(Ctx c, AnalyticsQuery q, bool all, CancellationToken ct)
    {
        var sales = await repo.SalesAsync(c.StoreIds, c.FromUtc, c.ToUtc, ct);
        var returns = await repo.ReturnsAsync(c.StoreIds, c.FromUtc, c.ToUtc, ct);
        var (flow, repaid, supplier) = await CashFlowAsync(c, sales, returns, ct);
        var (_, customerDebt) = await repo.OpenDebtsAsync(c.StoreIds, ct);
        var supplierDebt = (await repo.DocumentsAsync(c.StoreIds, null, null, ct))
            .Where(d => d.Type == StockDocumentType.Receipt && d.Status == StockDocumentStatus.Posted && d.SupplierId != null).Sum(d => d.Total - d.Paid);
        var debtIssued = sales.SelectMany(s => s.Payments).Where(p => p.Method == PaymentMethod.Debt).Sum(p => p.Amount);

        var kpis = new List<KpiDto>
        {
            Kpi("inflow", flow.Inflow, null, numerator: flow.SalePayments, denominator: flow.DebtRepaid),
            Kpi("outflow", flow.Outflow, null, numerator: flow.Refunds, denominator: flow.SupplierPaid),
            Kpi("netFlow", flow.Inflow - flow.Outflow, null),
            Kpi("debtIssued", debtIssued, null),
            Kpi("customerDebt", customerDebt, null, note: "asOfNow"),
            Kpi("supplierDebt", supplierDebt, null, note: "asOfNow"),
            Unavailable("expenses", "noExpensesModule"),
        };

        // Операциялар: бакет × түрі × төлем түрі (§19 кесте).
        var ops = new List<(DateTime At, string Type, string? Method, decimal Amount)>();
        ops.AddRange(sales.SelectMany(s => s.Payments.Where(p => p.Method != PaymentMethod.Debt).Select(p => (s.CreatedAt, "salePayment", (string?)p.Method.ToString(), p.Amount))));
        ops.AddRange(repaid.Select(p => (p.CreatedAt, "debtRepayment", p.Method?.ToString(), p.Amount)));
        ops.AddRange(returns.Where(r => r.Refunded > 0).Select(r => (r.CreatedAt, "refund", r.RefundMethod?.ToString(), -r.Refunded)));
        ops.AddRange(supplier.Select(p => (p.CreatedAt, "supplierPayment", (string?)p.Method.ToString(), -p.Amount)));

        var inflow = Zeros(c); var outflow = Zeros(c);
        foreach (var o in ops) { var i = c.BucketOf(o.At); if (i < 0) continue; if (o.Amount >= 0) inflow[i] += o.Amount; else outflow[i] += -o.Amount; }
        var net = inflow.Select((v, i) => v - outflow[i]).ToArray();

        var inByMethod = Shares(ops.Where(o => o.Amount > 0).GroupBy(o => o.Method ?? "Other").Select(g => (g.Key, (string?)null, g.Sum(o => o.Amount), (int?)g.Count())));
        var outByType = Shares(ops.Where(o => o.Amount < 0).GroupBy(o => o.Type).Select(g => (g.Key, (string?)null, -g.Sum(o => o.Amount), (int?)g.Count())));

        var rows = ops.Select(o => (Bucket: c.BucketOf(o.At), o.Type, o.Method, o.Amount)).Where(o => o.Bucket >= 0)
            .GroupBy(o => (o.Bucket, o.Type, o.Method))
            .Select(g => new Row
            {
                ["bucketStart"] = c.Buckets[g.Key.Bucket].Start, ["bucketEnd"] = c.Buckets[g.Key.Bucket].End, ["type"] = g.Key.Type, ["method"] = g.Key.Method,
                ["operations"] = g.Count(), ["amount"] = g.Sum(o => o.Amount),
            }).ToList();
        var columns = new[] { Col("bucketStart", "period"), Col("type", "enum"), Col("method", "enum"), Col("operations", "int"), Col("amount", "money") };
        return new AnalyticsTabDto(c.Meta(), kpis, c.Buckets,
            [new SeriesDto("inflow", "money", "bar", inflow), new SeriesDto("outflow", "money", "bar", outflow), new SeriesDto("netFlow", "money", "line", net)],
            [Breakdown("inflowByMethod", "money", inByMethod), Breakdown("outflowByType", "money", outByType)],
            Table(columns, rows, q, "bucketStart", false, all),
            ["flowIsNotProfit", "inflowIsNotRevenue", "debtSalesNotInflow"]);
    }

    // ---------- Бонус (§20) ----------

    private async Task<AnalyticsTabDto> BonusTabAsync(Ctx c, AnalyticsQuery q, bool all, CancellationToken ct)
    {
        var tx = await repo.BonusTransactionsAsync(c.StoreIds, c.FromUtc, c.ToUtc, ct);
        var sales = await repo.SalesAsync(c.StoreIds, c.FromUtc, c.ToUtc, ct);
        var balance = await repo.BonusBalanceAsync(c.StoreIds, ct);
        var accrued = tx.Where(t => IsAccrual(t.Type)).Sum(t => t.Amount);
        var redeemed = -tx.Where(t => t.Type == BonusTransactionType.Redemption).Sum(t => t.Amount);
        var adjustments = tx.Where(t => t.Type is BonusTransactionType.ReturnRestore or BonusTransactionType.ReturnReversal or BonusTransactionType.Expiration).ToList();
        var withBonus = sales.Count(s => s.BonusRedeemed > 0);

        var kpis = new List<KpiDto>
        {
            Kpi("bonusAccrued", accrued, null, numerator: tx.Count(t => IsAccrual(t.Type))),
            Kpi("bonusRedeemed", redeemed, null, numerator: tx.Count(t => t.Type == BonusTransactionType.Redemption)),
            Kpi("bonusAdjustments", adjustments.Sum(t => t.Amount), null, numerator: adjustments.Count),
            Kpi("bonusBalance", balance, null, note: "asOfNow"),
            Kpi("salesWithBonus", withBonus, null, numerator: withBonus, denominator: sales.Count),
        };
        var acc = Zeros(c); var red = Zeros(c);
        foreach (var t in tx)
        {
            var i = c.BucketOf(t.CreatedAt);
            if (i < 0) continue;
            if (IsAccrual(t.Type)) acc[i] += t.Amount;
            else if (t.Type == BonusTransactionType.Redemption) red[i] += -t.Amount;
        }
        var storeNames = c.Stores.ToDictionary(s => s.Id, s => s.Name);
        var byType = Shares(tx.GroupBy(t => t.Type).Select(g => (g.Key.ToString(), (string?)null, (decimal)Math.Abs(g.Sum(t => t.Amount)), (int?)g.Count())));
        var byStore = Shares(tx.Where(t => IsAccrual(t.Type)).GroupBy(t => t.StoreId)
            .Select(g => (g.Key.ToString(), (string?)storeNames.GetValueOrDefault(g.Key), (decimal)g.Sum(t => t.Amount), (int?)g.Count())));
        var rows = tx.GroupBy(t => (t.Type, t.StoreId)).Select(g => new Row
        {
            ["type"] = g.Key.Type.ToString(), ["store"] = storeNames.GetValueOrDefault(g.Key.StoreId), ["operations"] = g.Count(),
            ["points"] = (decimal)g.Sum(t => t.Amount), ["sales"] = g.Where(t => t.ReceiptId != null).Select(t => t.ReceiptId).Distinct().Count(),
            ["lastAt"] = g.Max(t => t.CreatedAt),
        }).ToList();
        var columns = new[] { Col("type", "enum"), Col("store", "text"), Col("operations", "int"), Col("points", "points"), Col("sales", "int"), Col("lastAt", "datetime") };
        return new AnalyticsTabDto(c.Meta(), kpis, c.Buckets,
            [new SeriesDto("bonusAccrued", "points", "bar", acc), new SeriesDto("bonusRedeemed", "points", "bar", red)],
            [Breakdown("bonusByType", "points", byType), Breakdown("bonusByStore", "points", byStore)],
            Table(columns, rows, q, "points", true, all),
            ["bonusNotRevenue", "balanceAsOfNow"]);
    }

    // ---------- Клиенты (§21) ----------

    private async Task<AnalyticsTabDto> CustomersTabAsync(Ctx c, AnalyticsQuery q, bool all, CancellationToken ct)
    {
        var sales = await repo.SalesAsync(c.StoreIds, c.FromUtc, c.ToUtc, ct);
        var buyers = sales.Where(s => s.CustomerId != null).Select(s => s.CustomerId!.Value).Distinct().ToList();
        var history = await repo.CustomerHistoryAsync(c.OrgId, buyers, ct);
        var inactiveDays = q.InactiveDays is 60 or 90 ? q.InactiveDays.Value : 30;
        var inactiveCount = await repo.InactiveCustomersCountAsync(c.StoreIds, c.NowUtc.AddDays(-inactiveDays), ct);
        var isNew = buyers.ToDictionary(b => b, b => history.TryGetValue(b, out var h) && h.FirstAt >= c.FromUtc);
        var repeat = isNew.Count(x => !x.Value);
        var period = sales.Where(s => s.CustomerId != null).GroupBy(s => s.CustomerId!.Value)
            .ToDictionary(g => g.Key, g => (Count: g.Count(), Amount: g.Sum(s => s.Total), First: g.Min(s => s.CreatedAt), Last: g.Max(s => s.CreatedAt)));

        var kpis = new List<KpiDto>
        {
            Kpi("customersInBase", await repo.CustomersInBaseAsync(c.StoreIds, ct), null),
            Kpi("buyers", buyers.Count, null),
            Kpi("newCustomers", buyers.Count - repeat, null),
            Kpi("repeatCustomers", repeat, null, numerator: repeat, denominator: buyers.Count),
            new KpiDto("returningRate", Rate(repeat, buyers.Count), null, null, null, Numerator: repeat, Denominator: buyers.Count,
                Available: buyers.Count > 0, Note: buyers.Count > 0 ? null : "noDenominator"),
            Kpi("avgPurchases", buyers.Count == 0 ? 0 : Math.Round((decimal)period.Values.Sum(v => v.Count) / buyers.Count, 2), null),
            Kpi("inactiveCustomers", inactiveCount, null, numerator: inactiveDays),
            Kpi("unidentifiedReceipts", sales.Count(s => s.CustomerId == null), null, numerator: sales.Count),
        };
        var newSeries = Zeros(c); var repeatSeries = Zeros(c);
        foreach (var (id, v) in period)
        {
            var i = c.BucketOf(v.First);
            if (i < 0) continue;
            if (isNew[id]) newSeries[i]++; else repeatSeries[i]++;
        }
        var frequency = Shares(new[] { ("1", 1, 1), ("2-3", 2, 3), ("4-5", 4, 5), ("6+", 6, int.MaxValue) }
            .Select(f => (f.Item1, (string?)null, (decimal)period.Values.Count(v => v.Count >= f.Item2 && v.Count <= f.Item3), (int?)null)));

        // Тізім: сегмент бойынша (§21).
        var segment = q.Segment is "new" or "repeat" or "regular" or "inactive" ? q.Segment : "buyers";
        List<(Guid Id, int Purchases, decimal Amount, int Total, DateTime Last)> picked;
        if (segment == "inactive")
        {
            var inactive = await repo.InactiveCustomersAsync(c.StoreIds, c.NowUtc.AddDays(-inactiveDays), 1000, ct);
            picked = inactive.Select(h => (h.CustomerId, 0, 0m, h.Purchases, h.LastAt)).ToList();
        }
        else
        {
            picked = buyers.Where(b => segment switch
                {
                    "new" => isNew[b],
                    "repeat" => !isNew[b],
                    "regular" => history.TryGetValue(b, out var h) && h.Purchases >= RegularThreshold,
                    _ => true,
                })
                .Select(b => (b, period[b].Count, period[b].Amount, history.TryGetValue(b, out var h) ? h.Purchases : period[b].Count,
                    history.TryGetValue(b, out var h2) ? h2.LastAt : period[b].Last)).ToList();
        }
        var people = await repo.CustomersAsync(picked.Select(p => p.Id).ToList(), ct);
        var rows = picked.Select(p => new Row
        {
            ["customerId"] = p.Id, ["customer"] = people.GetValueOrDefault(p.Id)?.Name is { Length: > 0 } n ? n : "—",
            ["phone"] = Phone(people.GetValueOrDefault(p.Id)?.Phone, c.CanContacts), ["purchases"] = p.Purchases, ["amount"] = p.Amount,
            ["purchasesTotal"] = p.Total, ["lastPurchase"] = p.Last,
        }).ToList();
        var columns = new[]
        {
            Col("customer", "text"), Col("phone", "text", false), Col("purchases", "int"), Col("amount", "money"), Col("purchasesTotal", "int"), Col("lastPurchase", "datetime"),
        };
        return new AnalyticsTabDto(c.Meta(), kpis, c.Buckets,
            [new SeriesDto("newCustomers", "count", "bar", newSeries), new SeriesDto("repeatCustomers", "count", "bar", repeatSeries)],
            [Breakdown("frequency", "count", frequency)],
            Table(columns, rows, q, segment == "inactive" ? "lastPurchase" : "amount", segment != "inactive", all),
            ["newIsFirstInCompany", "regularThreshold", "unidentifiedExcluded", c.CanContacts ? "contactsVisible" : "contactsHidden"]);
    }

    // ---------- Сотрудники (§22) ----------

    private async Task<AnalyticsTabDto> EmployeesTabAsync(Ctx c, AnalyticsQuery q, bool all, CancellationToken ct)
    {
        var sales = Filter(await repo.SalesAsync(c.StoreIds, c.FromUtc, c.ToUtc, ct), q);
        var returns = FilterReturns(await repo.ReturnsAsync(c.StoreIds, c.FromUtc, c.ToUtc, ct), q);
        var ids = sales.Select(s => s.StaffUserId).Concat(returns.Select(r => r.StaffUserId)).Distinct().ToList();
        var names = await repo.StaffNamesAsync(ids, ct);
        var revenue = sales.Sum(s => s.Total);

        var kpis = new List<KpiDto>
        {
            Kpi("activeStaff", sales.Select(s => s.StaffUserId).Distinct().Count(), null),
            Kpi("salesCount", sales.Count, null),
            Kpi("revenue", revenue, null),
            Kpi("averageCheck", sales.Count == 0 ? 0 : Math.Round(revenue / sales.Count, 0), null, numerator: revenue, denominator: sales.Count),
            Kpi("returns", returns.Count, null, returns.Sum(r => r.Amount)),
            Kpi("discounts", sales.Sum(s => s.DiscountAmount), null, numerator: sales.Count(s => s.DiscountAmount > 0)),
        };
        var rev = Zeros(c); var cnt = Zeros(c);
        foreach (var s in sales) { var i = c.BucketOf(s.CreatedAt); if (i < 0) continue; rev[i] += s.Total; cnt[i]++; }
        var rows = ids.Select(id =>
        {
            var mine = sales.Where(s => s.StaffUserId == id).ToList();
            var back = returns.Where(r => r.StaffUserId == id).ToList();
            return new Row
            {
                ["staffUserId"] = id, ["employee"] = names.GetValueOrDefault(id) ?? "—", ["receipts"] = mine.Count, ["revenue"] = mine.Sum(s => s.Total),
                ["average"] = mine.Count == 0 ? null : Math.Round(mine.Sum(s => s.Total) / mine.Count, 0),
                ["returns"] = back.Count, ["returnsAmount"] = back.Sum(r => r.Amount),
                ["discounts"] = mine.Sum(s => s.DiscountAmount), ["discountReceipts"] = mine.Count(s => s.DiscountAmount > 0),
            };
        }).ToList();
        var columns = new[]
        {
            Col("employee", "text"), Col("receipts", "int"), Col("revenue", "money"), Col("average", "money"), Col("returns", "int"),
            Col("returnsAmount", "money"), Col("discounts", "money"), Col("discountReceipts", "int"),
        };
        return new AnalyticsTabDto(c.Meta(), kpis, c.Buckets,
            [new SeriesDto("revenue", "money", "bar", rev), new SeriesDto("salesCount", "count", "line", cnt)],
            [Breakdown("revenueByEmployee", "money", Shares(rows.Select(r => (r["staffUserId"]!.ToString()!, (string?)r["employee"], (decimal)r["revenue"]!, (int?)(int)r["receipts"]!))
                .OrderByDescending(x => x.Item3)))],
            Table(columns, rows, q, "revenue", true, all),
            ["noConclusions"]);
    }

    // ---------- Касса (§23) ----------

    private async Task<AnalyticsTabDto> RegistersTabAsync(Ctx c, AnalyticsQuery q, bool all, CancellationToken ct)
    {
        var sales = Filter(await repo.SalesAsync(c.StoreIds, c.FromUtc, c.ToUtc, ct), q);
        var returns = FilterReturns(await repo.ReturnsAsync(c.StoreIds, c.FromUtc, c.ToUtc, ct), q);
        var registers = await repo.RegistersAsync(c.StoreIds, ct);
        var storeNames = c.Stores.ToDictionary(s => s.Id, s => s.Name);
        var revenue = sales.Sum(s => s.Total);

        var kpis = new List<KpiDto>
        {
            Kpi("activeRegisters", sales.Select(s => s.RegisterId).Distinct().Count(), null, numerator: registers.Count),
            Kpi("salesCount", sales.Count, null),
            Kpi("revenue", revenue, null),
            Kpi("averageCheck", sales.Count == 0 ? 0 : Math.Round(revenue / sales.Count, 0), null, numerator: revenue, denominator: sales.Count),
            Kpi("returns", returns.Count, null, returns.Sum(r => r.Amount)),
            Unavailable("shifts", "shiftsNotImplemented"),
        };
        var rev = Zeros(c); var cnt = Zeros(c);
        foreach (var s in sales) { var i = c.BucketOf(s.CreatedAt); if (i < 0) continue; rev[i] += s.Total; cnt[i]++; }
        var ids = sales.Select(s => s.RegisterId).Concat(returns.Select(r => r.RegisterId)).Distinct().ToList();
        var rows = ids.Select(id =>
        {
            var mine = sales.Where(s => s.RegisterId == id).ToList();
            var back = returns.Where(r => r.RegisterId == id).ToList();
            var reg = registers.GetValueOrDefault(id);
            var lastOp = mine.Select(s => (DateTime?)s.CreatedAt).Concat(back.Select(r => (DateTime?)r.CreatedAt)).Max();
            return new Row
            {
                ["registerId"] = id, ["register"] = reg.Name ?? "—", ["store"] = storeNames.GetValueOrDefault(reg.StoreId),
                ["cashiers"] = mine.Select(s => s.StaffUserId).Distinct().Count(), ["receipts"] = mine.Count, ["revenue"] = mine.Sum(s => s.Total),
                ["returns"] = back.Count, ["returnsAmount"] = back.Sum(r => r.Amount),
                ["average"] = mine.Count == 0 ? null : Math.Round(mine.Sum(s => s.Total) / mine.Count, 0), ["lastOperation"] = lastOp,
            };
        }).ToList();
        var columns = new[]
        {
            Col("register", "text"), Col("store", "text"), Col("cashiers", "int"), Col("receipts", "int"), Col("revenue", "money"), Col("returns", "int"),
            Col("returnsAmount", "money"), Col("average", "money"), Col("lastOperation", "datetime"),
        };
        return new AnalyticsTabDto(c.Meta(), kpis, c.Buckets,
            [new SeriesDto("revenue", "money", "bar", rev), new SeriesDto("salesCount", "count", "line", cnt)],
            [Breakdown("paymentMethods", "money", PaymentShares(sales)),
             Breakdown("revenueByRegister", "money", Shares(rows.Select(r => (r["registerId"]!.ToString()!, (string?)r["register"], (decimal)r["revenue"]!, (int?)(int)r["receipts"]!))))],
            Table(columns, rows, q, "revenue", true, all),
            ["shiftsNotImplemented"]);
    }
}
