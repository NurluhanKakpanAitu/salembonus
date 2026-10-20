namespace SalemBonus.Application.Pos.Inventory;

// ---------- Қоймалар (ТЗ «Склад» §6) ----------

public record WarehouseListDto(
    Guid Id, string Name, Guid StoreId, string StoreName, string? Type, string? Comment, bool IsDefault, bool IsActive,
    int ProductKinds, decimal TotalUnits, DateTime CreatedAt);

public record SaveWarehouseRequest(string? Name, string? Type, string? Comment, bool MakeDefault);

// ---------- Қалдықтар (§4–5) ----------

public record StockKpiDto(int ProductKinds, decimal TotalUnits, int WithStock, int WithoutStock, int BelowMin);

public record StockWarehouseQtyDto(Guid WarehouseId, string WarehouseName, decimal Quantity);

public record StockRowDto(
    Guid ProductId, string Name, string? Article, string? Barcode, string? ImageUrl, IReadOnlyList<string> PathNames,
    string? BrandName, string? UnitShortName, string Status, decimal Total, decimal? MinStock,
    IReadOnlyList<StockWarehouseQtyDto> ByWarehouse, DateTime? LastMovementAt);

public record StockPageDto(IReadOnlyList<StockRowDto> Items, int Total, int Page, int PageSize);

// ---------- Қозғалыс журналы (§14) ----------

public record MovementDto(
    Guid Id, DateTime CreatedAt, string Type, Guid? DocumentId, string? DocumentNumber,
    Guid ProductId, string ProductName, string? Article, Guid WarehouseId, string WarehouseName,
    decimal Quantity, decimal BalanceAfter, string? StaffName, string? Comment);

public record MovementPageDto(IReadOnlyList<MovementDto> Items, int Total, int Page, int PageSize);

// ---------- Приёмка және списание (§7, §11) ----------

public record DocumentLineDto(
    Guid Id, Guid ProductId, string Name, string? Article, string? Barcode, string? ImageUrl, string? Unit,
    decimal Quantity, decimal PurchasePrice, decimal? RetailPrice, decimal LineTotal,
    /// <summary>Қоймадағы ағымдағы қалдық (проведениеге дейін).</summary>
    decimal StockNow,
    /// <summary>Каталогтағы ағымдағы розничная баға (анықтама үшін: «из каталога»).</summary>
    decimal? CatalogRetailPrice);

public record SupplierPaymentDto(
    Guid Id, Guid SupplierId, string SupplierName, Guid? DocumentId, string? DocumentNumber, decimal Amount, string Method,
    string? Reference, DateOnly Date, string? Comment, string Status, string CreatedByName, DateTime CreatedAt, string? ReversalReason);

public record StockDocumentDto(
    Guid Id, string Type, string Number, DateTime Date, string Status, Guid StoreId, Guid WarehouseId, string WarehouseName,
    Guid? SupplierId, string? SupplierName, string? SupplierInvoiceNumber, string? Basis, string? AttachmentUrl, string? Comment,
    string? Reason, bool IsQuick, decimal Total, decimal Paid, decimal Remaining, int LineCount, decimal TotalQuantity,
    string CreatedByName, DateTime CreatedAt, DateTime? PostedAt, string? PostedByName, DateTime? ReversedAt, string? ReversalReason,
    IReadOnlyList<DocumentLineDto> Lines, IReadOnlyList<SupplierPaymentDto> Payments,
    DateOnly? PaymentDueDate = null);

public record StockDocumentListItemDto(
    Guid Id, string Number, DateTime Date, string Status, Guid WarehouseId, string WarehouseName, Guid? SupplierId, string? SupplierName,
    string? Reason, bool IsQuick, int LineCount, decimal TotalQuantity, decimal Total, decimal Paid, decimal Remaining,
    string CreatedByName, DateTime CreatedAt);

public record DocumentStatsDto(int Total, int Drafts, int Posted, int AwaitingPayment);

public record StockDocumentPageDto(IReadOnlyList<StockDocumentListItemDto> Items, int Total, int Page, int PageSize, DocumentStatsDto Stats);

public record SaveDocumentLineRequest(Guid ProductId, decimal Quantity, decimal? PurchasePrice, decimal? RetailPrice);

public record SaveReceiptRequest(
    Guid? WarehouseId, DateTime? Date, Guid? SupplierId, string? SupplierInvoiceNumber, string? Basis, string? AttachmentUrl,
    string? Comment, bool IsQuick, IReadOnlyList<SaveDocumentLineRequest>? Lines, DateOnly? PaymentDueDate = null);

public record SaveWriteOffRequest(
    Guid? WarehouseId, DateTime? Date, string? Reason, string? Comment, string? AttachmentUrl, IReadOnlyList<SaveDocumentLineRequest>? Lines);

public record ReverseRequest(string? Reason);

public record RegisterPaymentRequest(
    Guid? ClientRequestId, Guid? DocumentId, decimal Amount, string? Method, string? Reference, DateOnly? Date, string? Comment,
    /// <summary>Ақша қай шоттан кетеді (ТЗ «Финансы» §12.2); бос — төлем әдісінің әдепкі шоты.</summary>
    Guid? AccountId = null);

// ---------- Жеткізушілер (§12–13) ----------

public record SupplierDto(
    Guid Id, string Name, string Type, string? TaxId, string? Phone, string? Email, string? Address, string? Comment, string Status,
    int ReceiptCount, decimal TotalReceipts, decimal TotalPaid, decimal Debt, DateTime CreatedAt);

public record SaveSupplierRequest(string? Name, string? Type, string? TaxId, string? Phone, string? Email, string? Address, string? Comment);

public record SupplierCardDto(SupplierDto Supplier, IReadOnlyList<StockDocumentListItemDto> Receipts, IReadOnlyList<SupplierPaymentDto> Payments);

// ---------- Перемещения (§10) ----------

public record TransferLineDto(
    Guid Id, Guid ProductId, string Name, string? Article, string? Barcode, string? ImageUrl, string? Unit,
    decimal QuantitySent, decimal? QuantityReceived, string? DiscrepancyReason, decimal SourceStock);

public record TransferDto(
    Guid Id, string Number, string Status,
    Guid SourceStoreId, string SourceStoreName, Guid SourceWarehouseId, string SourceWarehouseName,
    Guid DestinationStoreId, string DestinationStoreName, Guid DestinationWarehouseId, string DestinationWarehouseName,
    string? Comment, int LineCount, decimal TotalQuantity, decimal? TotalReceived,
    string CreatedByName, DateTime CreatedAt, DateTime? SentAt, string? SentByName, DateTime? ReceivedAt, string? ReceivedByName,
    /// <summary>Ағымдағы дүкен бұл құжатты қабылдай ала ма (алушы дүкен + құқық).</summary>
    bool CanReceive,
    IReadOnlyList<TransferLineDto> Lines);

public record TransferListItemDto(
    Guid Id, string Number, string Status, string SourceStoreName, string SourceWarehouseName, string DestinationStoreName,
    string DestinationWarehouseName, Guid DestinationStoreId, int LineCount, decimal TotalQuantity, decimal? TotalReceived,
    string CreatedByName, DateTime CreatedAt, DateTime? SentAt, DateTime? ReceivedAt);

public record TransferStatsDto(int Total, int Drafts, int InTransit, int Completed);

public record TransferPageDto(IReadOnlyList<TransferListItemDto> Items, int Total, int Page, int PageSize, TransferStatsDto Stats);

public record SaveTransferLineRequest(Guid ProductId, decimal Quantity);

public record SaveTransferRequest(
    Guid? SourceWarehouseId, Guid? DestinationStoreId, Guid? DestinationWarehouseId, string? Comment,
    IReadOnlyList<SaveTransferLineRequest>? Lines);

public record ReceiveTransferLineRequest(Guid ProductId, decimal QuantityReceived, string? DiscrepancyReason);

public record ReceiveTransferRequest(IReadOnlyList<ReceiveTransferLineRequest>? Lines);

/// <summary>Перемещение үшін алушы дүкендер мен олардың белсенді қоймалары (қызметкердің мүшелігі бар дүкендер).</summary>
public record TransferTargetDto(Guid StoreId, string StoreName, IReadOnlyList<WarehouseOptionDto> Warehouses);

public record WarehouseOptionDto(Guid Id, string Name, bool IsDefault);
