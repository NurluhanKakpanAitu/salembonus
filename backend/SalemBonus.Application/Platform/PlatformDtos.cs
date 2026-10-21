namespace SalemBonus.Application.Platform;

// ---------- Кіру мен әкімшілер ----------

public record AdminLoginRequest(string? Email, string? Password);
public record AdminDto(Guid Id, string Email, string Name, bool IsActive, DateTime? LastLoginAt, DateTime CreatedAt);
public record AdminSessionDto(string AccessToken, DateTime ExpiresAt, AdminDto Admin);
public record AdminChangePasswordRequest(string? CurrentPassword, string? NewPassword);
public record CreateAdminRequest(string? Email, string? Name);
public record AdminCreatedDto(AdminDto Admin, string TempPassword);
public record SetActiveRequest(bool Active);

// ---------- Тарифтер ----------

public record PlanDto(Guid Id, string Name, decimal MonthlyPrice, IReadOnlyList<string> Modules, string? Description, bool IsActive, int SortOrder, int Businesses);
public record SavePlanRequest(string? Name, decimal? MonthlyPrice, IReadOnlyList<string>? Modules, string? Description, bool? IsActive, int? SortOrder);

// ---------- Бизнестер ----------

/// <summary>Төлем күйі: trial, active, dueSoon, overdue, unpaid, suspended.</summary>
public record BusinessRowDto(Guid Id, string Name, string? Bin, string? City, string? ContactName, string? ContactPhone, Guid? PlanId, string? PlanName,
    decimal Fee, bool CustomFee, string Status, DateOnly? PaidUntil, DateOnly? TrialUntil, int? DaysOverdue, IReadOnlyList<string> Modules, int Stores, int Staff,
    int Sales30, decimal Revenue30, DateTime? LastSaleAt, int Clients, string? OwnerName, string? OwnerPhone, DateTime? LastLoginAt, DateTime CreatedAt);

public record BusinessPageDto(IReadOnlyList<BusinessRowDto> Rows, int Total, IReadOnlyDictionary<string, int> Counts);

public record BusinessStoreDto(Guid Id, string Name, string Category, string? Address, string? Phone, string? KatoCode, string TimeZone, bool IsActive,
    string JoinCode, int Registers, DateTime CreatedAt);

public record StaffRoleDto(Guid StoreId, string StoreName, string Role, bool Active);

public record StaffAccountDto(Guid Id, string Name, string Phone, bool IsActive, bool HasPassword, bool HasPin, DateTime? LastLoginAt, DateTime CreatedAt,
    IReadOnlyList<StaffRoleDto> Roles);

public record PaymentDto(Guid Id, Guid OrganizationId, string OrganizationName, decimal Amount, DateOnly PaidOn, DateOnly PeriodFrom, DateOnly PeriodTo,
    string Method, string? Reference, string? Comment, string CreatedBy, DateTime CreatedAt, bool Voided, DateTime? VoidedAt, string? VoidedBy, string? VoidReason);

public record PlatformAuditDto(Guid Id, DateTime CreatedAt, string Admin, string Action, string? Data);

public record BusinessDetailDto(BusinessRowDto Business, string? ContactEmail, string? Comment, DateTime? SuspendedAt, string? SuspendReason,
    IReadOnlyList<BusinessStoreDto> Stores, IReadOnlyList<StaffAccountDto> Staff, IReadOnlyList<PaymentDto> Payments, decimal PaidTotal,
    IReadOnlyList<PlatformAuditDto> History);

public record CreateStoreRequest(string? Name, string? Category, string? Address, string? Phone, string? KatoCode, string? TimeZone);
public record CreateAccountRequest(string? Phone, string? FirstName, string? LastName, string? Role, Guid? StoreId);

public record CreateBusinessRequest(string? Name, string? Bin, string? City, string? ContactName, string? ContactPhone, string? ContactEmail, string? Comment,
    Guid? PlanId, decimal? MonthlyFee, int? TrialDays, IReadOnlyList<string>? Modules, CreateStoreRequest? Store, CreateAccountRequest? Owner);

public record BusinessCreatedDto(BusinessDetailDto Business, string OwnerPhone, string TempPassword);

public record UpdateBusinessRequest(string? Name, string? Bin, string? City, string? ContactName, string? ContactPhone, string? ContactEmail, string? Comment,
    Guid? PlanId, bool ApplyPlanModules, bool CustomFee, decimal? MonthlyFee, DateOnly? TrialUntil);

public record SetModulesRequest(IReadOnlyList<string>? Modules);
public record ReasonRequest(string? Reason);
public record StaffCredentialsDto(StaffAccountDto Account, string TempPassword);

// ---------- Төлемдер мен шолу ----------

public record CreatePaymentRequest(Guid OrganizationId, decimal Amount, DateOnly? PaidOn, int? Months, DateOnly? PeriodFrom, DateOnly? PeriodTo, string? Method,
    string? Reference, string? Comment);

public record PaymentSuggestionDto(DateOnly PeriodFrom, DateOnly PeriodTo, decimal Amount, decimal MonthlyFee);
public record PaymentsPageDto(IReadOnlyList<PaymentDto> Rows, int Total, decimal Sum);
public record MonthAmountDto(string Month, decimal Amount, int Count);
public record ModuleUsageDto(string Module, int Businesses);

public record PlatformOverviewDto(int Businesses, IReadOnlyDictionary<string, int> ByStatus, decimal Mrr, decimal PaidThisMonth, int PaymentsThisMonth,
    decimal PaidLastMonth, int NewThisMonth, IReadOnlyList<MonthAmountDto> Revenue, IReadOnlyList<BusinessRowDto> Overdue, IReadOnlyList<BusinessRowDto> Renewals,
    IReadOnlyList<PaymentDto> RecentPayments, IReadOnlyList<ModuleUsageDto> Modules, int NewLeads);
