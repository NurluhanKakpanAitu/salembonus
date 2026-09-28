namespace SalemBonus.Application.Pos.Sales;

public record TransferRecipientDto(Guid Id, string BankName, string Account, string HolderName);

public record ApproverDto(Guid Id, string Name);

/// <summary>Касса экранына керектің бәрі бір сұраныспен: дүкен, касса, төлем түрлері, кассир шектері.</summary>
public record CashierContextDto(
    Guid StoreId,
    string StoreName,
    string? StoreAddress,
    Guid RegisterId,
    string RegisterName,
    IReadOnlyList<string> PaymentMethods,
    bool MixedEnabled,
    IReadOnlyList<TransferRecipientDto> TransferRecipients,
    decimal MaxDiscountPercent,
    bool CanApproveDiscount,
    bool CanSellWithoutStock,
    bool CanReturn,
    bool CanViewStoreFinance,
    bool AutoPrint,
    bool ElectronicReceipt,
    bool CanManageSettings,
    decimal MaxRedeemPercent,
    IReadOnlyList<ApproverDto> Approvers);

public record CashierProductDto(
    Guid Id, string Name, string? Article, string? Barcode, string? ImageUrl, string? BrandName, string? UnitShortName,
    decimal? Price, decimal Stock, Guid? NodeId);

public record CashierCatalogDto(IReadOnlyList<CashierProductDto> Items, int Total, int Page, int PageSize);

public record CashierCustomerDto(
    Guid Id, string FullName, string Phone, DateOnly? BirthDate, string Level, string LevelKey, int Balance,
    decimal AccrualPercent, bool HasCard, decimal DebtTotal);

public record RegisterCustomerRequest(string? Phone, string? FirstName, string? LastName, DateOnly? BirthDate);

public record SaleLineRequest(Guid ProductId, decimal Quantity);

public record SaleDiscountRequest(string? Kind, decimal Value);

public record DiscountApprovalRequest(Guid StaffUserId, string? Pin);

/// <summary>Төлем. Қарызда — қайтару мерзімі мен түсініктеме (ТЗ §11.3–11.4).</summary>
public record SalePaymentRequest(string? Method, decimal Amount, decimal? Received, Guid? TransferRecipientId,
    DateOnly? DueDate = null, string? Comment = null);

public record CreateSaleRequest(
    Guid ClientRequestId,
    IReadOnlyList<SaleLineRequest>? Items,
    Guid? CustomerId,
    int BonusRedeem,
    SaleDiscountRequest? Discount,
    DiscountApprovalRequest? Approval,
    IReadOnlyList<SalePaymentRequest>? Payments);

public record ReceiptItemDto(Guid Id, Guid ProductId, string Name, string? Article, string? Unit, decimal Quantity, decimal Price,
    decimal LineTotal, decimal Discount, decimal ReturnedQuantity);

public record ReceiptPaymentDto(string Method, decimal Amount, decimal? Received, decimal? Change, string? TransferRecipient);

public record ReceiptCustomerDto(Guid Id, string FullName, string Phone, int? BonusBalance);

public record ReceiptDebtDto(Guid Id, decimal Amount, decimal Paid, decimal Remaining, DateOnly DueDate, string Status, string? Comment);

public record ReceiptReturnItemDto(string Name, decimal Quantity, decimal Amount);

public record ReceiptReturnDto(Guid Id, DateTime CreatedAt, string CashierName, decimal Amount, decimal Refunded,
    string? RefundMethod, decimal DebtReduced, int BonusRestored, int BonusReversed, string? Reason,
    IReadOnlyList<ReceiptReturnItemDto> Items);

public record ReceiptDto(
    Guid Id,
    long Number,
    DateTime CreatedAt,
    string StoreName,
    string RegisterName,
    string CashierName,
    ReceiptCustomerDto? Customer,
    IReadOnlyList<ReceiptItemDto> Items,
    decimal Subtotal,
    string? DiscountKind,
    decimal? DiscountValue,
    decimal DiscountAmount,
    int BonusRedeemed,
    int BonusAccrued,
    decimal Total,
    IReadOnlyList<ReceiptPaymentDto> Payments,
    string Status,
    decimal ReturnedAmount,
    ReceiptDebtDto? Debt,
    IReadOnlyList<ReceiptReturnDto> Returns);

/// <summary>Чектер тізімі — тек қысқа мәлімет (ТЗ §12.2: жеңілдік/бонус/қайтарыммен толтырмау).</summary>
public record SaleListItemDto(Guid Id, long Number, DateTime CreatedAt, string? CustomerName, decimal Total,
    IReadOnlyList<string> Methods, string Status);

public record SalePageDto(IReadOnlyList<SaleListItemDto> Items, int Total, int Page, int PageSize);

public record ReturnLineRequest(Guid SaleItemId, decimal Quantity);

public record CreateReturnRequest(Guid ClientRequestId, IReadOnlyList<ReturnLineRequest>? Items, string? RefundMethod,
    Guid? TransferRecipientId, string? Reason);

public record DebtPaymentDto(decimal Amount, string? Method, bool IsReturn, string? TransferRecipient, decimal RemainingAfter,
    DateTime CreatedAt, string CashierName);

public record DebtDto(Guid Id, Guid? SaleId, long? SaleNumber, decimal Amount, decimal Paid, decimal Remaining, DateOnly DueDate,
    string? Comment, string Status, DateTime CreatedAt, string CashierName, IReadOnlyList<DebtPaymentDto> Payments);

public record RepayDebtRequest(decimal Amount, string? Method, Guid? TransferRecipientId);

// ---------- Финанс (ТЗ §14) ----------

public record FinanceMethodDto(string Method, decimal Amount, int Count);

/// <summary>
/// Күн (не кезең) қорытындысы. <see cref="Revenue"/> = сатылым − қайтарым. <see cref="CashInDrawer"/> —
/// кассадағы қолма-қол: қолма-қол сатылым + қолма-қол қарыз өтеуі − қолма-қол қайтарым.
/// </summary>
public record FinanceDto(
    DateOnly From, DateOnly To, bool WholeStore,
    decimal SalesTotal, int SalesCount,
    IReadOnlyList<FinanceMethodDto> Methods,
    decimal MixedTotal, int MixedCount,
    decimal Discounts, int DiscountCount,
    int BonusRedeemed, int BonusAccrued,
    decimal ReturnsTotal, int ReturnsCount, IReadOnlyList<FinanceMethodDto> RefundsByMethod,
    decimal DebtIssued, decimal DebtRepaid, IReadOnlyList<FinanceMethodDto> DebtRepaidByMethod,
    decimal Revenue, decimal CashInDrawer);

// ---------- Клиент карточкасы (ТЗ §5) ----------

public record CustomerPurchaseDto(Guid SaleId, long Number, DateTime CreatedAt, decimal Total, int BonusAccrued, int BonusRedeemed, string Status);

public record CustomerBonusDto(DateTime CreatedAt, string Type, int Amount, long? ReceiptNumber);

public record CustomerReturnDto(Guid SaleId, long Number, DateTime CreatedAt, decimal Amount, string Items);

public record CustomerCardDto(
    CashierCustomerDto Customer,
    string? Region, string? District, string? Settlement, DateTime RegisteredAt,
    decimal PurchasesTotal, int PurchasesCount, decimal ReturnsTotal, int ReturnsCount,
    IReadOnlyList<CustomerPurchaseDto> Purchases,
    IReadOnlyList<CustomerBonusDto> Bonus,
    IReadOnlyList<CustomerReturnDto> Returns);

// ---------- Баптаулар мен хабарламалар (ТЗ §17, §19) ----------

public record CashierSettingsDto(
    string StoreName, string? StoreAddress,
    Guid RegisterId, string RegisterName, int? AutoLockMinutes,
    IReadOnlyList<string> EnabledMethods, bool MixedEnabled,
    bool AutoPrint, bool ElectronicReceipt,
    bool NotifyOutOfStock, bool NotifyLowStock, bool NotifyDebt, bool NotifyReturn, bool NotifyCashierChange,
    int LowStockThreshold,
    IReadOnlyList<TransferRecipientDto> Recipients,
    bool CanManage);

public record UpdateCashierSettingsRequest(
    string? RegisterName, int? AutoLockMinutes,
    IReadOnlyList<string>? EnabledMethods, bool MixedEnabled,
    bool AutoPrint, bool ElectronicReceipt,
    bool NotifyOutOfStock, bool NotifyLowStock, bool NotifyDebt, bool NotifyReturn, bool NotifyCashierChange,
    int LowStockThreshold);

public record SaveRecipientRequest(string? BankName, string? Account, string? HolderName);

public record StoreNotificationDto(Guid Id, string Type, string? Subject, decimal? Amount, long? Number, string? StaffName, DateTime CreatedAt);
