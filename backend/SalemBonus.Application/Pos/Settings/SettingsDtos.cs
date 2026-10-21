using SalemBonus.Application.Pos.Sales;

namespace SalemBonus.Application.Pos.Settings;

/// <summary>Ағымдағы дүкеннің профилі мен бизнес деректері («Настройка → Магазин»).</summary>
public record StoreProfileDto(
    Guid Id, string Name, string Category, string Description, string? Address, string? Phone, string? KatoCode, string TimeZone,
    /// <summary>Клиенттің SalemBonus-та картаны ашатын коды (плакаттағы QR).</summary>
    string JoinCode,
    string OrganizationName, string? OrganizationBin,
    /// <summary>Бизнес атауы мен БСН-ді тек иесі өзгертеді.</summary>
    bool CanEditOrganization,
    IReadOnlyList<string> TimeZones,
    /// <summary>Елді мекеннің ҚАТО жолы (облыс → аудан → елді мекен кодтары) — таңдағыш үшін.</summary>
    IReadOnlyList<string> KatoPath);

public record UpdateStoreProfileRequest(
    string? Name, string? Category, string? Description, string? Address, string? Phone, string? KatoCode, string? TimeZone,
    string? OrganizationName, string? OrganizationBin);

public record StoreRowDto(Guid Id, string Name, string Category, string? Address, string? Phone, string TimeZone, bool IsActive, string JoinCode,
    int Registers, int Staff, bool IsCurrent, DateTime CreatedAt);

public record CreateStoreRequest(string? Name, string? Category, string? Address, string? Phone, string? KatoCode, string? TimeZone);

public record StoreActiveRequest(bool Active);

/// <summary>Касса («Настройка → Кассы»): байланған құрылғысы бар ма, осы браузер ма.</summary>
public record RegisterRowDto(Guid Id, string Name, int? AutoLockMinutes, bool IsBound, DateTime? ActivatedAt, bool IsActive, bool IsThisDevice,
    int Sales30, DateTime? LastSaleAt);

public record SaveRegisterRequest(string? Name, int? AutoLockMinutes);

public record RegisterActiveRequest(bool Active);

/// <summary>Дүкен деңгейіндегі касса баптаулары — құрылғыға тәуелсіз («Настройка → Оплата и чек», «Уведомления»).</summary>
public record StoreCashierSettingsDto(
    IReadOnlyList<string> EnabledMethods, bool MixedEnabled,
    bool AutoPrint, bool ElectronicReceipt,
    bool NotifyOutOfStock, bool NotifyLowStock, bool NotifyDebt, bool NotifyReturn, bool NotifyCashierChange, int LowStockThreshold,
    string? ReceiptHeader, string? ReceiptFooter, bool ReceiptShowAddress, bool ReceiptShowPhone, bool ReceiptShowBin,
    IReadOnlyList<TransferRecipientDto> Recipients,
    /// <summary>Чекті алдын ала қарау үшін: дүкен аты, мекенжайы, телефоны, бизнестің БСН-і.</summary>
    string StoreName, string? StoreAddress, string? StorePhone, string? OrganizationBin);

public record UpdateStoreCashierSettingsRequest(
    IReadOnlyList<string>? EnabledMethods, bool MixedEnabled,
    bool AutoPrint, bool ElectronicReceipt,
    bool NotifyOutOfStock, bool NotifyLowStock, bool NotifyDebt, bool NotifyReturn, bool NotifyCashierChange, int LowStockThreshold,
    string? ReceiptHeader, string? ReceiptFooter, bool ReceiptShowAddress, bool ReceiptShowPhone, bool ReceiptShowBin);

public record PlanModuleDto(string Key, bool Enabled);

/// <summary>«Настройка → Тариф»: тариф, күйі, қай күнге дейін төленген, қосылған модульдер. Тек оқуға — өзгертуді SalemTech жасайды.</summary>
public record PlanInfoDto(string? PlanName, decimal MonthlyFee, string Status, DateOnly? PaidUntil, DateOnly? TrialUntil, int? DaysLeft, int? DaysOverdue,
    IReadOnlyList<PlanModuleDto> Modules, string SupportUrl);
