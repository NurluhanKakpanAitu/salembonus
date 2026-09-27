namespace SalemBonus.Domain.Core;

/// <summary>
/// Бизнестің қызметкері: иесі, әкімші немесе кассир. Клиенттен (<see cref="Entities.Customer"/>) бөлек:
/// кіру паролмен, өзін-өзі тіркей алмайды — оны иесі қосады.
/// </summary>
public class StaffUser
{
    public const int MaxFailedLogins = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    /// <summary>PIN 4 таңба ғана — теріп көру оңай, сондықтан шегі қатаң. Асса — құпиясөзбен кіру керек.</summary>
    public const int MaxFailedPins = 5;
    public static readonly TimeSpan PinLockoutDuration = TimeSpan.FromMinutes(15);

    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    /// <summary>+7XXXXXXXXXX форматында, жүйе бойынша бірегей.</summary>
    public string Phone { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    /// <summary>Кассаны ашатын 4 таңбалы PIN-нің хеші. Бос болса — PIN әлі қойылмаған.</summary>
    public string? PinHash { get; set; }
    /// <summary>Интерфейс тілі: "ru" немесе "kk". ТЗ бойынша әдепкісі орысша.</summary>
    public string Language { get; set; } = "ru";
    public bool IsActive { get; set; } = true;
    public int FailedLoginCount { get; set; }
    public DateTime? LockedUntil { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public int PinFailedCount { get; set; }
    public DateTime? PinLockedUntil { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Organization? Organization { get; set; }
    public List<StoreMembership> Memberships { get; set; } = [];

    public string FullName => $"{FirstName} {LastName}".Trim();

    public bool IsLockedOut(DateTime now) => LockedUntil is { } until && until > now;

    public bool IsPinLockedOut(DateTime now) => PinLockedUntil is { } until && until > now;

    public bool HasPin => PinHash is not null;
}
