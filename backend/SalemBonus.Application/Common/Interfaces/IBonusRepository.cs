using SalemBonus.Domain.Entities;
using SalemBonus.Domain.Enums;
using SalemBonus.Domain.Pos.Bonus;

namespace SalemBonus.Application.Common.Interfaces;

/// <summary>Бонус картасы + клиент (тізімдер, панель).</summary>
public record BonusCustomerRow(BonusCard Card, string FirstName, string LastName, string Phone, DateOnly? BirthDate, string QrCode, DateTime RegisteredAt, DateTime? LastOperationAt);

public record BonusCustomerFilter(Guid StoreId, string? Search, Guid? StatusId, bool PositiveOnly, string? Sort, bool Desc, int Skip, int Take);

/// <summary>Операция журналы жолы: операция, карта дүкені, клиент.</summary>
public record BonusOperationRow(BonusTransaction Tx, Guid StoreId, Guid CustomerId, string FirstName, string LastName, string Phone, string QrCode);

public record BonusOperationFilter(
    IReadOnlyCollection<Guid> StoreIds, DateTime? FromUtc, DateTime? ToUtc, IReadOnlyCollection<BonusTransactionType>? Types, string? Status,
    Guid? StaffUserId, string? Source, int? MinAmount, int? MaxAmount, string? Search, Guid? CustomerId, Guid? SourceId, int Skip, int Take);

/// <summary>Статистикаға жеңіл жол.</summary>
public record BonusFlowRow(DateTime CreatedAt, BonusTransactionType Type, int Amount, Guid CardId, Guid CustomerId, Guid StoreId, string? Source, Guid? SourceId);

public record BonusLotRow(BonusTransaction Lot, Guid StoreId, Guid CustomerId, string FirstName, string LastName, string Phone, Guid? StatusId);

/// <summary>Сатып алу көрсеткіштері (мәртебе, туған күн, аудитория шарттары): pos.sales бойынша.</summary>
public record PurchaseStats(decimal Gross, decimal Returned, int Count, int CountNotReturned, DateTime? LastAt)
{
    public static readonly PurchaseStats Empty = new(0, 0, 0, 0, null);
    public decimal Amount(bool countReturns) => countReturns ? Gross - Returned : Gross;
    public int Purchases(bool countReturns) => countReturns ? CountNotReturned : Count;
}

public interface IBonusRepository
{
    // Мәртебелер мен баптаулар
    Task<IReadOnlyList<LoyaltyStatus>> StatusesAsync(Guid storeId, CancellationToken ct = default);
    Task<LoyaltyStatus?> GetStatusAsync(Guid storeId, Guid id, CancellationToken ct = default);
    Task<bool> StatusNameExistsAsync(Guid storeId, string name, Guid? excludeId, CancellationToken ct = default);
    Task<Dictionary<Guid, int>> StatusUsageAsync(Guid storeId, CancellationToken ct = default);
    void AddStatus(LoyaltyStatus status);
    void RemoveStatus(LoyaltyStatus status);
    /// <summary>Бағдарлама іске қосылғанда: ескі деңгейдегі карталарға сәйкес мәртебе.</summary>
    Task AssignStatusByLevelAsync(Guid storeId, CustomerLevel level, Guid statusId, CancellationToken ct = default);
    Task<BonusSettings?> SettingsAsync(Guid storeId, CancellationToken ct = default);
    void AddSettings(BonusSettings settings);
    Task<IReadOnlyList<BonusSettings>> AllSettingsAsync(CancellationToken ct = default);
    /// <summary>Ескі баспалдақты (SalemBonus қосымшасы мен терминал API-ы көретін) мәртебелерден қайта жазу.</summary>
    Task ReplaceLadderAsync(Guid storeId, IReadOnlyList<StoreLevel> levels, CancellationToken ct = default);
    /// <summary>Мәртебенің қосымшадағы деңгейі өзгерсе — оны иеленген карталардың деңгейі де.</summary>
    Task SyncCardLevelsAsync(Guid storeId, Guid statusId, CustomerLevel level, CancellationToken ct = default);

    // Клиенттер мен карталар
    Task<(IReadOnlyList<BonusCustomerRow> Rows, int Total)> CustomersAsync(BonusCustomerFilter f, CancellationToken ct = default);
    Task<IReadOnlyList<BonusCustomerRow>> AllCustomersAsync(Guid storeId, CancellationToken ct = default);
    Task<BonusCustomerRow?> CustomerAsync(Guid storeId, Guid customerId, CancellationToken ct = default);
    Task<BonusCard?> CardForUpdateAsync(Guid storeId, Guid customerId, CancellationToken ct = default);
    Task<BonusCard?> CardByIdForUpdateAsync(Guid cardId, CancellationToken ct = default);
    Task<IReadOnlyList<BonusCard>> CardsDueForReviewAsync(DateTime now, int take, CancellationToken ct = default);
    Task<PurchaseStats> PurchaseStatsAsync(Guid storeId, Guid customerId, DateTime? fromUtc, CancellationToken ct = default);
    Task<Dictionary<Guid, PurchaseStats>> PurchaseStatsBulkAsync(Guid storeId, IReadOnlyCollection<Guid> customerIds, DateTime? fromUtc, CancellationToken ct = default);
    Task<int> CustomersCountAsync(IReadOnlyCollection<Guid> storeIds, CancellationToken ct = default);
    Task<IReadOnlyList<(Guid CardId, Guid CustomerId, int Balance, Guid? StatusId)>> BalancesAsync(IReadOnlyCollection<Guid> storeIds, CancellationToken ct = default);

    // Партиялар мен операциялар
    Task<IReadOnlyList<BonusTransaction>> LotsAsync(Guid cardId, CancellationToken ct = default);
    Task<IReadOnlyList<BonusTransaction>> OpenLotsForUpdateAsync(Guid cardId, CancellationToken ct = default);
    Task<BonusTransaction?> LotForUpdateAsync(Guid lotId, CancellationToken ct = default);
    Task<(IReadOnlyList<BonusLotRow> Rows, int Total, int Sum)> ExpiringLotsAsync(IReadOnlyCollection<Guid> storeIds, DateTime fromUtc, DateTime toUtc,
        string? search, Guid? statusId, int skip, int take, CancellationToken ct = default);
    Task<int> ExpiringSumAsync(IReadOnlyCollection<Guid> storeIds, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    Task<IReadOnlyList<BonusLotRow>> WarnableLotsAsync(Guid storeId, DateTime nowUtc, DateTime untilUtc, CancellationToken ct = default);
    Task<HashSet<(Guid LotId, int Days)>> WarningsAsync(IReadOnlyCollection<Guid> lotIds, CancellationToken ct = default);
    void AddWarning(ExpiryWarning warning);
    Task<IReadOnlyList<BonusTransaction>> OpenLotsOfStoreAsync(Guid storeId, CancellationToken ct = default);
    Task<(IReadOnlyList<BonusOperationRow> Rows, int Total)> OperationsAsync(BonusOperationFilter f, CancellationToken ct = default);
    Task<BonusOperationRow?> OperationAsync(Guid id, CancellationToken ct = default);
    Task<BonusTransaction?> OperationForUpdateAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<BonusTransaction>> RelatedAsync(Guid id, CancellationToken ct = default);
    Task<int> RestoredForRedemptionAsync(BonusTransaction redemption, CancellationToken ct = default);
    Task<BonusTransaction?> ByIdempotencyKeyAsync(Guid key, CancellationToken ct = default);
    Task<IReadOnlyList<BonusFlowRow>> FlowsAsync(IReadOnlyCollection<Guid> storeIds, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    Task<IReadOnlyList<BonusOperationRow>> RecentAsync(IReadOnlyCollection<Guid> storeIds, int take, CancellationToken ct = default);
    Task<Dictionary<Guid, DateTime>> LastOperationAsync(IReadOnlyCollection<Guid> cardIds, CancellationToken ct = default);

    // Туған күн
    Task<IReadOnlyList<BirthdayGrant>> GrantsAsync(Guid storeId, IReadOnlyCollection<int> years, CancellationToken ct = default);
    Task<BirthdayGrant?> GrantForUpdateAsync(Guid storeId, Guid customerId, int year, CancellationToken ct = default);
    void AddGrant(BirthdayGrant grant);

    // Таратулар
    Task<(IReadOnlyList<BonusCampaign> Items, int Total)> CampaignsAsync(Guid storeId, string? status, string? search, int skip, int take, CancellationToken ct = default);
    Task<BonusCampaign?> CampaignAsync(Guid storeId, Guid id, CancellationToken ct = default);
    Task<BonusCampaign?> CampaignForUpdateAsync(Guid id, CancellationToken ct = default);
    void AddCampaign(BonusCampaign campaign);
    Task<IReadOnlyList<Guid>> DueCampaignIdsAsync(DateTime now, CancellationToken ct = default);
    Task<(IReadOnlyList<CampaignRecipient> Items, int Total)> RecipientsAsync(Guid campaignId, string? status, int skip, int take, CancellationToken ct = default);
    Task<IReadOnlyList<CampaignRecipient>> RecipientsForUpdateAsync(Guid campaignId, RecipientStatus status, CancellationToken ct = default);
    Task<bool> HasRecipientsAsync(Guid campaignId, CancellationToken ct = default);
    void AddRecipient(CampaignRecipient recipient);
    Task<int> ReadCountAsync(Guid campaignId, CancellationToken ct = default);
    Task<HashSet<Guid>> ReadNotificationsAsync(IReadOnlyCollection<Guid> notificationIds, CancellationToken ct = default);

    // Ережелер
    Task<IReadOnlyList<BonusTerms>> TermsAsync(Guid storeId, CancellationToken ct = default);
    Task<BonusTerms?> TermsVersionAsync(Guid storeId, Guid id, CancellationToken ct = default);
    Task<BonusTerms?> DraftForUpdateAsync(Guid storeId, CancellationToken ct = default);
    Task<BonusTerms?> PublishedForUpdateAsync(Guid storeId, CancellationToken ct = default);
    Task<int> MaxTermsVersionAsync(Guid storeId, CancellationToken ct = default);
    void AddTerms(BonusTerms terms);

    // Атаулар
    Task<Dictionary<Guid, string>> StaffNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
    Task<Dictionary<Guid, long>> SaleNumbersAsync(IReadOnlyCollection<Guid> saleIds, CancellationToken ct = default);
    Task<Dictionary<Guid, string>> CampaignNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
    Task<Dictionary<Guid, string>> StoreNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
    Task<Guid?> SaleIdByNumberAsync(Guid storeId, long number, CancellationToken ct = default);
}
