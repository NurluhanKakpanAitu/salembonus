namespace SalemBonus.Application.Pos.Dtos;

/// <summary>Кассада тұтынушыны тапқандағы жауап.</summary>
public record PosCustomerDto(
    Guid CustomerId,
    string DisplayName,
    string PhoneMasked,
    bool IsNewAtStore,
    int Balance,
    string Level,
    decimal CashbackPercent,
    decimal MaxRedeemPercent);

/// <summary>Сатып алуды тіркеу: бонуспен төлеу (RedeemAmount) және қалғанына бонус есептеу.</summary>
public record PurchaseRequest(
    string CustomerCode,
    decimal PurchaseAmount,
    int RedeemAmount = 0,
    string? Comment = null);

public record PurchaseResultDto(
    Guid CustomerId,
    Guid BonusCardId,
    decimal PurchaseAmount,
    int Redeemed,
    decimal PaidAmount,
    int Accrued,
    int NewBalance,
    string Level,
    bool LevelUpgraded,
    Guid? AccrualTransactionId,
    Guid? RedemptionTransactionId);

public record PosStoreDto(Guid Id, string Name, string Category, decimal CashbackPercent, decimal MaxRedeemPercent);
