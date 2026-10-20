using SalemBonus.Domain.Entities;
using SalemBonus.Domain.Pos.Catalog;
using SalemBonus.Domain.Pos.Inventory;

namespace SalemBonus.Application.Common.Interfaces;

public record StockRow(Guid WarehouseId, string WarehouseName, Guid StoreId, string StoreName, decimal Quantity);

/// <summary>Қалдықтар тізімінің сүзгісі (ТЗ «Склад» §5). Availability: null, "in", "out", "belowMin".</summary>
public record StockFilter(
    IReadOnlyCollection<Guid> WarehouseIds, string? Search, string? NodePath, Guid? BrandId, string? Availability, CatalogStatus? Status,
    string? Sort, int Skip, int Take);

public record StockProductRow(Product Product, IReadOnlyList<(Guid WarehouseId, decimal Quantity)> Balances, DateTime? LastMovementAt);

public record MovementFilter(
    IReadOnlyCollection<Guid> WarehouseIds, Guid? ProductId, StockMovementType? Type, Guid? StaffUserId, string? DocumentNumber,
    DateTime? From, DateTime? To, int Skip, int Take);

public record DocumentFilter(
    StockDocumentType Type, Guid? StoreId, Guid? WarehouseId, Guid? SupplierId, StockDocumentStatus? Status, string? Search,
    DateTime? From, DateTime? To, int Skip, int Take);

public record TransferFilter(
    Guid? StoreId, TransferStatus? Status, Guid? SourceWarehouseId, Guid? DestinationWarehouseId, bool IncomingOnly, string? Search,
    DateTime? From, DateTime? To, int Skip, int Take);

public record WarehouseTotals(int ProductKinds, decimal TotalUnits);

public record SupplierTotals(int ReceiptCount, decimal TotalReceipts, decimal TotalPaid);

/// <summary>Қоймалар, қалдық, қозғалыс, құжаттар, жеткізушілер мен дүкен бағалары.</summary>
public interface IInventoryRepository
{
    // Қоймалар
    Task<IReadOnlyList<Warehouse>> ListWarehousesAsync(Guid storeId, CancellationToken ct = default);
    Task<IReadOnlyList<Warehouse>> ListOrgWarehousesAsync(Guid orgId, CancellationToken ct = default);
    Task<Warehouse?> GetWarehouseAsync(Guid storeId, Guid id, CancellationToken ct = default);
    /// <summary>Кез келген күйдегі қойма (архивті де), өзгерту үшін.</summary>
    Task<Warehouse?> GetWarehouseForUpdateAsync(Guid orgId, Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Warehouse>> ListWarehousesForUpdateAsync(Guid storeId, CancellationToken ct = default);
    Task<bool> WarehouseNameExistsAsync(Guid storeId, string name, Guid? excludeId, CancellationToken ct = default);
    Task<Dictionary<Guid, WarehouseTotals>> WarehouseTotalsAsync(Guid orgId, CancellationToken ct = default);
    /// <summary>Черновик приёмка/списание немесе жолдағы перемещение бар ма (архивтеуге кедергі).</summary>
    Task<bool> WarehouseHasOpenDocumentsAsync(Guid warehouseId, CancellationToken ct = default);
    void AddWarehouse(Warehouse warehouse);

    // Қалдық
    Task<IReadOnlyList<StockRow>> StockByProductAsync(Guid orgId, Guid productId, CancellationToken ct = default);
    Task<Dictionary<Guid, StockBalance>> BalancesForUpdateAsync(Guid warehouseId, IReadOnlyCollection<Guid> productIds, CancellationToken ct = default);
    Task<Dictionary<Guid, decimal>> BalancesAsync(Guid warehouseId, IReadOnlyCollection<Guid> productIds, CancellationToken ct = default);
    Task<(IReadOnlyList<StockProductRow> Items, int Total)> SearchStockAsync(Guid orgId, StockFilter filter, CancellationToken ct = default);
    Task<(int ProductKinds, decimal TotalUnits, int WithStock, int WithoutStock, int BelowMin)> StockKpiAsync(
        Guid orgId, IReadOnlyCollection<Guid> warehouseIds, decimal defaultMinStock, CancellationToken ct = default);
    void AddBalance(StockBalance balance);

    // Қозғалыс журналы
    Task<(IReadOnlyList<StockMovement> Items, int Total)> SearchMovementsAsync(Guid orgId, MovementFilter filter, CancellationToken ct = default);
    void AddMovement(StockMovement movement);

    // Бағалар
    Task<ProductPrice?> GetPriceAsync(Guid productId, Guid storeId, CancellationToken ct = default);
    Task<Dictionary<Guid, ProductPrice>> PricesAsync(Guid storeId, IReadOnlyCollection<Guid> productIds, CancellationToken ct = default);
    Task<IReadOnlyList<ProductPrice>> ListPricesForUpdateAsync(Guid storeId, CancellationToken ct = default);
    Task<ProductPrice?> GetPriceForUpdateAsync(Guid productId, Guid storeId, CancellationToken ct = default);
    void AddPrice(ProductPrice price);

    // Құжаттар (приёмка, списание)
    Task<long> NextDocumentNumberAsync(Guid orgId, string kind, CancellationToken ct = default);
    Task<StockDocument?> GetDocumentAsync(Guid orgId, Guid id, CancellationToken ct = default);
    Task<StockDocument?> GetDocumentForUpdateAsync(Guid orgId, Guid id, CancellationToken ct = default);
    Task<(IReadOnlyList<StockDocument> Items, int Total)> SearchDocumentsAsync(Guid orgId, DocumentFilter filter, CancellationToken ct = default);
    Task<(int Total, int Drafts, int Posted, int AwaitingPayment)> DocumentStatsAsync(Guid orgId, DocumentFilter filter, CancellationToken ct = default);
    Task<IReadOnlyList<StockDocument>> ListSupplierDocumentsAsync(Guid orgId, Guid supplierId, CancellationToken ct = default);
    void AddDocument(StockDocument document);
    void RemoveDocumentLine(StockDocumentLine line);

    // Перемещения
    Task<StockTransfer?> GetTransferAsync(Guid orgId, Guid id, CancellationToken ct = default);
    Task<StockTransfer?> GetTransferForUpdateAsync(Guid orgId, Guid id, CancellationToken ct = default);
    Task<(IReadOnlyList<StockTransfer> Items, int Total)> SearchTransfersAsync(Guid orgId, TransferFilter filter, CancellationToken ct = default);
    Task<(int Total, int Drafts, int InTransit, int Completed)> TransferStatsAsync(Guid orgId, TransferFilter filter, CancellationToken ct = default);
    void AddTransfer(StockTransfer transfer);
    void RemoveTransferLine(StockTransferLine line);

    // Жеткізушілер мен төлемдер
    Task<IReadOnlyList<Supplier>> ListSuppliersAsync(Guid orgId, CancellationToken ct = default);
    Task<Supplier?> GetSupplierAsync(Guid orgId, Guid id, CancellationToken ct = default);
    Task<Supplier?> GetSupplierForUpdateAsync(Guid orgId, Guid id, CancellationToken ct = default);
    Task<bool> SupplierNameExistsAsync(Guid orgId, string name, Guid? excludeId, CancellationToken ct = default);
    Task<Dictionary<Guid, SupplierTotals>> SupplierTotalsAsync(Guid orgId, CancellationToken ct = default);
    Task<bool> SupplierHasOpenDocumentsAsync(Guid supplierId, CancellationToken ct = default);
    void AddSupplier(Supplier supplier);
    Task<IReadOnlyList<SupplierPayment>> ListPaymentsAsync(Guid orgId, Guid? supplierId, Guid? documentId, CancellationToken ct = default);
    Task<SupplierPayment?> GetPaymentForUpdateAsync(Guid orgId, Guid id, CancellationToken ct = default);
    Task<SupplierPayment?> FindPaymentByClientRequestAsync(Guid orgId, Guid clientRequestId, CancellationToken ct = default);
    void AddPayment(SupplierPayment payment);

    // Анықтамалар
    Task<Dictionary<Guid, Store>> StoresAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
    Task<Dictionary<Guid, string>> StaffNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
    Task<IReadOnlyList<Product>> ProductsAsync(Guid orgId, IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
}
