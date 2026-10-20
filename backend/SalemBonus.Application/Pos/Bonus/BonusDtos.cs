using SalemBonus.Domain.Pos.Bonus;

namespace SalemBonus.Application.Pos.Bonus;

// ---------- Мета ----------

public record BonusPermissionsDto(bool Manage, bool Campaigns, bool Accrue, bool WriteOff, bool Reverse, bool Terms, bool Export, bool Contacts);

/// <summary>Тарату арнасы: тек қосылған интеграциялар таңдалады (§6.4, §15).</summary>
public record BonusChannelDto(string Key, bool Available, string? Note);

public record BonusMetaDto(Guid StoreId, string StoreName, string TimeZone, IReadOnlyList<BonusStatusDto> Statuses, BonusPermissionsDto Can,
    IReadOnlyList<BonusChannelDto> Channels, decimal MaxRedeemPercent, bool ExpiryEnabled, bool BirthdayEnabled);

// ---------- Мәртебелер (§3) ----------

public record BonusStatusDto(Guid Id, string Name, string Icon, string Color, string Rule, decimal? ThresholdAmount, int? ThresholdCount, string Period,
    decimal AccrualPercent, bool CountReturns, int? ValidityDays, bool AutoDowngrade, string? Description, bool IsActive, int SortOrder, string Level,
    int Customers, DateTime CreatedAt, DateTime UpdatedAt);

public record SaveStatusRequest(string? Name, string? Icon, string? Color, string? Rule, decimal? ThresholdAmount, int? ThresholdCount, string? Period,
    decimal AccrualPercent, bool CountReturns, int? ValidityDays, bool AutoDowngrade, string? Description, bool IsActive = true);

/// <summary>Клиент мәртебесін өзгерту (§3.4): «rules» — ережелер бойынша қайта бағалау, «manual» — қолмен, себеп міндетті.</summary>
public record ChangeStatusRequest(string? Mode, Guid? StatusId, string? Reason);

public record OptOutRequest(bool OptOut, string? Reason);

// ---------- Клиенттер (§10) ----------

public record BonusCustomerDto(Guid CustomerId, string Name, string? Phone, string? CardCode, Guid? StatusId, string StatusName, string StatusColor,
    bool StatusManual, int Balance, decimal TotalSpent, DateOnly? BirthDate, DateTime RegisteredAt, DateTime? LastOperationAt, bool OptOut);

public record BonusCustomersPageDto(IReadOnlyList<BonusCustomerDto> Items, int Total, int Page, int PageSize);

public record BonusLotDto(Guid Id, string Type, string? Source, int Original, int Remaining, int Used, DateTime AccruedAt, DateTime? ExpiresAt, string State, string? Comment);

public record PurchaseStatsDto(decimal Amount, int Count, DateTime? LastAt);

public record BonusCustomerCardDto(BonusCustomerDto Customer, decimal Percent, int? NextExpiryAmount, DateTime? NextExpiryAt, string? StatusReason,
    DateTime? StatusAssignedAt, DateTime? StatusExpiresAt, PurchaseStatsDto Purchases, IReadOnlyList<BonusLotDto> Lots, IReadOnlyList<BonusOperationDto> Operations);

// ---------- Операциялар (§8) ----------

public record BonusOperationDto(Guid Id, DateTime CreatedAt, Guid StoreId, string StoreName, Guid CustomerId, string CustomerName, string? Phone,
    string Type, int Amount, int? BalanceBefore, int? BalanceAfter, string Basis, string? BasisLink, string Source, string Author, string Status,
    string? Comment, decimal? Percent, Guid? ReceiptId, long? ReceiptNumber, Guid? SourceTransactionId, Guid? SourceId, DateTime? ExpiresAt, int Remaining,
    bool CanReverse, bool CanReturn);

public record BonusOperationsPageDto(IReadOnlyList<BonusOperationDto> Items, int Total, int Page, int PageSize);

public record BonusOperationDetailDto(BonusOperationDto Operation, IReadOnlyList<BonusOperationDto> Related, int ReturnableAmount);

/// <summary>Журнал сүзгілері. Кезең — дүкеннің жергілікті күндері.</summary>
public class BonusListQuery
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public string? StoreIds { get; set; }
    public string? Type { get; set; }
    public string? Types { get; set; }
    public string? Status { get; set; }
    public Guid? StaffUserId { get; set; }
    public string? Source { get; set; }
    public int? MinAmount { get; set; }
    public int? MaxAmount { get; set; }
    public string? Search { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? CampaignId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public record ManualAccrualRequest(Guid CustomerId, int Amount, string? Reason, int? ValidityDays, bool Unlimited, string? Comment, Guid? ClientRequestId);
public record ManualWriteOffRequest(Guid CustomerId, int Amount, string? Reason, string? Comment, Guid? ClientRequestId);
public record ReturnBonusRequest(int Amount, string? Reason, Guid? ClientRequestId);
public record ReverseBonusRequest(string? Reason);

// ---------- Жану (§4) ----------

public record ExpirySettingsDto(bool Enabled, int Value, string Unit, IReadOnlyList<Guid> StatusIds, IReadOnlyList<string> ExcludedTypes,
    bool WarnEnabled, IReadOnlyList<int> WarnDays, string WarnChannel, int OpenLots, int OpenLotsWithoutExpiry, DateTime UpdatedAt);

public record SaveExpirySettingsRequest(bool Enabled, int Value, string? Unit, IReadOnlyList<Guid>? StatusIds, IReadOnlyList<string>? ExcludedTypes,
    bool WarnEnabled, IReadOnlyList<int>? WarnDays, string? WarnChannel, bool ApplyToExisting);

public record ExpiringLotDto(Guid LotId, DateTime ExpiresAt, Guid CustomerId, string CustomerName, string? Phone, string StatusName, string StatusColor,
    int Amount, int Original, string Type, DateTime AccruedAt, string Notification, string State);

public record ExpiringPageDto(IReadOnlyList<ExpiringLotDto> Items, int Total, int Page, int PageSize, int Sum, bool Enabled);

public record SetLotExpiryRequest(DateTime? ExpiresAt, string? Reason);

// ---------- Туған күн (§5) ----------

public record BonusCustomerRefDto(Guid Id, string Name, string? Phone);

public record BirthdaySettingsDto(bool Enabled, int Amount, string Timing, int TimingDays, string Time, string Validity, int? ValidityDays,
    DateOnly? ValidityDate, IReadOnlyList<Guid> StatusIds, decimal? MinPurchaseAmount, int? MinPurchaseCount, int PurchasePeriodDays,
    IReadOnlyList<BonusCustomerRefDto> Excluded, string Message, DateTime UpdatedAt);

public record SaveBirthdaySettingsRequest(bool Enabled, int Amount, string? Timing, int TimingDays, string? Time, string? Validity, int? ValidityDays,
    DateOnly? ValidityDate, IReadOnlyList<Guid>? StatusIds, decimal? MinPurchaseAmount, int? MinPurchaseCount, int PurchasePeriodDays,
    IReadOnlyList<Guid>? ExcludedCustomerIds, string? Message);

public record UpcomingBirthdayDto(DateOnly Birthday, int DaysLeft, Guid CustomerId, string CustomerName, string? Phone, string StatusName, string StatusColor,
    bool Eligible, int Amount, DateOnly PlannedDate, string State, Guid? TransactionId);

public record BirthdaysPageDto(IReadOnlyList<UpcomingBirthdayDto> Items, int Total, int Page, int PageSize, bool Enabled, int WithoutBirthDate);

public record BirthdayPreviewDto(string Customer, string Message, int Amount, DateOnly PlannedDate, DateTime AccrualAt, DateTime? ExpiresAt);

public record ManualBirthdayRequest(Guid CustomerId, string? Reason);

// ---------- Таратулар (§6) ----------

public record CampaignDto(Guid Id, string Name, string Type, string Status, string Channel, string AudienceMode, AudienceFilter Filter,
    IReadOnlyList<Guid> CustomerIds, string Message, int? BonusAmount, int? BonusValidityDays, DateTime? ScheduledAt,
    int Recipients, int Sent, int Delivered, int Read, int Failed, int Excluded, string Author, DateTime CreatedAt, DateTime? StartedAt,
    DateTime? CompletedAt, DateTime? CancelledAt, bool CanEdit, bool CanCancel, bool CanSend, bool CanRetry);

public record CampaignsPageDto(IReadOnlyList<CampaignDto> Items, int Total, int Page, int PageSize);

public record SaveCampaignRequest(string? Name, string? Type, string? Channel, string? AudienceMode, AudienceFilter? Filter,
    IReadOnlyList<Guid>? CustomerIds, string? Message, int? BonusAmount, int? BonusValidityDays, DateTime? ScheduledAt);

public record ExclusionDto(string Reason, int Count);

public record AudiencePreviewDto(int Found, int Eligible, int Excluded, IReadOnlyList<ExclusionDto> Reasons, IReadOnlyList<BonusCustomerRefDto> Sample, int BonusTotal);

public record ScheduleCampaignRequest(DateTime? ScheduledAt);

public record TestSendRequest(string? Phone, Guid? CustomerId, string? Message);

public record TestSendResultDto(string Customer, string Message, bool Delivered);

public record CampaignRecipientDto(Guid Id, Guid CustomerId, string CustomerName, string? Contact, string Status, string? Reason, DateTime? SentAt,
    bool Read, Guid? BonusTransactionId);

public record CampaignRecipientsPageDto(IReadOnlyList<CampaignRecipientDto> Items, int Total, int Page, int PageSize);

// ---------- Шолу мен статистика (§2, §7) ----------

public record BonusKpiDto(string Key, decimal? Value, decimal? Previous, decimal? ChangePercent, bool AsOfNow, string? Note = null);

public record BonusDayDto(DateOnly Date, int Accrued, int Used, int Returned, int Expired);

public record StatusShareDto(Guid? StatusId, string Name, string Color, int Customers, decimal Percent);

public record BonusOverviewDto(DateOnly From, DateOnly To, DateTime GeneratedAt, IReadOnlyList<BonusKpiDto> Kpis, IReadOnlyList<BonusDayDto> Days,
    IReadOnlyList<StatusShareDto> Statuses, IReadOnlyList<ExpiringLotDto> Expiring, bool ExpiryEnabled, IReadOnlyList<UpcomingBirthdayDto> Birthdays,
    bool BirthdayEnabled, IReadOnlyList<BonusOperationDto> Recent);

public record TypeTotalDto(string Type, int Amount, int Count);

public record TopCustomerDto(int Place, Guid CustomerId, string Name, string StatusName, string StatusColor, int Value, int Returned, DateTime? LastOperationAt);

public record CampaignEffectDto(Guid Id, string Name, DateTime? SentAt, int Recipients, int Sent, int Read, decimal? ReadPercent, int BonusTotal);

public record BonusStatisticsDto(DateOnly From, DateOnly To, DateTime GeneratedAt, IReadOnlyList<BonusKpiDto> Kpis, IReadOnlyList<BonusDayDto> Days,
    IReadOnlyList<TypeTotalDto> Types, IReadOnlyList<StatusShareDto> Statuses, IReadOnlyList<TopCustomerDto> TopBalance, IReadOnlyList<TopCustomerDto> TopUsage,
    IReadOnlyList<CampaignEffectDto> Campaigns, int ExpiredInPeriod, int Expiring30, int Expiring60, int Expiring90);

// ---------- Ережелер (§9) ----------

public record BonusTermsDto(Guid Id, int Version, string Content, string Status, string? Comment, DateOnly? EffectiveFrom, int? BasedOnVersion,
    string Author, DateTime UpdatedAt, DateTime? PublishedAt, string? PublishedBy);

public record TermsStateDto(BonusTermsDto? Published, BonusTermsDto? Draft, IReadOnlyList<BonusTermsDto> Versions, string Template);

public record SaveTermsDraftRequest(string? Content, string? Comment);

public record PublishTermsRequest(DateOnly? EffectiveFrom, string? Comment);
