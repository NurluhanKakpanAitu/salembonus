using Microsoft.EntityFrameworkCore;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Domain.Enums;
using SalemBonus.Domain.Pos.Catalog;
using SalemBonus.Domain.Pos.Sales;

namespace SalemBonus.Infrastructure.Persistence.Repositories;

public class DashboardRepository(AppDbContext db) : IDashboardRepository
{
    public async Task<IReadOnlyList<Sale>> SalesWithItemsAsync(Guid storeId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default) =>
        await db.Sales.AsNoTracking().Include(s => s.Items).Include(s => s.Payments).AsSplitQuery()
            .Where(s => s.StoreId == storeId && s.CreatedAt >= fromUtc && s.CreatedAt < toUtc)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<DashboardBonusTx>> BonusTransactionsAsync(Guid storeId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default) =>
        await (from t in db.BonusTransactions
               join c in db.BonusCards on t.BonusCardId equals c.Id
               where c.StoreId == storeId && t.CreatedAt >= fromUtc && t.CreatedAt < toUtc
               select new DashboardBonusTx(t.CreatedAt, t.Type, t.Amount)).ToListAsync(ct);

    public Task<int> NewCustomersAsync(Guid storeId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default) =>
        db.BonusCards.CountAsync(c => c.StoreId == storeId && c.CreatedAt >= fromUtc && c.CreatedAt < toUtc, ct);

    public async Task<Dictionary<Guid, CustomerLevel>> LevelsAsync(Guid storeId, IReadOnlyCollection<Guid> customerIds, CancellationToken ct = default) =>
        await db.BonusCards.AsNoTracking().Where(c => c.StoreId == storeId && customerIds.Contains(c.CustomerId))
            .ToDictionaryAsync(c => c.CustomerId, c => c.Level, ct);

    public async Task<int> TotalBonusBalanceAsync(Guid storeId, CancellationToken ct = default) =>
        await db.BonusCards.Where(c => c.StoreId == storeId).SumAsync(c => (int?)c.Balance, ct) ?? 0;

    public async Task<IReadOnlyList<DashboardProduct>> ProductsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
        await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id))
            .Select(p => new DashboardProduct(p.Id, p.Name,
                p.Images.OrderByDescending(i => i.IsPrimary).ThenBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault(),
                p.CatalogNodeId))
            .ToListAsync(ct);

    public async Task<(int Low, int Out)> StockAlertsAsync(Guid orgId, Guid warehouseId, decimal threshold, CancellationToken ct = default)
    {
        var stock = db.Products.AsNoTracking()
            .Where(p => p.OrganizationId == orgId && p.Status == CatalogStatus.Active && p.Type == ProductType.Goods)
            .Select(p => db.StockBalances.Where(b => b.ProductId == p.Id && b.WarehouseId == warehouseId).Select(b => (decimal?)b.Quantity).FirstOrDefault() ?? 0);
        var low = await stock.CountAsync(q => q > 0 && q <= threshold, ct);
        var @out = await stock.CountAsync(q => q <= 0, ct);
        return (low, @out);
    }

    public async Task<(int Count, decimal Sum, int Overdue)> OpenDebtsAsync(Guid storeId, DateOnly today, CancellationToken ct = default)
    {
        var open = db.Debts.Where(d => d.StoreId == storeId && d.Status == DebtStatus.Open);
        return (await open.CountAsync(ct), await open.SumAsync(d => (decimal?)(d.Amount - d.Paid), ct) ?? 0,
            await open.CountAsync(d => d.DueDate < today, ct));
    }
}
