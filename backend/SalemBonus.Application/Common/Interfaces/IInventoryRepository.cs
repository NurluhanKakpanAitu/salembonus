using SalemBonus.Domain.Pos.Inventory;

namespace SalemBonus.Application.Common.Interfaces;

public record StockRow(Guid WarehouseId, string WarehouseName, Guid StoreId, string StoreName, decimal Quantity);

/// <summary>Қоймалар, қалдық және дүкен бағалары (толық «Склад» модулі кейін).</summary>
public interface IInventoryRepository
{
    Task<IReadOnlyList<Warehouse>> ListWarehousesAsync(Guid storeId, CancellationToken ct = default);
    Task<Warehouse?> GetWarehouseAsync(Guid storeId, Guid id, CancellationToken ct = default);
    void AddWarehouse(Warehouse warehouse);
    /// <summary>Тауардың бизнестің барлық қоймасындағы қалдығы.</summary>
    Task<IReadOnlyList<StockRow>> StockByProductAsync(Guid orgId, Guid productId, CancellationToken ct = default);
    Task<ProductPrice?> GetPriceAsync(Guid productId, Guid storeId, CancellationToken ct = default);
    Task<ProductPrice?> GetPriceForUpdateAsync(Guid productId, Guid storeId, CancellationToken ct = default);
    void AddMovement(StockMovement movement);
    void AddBalance(StockBalance balance);
    void AddPrice(ProductPrice price);
}
