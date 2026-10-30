using Microsoft.EntityFrameworkCore;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Domain.Entities;
using SalemBonus.Domain.Pos.Catalog;
using SalemBonus.Domain.Pos.Inventory;

namespace SalemBonus.Infrastructure.Persistence.Repositories;

public class InventoryRepository(AppDbContext db) : IInventoryRepository
{
    // ---------- Қоймалар ----------

    public async Task<IReadOnlyList<Warehouse>> ListWarehousesAsync(Guid storeId, CancellationToken ct = default) =>
        await db.Warehouses.Where(w => w.StoreId == storeId)
            .OrderByDescending(w => w.IsDefault).ThenBy(w => w.Name).ToListAsync(ct);

    public async Task<IReadOnlyList<Warehouse>> ListOrgWarehousesAsync(Guid orgId, CancellationToken ct = default) =>
        await db.Warehouses.AsNoTracking().Where(w => w.OrganizationId == orgId)
            .OrderByDescending(w => w.IsDefault).ThenBy(w => w.Name).ToListAsync(ct);

    public Task<Warehouse?> GetWarehouseAsync(Guid storeId, Guid id, CancellationToken ct = default) =>
        db.Warehouses.FirstOrDefaultAsync(w => w.StoreId == storeId && w.Id == id && w.IsActive, ct);

    public Task<Warehouse?> GetWarehouseForUpdateAsync(Guid orgId, Guid id, CancellationToken ct = default) =>
        db.Warehouses.FirstOrDefaultAsync(w => w.OrganizationId == orgId && w.Id == id, ct);

    public async Task<IReadOnlyList<Warehouse>> ListWarehousesForUpdateAsync(Guid storeId, CancellationToken ct = default) =>
        await db.Warehouses.Where(w => w.StoreId == storeId).ToListAsync(ct);

    public Task<bool> WarehouseNameExistsAsync(Guid storeId, string name, Guid? excludeId, CancellationToken ct = default) =>
        db.Warehouses.AnyAsync(w => w.StoreId == storeId && w.Id != excludeId && w.Name.ToLower() == name.ToLower(), ct);

    public async Task<Dictionary<Guid, WarehouseTotals>> WarehouseTotalsAsync(Guid orgId, CancellationToken ct = default) =>
        await (from b in db.StockBalances
               join w in db.Warehouses on b.WarehouseId equals w.Id
               where w.OrganizationId == orgId && b.Quantity != 0
               group b by b.WarehouseId into g
               select new { g.Key, Kinds = g.Count(x => x.Quantity > 0), Units = g.Sum(x => x.Quantity) })
            .ToDictionaryAsync(x => x.Key, x => new WarehouseTotals(x.Kinds, x.Units), ct);

    public async Task<bool> WarehouseHasOpenDocumentsAsync(Guid warehouseId, CancellationToken ct = default) =>
        await db.StockDocuments.AnyAsync(d => d.WarehouseId == warehouseId && d.Status == StockDocumentStatus.Draft, ct)
        || await db.StockTransfers.AnyAsync(t => (t.SourceWarehouseId == warehouseId || t.DestinationWarehouseId == warehouseId)
                                                  && (t.Status == TransferStatus.Draft || t.Status == TransferStatus.InTransit), ct);

    public void AddWarehouse(Warehouse warehouse) => db.Warehouses.Add(warehouse);

    // ---------- Қалдық ----------

    public async Task<IReadOnlyList<StockRow>> StockByProductAsync(Guid orgId, Guid productId, CancellationToken ct = default) =>
        await (from b in db.StockBalances
               join w in db.Warehouses on b.WarehouseId equals w.Id
               join s in db.Stores on w.StoreId equals s.Id
               where b.ProductId == productId && w.OrganizationId == orgId
               orderby s.Name, w.Name
               select new StockRow(w.Id, w.Name, s.Id, s.Name, b.Quantity)).ToListAsync(ct);

    public async Task<Dictionary<Guid, StockBalance>> BalancesForUpdateAsync(Guid warehouseId, IReadOnlyCollection<Guid> productIds, CancellationToken ct = default)
    {
        if (productIds.Count == 0) return [];
        var ids = productIds.ToArray();
        return await db.StockBalances
            .FromSql($"""SELECT * FROM pos.stock_balances WHERE "WarehouseId" = {warehouseId} AND "ProductId" = ANY({ids}) FOR UPDATE""")
            .ToDictionaryAsync(b => b.ProductId, ct);
    }

    public async Task<Dictionary<Guid, decimal>> BalancesAsync(Guid warehouseId, IReadOnlyCollection<Guid> productIds, CancellationToken ct = default)
    {
        if (productIds.Count == 0) return [];
        return await db.StockBalances.AsNoTracking().Where(b => b.WarehouseId == warehouseId && productIds.Contains(b.ProductId))
            .ToDictionaryAsync(b => b.ProductId, b => b.Quantity, ct);
    }

    public async Task<(IReadOnlyList<StockProductRow> Items, int Total)> SearchStockAsync(Guid orgId, StockFilter f, CancellationToken ct = default)
    {
        var wids = f.WarehouseIds.ToArray();
        var q = db.Products.AsNoTracking().Where(p => p.OrganizationId == orgId);
        if (f.Status is { } st) q = q.Where(p => p.Status == st);
        if (f.BrandId is { } bid) q = q.Where(p => p.BrandId == bid);
        if (f.NodePath is { } path)
            q = q.Where(p => p.CatalogNodeId != null && db.CatalogNodes.Any(n => n.Id == p.CatalogNodeId && (n.Path == path || n.Path.StartsWith(path + "/"))));
        q = q.Search(db, f.Search);

        var totals = db.StockBalances.Where(b => wids.Contains(b.WarehouseId)).GroupBy(b => b.ProductId)
            .Select(g => new { ProductId = g.Key, Total = g.Sum(x => x.Quantity) });
        var joined = from p in q
                     join t in totals on p.Id equals t.ProductId into pt
                     from t in pt.DefaultIfEmpty()
                     select new { Product = p, Total = (decimal?)t.Total ?? 0 };

        joined = f.Availability switch
        {
            "in" => joined.Where(x => x.Total > 0),
            "out" => joined.Where(x => x.Total <= 0),
            "belowMin" => joined.Where(x => x.Product.MinStock != null && x.Total <= x.Product.MinStock),
            _ => joined,
        };
        joined = f.Sort switch
        {
            "stockDesc" => joined.OrderByDescending(x => x.Total).ThenBy(x => x.Product.Name),
            "stockAsc" => joined.OrderBy(x => x.Total).ThenBy(x => x.Product.Name),
            "article" => joined.OrderBy(x => x.Product.Article).ThenBy(x => x.Product.Name),
            _ => joined.OrderBy(x => x.Product.Name),
        };

        var total = await joined.CountAsync(ct);
        var page = await joined.Skip(f.Skip).Take(f.Take).Select(x => x.Product).Include(p => p.Barcodes).Include(p => p.Images).AsSplitQuery().ToListAsync(ct);
        var ids = page.Select(p => p.Id).ToList();
        var balances = await db.StockBalances.AsNoTracking().Where(b => wids.Contains(b.WarehouseId) && ids.Contains(b.ProductId)).ToListAsync(ct);
        var last = await db.StockMovements.AsNoTracking().Where(m => wids.Contains(m.WarehouseId) && ids.Contains(m.ProductId))
            .GroupBy(m => m.ProductId).Select(g => new { g.Key, At = g.Max(m => m.CreatedAt) }).ToDictionaryAsync(x => x.Key, x => x.At, ct);
        var rows = page.Select(p => new StockProductRow(p,
            balances.Where(b => b.ProductId == p.Id).Select(b => (b.WarehouseId, b.Quantity)).ToList(),
            last.TryGetValue(p.Id, out var at) ? at : null)).ToList();
        return (rows, total);
    }

    public async Task<(int ProductKinds, decimal TotalUnits, int WithStock, int WithoutStock, int BelowMin)> StockKpiAsync(
        Guid orgId, IReadOnlyCollection<Guid> warehouseIds, decimal defaultMinStock, CancellationToken ct = default)
    {
        var wids = warehouseIds.ToArray();
        var products = db.Products.AsNoTracking().Where(p => p.OrganizationId == orgId && p.Status == CatalogStatus.Active);
        var totals = db.StockBalances.Where(b => wids.Contains(b.WarehouseId)).GroupBy(b => b.ProductId)
            .Select(g => new { ProductId = g.Key, Total = g.Sum(x => x.Quantity) });
        var joined = from p in products
                     join t in totals on p.Id equals t.ProductId into pt
                     from t in pt.DefaultIfEmpty()
                     select new { p.MinStock, Total = (decimal?)t.Total ?? 0 };
        var list = await joined.ToListAsync(ct);
        return (list.Count, list.Sum(x => x.Total), list.Count(x => x.Total > 0), list.Count(x => x.Total <= 0),
            list.Count(x => x.Total > 0 && x.Total <= (x.MinStock ?? defaultMinStock)));
    }

    public void AddBalance(StockBalance balance) => db.StockBalances.Add(balance);

    // ---------- Журнал ----------

    public async Task<(IReadOnlyList<StockMovement> Items, int Total)> SearchMovementsAsync(Guid orgId, MovementFilter f, CancellationToken ct = default)
    {
        var wids = f.WarehouseIds.ToArray();
        var q = db.StockMovements.AsNoTracking().Where(m => m.OrganizationId == orgId && wids.Contains(m.WarehouseId));
        if (f.ProductId is { } pid) q = q.Where(m => m.ProductId == pid);
        if (f.Type is { } type) q = q.Where(m => m.Type == type);
        if (f.StaffUserId is { } sid) q = q.Where(m => m.StaffUserId == sid);
        if (!string.IsNullOrWhiteSpace(f.DocumentNumber)) q = q.Where(m => m.DocumentNumber != null && EF.Functions.ILike(m.DocumentNumber, $"%{f.DocumentNumber}%"));
        if (f.From is { } from) q = q.Where(m => m.CreatedAt >= from);
        if (f.To is { } to) q = q.Where(m => m.CreatedAt < to);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id).Skip(f.Skip).Take(f.Take).ToListAsync(ct);
        return (items, total);
    }

    public void AddMovement(StockMovement movement) => db.StockMovements.Add(movement);

    // ---------- Бағалар ----------

    public Task<ProductPrice?> GetPriceAsync(Guid productId, Guid storeId, CancellationToken ct = default) =>
        db.ProductPrices.AsNoTracking().FirstOrDefaultAsync(p => p.ProductId == productId && p.StoreId == storeId, ct);

    public async Task<Dictionary<Guid, ProductPrice>> PricesAsync(Guid storeId, IReadOnlyCollection<Guid> productIds, CancellationToken ct = default)
    {
        if (productIds.Count == 0) return [];
        return await db.ProductPrices.Where(p => p.StoreId == storeId && productIds.Contains(p.ProductId)).ToDictionaryAsync(p => p.ProductId, ct);
    }

    public async Task<IReadOnlyList<ProductPrice>> ListPricesForUpdateAsync(Guid storeId, CancellationToken ct = default) =>
        await db.ProductPrices.Where(p => p.StoreId == storeId).ToListAsync(ct);

    public Task<ProductPrice?> GetPriceForUpdateAsync(Guid productId, Guid storeId, CancellationToken ct = default) =>
        db.ProductPrices.FirstOrDefaultAsync(p => p.ProductId == productId && p.StoreId == storeId, ct);

    public void AddPrice(ProductPrice price) => db.ProductPrices.Add(price);

    // ---------- Құжаттар ----------

    public async Task<long> NextDocumentNumberAsync(Guid orgId, string kind, CancellationToken ct = default)
    {
        // Бір сұраныс, жол транзакция соңына дейін бұғатталады: нөмір қайталанбайды, бос орын қалмайды.
        var numbers = await db.Database.SqlQuery<long>($"""
            INSERT INTO pos.document_counters ("OrganizationId", "Kind", "LastNumber") VALUES ({orgId}, {kind}, 1)
            ON CONFLICT ("OrganizationId", "Kind") DO UPDATE SET "LastNumber" = pos.document_counters."LastNumber" + 1
            RETURNING "LastNumber" AS "Value"
            """).ToListAsync(ct);
        return numbers[0];
    }

    public Task<StockDocument?> GetDocumentAsync(Guid orgId, Guid id, CancellationToken ct = default) =>
        db.StockDocuments.AsNoTracking().Include(d => d.Lines).FirstOrDefaultAsync(d => d.OrganizationId == orgId && d.Id == id, ct);

    public Task<StockDocument?> GetDocumentForUpdateAsync(Guid orgId, Guid id, CancellationToken ct = default) =>
        db.StockDocuments.Include(d => d.Lines).FirstOrDefaultAsync(d => d.OrganizationId == orgId && d.Id == id, ct);

    private IQueryable<StockDocument> DocumentsQuery(Guid orgId, DocumentFilter f)
    {
        var q = db.StockDocuments.AsNoTracking().Where(d => d.OrganizationId == orgId && d.Type == f.Type);
        if (f.StoreId is { } sid) q = q.Where(d => d.StoreId == sid);
        if (f.WarehouseId is { } wid) q = q.Where(d => d.WarehouseId == wid);
        if (f.SupplierId is { } sup) q = q.Where(d => d.SupplierId == sup);
        if (f.From is { } from) q = q.Where(d => d.Date >= from);
        if (f.To is { } to) q = q.Where(d => d.Date < to);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var like = $"%{f.Search}%";
            q = q.Where(d => EF.Functions.ILike(d.Number, like)
                             || (d.SupplierInvoiceNumber != null && EF.Functions.ILike(d.SupplierInvoiceNumber, like))
                             || (d.SupplierId != null && db.Suppliers.Any(s => s.Id == d.SupplierId && EF.Functions.ILike(s.Name, like)))
                             || d.Lines.Any(l => EF.Functions.ILike(l.Name, like)));
        }
        return q;
    }

    public async Task<(IReadOnlyList<StockDocument> Items, int Total)> SearchDocumentsAsync(Guid orgId, DocumentFilter f, CancellationToken ct = default)
    {
        var q = DocumentsQuery(orgId, f);
        if (f.Status is { } st) q = q.Where(d => d.Status == st);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(d => d.Seq).Skip(f.Skip).Take(f.Take).Include(d => d.Lines).AsSplitQuery().ToListAsync(ct);
        return (items, total);
    }

    public async Task<(int Total, int Drafts, int Posted, int AwaitingPayment)> DocumentStatsAsync(Guid orgId, DocumentFilter f, CancellationToken ct = default)
    {
        var q = DocumentsQuery(orgId, f);
        var rows = await q.Select(d => new { d.Status, d.Total, d.Paid, d.SupplierId }).ToListAsync(ct);
        return (rows.Count, rows.Count(r => r.Status == StockDocumentStatus.Draft), rows.Count(r => r.Status == StockDocumentStatus.Posted),
            rows.Count(r => r.Status == StockDocumentStatus.Posted && r.SupplierId != null && r.Paid < r.Total));
    }

    public async Task<IReadOnlyList<StockDocument>> ListSupplierDocumentsAsync(Guid orgId, Guid supplierId, CancellationToken ct = default) =>
        await db.StockDocuments.AsNoTracking().Include(d => d.Lines).Where(d => d.OrganizationId == orgId && d.SupplierId == supplierId)
            .OrderByDescending(d => d.Seq).Take(200).AsSplitQuery().ToListAsync(ct);

    public void AddDocument(StockDocument document) => db.StockDocuments.Add(document);
    public void RemoveDocumentLine(StockDocumentLine line) => db.Set<StockDocumentLine>().Remove(line);

    // ---------- Перемещения ----------

    public Task<StockTransfer?> GetTransferAsync(Guid orgId, Guid id, CancellationToken ct = default) =>
        db.StockTransfers.AsNoTracking().Include(t => t.Lines).FirstOrDefaultAsync(t => t.OrganizationId == orgId && t.Id == id, ct);

    public Task<StockTransfer?> GetTransferForUpdateAsync(Guid orgId, Guid id, CancellationToken ct = default) =>
        db.StockTransfers.Include(t => t.Lines).FirstOrDefaultAsync(t => t.OrganizationId == orgId && t.Id == id, ct);

    private IQueryable<StockTransfer> TransfersQuery(Guid orgId, TransferFilter f)
    {
        var q = db.StockTransfers.AsNoTracking().Where(t => t.OrganizationId == orgId);
        if (f.StoreId is { } sid)
            q = f.IncomingOnly ? q.Where(t => t.DestinationStoreId == sid) : q.Where(t => t.SourceStoreId == sid || t.DestinationStoreId == sid);
        if (f.SourceWarehouseId is { } sw) q = q.Where(t => t.SourceWarehouseId == sw);
        if (f.DestinationWarehouseId is { } dw) q = q.Where(t => t.DestinationWarehouseId == dw);
        if (f.From is { } from) q = q.Where(t => t.CreatedAt >= from);
        if (f.To is { } to) q = q.Where(t => t.CreatedAt < to);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var like = $"%{f.Search}%";
            q = q.Where(t => EF.Functions.ILike(t.Number, like) || t.Lines.Any(l => EF.Functions.ILike(l.Name, like)));
        }
        return q;
    }

    public async Task<(IReadOnlyList<StockTransfer> Items, int Total)> SearchTransfersAsync(Guid orgId, TransferFilter f, CancellationToken ct = default)
    {
        var q = TransfersQuery(orgId, f);
        if (f.Status is { } st) q = q.Where(t => t.Status == st);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(t => t.Seq).Skip(f.Skip).Take(f.Take).Include(t => t.Lines).AsSplitQuery().ToListAsync(ct);
        return (items, total);
    }

    public async Task<(int Total, int Drafts, int InTransit, int Completed)> TransferStatsAsync(Guid orgId, TransferFilter f, CancellationToken ct = default)
    {
        var statuses = await TransfersQuery(orgId, f).Select(t => t.Status).ToListAsync(ct);
        return (statuses.Count, statuses.Count(s => s == TransferStatus.Draft), statuses.Count(s => s == TransferStatus.InTransit),
            statuses.Count(s => s == TransferStatus.Completed));
    }

    public void AddTransfer(StockTransfer transfer) => db.StockTransfers.Add(transfer);
    public void RemoveTransferLine(StockTransferLine line) => db.Set<StockTransferLine>().Remove(line);

    // ---------- Жеткізушілер ----------

    public async Task<IReadOnlyList<Supplier>> ListSuppliersAsync(Guid orgId, CancellationToken ct = default) =>
        await db.Suppliers.AsNoTracking().Where(s => s.OrganizationId == orgId).OrderBy(s => s.Name).ToListAsync(ct);

    public Task<Supplier?> GetSupplierAsync(Guid orgId, Guid id, CancellationToken ct = default) =>
        db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.OrganizationId == orgId && s.Id == id, ct);

    public Task<Supplier?> GetSupplierForUpdateAsync(Guid orgId, Guid id, CancellationToken ct = default) =>
        db.Suppliers.FirstOrDefaultAsync(s => s.OrganizationId == orgId && s.Id == id, ct);

    public Task<bool> SupplierNameExistsAsync(Guid orgId, string name, Guid? excludeId, CancellationToken ct = default) =>
        db.Suppliers.AnyAsync(s => s.OrganizationId == orgId && s.Id != excludeId && s.Name.ToLower() == name.ToLower(), ct);

    public async Task<Dictionary<Guid, SupplierTotals>> SupplierTotalsAsync(Guid orgId, CancellationToken ct = default)
    {
        var receipts = await db.StockDocuments.AsNoTracking()
            .Where(d => d.OrganizationId == orgId && d.Type == StockDocumentType.Receipt && d.SupplierId != null && d.Status == StockDocumentStatus.Posted)
            .GroupBy(d => d.SupplierId!.Value)
            .Select(g => new { g.Key, Count = g.Count(), Total = g.Sum(d => d.Total), Paid = g.Sum(d => d.Paid) }).ToListAsync(ct);
        return receipts.ToDictionary(r => r.Key, r => new SupplierTotals(r.Count, r.Total, r.Paid));
    }

    public Task<bool> SupplierHasOpenDocumentsAsync(Guid supplierId, CancellationToken ct = default) =>
        db.StockDocuments.AnyAsync(d => d.SupplierId == supplierId && d.Status == StockDocumentStatus.Draft, ct);

    public void AddSupplier(Supplier supplier) => db.Suppliers.Add(supplier);

    public async Task<IReadOnlyList<SupplierPayment>> ListPaymentsAsync(Guid orgId, Guid? supplierId, Guid? documentId, CancellationToken ct = default)
    {
        var q = db.SupplierPayments.AsNoTracking().Where(p => p.OrganizationId == orgId);
        if (supplierId is { } sid) q = q.Where(p => p.SupplierId == sid);
        if (documentId is { } did) q = q.Where(p => p.DocumentId == did);
        return await q.OrderByDescending(p => p.CreatedAt).Take(500).ToListAsync(ct);
    }

    public Task<SupplierPayment?> GetPaymentForUpdateAsync(Guid orgId, Guid id, CancellationToken ct = default) =>
        db.SupplierPayments.FirstOrDefaultAsync(p => p.OrganizationId == orgId && p.Id == id, ct);

    public Task<SupplierPayment?> FindPaymentByClientRequestAsync(Guid orgId, Guid clientRequestId, CancellationToken ct = default) =>
        db.SupplierPayments.AsNoTracking().FirstOrDefaultAsync(p => p.OrganizationId == orgId && p.ClientRequestId == clientRequestId, ct);

    public void AddPayment(SupplierPayment payment) => db.SupplierPayments.Add(payment);

    // ---------- Анықтамалар ----------

    public async Task<Dictionary<Guid, Store>> StoresAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0) return [];
        return await db.Stores.AsNoTracking().Where(s => ids.Contains(s.Id)).ToDictionaryAsync(s => s.Id, ct);
    }

    public async Task<Dictionary<Guid, string>> StaffNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0) return [];
        return await db.StaffUsers.AsNoTracking().Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => (u.FirstName + " " + u.LastName).Trim(), ct);
    }

    public async Task<IReadOnlyList<Product>> ProductsAsync(Guid orgId, IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0) return [];
        return await db.Products.AsNoTracking().Include(p => p.Barcodes).Include(p => p.Images).AsSplitQuery()
            .Where(p => p.OrganizationId == orgId && ids.Contains(p.Id)).ToListAsync(ct);
    }
}
