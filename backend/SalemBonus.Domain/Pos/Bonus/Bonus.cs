using SalemBonus.Domain.Enums;

namespace SalemBonus.Domain.Pos.Bonus;

/// <summary>Мәртебеге өту шарты (ТЗ «Бонусы» §3.2).</summary>
public enum StatusRule
{
    /// <summary>Тіркелгенде — бастапқы мәртебе.</summary>
    Registration,
    /// <summary>Кезеңдегі сатып алу сомасы шектен асқанда.</summary>
    PurchaseAmount,
    /// <summary>Кезеңдегі сатып алу саны шектен асқанда.</summary>
    PurchaseCount,
    /// <summary>Тек қолмен (себеппен) беріледі — «Партнёр» сияқты.</summary>
    Manual,
}

public enum StatusPeriod
{
    AllTime,
    Last12Months,
    CalendarYear,
}

/// <summary>
/// Бонус бағдарламасының мәртебесі (ТЗ «Бонусы» §3). Бағдарлама — дүкен бойынша: клиенттің бонус картасы да дүкендікі.
/// Деңгей реті — есептеу пайызы бойынша (жоғары пайыз — жоғары мәртебе); <see cref="Level"/> — SalemBonus қосымшасындағы
/// төрт деңгейдің қайсысымен көрсетілетіні (белгі мен түс), атауы — <see cref="Name"/>.
/// </summary>
public class LoyaltyStatus
{
    public Guid Id { get; set; }
    public Guid StoreId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = "star";
    public string Color { get; set; } = "#1769FF";
    public StatusRule Rule { get; set; }
    public decimal? ThresholdAmount { get; set; }
    public int? ThresholdCount { get; set; }
    public StatusPeriod Period { get; set; } = StatusPeriod.AllTime;
    public decimal AccrualPercent { get; set; }
    /// <summary>Қайтарымдар сатып алу сомасын азайтады.</summary>
    public bool CountReturns { get; set; } = true;
    /// <summary>Мәртебенің мерзімі (күн); бос — мерзімсіз.</summary>
    public int? ValidityDays { get; set; }
    /// <summary>Мерзімі біткенде шарттар қайта тексеріледі, орындалмаса — төмендейді.</summary>
    public bool AutoDowngrade { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public CustomerLevel Level { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum ExpiryUnit { Days, Months }
public enum BirthdayTiming { OnDay, DaysBefore, DaysAfter }
public enum BirthdayValidity { DaysAfterAccrual, UntilBirthday, DaysAfterBirthday, FixedDate, Unlimited }

/// <summary>
/// Дүкен бағдарламасының баптаулары (ТЗ «Бонусы» §4, §5): жану ережесі мен туған күн сыйлығы. Бір дүкенге бір жол.
/// Жоқ болса — ескі SalemBonus ережесі (<c>Store.BonusLifetimeDays</c>) әрекет етеді.
/// </summary>
public class BonusSettings
{
    public Guid StoreId { get; set; }

    public bool ExpiryEnabled { get; set; }
    public int ExpiryValue { get; set; } = 12;
    public ExpiryUnit ExpiryUnit { get; set; } = ExpiryUnit.Months;
    /// <summary>Бос — барлық мәртебеге.</summary>
    public List<Guid> ExpiryStatusIds { get; set; } = [];
    /// <summary>Жанбайтын операция түрлері (мысалы, Adjustment — қолмен есептеу).</summary>
    public List<string> ExpiryExcludedTypes { get; set; } = [];
    public bool WarnEnabled { get; set; }
    public List<int> WarnDays { get; set; } = [7, 1];
    public string WarnChannel { get; set; } = "app";

    public bool BirthdayEnabled { get; set; }
    public int BirthdayAmount { get; set; }
    public BirthdayTiming BirthdayTiming { get; set; } = BirthdayTiming.OnDay;
    public int BirthdayTimingDays { get; set; }
    public TimeOnly BirthdayTime { get; set; } = new(9, 0);
    public BirthdayValidity BirthdayValidity { get; set; } = BirthdayValidity.DaysAfterAccrual;
    public int? BirthdayValidityDays { get; set; } = 3;
    public DateOnly? BirthdayValidityDate { get; set; }
    public List<Guid> BirthdayStatusIds { get; set; } = [];
    public decimal? BirthdayMinPurchaseAmount { get; set; }
    public int? BirthdayMinPurchaseCount { get; set; }
    public int BirthdayPurchasePeriodDays { get; set; } = 365;
    public List<Guid> BirthdayExcludedCustomerIds { get; set; } = [];
    public string BirthdayMessage { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid? UpdatedBy { get; set; }
}

public enum BirthdayGrantStatus { Granted, Skipped }

/// <summary>Туған күн өңделгені: бір клиентке жылына бір жол (бірегей) — қайта өңдеу екінші сыйлық жасамайды (§18).</summary>
public class BirthdayGrant
{
    public Guid Id { get; set; }
    public Guid StoreId { get; set; }
    public Guid CustomerId { get; set; }
    public int Year { get; set; }
    public DateOnly BirthdayDate { get; set; }
    public BirthdayGrantStatus Status { get; set; }
    /// <summary>Өткізілмеу себебі: status, purchases, excluded, noBirthDate.</summary>
    public string? Reason { get; set; }
    public Guid? TransactionId { get; set; }
    public bool Manual { get; set; }
    public Guid? StaffUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Жану туралы ескерту жіберілгені: партия × күн бірегей.</summary>
public class ExpiryWarning
{
    public Guid Id { get; set; }
    public Guid LotId { get; set; }
    public int DaysBefore { get; set; }
    public Guid? NotificationId { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}

public enum CampaignType { Birthday, Expiring, Gift, Promo, Winback, Custom }
public enum CampaignStatus { Draft, Scheduled, Queued, Sending, Completed, CompletedWithErrors, Cancelled }
public enum AudienceMode { All, Manual }

/// <summary>Аудитория сүзгілері (§6.3); jsonb.</summary>
public class AudienceFilter
{
    public List<Guid> StatusIds { get; set; } = [];
    public bool PositiveBalance { get; set; }
    public int? ExpiringWithinDays { get; set; }
    public decimal? MinPurchaseAmount { get; set; }
    public int? MinPurchaseCount { get; set; }
    /// <summary>Соңғы сатып алудан бері өткен күн (кеткен клиенттерді қайтару).</summary>
    public int? InactiveDays { get; set; }
    public int? BirthdayMonth { get; set; }
    public DateOnly? RegisteredFrom { get; set; }
    public DateOnly? RegisteredTo { get; set; }
}

/// <summary>Бонус таратуы (ТЗ «Бонусы» §6). Арна — SalemBonus қосымшасының хабарламалары (басқа интеграциялар қосылмаған).</summary>
public class BonusCampaign
{
    public Guid Id { get; set; }
    public Guid StoreId { get; set; }
    public string Name { get; set; } = string.Empty;
    public CampaignType Type { get; set; }
    public CampaignStatus Status { get; set; } = CampaignStatus.Draft;
    public string Channel { get; set; } = "app";
    public AudienceMode AudienceMode { get; set; }
    public AudienceFilter Filter { get; set; } = new();
    public List<Guid> CustomerIds { get; set; } = [];
    public string Message { get; set; } = string.Empty;
    public int? BonusAmount { get; set; }
    public int? BonusValidityDays { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public int RecipientsCount { get; set; }
    public int SentCount { get; set; }
    public int FailedCount { get; set; }
    public int ExcludedCount { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public Guid? CancelledBy { get; set; }
}

public enum RecipientStatus { Pending, Sent, Failed, Excluded, Cancelled }

public class CampaignRecipient
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid BonusCardId { get; set; }
    public string Contact { get; set; } = string.Empty;
    public RecipientStatus Status { get; set; }
    /// <summary>Қате не алып тастау себебі: optOut, noContact, …</summary>
    public string? Reason { get; set; }
    public Guid? NotificationId { get; set; }
    public Guid? BonusTransactionId { get; set; }
    public DateTime? SentAt { get; set; }
}

public enum TermsStatus { Draft, Published, Archived }

/// <summary>Бағдарлама ережелерінің нұсқасы (ТЗ «Бонусы» §9): бір черновик, бір жарияланған, қалғаны — тарих.</summary>
public class BonusTerms
{
    public Guid Id { get; set; }
    public Guid StoreId { get; set; }
    /// <summary>Жарияланғанда беріледі; черновикте 0.</summary>
    public int Version { get; set; }
    public string Content { get; set; } = string.Empty;
    public TermsStatus Status { get; set; }
    public string? Comment { get; set; }
    public DateOnly? EffectiveFrom { get; set; }
    public int? BasedOnVersion { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAt { get; set; }
    public Guid? PublishedBy { get; set; }
}
