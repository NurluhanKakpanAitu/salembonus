using SalemBonus.Domain.Entities;
using SalemBonus.Domain.Enums;
using SalemBonus.Domain.Pos.Clients;
using SalemBonus.Domain.Pos.Sales;

namespace SalemBonus.Application.Common.Interfaces;

/// <summary>
/// Клиенттер тізімінің сұранысы: ұйым, таңдалған дүкендер (көрсеткіштер солар бойынша), ағымдағы дүкен (бонус мәртебесі),
/// белсенділік шектері (баптаулардан, UTC) және сүзгі. <see cref="Ids"/> — сегмент не таңдау бойынша шектеу.
/// </summary>
public record ClientQuery(Guid OrgId, IReadOnlyList<Guid> StoreIds, Guid CurrentStoreId, TimeZoneInfo Zone, DateOnly Today,
    DateTime ActiveSince, DateTime PassiveBefore, DateTime NewSince, ClientFilter Filter)
{
    public IReadOnlyList<Guid>? Ids { get; init; }
    /// <summary>«Дни рождения» жылдам сүзгісі: бүгіннен бастап неше күн.</summary>
    public int BirthdayDays { get; init; } = 7;
    public string? Sort { get; init; }
    public bool Desc { get; init; }
    public int Skip { get; init; }
    public int Take { get; init; } = 25;
}

/// <summary>Тізім жолы: CRM карточкасы + жаһандық клиенттен аты-жөні (карточкада бос болса), таңдалған дүкендер бойынша есептелген көрсеткіштер.</summary>
public record ClientRow(ClientProfile Profile, string FirstName, string LastName, DateOnly? BirthDate, string Phone, string QrCode, bool HasApp,
    Guid? StatusId, CustomerLevel? Level, bool HasCurrentCard, bool OptOut, int Purchases, decimal Amount, DateTime? FirstPurchaseAt, DateTime? LastPurchaseAt,
    int Bonus, decimal Debt, decimal Overdue)
{
    public decimal Average => Purchases == 0 ? 0 : Math.Round(Amount / Purchases, 2);
}

public record ClientKpiCounts(int Total, int Active, int Passive, int New, int Repeat, int WithDebt, int Overdue, int WithBonus);

public record ClientSaleRow(Guid Id, Guid StoreId, long Number, DateTime CreatedAt, decimal Total, decimal ReturnedAmount, SaleStatus Status, int Items,
    int BonusAccrued, int BonusRedeemed, decimal DiscountAmount, Guid StaffUserId, IReadOnlyList<PaymentMethod> Methods);

public record ClientReturnRow(Guid Id, Guid StoreId, Guid SaleId, long SaleNumber, DateTime CreatedAt, decimal Amount, decimal Refunded, PaymentMethod? RefundMethod,
    string? Reason, Guid StaffUserId);

public record ClientProductRow(Guid ProductId, string Name, decimal Quantity, decimal Amount, int Purchases);

public record ClientFinanceRow(Guid Id, string Number, DateTime Date, string Type, string Status, decimal Amount, string Description, Guid StoreId);

public record ClientDebtPaymentRow(Guid Id, Guid DebtId, Guid StoreId, long? SaleNumber, DateTime CreatedAt, decimal Amount, PaymentMethod? Method, bool IsReturn, Guid StaffUserId);

public record ClientAuditRow(Guid Id, DateTime CreatedAt, Guid? StaffUserId, string Action, string? OldValues, string? NewValues);

public record ClientMergeCounts(int Sales, int Debts, int CardsMoved, int CardsMerged, int BonusMoved, int Operations, int Notes);

public interface IClientRepository
{
    /// <summary>Ұйым дүкендерінде картасы бар, бірақ CRM карточкасы әлі жоқ клиенттерге карточка жасайды (бір SQL, жарысқа қауіпсіз).</summary>
    Task EnsureProfilesAsync(Guid orgId, CancellationToken ct = default);

    Task<ClientSettings?> SettingsAsync(Guid orgId, CancellationToken ct = default);
    void AddSettings(ClientSettings settings);

    Task<ClientProfile?> GetAsync(Guid orgId, Guid id, CancellationToken ct = default);
    Task<ClientProfile?> ByCustomerAsync(Guid orgId, Guid customerId, CancellationToken ct = default);
    Task<ClientProfile?> ByNumberAsync(Guid orgId, long number, CancellationToken ct = default);
    Task<Dictionary<Guid, ClientProfile>> ByCustomersAsync(Guid orgId, IReadOnlyCollection<Guid> customerIds, CancellationToken ct = default);
    Task<long> NextNumberAsync(Guid orgId, CancellationToken ct = default);
    void Add(ClientProfile profile);

    Task<IReadOnlyList<Guid>> OrgStoreIdsAsync(Guid orgId, CancellationToken ct = default);
    Task<Dictionary<Guid, string>> StoreNamesAsync(Guid orgId, CancellationToken ct = default);
    Task<Dictionary<Guid, string>> StaffNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
    Task<bool> HasAppAsync(Guid customerId, CancellationToken ct = default);
    Task<bool> HasCardsOutsideAsync(Guid customerId, IReadOnlyCollection<Guid> storeIds, CancellationToken ct = default);
    Task<Dictionary<string, Customer>> CustomersByPhonesAsync(IReadOnlyCollection<string> phones, CancellationToken ct = default);
    Task<BonusCard?> CardAsync(Guid customerId, Guid storeId, CancellationToken ct = default);

    Task<(IReadOnlyList<ClientRow> Rows, int Total)> ListAsync(ClientQuery q, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> IdsAsync(ClientQuery q, CancellationToken ct = default);
    Task<ClientKpiCounts> KpisAsync(ClientQuery q, CancellationToken ct = default);
    /// <summary>Бір карточканың жолы (мұрағаттағысы да; картасы болмаса да).</summary>
    Task<ClientRow?> RowAsync(ClientQuery q, Guid profileId, CancellationToken ct = default);
    /// <summary>Аудиториядағы карточкалардың тіркелу уақыты (шолудағы «жаңа клиенттер» графигі).</summary>
    Task<IReadOnlyList<DateTime>> RegistrationsAsync(ClientQuery q, DateTime sinceUtc, CancellationToken ct = default);

    Task<(IReadOnlyList<ClientSaleRow> Rows, int Total)> SalesAsync(Guid customerId, IReadOnlyCollection<Guid> storeIds, int skip, int take, CancellationToken ct = default);
    /// <summary>Толық қайтарылмаған сатылымдар: уақыты мен таза сомасы (белсенділік есептері).</summary>
    Task<IReadOnlyList<(DateTime At, decimal Net)>> SaleTimelineAsync(Guid customerId, IReadOnlyCollection<Guid> storeIds, CancellationToken ct = default);
    Task<IReadOnlyList<ClientReturnRow>> ReturnsAsync(Guid customerId, IReadOnlyCollection<Guid> storeIds, CancellationToken ct = default);
    Task<IReadOnlyList<ClientProductRow>> TopProductsAsync(Guid customerId, IReadOnlyCollection<Guid> storeIds, int take, CancellationToken ct = default);
    Task<IReadOnlyList<ClientFinanceRow>> FinanceOperationsAsync(Guid orgId, Guid customerId, IReadOnlyCollection<Guid> storeIds, CancellationToken ct = default);
    Task<IReadOnlyList<ClientDebtPaymentRow>> DebtPaymentsAsync(Guid customerId, IReadOnlyCollection<Guid> storeIds, CancellationToken ct = default);

    Task<IReadOnlyList<ClientNote>> NotesAsync(Guid profileId, CancellationToken ct = default);
    Task<ClientNote?> NoteAsync(Guid orgId, Guid id, CancellationToken ct = default);
    void AddNote(ClientNote note);
    void RemoveNote(ClientNote note);
    Task<IReadOnlyList<ClientCommunication>> CommunicationsAsync(Guid profileId, int take, CancellationToken ct = default);
    void AddCommunication(ClientCommunication communication);
    Task<IReadOnlyList<ClientAuditRow>> AuditAsync(Guid orgId, Guid profileId, int take, CancellationToken ct = default);

    Task<IReadOnlyList<ClientSegment>> SegmentsAsync(Guid orgId, Guid staffUserId, CancellationToken ct = default);
    Task<ClientSegment?> SegmentAsync(Guid orgId, Guid id, CancellationToken ct = default);
    void AddSegment(ClientSegment segment);
    void RemoveSegment(ClientSegment segment);

    Task<ClientImport?> ImportAsync(Guid orgId, Guid id, CancellationToken ct = default);
    void AddImport(ClientImport import);

    /// <summary>Телікті негізгіге көшіру (транзакция ішінде): ұйым дүкендеріндегі сатылым, қарыз, бонус карталары, қаржы операциялары, жазбалар.</summary>
    Task<ClientMergeCounts> MergeAsync(Guid orgId, IReadOnlyList<Guid> orgStoreIds, ClientProfile primary, ClientProfile duplicate, CancellationToken ct = default);
    void AddMergeLog(ClientMergeLog log);
}
