namespace SalemBonus.Application.Pos.Staff;

/// <summary>Қызметкердің бір дүкендегі қолжетімділігі.</summary>
public record StaffStoreAccessDto(Guid StoreId, string StoreName, bool StoreActive, string Role, IReadOnlyList<string> Permissions, decimal MaxDiscountPercent, bool IsActive);

/// <summary>«Сотрудники» тізімінің жолы.</summary>
public record StaffRowDto(
    Guid Id, string FirstName, string LastName, string Phone, string Language,
    /// <summary>Негізгі рөл — белсенді мүшеліктердің ең жоғарысы (иесі > әкімші > кассир).</summary>
    string Role,
    IReadOnlyList<Guid> StoreIds, IReadOnlyList<string> StoreNames,
    bool IsActive, bool HasPin, bool HasPassword,
    /// <summary>Қате кіру/PIN әрекеттерінен уақытша бұғатталған.</summary>
    bool IsLocked,
    DateTime? LastLoginAt, DateTime CreatedAt,
    int Sales30, decimal Revenue30, DateTime? LastSaleAt,
    /// <summary>Бұл — сұрап тұрған қызметкердің өзі (өз доступын өзгерте алмайды).</summary>
    bool IsSelf,
    /// <summary>Сұраушы өзгерте ала ма (иесін тек иесі өзгертеді).</summary>
    bool CanEdit);

public record StaffSessionDto(Guid Id, string? Device, DateTime CreatedAt, DateTime ExpiresAt, bool IsCurrent);

public record StaffDetailDto(
    StaffRowDto Staff,
    /// <summary>Барлық дүкенге ортақ рұқсаттар (бірінші белсенді мүшеліктен).</summary>
    IReadOnlyList<string> Permissions,
    decimal MaxDiscountPercent,
    IReadOnlyList<StaffStoreAccessDto> Access,
    IReadOnlyList<StaffSessionDto> Sessions);

public record StaffStoreOptionDto(Guid Id, string Name, bool IsActive);

public record StaffListDto(IReadOnlyList<StaffRowDto> Rows, IReadOnlyList<StaffStoreOptionDto> Stores,
    /// <summary>Сұраушы иесі ме: иелерді қосу/өзгерту тек оған ашық.</summary>
    bool IsOwner);

/// <summary>Қызметкерді қосу/өзгерту. Иесі — барлық дүкенде, барлық рұқсатпен; StoreIds/Permissions ескерілмейді.</summary>
public record SaveStaffRequest(
    string? FirstName, string? LastName, string? Phone, string? Language,
    string? Role, IReadOnlyList<Guid>? StoreIds, IReadOnlyList<string>? Permissions, decimal? MaxDiscountPercent,
    /// <summary>Жаңа PIN (қосқанда кассирге міндетті; өзгерткенде бос — ауыспайды).</summary>
    string? Pin,
    /// <summary>Қосқанда: иесі/әкімшіге уақытша құпиясөз жасау (әдепкі — иә).</summary>
    bool? GeneratePassword);

/// <summary>Жаңа қызметкер немесе құпиясөзді қалпына келтіру: уақытша құпиясөз бір рет қана көрсетіледі.</summary>
public record StaffSavedDto(StaffDetailDto Staff, string? TempPassword);

public record SetStaffPinRequest(string? Pin);
public record StaffActiveRequest(bool Active);

public record AuditRowDto(
    Guid Id, DateTime CreatedAt, string Action, string? Entity, string? EntityId,
    Guid? StaffUserId, string? StaffName, Guid? StoreId, string? StoreName, string? RegisterName,
    bool Success, string? Details, string? OldValues, string? NewValues, decimal? Amount, string? Ip);

public record AuditPageDto(IReadOnlyList<AuditRowDto> Rows, int Total, IReadOnlyList<string> Actions);
