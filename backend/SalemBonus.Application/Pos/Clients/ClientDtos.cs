using SalemBonus.Domain.Pos.Clients;

namespace SalemBonus.Application.Pos.Clients;

/// <summary>Батырмалар мен бөлімдерді көрсету үшін — тексеріс бәрібір серверде.</summary>
public record ClientPermissionsDto(bool Edit, bool Archive, bool Export, bool Merge, bool Debts, bool Contacts, bool Settings,
    bool BonusView, bool BonusAccrue, bool BonusManage, bool BonusCampaigns, bool SalesView, bool SalesCreate, bool FinanceOperate);

public record ClientStatusOptionDto(Guid Id, string Name, string Color);

public record ClientSettingsDto(bool RequireLastName, bool RequireBirthDate, int ActiveDays, int PassiveDays, int NewDays, int DueSoonDays,
    IReadOnlyList<string> DefaultColumns, IReadOnlyList<CustomFieldDefinition> CustomFields, DateTime? UpdatedAt);

public record SaveClientSettingsRequest(bool? RequireLastName, bool? RequireBirthDate, int? ActiveDays, int? PassiveDays, int? NewDays, int? DueSoonDays,
    IReadOnlyList<string>? DefaultColumns, IReadOnlyList<CustomFieldDefinition>? CustomFields);

public record ClientMetaDto(ClientPermissionsDto Can, ClientSettingsDto Settings, IReadOnlyList<ClientStatusOptionDto> Statuses, Guid CurrentStoreId);

// ---------- Тізім ----------

public record ClientSearchRequest(string? StoreIds, ClientFilter? Filter, Guid? SegmentId, string? Sort, string? Dir, int Page = 1, int PageSize = 25);

public record ClientRowDto(Guid Id, Guid CustomerId, long Number, string Name, string FirstName, string LastName, string? MiddleName, string? Phone, string? Email,
    DateOnly? BirthDate, string Gender, string Source, Guid? StatusId, string StatusName, string StatusColor, int Purchases, decimal Amount, decimal Average,
    DateTime? FirstPurchaseAt, DateTime? LastPurchaseAt, int? DaysSinceLastPurchase, int Bonus, decimal Debt, decimal Overdue, DateTime RegisteredAt, bool HasApp,
    bool HasCard, bool IsArchived, bool IsNew, string Activity, bool ConsentApp, bool ConsentSms, bool ConsentWhatsApp, bool ConsentEmail, string? KatoCode,
    IReadOnlyDictionary<string, string> CustomFields);

public record ClientPageDto(IReadOnlyList<ClientRowDto> Rows, int Total, int Page, int PageSize);

public record ClientKpisDto(int Total, int Active, int Passive, int New, int Repeat, int WithDebt, int Overdue, int WithBonus, DateTime GeneratedAt);

public record ClientSegmentCountDto(string Key, int Count);

public record ClientMonthDto(string Month, int Count, decimal Amount);

public record ClientBirthdayDto(ClientRowDto Client, DateOnly NextBirthday, int DaysLeft, int? Age);

public record ClientOverviewDto(ClientKpisDto Kpis, IReadOnlyList<ClientSegmentCountDto> Activity, IReadOnlyList<ClientMonthDto> NewByMonth,
    IReadOnlyList<ClientRowDto> Top, IReadOnlyList<ClientRowDto> AtRisk, IReadOnlyList<ClientBirthdayDto> Birthdays, decimal DebtTotal, decimal DebtOverdue);

public record ClientLookupDto(bool Exists, bool Global, Guid? Id, long? Number, string? Name, bool Archived);

// ---------- Карточка ----------

public record ClientSaveRequest(string? Phone, string? FirstName, string? LastName, string? MiddleName, DateOnly? BirthDate, string? Gender, string? KatoCode,
    string? Address, string? Email, string? Comment, bool? ConsentApp, bool? ConsentSms, bool? ConsentWhatsApp, bool? ConsentEmail,
    Dictionary<string, string>? CustomFields);

public record ClientArchiveRequest(string? Reason, bool Force = false);

public record ClientMergeRequest(Guid PrimaryId, Guid DuplicateId);

public record ClientKatoDto(string Code, string Name);

public record ClientProductDto(Guid ProductId, string Name, decimal Quantity, decimal Amount, int Purchases);

public record ClientSaleDto(Guid Id, long Number, DateTime CreatedAt, Guid StoreId, string StoreName, decimal Total, decimal Returned, string Status, int Items,
    int BonusAccrued, int BonusRedeemed, decimal Discount, string Cashier, IReadOnlyList<string> Methods);

public record ClientActivitySummaryDto(string Segment, int? DaysSinceLastPurchase, decimal? AverageInterval, string ChurnRisk);

public record ClientBonusCardDto(bool HasCard, int Balance, Guid? StatusId, string StatusName, string StatusColor, bool OptOut);

public record ClientCardDto(ClientRowDto Client, string? Address, IReadOnlyList<ClientKatoDto> Kato, string? Comment, DateTime? ConsentUpdatedAt,
    string? CreatedBy, DateTime UpdatedAt, DateTime? ArchivedAt, string? ArchivedBy, string? ArchiveReason, Guid? MergedIntoId, long? MergedIntoNumber,
    bool CanEditPhone, ClientBonusCardDto Bonus, ClientActivitySummaryDto ActivitySummary, IReadOnlyList<ClientProductDto> TopProducts,
    IReadOnlyList<ClientSaleDto> RecentSales, int Notes);

public record ClientSalesPageDto(IReadOnlyList<ClientSaleDto> Rows, int Total);

public record ClientReceiptDto(string Kind, Guid Id, Guid SaleId, long Number, DateTime CreatedAt, string StoreName, decimal Amount, string Status,
    IReadOnlyList<string> Methods, string Staff, string? Comment);

public record ClientDocumentDto(string Kind, Guid Id, string Number, DateTime Date, decimal Amount, string Status, string StoreName, Guid? SaleId, Guid? OperationId,
    string? Description);

public record ClientCommunicationDto(Guid Id, DateTime CreatedAt, string Channel, string Text, string Status, string Staff, string StoreName);

public record ClientActivityDto(ClientActivitySummaryDto Summary, int Purchases30, int Purchases90, int Purchases365, decimal Amount30, decimal Amount90,
    decimal Amount365, DateTime? FirstPurchaseAt, DateTime? LastPurchaseAt, IReadOnlyList<ClientMonthDto> Months, IReadOnlyList<ClientCommunicationDto> Communications);

public record ClientNoteDto(Guid Id, string Text, string Category, Guid AuthorId, string Author, DateTime CreatedAt, DateTime? UpdatedAt, string? UpdatedBy, bool CanEdit);

public record SaveClientNoteRequest(string? Text, string? Category);

public record ClientChangeDto(string Field, string? Old, string? New);

public record ClientHistoryDto(Guid Id, DateTime CreatedAt, string Staff, string Action, IReadOnlyList<ClientChangeDto> Changes);

public record ClientMessageRequest(string? Channel, string? Text);

public record ClientDuplicateDto(Guid Id, long Number, string Name, string? Phone, DateOnly? BirthDate, string Reason);

// ---------- Сегменттер мен топтық әрекеттер ----------

public record ClientSegmentDto(Guid Id, string Name, string? Description, ClientFilter Filter, int Included, bool IsShared, bool IsMine, int Count,
    DateTime CreatedAt, DateTime UpdatedAt, string Author);

public record SaveClientSegmentRequest(string? Name, string? Description, ClientFilter? Filter, bool? IsShared, IReadOnlyList<Guid>? IncludeIds);

public record ClientBulkSegmentRequest(IReadOnlyList<Guid>? Ids, Guid SegmentId);

public record ClientBulkStatusRequest(IReadOnlyList<Guid>? Ids, Guid? StatusId, string? Reason);

public record ClientBulkResultDto(int Processed, int Skipped);

// ---------- Импорт пен экспорт ----------

public record ClientImportRowDto(int Row, string Status, string? Phone, string? Name, string? Message, Guid? ProfileId);

public record ClientImportPreviewDto(IReadOnlyList<string> Headers, IReadOnlyDictionary<string, int> Mapping, int Rows, int New, int Update, int Skip, int Errors,
    IReadOnlyList<ClientImportRowDto> Items);

public record ClientImportResultDto(Guid Id, string FileName, int Rows, int Created, int Updated, int Skipped, int Errors, IReadOnlyList<ClientImportRowDto> Items,
    DateTime CreatedAt);

public record ClientExportRequest(string? StoreIds, ClientFilter? Filter, Guid? SegmentId, IReadOnlyList<Guid>? Ids, string? Format, IReadOnlyList<string>? Fields);
