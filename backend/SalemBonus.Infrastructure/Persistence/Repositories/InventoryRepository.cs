using Microsoft.EntityFrameworkCore;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Domain.Pos.Inventory;

namespace SalemBonus.Infrastructure.Persistence.Repositories;

public class InventoryRepository(AppDbContext db) : IInventoryRepository
{
    public async Task<IReadOnlyList<Warehouse>> ListWarehousesAsync(Guid storeId, CancellationToken ct = default) =>
        await db.Warehouses.Where(w => w.StoreId == storeId)
            .OrderByDescending(w => w.IsDefault).ThenBy(w => w.Name).ToListAsync(ct);

    public Task<Warehouse?> GetWarehouseAsync(Guid storeId, Guid id, CancellationToken ct = default) =>
        db.Warehouses.FirstOrDefaultAsync(w => w.StoreId == storeId && w.Id == id && w.IsActive, ct);

    public void AddWarehouse(Warehouse warehouse) => db.Warehouses.Add(warehouse);

    public async Task<IReadOnlyList<StockRow>> StockByProductAsync(Guid orgId, Guid productId, CancellationToken ct = default) =>
        await (from b in db.StockBalances
               join w in db.Warehouses on b.WarehouseId equals w.Id
               join s in db.Stores on w.StoreId equals s.Id
               where b.ProductId == productId && w.OrganizationId == orgId
               orderby s.Name, w.Name
               select new StockRow(w.Id, w.Name, s.Id, s.Name, b.Quantity)).ToListAsync(ct);

    public Task<ProductPrice?> GetPriceAsync(Guid productId, Guid storeId, CancellationToken ct = default) =>
        db.ProductPrices.AsNoTracking().FirstOrDefaultAsync(p => p.ProductId == productId && p.StoreId == storeId, ct);

    public void AddMovement(StockMovement movement) => db.StockMovements.Add(movement);
    public void AddBalance(StockBalance balance) => db.StockBalances.Add(balance);
    public void AddPrice(ProductPrice price) => db.ProductPrices.Add(price);
}
