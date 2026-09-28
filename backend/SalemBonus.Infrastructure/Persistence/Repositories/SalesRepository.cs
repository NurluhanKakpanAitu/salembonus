using Microsoft.EntityFrameworkCore;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Domain.Core;
using SalemBonus.Domain.Entities;
using SalemBonus.Domain.Pos.Catalog;
using SalemBonus.Domain.Pos.Inventory;
using SalemBonus.Domain.Pos.Sales;

namespace SalemBonus.Infrastructure.Persistence.Repositories;

public class SalesRepository(AppDbContext db) : ISalesRepository
{
    private IQueryable<Product> Sellable(Guid orgId) =>
        db.Products.AsNoTracking().Where(p => p.OrganizationId == orgId && p.Status == CatalogStatus.Active && !p.HideInCashier);

    public async Task<(IReadOnlyList<CashierCatalogRow> Items, int Total)> SearchCatalogAsync(Guid orgId, Guid storeId, Guid warehouseId,
        CashierCatalogFilter f, CancellationToken ct = default)
    {
        var products = Sellable(orgId);
        if (f.NodePath is { } path)
        {
            var prefix = path + "/";
            products = products.Where(p => db.CatalogNodes.Any(n => n.Id == p.CatalogNodeId && (n.Path == path || n.Path.StartsWith(prefix))));
        }
        if (f.Search is { } term)
        {
            var like = $"%{term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
            products = products.Where(p => EF.Functions.ILike(p.Name, like, "\\")
                || (p.Article != null && EF.Functions.ILike(p.Article, like, "\\"))
                || p.Barcodes.Any(b => b.Barcode.StartsWith(term)));
        }

        var since = DateTime.UtcNow.AddDays(-30);
        var rows = products.Select(p => new
        {
            Product = p,
            Price = db.ProductPrices.Where(x => x.ProductId == p.Id && x.StoreId == storeId).Select(x => (decimal?)x.SalePrice).FirstOrDefault(),
            Stock = db.StockBalances.Where(b => b.ProductId == p.Id && b.WarehouseId == warehouseId).Select(b => (decimal?)b.Quantity).FirstOrDefault() ?? 0,
            Sold = db.SaleItems.Where(i => i.ProductId == p.Id
                    && db.Sales.Any(s => s.Id == i.SaleId && s.StoreId == storeId && s.CreatedAt >= since))
                .Sum(i => (decimal?)i.Quantity) ?? 0,
        });

        rows = f.Sort switch
        {
            CashierSort.PriceAsc => rows.OrderBy(r => r.Price == null).ThenBy(r => r.Price).ThenBy(r => r.Product.Name),
            CashierSort.PriceDesc => rows.OrderBy(r => r.Price == null).ThenByDescending(r => r.Price).ThenBy(r => r.Product.Name),
            CashierSort.Stock => rows.OrderByDescending(r => r.Stock).ThenBy(r => r.Product.Name),
            CashierSort.Name => rows.OrderBy(r => r.Product.Name),
            _ => rows.OrderByDescending(r => r.Sold).ThenByDescending(r => r.Stock > 0).ThenBy(r => r.Product.Name),
        };

        var total = await products.CountAsync(ct);
        var page = await rows.Skip(f.Skip).Take(f.Take).ToListAsync(ct);
        await LoadMediaAsync(page.Select(r => r.Product).ToList(), ct);
        return (page.Select(r => new CashierCatalogRow(r.Product, r.Price, r.Stock)).ToList(), total);
    }

    public async Task<CashierCatalogRow?> FindByBarcodeAsync(Guid orgId, Guid storeId, Guid warehouseId, string barcode, CancellationToken ct = default)
    {
        var row = await Sellable(orgId).Where(p => p.Barcodes.Any(b => b.Barcode == barcode))
            .Select(p => new
            {
                Product = p,
                Price = db.ProductPrices.Where(x => x.ProductId == p.Id && x.StoreId == storeId).Select(x => (decimal?)x.SalePrice).FirstOrDefault(),
                Stock = db.StockBalances.Where(b => b.ProductId == p.Id && b.WarehouseId == warehouseId).Select(b => (decimal?)b.Quantity).FirstOrDefault() ?? 0,
            })
            .FirstOrDefaultAsync(ct);
        if (row is null) return null;
        await LoadMediaAsync([row.Product], ct);
        return new CashierCatalogRow(row.Product, row.Price, row.Stock);
    }

    /// <summary>Бетке шыққан тауарлардың суреті мен штрихкоды бөлек сұраныспен (әр жолға JOIN емес).</summary>
    private async Task LoadMediaAsync(List<Product> products, CancellationToken ct)
    {
        if (products.Count == 0) return;
        var ids = products.Select(p => p.Id).ToList();
        var images = await db.ProductImages.AsNoTracking().Where(i => ids.Contains(i.ProductId)).ToListAsync(ct);
        var barcodes = await db.ProductBarcodes.AsNoTracking().Where(b => ids.Contains(b.ProductId)).ToListAsync(ct);
        foreach (var p in products)
        {
            p.Images = images.Where(i => i.ProductId == p.Id).ToList();
            p.Barcodes = barcodes.Where(b => b.ProductId == p.Id).ToList();
        }
    }

    public async Task<IReadOnlyList<Product>> ListProductsAsync(Guid orgId, IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
        await db.Products.AsNoTracking().Where(p => p.OrganizationId == orgId && ids.Contains(p.Id)).ToListAsync(ct);

    public async Task<Dictionary<Guid, decimal>> PricesAsync(Guid storeId, IReadOnlyCollection<Guid> productIds, CancellationToken ct = default) =>
        await db.ProductPrices.AsNoTracking().Where(p => p.StoreId == storeId && productIds.Contains(p.ProductId))
            .ToDictionaryAsync(p => p.ProductId, p => p.SalePrice, ct);

    public async Task<Dictionary<Guid, StockBalance>> BalancesForUpdateAsync(Guid warehouseId, IReadOnlyCollection<Guid> productIds, CancellationToken ct = default)
    {
        // FOR UPDATE: транзакция біткенше басқа касса бұл жолдарды өзгерте алмайды (соңғы дананы екеуі сата алмайды).
        var ids = productIds.ToArray();
        return await db.StockBalances
            .FromSql($"""SELECT * FROM pos.stock_balances WHERE "WarehouseId" = {warehouseId} AND "ProductId" = ANY({ids}) FOR UPDATE""")
            .ToDictionaryAsync(b => b.ProductId, ct);
    }

    public async Task<long> NextReceiptNumberAsync(Guid storeId, CancellationToken ct = default)
    {
        // Бір сұраныс: жол жоқ болса 1, бар болса +1. Жол транзакция соңына дейін бұғатталады —
        // бір дүкеннің екі кассасы бір сәтте сатса да, нөмір қайталанбайды және бос орын қалмайды.
        var numbers = await db.Database.SqlQuery<long>($"""
            INSERT INTO pos.receipt_counters ("StoreId", "LastNumber") VALUES ({storeId}, 1)
            ON CONFLICT ("StoreId") DO UPDATE SET "LastNumber" = pos.receipt_counters."LastNumber" + 1
            RETURNING "LastNumber" AS "Value"
            """).ToListAsync(ct);
        return numbers[0];
    }

    public Task<Sale?> FindByClientRequestAsync(Guid storeId, Guid clientRequestId, CancellationToken ct = default) =>
        db.Sales.AsNoTracking().Include(s => s.Items).Include(s => s.Payments).AsSplitQuery()
            .FirstOrDefaultAsync(s => s.StoreId == storeId && s.ClientRequestId == clientRequestId, ct);

    public Task<Sale?> GetSaleAsync(Guid storeId, Guid id, CancellationToken ct = default) =>
        db.Sales.AsNoTracking().Include(s => s.Items).Include(s => s.Payments).AsSplitQuery()
            .FirstOrDefaultAsync(s => s.StoreId == storeId && s.Id == id, ct);

    public void AddSale(Sale sale) => db.Sales.Add(sale);

    public async Task<(IReadOnlyList<Sale> Items, int Total)> SearchSalesAsync(Guid storeId, DateTime? fromUtc, DateTime? toUtc,
        string? search, int skip, int take, CancellationToken ct = default)
    {
        var q = db.Sales.AsNoTracking().Where(s => s.StoreId == storeId);
        if (fromUtc is { } from) q = q.Where(s => s.CreatedAt >= from);
        if (toUtc is { } to) q = q.Where(s => s.CreatedAt < to);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var digits = new string(term.Where(char.IsDigit).ToArray());
            var like = $"%{term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
            // Тек сандар — чек нөмірі не сома; телефонның бөлігі де (4+ сан).
            var numeric = digits.Length > 0 && digits.Length == term.Replace(" ", "").Length;
            long.TryParse(digits, out var number);
            decimal.TryParse(digits, out var amount);
            var byPhone = digits.Length >= 4;
            q = q.Where(s =>
                (numeric && (s.Number == number || s.Total == amount))
                || db.Customers.Any(c => c.Id == s.CustomerId
                    && (EF.Functions.ILike(c.FirstName + " " + c.LastName, like, "\\")
                        || (byPhone && c.Phone.Contains(digits)))));
        }
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(s => s.Number).Skip(skip).Take(take).Include(s => s.Payments).ToListAsync(ct);
        return (items, total);
    }

    public Task<Sale?> GetSaleForUpdateAsync(Guid storeId, Guid id, CancellationToken ct = default) =>
        db.Sales.Include(s => s.Items).Include(s => s.Payments).AsSplitQuery()
            .FirstOrDefaultAsync(s => s.StoreId == storeId && s.Id == id, ct);

    public async Task<IReadOnlyList<SaleReturn>> ListReturnsAsync(Guid saleId, CancellationToken ct = default) =>
        await db.SaleReturns.AsNoTracking().Include(r => r.Items).Where(r => r.SaleId == saleId).OrderBy(r => r.CreatedAt).ToListAsync(ct);

    public void AddReturn(SaleReturn saleReturn) => db.SaleReturns.Add(saleReturn);

    public Task<Debt?> GetDebtForUpdateAsync(Guid storeId, Guid id, CancellationToken ct = default) =>
        db.Debts.Include(d => d.Payments).FirstOrDefaultAsync(d => d.StoreId == storeId && d.Id == id, ct);

    public Task<Debt?> GetDebtBySaleForUpdateAsync(Guid saleId, CancellationToken ct = default) =>
        db.Debts.Include(d => d.Payments).FirstOrDefaultAsync(d => d.SaleId == saleId, ct);

    public Task<Debt?> GetDebtBySaleAsync(Guid saleId, CancellationToken ct = default) =>
        db.Debts.AsNoTracking().FirstOrDefaultAsync(d => d.SaleId == saleId, ct);

    public async Task<IReadOnlyList<Debt>> ListCustomerDebtsAsync(Guid storeId, Guid customerId, CancellationToken ct = default) =>
        await db.Debts.AsNoTracking().Include(d => d.Payments)
            .Where(d => d.StoreId == storeId && d.CustomerId == customerId)
            .OrderBy(d => d.Status).ThenByDescending(d => d.CreatedAt).ToListAsync(ct);

    public async Task<decimal> OpenDebtTotalAsync(Guid storeId, Guid customerId, CancellationToken ct = default) =>
        await db.Debts.Where(d => d.StoreId == storeId && d.CustomerId == customerId && d.Status == DebtStatus.Open)
            .SumAsync(d => (decimal?)(d.Amount - d.Paid), ct) ?? 0;

    public void AddDebt(Debt debt) => db.Debts.Add(debt);

    public void AddDebtPayment(DebtPayment payment) => db.DebtPayments.Add(payment);

    public async Task<Dictionary<Guid, string>> CustomerNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
        (await db.Customers.AsNoTracking().Where(c => ids.Contains(c.Id))
            .Select(c => new { c.Id, c.FirstName, c.LastName, c.Phone }).ToListAsync(ct))
        .ToDictionary(c => c.Id, c => string.IsNullOrWhiteSpace(c.FirstName) ? c.Phone : $"{c.FirstName} {c.LastName}".Trim());

    public async Task<Dictionary<Guid, string>> StaffNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
        (await db.StaffUsers.AsNoTracking().Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName }).ToListAsync(ct))
        .ToDictionary(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim());

    public async Task<IReadOnlyList<Sale>> ListSalesInRangeAsync(Guid storeId, DateTime fromUtc, DateTime toUtc, Guid? staffId, CancellationToken ct = default) =>
        await db.Sales.AsNoTracking().Include(s => s.Payments)
            .Where(s => s.StoreId == storeId && s.CreatedAt >= fromUtc && s.CreatedAt < toUtc && (staffId == null || s.StaffUserId == staffId))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<SaleReturn>> ListReturnsInRangeAsync(Guid storeId, DateTime fromUtc, DateTime toUtc, Guid? staffId, CancellationToken ct = default) =>
        await db.SaleReturns.AsNoTracking()
            .Where(r => r.StoreId == storeId && r.CreatedAt >= fromUtc && r.CreatedAt < toUtc && (staffId == null || r.StaffUserId == staffId))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<DebtPayment>> ListDebtPaymentsInRangeAsync(Guid storeId, DateTime fromUtc, DateTime toUtc, Guid? staffId, CancellationToken ct = default) =>
        await db.DebtPayments.AsNoTracking()
            .Where(p => !p.IsReturn && p.CreatedAt >= fromUtc && p.CreatedAt < toUtc && (staffId == null || p.StaffUserId == staffId)
                        && db.Debts.Any(d => d.Id == p.DebtId && d.StoreId == storeId))
            .ToListAsync(ct);

    public async Task<(IReadOnlyList<Sale> Recent, decimal Total, int Count)> CustomerSalesAsync(Guid storeId, Guid customerId, int take, CancellationToken ct = default)
    {
        var q = db.Sales.AsNoTracking().Where(s => s.StoreId == storeId && s.CustomerId == customerId);
        var total = await q.SumAsync(s => (decimal?)s.Total, ct) ?? 0;
        var count = await q.CountAsync(ct);
        var recent = await q.OrderByDescending(s => s.CreatedAt).Take(take).ToListAsync(ct);
        return (recent, total, count);
    }

    public async Task<(IReadOnlyList<SaleReturn> Recent, decimal Total, int Count)> CustomerReturnsAsync(Guid storeId, Guid customerId, int take, CancellationToken ct = default)
    {
        var q = db.SaleReturns.AsNoTracking()
            .Where(r => r.StoreId == storeId && db.Sales.Any(s => s.Id == r.SaleId && s.CustomerId == customerId));
        var total = await q.SumAsync(r => (decimal?)r.Amount, ct) ?? 0;
        var count = await q.CountAsync(ct);
        var recent = await q.OrderByDescending(r => r.CreatedAt).Take(take).Include(r => r.Items).ToListAsync(ct);
        return (recent, total, count);
    }

    public async Task<Dictionary<Guid, long>> SaleNumbersAsync(IReadOnlyCollection<Guid> saleIds, CancellationToken ct = default) =>
        await db.Sales.AsNoTracking().Where(s => saleIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Number, ct);

    public Task<StoreCashierSettings?> GetSettingsForUpdateAsync(Guid storeId, CancellationToken ct = default) =>
        db.CashierSettings.FirstOrDefaultAsync(s => s.StoreId == storeId, ct);

    public void AddSettings(StoreCashierSettings settings) => db.CashierSettings.Add(settings);

    public Task<TransferRecipient?> GetRecipientForUpdateAsync(Guid storeId, Guid id, CancellationToken ct = default) =>
        db.TransferRecipients.FirstOrDefaultAsync(r => r.StoreId == storeId && r.Id == id, ct);

    public void AddRecipient(TransferRecipient recipient) => db.TransferRecipients.Add(recipient);

    public void AddNotification(StoreNotification notification) => db.StoreNotifications.Add(notification);

    public async Task<IReadOnlyList<StoreNotification>> ListNotificationsAsync(Guid storeId, int take, CancellationToken ct = default) =>
        await db.StoreNotifications.AsNoTracking().Where(n => n.StoreId == storeId)
            .OrderByDescending(n => n.CreatedAt).Take(take).ToListAsync(ct);

    public Task<StoreCashierSettings?> GetSettingsAsync(Guid storeId, CancellationToken ct = default) =>
        db.CashierSettings.AsNoTracking().FirstOrDefaultAsync(s => s.StoreId == storeId, ct);

    public async Task<IReadOnlyList<TransferRecipient>> ListRecipientsAsync(Guid storeId, CancellationToken ct = default) =>
        await db.TransferRecipients.AsNoTracking().Where(r => r.StoreId == storeId)
            .OrderBy(r => r.SortOrder).ThenBy(r => r.CreatedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<Customer>> SearchStoreCustomersAsync(Guid storeId, string term, int take, CancellationToken ct = default)
    {
        var like = $"%{term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
        return await db.Customers.AsNoTracking()
            .Where(c => db.BonusCards.Any(b => b.CustomerId == c.Id && b.StoreId == storeId)
                        && (EF.Functions.ILike(c.FirstName + " " + c.LastName, like, "\\")
                            || EF.Functions.ILike(c.LastName + " " + c.FirstName, like, "\\")))
            .OrderBy(c => c.FirstName).ThenBy(c => c.LastName)
            .Take(take).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<(Guid Id, string Name)>> ListApproversAsync(Guid storeId, string permission, CancellationToken ct = default)
    {
        var members = await db.StoreMemberships.AsNoTracking().Include(m => m.StaffUser)
            .Where(m => m.StoreId == storeId && m.IsActive && m.StaffUser!.IsActive && m.StaffUser.PinHash != null)
            .ToListAsync(ct);
        return members.Where(m => m.Has(permission))
            .Select(m => (m.StaffUserId, $"{m.StaffUser!.FirstName} {m.StaffUser.LastName}".Trim()))
            .OrderBy(x => x.Item2).ToList();
    }
}
