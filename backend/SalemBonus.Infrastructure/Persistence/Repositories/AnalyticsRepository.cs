using Microsoft.EntityFrameworkCore;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Domain.Pos.Catalog;
using SalemBonus.Domain.Pos.Inventory;
using SalemBonus.Domain.Pos.Sales;

namespace SalemBonus.Infrastructure.Persistence.Repositories;

public class AnalyticsRepository(AppDbContext db) : IAnalyticsRepository
{
    public async Task<IReadOnlyList<Sale>> SalesAsync(IReadOnlyCollection<Guid> storeIds, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default) =>
        await db.Sales.AsNoTracking().Include(s => s.Items).Include(s => s.Payments).AsSplitQuery()
            .Where(s => storeIds.Contains(s.StoreId) && s.CreatedAt >= fromUtc && s.CreatedAt < toUtc)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<SaleReturn>> ReturnsAsync(IReadOnlyCollection<Guid> storeIds, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default) =>
        await db.SaleReturns.AsNoTracking().Include(r => r.Items)
            .Where(r => storeIds.Contains(r.StoreId) && r.CreatedAt >= fromUtc && r.CreatedAt < toUtc)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<AnalyticsDebtPayment>> DebtPaymentsAsync(IReadOnlyCollection<Guid> storeIds, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default) =>
        await (from p in db.DebtPayments
               join d in db.Debts on p.DebtId equals d.Id
               where storeIds.Contains(d.StoreId) && p.CreatedAt >= fromUtc && p.CreatedAt < toUtc
               select new AnalyticsDebtPayment(p.CreatedAt, p.Amount, p.Method, p.IsReturn, p.StaffUserId, p.RegisterId)).ToListAsync(ct);

    public async Task<(int Count, decimal Remaining)> OpenDebtsAsync(IReadOnlyCollection<Guid> storeIds, CancellationToken ct = default)
    {
        var open = db.Debts.Where(d => storeIds.Contains(d.StoreId) && d.Status == DebtStatus.Open);
        return (await open.CountAsync(ct), await open.SumAsync(d => (decimal?)(d.Amount - d.Paid), ct) ?? 0);
    }

    public async Task<Dictionary<Guid, AnalyticsCustomerHistory>> CustomerHistoryAsync(Guid orgId, IReadOnlyCollection<Guid> customerIds, CancellationToken ct = default) =>
        customerIds.Count == 0 ? [] : await db.Sales.AsNoTracking()
            .Where(s => s.OrganizationId == orgId && s.CustomerId != null && customerIds.Contains(s.CustomerId.Value))
            .GroupBy(s => s.CustomerId!.Value)
            .Select(g => new AnalyticsCustomerHistory(g.Key, g.Min(s => s.CreatedAt), g.Max(s => s.CreatedAt), g.Count(), g.Sum(s => s.Total)))
            .ToDictionaryAsync(x => x.CustomerId, ct);

    /// <summary>Дүкендер бойынша клиент тарихы: соңғы сатып алуы берілген сәттен ерте болғандар (EF аударатын анонимді проекция).</summary>
    private IQueryable<HistoryRow> InactiveHistory(IReadOnlyCollection<Guid> storeIds, DateTime lastBeforeUtc) =>
        db.Sales.AsNoTracking()
            .Where(s => storeIds.Contains(s.StoreId) && s.CustomerId != null)
            .GroupBy(s => s.CustomerId!.Value)
            .Select(g => new HistoryRow { CustomerId = g.Key, FirstAt = g.Min(s => s.CreatedAt), LastAt = g.Max(s => s.CreatedAt), Purchases = g.Count(), Amount = g.Sum(s => s.Total) })
            .Where(h => h.LastAt < lastBeforeUtc);

    private sealed class HistoryRow
    {
        public Guid CustomerId { get; init; }
        public DateTime FirstAt { get; init; }
        public DateTime LastAt { get; init; }
        public int Purchases { get; init; }
        public decimal Amount { get; init; }
    }

    public async Task<IReadOnlyList<AnalyticsCustomerHistory>> InactiveCustomersAsync(IReadOnlyCollection<Guid> storeIds, DateTime lastBeforeUtc, int take, CancellationToken ct = default) =>
        (await InactiveHistory(storeIds, lastBeforeUtc).OrderByDescending(h => h.Amount).Take(take).ToListAsync(ct))
        .Select(h => new AnalyticsCustomerHistory(h.CustomerId, h.FirstAt, h.LastAt, h.Purchases, h.Amount)).ToList();

    public Task<int> InactiveCustomersCountAsync(IReadOnlyCollection<Guid> storeIds, DateTime lastBeforeUtc, CancellationToken ct = default) =>
        InactiveHistory(storeIds, lastBeforeUtc).CountAsync(ct);

    public Task<int> CustomersInBaseAsync(IReadOnlyCollection<Guid> storeIds, CancellationToken ct = default) =>
        db.BonusCards.Where(c => storeIds.Contains(c.StoreId)).Select(c => c.CustomerId).Distinct().CountAsync(ct);

    public async Task<Dictionary<Guid, AnalyticsCustomer>> CustomersAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
        ids.Count == 0 ? [] : await db.Customers.AsNoTracking().Where(c => ids.Contains(c.Id))
            .Select(c => new AnalyticsCustomer(c.Id, (c.FirstName + " " + c.LastName).Trim(), c.Phone))
            .ToDictionaryAsync(c => c.Id, ct);

    public async Task<IReadOnlyList<AnalyticsBonusTx>> BonusTransactionsAsync(IReadOnlyCollection<Guid> storeIds, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default) =>
        await (from t in db.BonusTransactions
               join c in db.BonusCards on t.BonusCardId equals c.Id
               where storeIds.Contains(c.StoreId) && t.CreatedAt >= fromUtc && t.CreatedAt < toUtc
               select new AnalyticsBonusTx(t.CreatedAt, t.Type, t.Amount, t.ReceiptId, c.StoreId)).ToListAsync(ct);

    public async Task<int> BonusBalanceAsync(IReadOnlyCollection<Guid> storeIds, CancellationToken ct = default) =>
        await db.BonusCards.Where(c => storeIds.Contains(c.StoreId)).SumAsync(c => (int?)c.Balance, ct) ?? 0;

    public async Task<IReadOnlyList<AnalyticsProduct>> ProductsAsync(Guid orgId, IReadOnlyCollection<Guid>? ids, CancellationToken ct = default)
    {
        var q = db.Products.AsNoTracking().Where(p => p.OrganizationId == orgId);
        if (ids is not null) q = q.Where(p => ids.Contains(p.Id));
        return await q.Select(p => new AnalyticsProduct(
                p.Id, p.Name, p.Article, p.CatalogNodeId, p.BrandId,
                db.Brands.Where(b => b.Id == p.BrandId).Select(b => b.Name).FirstOrDefault(),
                db.Units.Where(u => u.Id == p.UnitId).Select(u => u.ShortName).FirstOrDefault(),
                p.Images.OrderByDescending(i => i.IsPrimary).ThenBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault(),
                p.MinStock, p.Type == ProductType.Goods, p.Status == CatalogStatus.Active))
            .ToListAsync(ct);
    }

    public async Task<Dictionary<Guid, (Guid RootId, string RootName)>> NodeRootsAsync(Guid orgId, CancellationToken ct = default)
    {
        var nodes = await db.CatalogNodes.AsNoTracking().Where(n => n.OrganizationId == orgId)
            .Select(n => new { n.Id, n.Name, n.Path }).ToListAsync(ct);
        var names = nodes.ToDictionary(n => n.Id, n => n.Name);
        return nodes.ToDictionary(n => n.Id, n =>
            Guid.TryParse(n.Path.Split('/')[0], out var root) && names.TryGetValue(root, out var rootName) ? (root, rootName) : (n.Id, n.Name));
    }

    public async Task<IReadOnlyList<Guid>> ProductIdsByBarcodeAsync(Guid orgId, string barcode, CancellationToken ct = default) =>
        await db.ProductBarcodes.AsNoTracking().Where(b => b.OrganizationId == orgId && b.Barcode == barcode).Select(b => b.ProductId).ToListAsync(ct);

    public async Task<Dictionary<(Guid StoreId, Guid ProductId), decimal>> PurchasePricesAsync(IReadOnlyCollection<Guid> storeIds, CancellationToken ct = default) =>
        await db.ProductPrices.AsNoTracking().Where(p => storeIds.Contains(p.StoreId) && p.PurchasePrice != null)
            .ToDictionaryAsync(p => (p.StoreId, p.ProductId), p => p.PurchasePrice!.Value, ct);

    public async Task<IReadOnlyList<(Guid Id, Guid StoreId, string Name, bool IsActive)>> WarehousesAsync(IReadOnlyCollection<Guid> storeIds, CancellationToken ct = default) =>
        (await db.Warehouses.AsNoTracking().Where(w => storeIds.Contains(w.StoreId)).OrderByDescending(w => w.IsDefault).ThenBy(w => w.Name)
            .Select(w => new { w.Id, w.StoreId, w.Name, w.IsActive }).ToListAsync(ct))
        .Select(w => (w.Id, w.StoreId, w.Name, w.IsActive)).ToList();

    public async Task<IReadOnlyList<AnalyticsBalance>> BalancesAsync(IReadOnlyCollection<Guid> warehouseIds, CancellationToken ct = default) =>
        await db.StockBalances.AsNoTracking().Where(b => warehouseIds.Contains(b.WarehouseId))
            .Select(b => new AnalyticsBalance(b.WarehouseId, b.ProductId, b.Quantity)).ToListAsync(ct);

    public async Task<IReadOnlyList<AnalyticsMovement>> MovementsAsync(IReadOnlyCollection<Guid> warehouseIds, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default) =>
        await db.StockMovements.AsNoTracking()
            .Where(m => warehouseIds.Contains(m.WarehouseId) && m.CreatedAt >= fromUtc && m.CreatedAt < toUtc)
            .Select(m => new AnalyticsMovement(m.CreatedAt, m.WarehouseId, m.ProductId, m.Type, m.Quantity)).ToListAsync(ct);

    public async Task<Dictionary<(Guid WarehouseId, Guid ProductId), DateTime>> LastMovementsAsync(IReadOnlyCollection<Guid> warehouseIds, CancellationToken ct = default) =>
        await db.StockMovements.AsNoTracking().Where(m => warehouseIds.Contains(m.WarehouseId))
            .GroupBy(m => new { m.WarehouseId, m.ProductId })
            .Select(g => new { g.Key.WarehouseId, g.Key.ProductId, Last = g.Max(m => m.CreatedAt) })
            .ToDictionaryAsync(x => (x.WarehouseId, x.ProductId), x => x.Last, ct);

    public async Task<IReadOnlyList<AnalyticsTransfer>> TransfersAsync(IReadOnlyCollection<Guid> warehouseIds, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default) =>
        await db.StockTransfers.AsNoTracking()
            .Where(t => (warehouseIds.Contains(t.SourceWarehouseId) || warehouseIds.Contains(t.DestinationWarehouseId))
                        && t.SentAt != null && t.SentAt >= fromUtc && t.SentAt < toUtc)
            .Select(t => new AnalyticsTransfer(t.SentAt, t.ReceivedAt, t.Status, t.SourceWarehouseId, t.DestinationWarehouseId,
                t.Lines.Sum(l => l.QuantitySent)))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<AnalyticsDocument>> DocumentsAsync(IReadOnlyCollection<Guid> storeIds, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct = default)
    {
        var q = db.StockDocuments.AsNoTracking().Where(d => storeIds.Contains(d.StoreId));
        if (fromUtc is { } from) q = q.Where(d => d.Date >= from);
        if (toUtc is { } to) q = q.Where(d => d.Date < to);
        return await q.Select(d => new AnalyticsDocument(d.Id, d.StoreId, d.WarehouseId, d.Type, d.Status, d.Date, d.PostedAt, d.SupplierId, d.Total, d.Paid, d.Lines.Count))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AnalyticsSupplierPayment>> SupplierPaymentsAsync(IReadOnlyCollection<Guid> storeIds, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default) =>
        await db.SupplierPayments.AsNoTracking()
            .Where(p => storeIds.Contains(p.StoreId) && p.Status == SupplierPaymentStatus.Registered && p.CreatedAt >= fromUtc && p.CreatedAt < toUtc)
            .Select(p => new AnalyticsSupplierPayment(p.CreatedAt, p.Date, p.SupplierId, p.DocumentId, p.Amount, p.Method)).ToListAsync(ct);

    public async Task<Dictionary<Guid, string>> SupplierNamesAsync(Guid orgId, CancellationToken ct = default) =>
        await db.Suppliers.AsNoTracking().Where(s => s.OrganizationId == orgId).ToDictionaryAsync(s => s.Id, s => s.Name, ct);

    public async Task<Dictionary<Guid, string>> StaffNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
        ids.Count == 0 ? [] : await db.StaffUsers.AsNoTracking().Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => (u.FirstName + " " + u.LastName).Trim(), ct);

    public async Task<Dictionary<Guid, (string Name, Guid StoreId)>> RegistersAsync(IReadOnlyCollection<Guid> storeIds, CancellationToken ct = default) =>
        (await db.Registers.AsNoTracking().Where(r => storeIds.Contains(r.StoreId)).Select(r => new { r.Id, r.Name, r.StoreId }).ToListAsync(ct))
        .ToDictionary(r => r.Id, r => (r.Name, r.StoreId));
}
