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
    decimal MaxRedeemPercent,
    IReadOnlyList<ApproverDto> Approvers);

public record CashierProductDto(
    Guid Id, string Name, string? Article, string? Barcode, string? ImageUrl, string? BrandName, string? UnitShortName,
    decimal? Price, decimal Stock, Guid? NodeId);

public record CashierCatalogDto(IReadOnlyList<CashierProductDto> Items, int Total, int Page, int PageSize);

public record CashierCustomerDto(
    Guid Id, string FullName, string Phone, DateOnly? BirthDate, string Level, string LevelKey, int Balance,
    decimal AccrualPercent, bool HasCard);

public record RegisterCustomerRequest(string? Phone, string? FirstName, string? LastName, DateOnly? BirthDate);

public record SaleLineRequest(Guid ProductId, decimal Quantity);

public record SaleDiscountRequest(string? Kind, decimal Value);

public record DiscountApprovalRequest(Guid StaffUserId, string? Pin);

public record SalePaymentRequest(string? Method, decimal Amount, decimal? Received, Guid? TransferRecipientId);

public record CreateSaleRequest(
    Guid ClientRequestId,
    IReadOnlyList<SaleLineRequest>? Items,
    Guid? CustomerId,
    int BonusRedeem,
    SaleDiscountRequest? Discount,
    DiscountApprovalRequest? Approval,
    IReadOnlyList<SalePaymentRequest>? Payments);

public record ReceiptItemDto(Guid ProductId, string Name, string? Article, string? Unit, decimal Quantity, decimal Price,
    decimal LineTotal, decimal Discount, decimal ReturnedQuantity);

public record ReceiptPaymentDto(string Method, decimal Amount, decimal? Received, decimal? Change, string? TransferRecipient);

public record ReceiptCustomerDto(Guid Id, string FullName, string Phone, int? BonusBalance);

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
    decimal ReturnedAmount);
