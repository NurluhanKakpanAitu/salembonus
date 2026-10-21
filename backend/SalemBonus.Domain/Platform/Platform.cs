namespace SalemBonus.Domain.Platform;

/// <summary>SalemTech әкімшісі (admin.salemtech.kz): бизнестерді, төлемдерді, модульдерді басқарады. Бизнес қызметкерлерінен бөлек аккаунт.</summary>
public class PlatformAdmin
{
    public const int MaxFailedLogins = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int FailedLoginCount { get; set; }
    public DateTime? LockedUntil { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsLockedOut(DateTime now) => LockedUntil is { } until && until > now;
}

/// <summary>Тариф: айлық бағасы және әдепкі модульдері (бизнеске тағайындағанда модульдерін көшіруге болады).</summary>
public class PlatformPlan
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal MonthlyPrice { get; set; }
    public List<string> Modules { get; set; } = [];
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    /// <summary>Лендингте «Популярный» деп белгіленген тариф — бір уақытта біреу ғана.</summary>
    public bool IsFeatured { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Бизнестің абонент төлемі: сома, төленген күн, қай кезеңді жабады, тәсілі. Өшірілмейді — тек жойылады (себебімен),
/// сонда «төленген күні» қайта есептеледі.
/// </summary>
public class PlatformPayment
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public decimal Amount { get; set; }
    public DateOnly PaidOn { get; set; }
    public DateOnly PeriodFrom { get; set; }
    public DateOnly PeriodTo { get; set; }
    /// <summary>kaspi, transfer, card, cash, other.</summary>
    public string Method { get; set; } = "transfer";
    public string? Reference { get; set; }
    public string? Comment { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? VoidedAt { get; set; }
    public Guid? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
}

/// <summary>
/// Лендингтен (salemtech.kz) түскен өтінім: аты, телефон, бизнес, қала, қызығушылық (pos, bonus, both). Әкімші күйін жүргізеді:
/// new → contacted → won (бизнес қосылды) не lost.
/// </summary>
public class PlatformLead
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? BusinessName { get; set; }
    public string? City { get; set; }
    public string? BusinessType { get; set; }
    public string Interest { get; set; } = "both";
    public string? Comment { get; set; }
    public string Language { get; set; } = "ru";
    public string Status { get; set; } = "new";
    public string? AdminComment { get; set; }
    public Guid? OrganizationId { get; set; }
    public string? Ip { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

/// <summary>Әкімші әрекеттерінің журналы: кім, қашан, қай бизнесте, не өзгерді.</summary>
public class PlatformAuditEntry
{
    public Guid Id { get; set; }
    public Guid? AdminId { get; set; }
    public string Action { get; set; } = string.Empty;
    public Guid? OrganizationId { get; set; }
    public string? Entity { get; set; }
    public string? EntityId { get; set; }
    public string? Data { get; set; }
    public string? Ip { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
