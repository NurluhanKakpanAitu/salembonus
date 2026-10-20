namespace SalemBonus.Application.Pos.Finance;

// ---------- Шоттар (§9) ----------

public record FinanceAccountDto(
    Guid Id, Guid StoreId, string StoreName, string Name, string Type, string Currency, IReadOnlyList<string> Methods,
    decimal Balance, decimal OpeningBalance, DateOnly? OpeningDate, bool IsActive, int SortOrder, string? Comment, bool HasEntries, DateTime CreatedAt);

public record SaveAccountRequest(string? Name, string? Type, IReadOnlyList<string>? Methods, string? Comment, int? SortOrder,
    /// <summary>Тек жаңа шотқа не проводкасы жоқ шотқа (ТЗ §9).</summary>
    decimal? OpeningBalance, DateOnly? OpeningDate);

public record AccountsSummaryDto(decimal Total, decimal Cash, decimal NonCash, decimal InTransit, IReadOnlyList<FinanceAccountDto> Accounts);

// ---------- Санаттар (§15.1) ----------

public record FinanceCategoryDto(Guid Id, string Name, string Type, string PnlClass, bool IsActive, int SortOrder, DateTime CreatedAt);

public record SaveCategoryRequest(string? Name, string? Type, string? PnlClass, int? SortOrder);

// ---------- Операциялар (§6–8, §13) ----------

public record FinanceEntryDto(Guid AccountId, string AccountName, decimal Amount, decimal BalanceAfter);

public record FinanceOperationDto(
    Guid Id, string Number, string Type, string Status, DateTime Date, decimal Amount, Guid StoreId, string StoreName,
    Guid? CategoryId, string? CategoryName, string Description, string? CounterpartyType, Guid? CounterpartyId, string? CounterpartyName,
    Guid? FromAccountId, string? FromAccountName, Guid? ToAccountId, string? ToAccountName, string? Method, string? DocumentNumber,
    string? AttachmentUrl, string? Comment, string? SourceModule, Guid? SourceDocumentId, string? SourceLink, Guid? ReversalOfId, Guid? ReversedById,
    string CreatedByName, DateTime CreatedAt, DateTime? PostedAt, string? PostedByName, DateTime? ReversedAt, string? ReversedByName, string? ReversalReason,
    IReadOnlyList<FinanceEntryDto> Entries, bool CanReverse);

public record FinanceOperationsPageDto(IReadOnlyList<FinanceOperationDto> Items, int Total, int Page, int PageSize,
    decimal Inflow, decimal Outflow, decimal Transfers, int Count);

/// <summary>Сүзгілер (ТЗ §6, §8). Кезең — дүкеннің жергілікті күндері, екі шеті қоса. Types — «Income,Expense».</summary>
public class FinanceListQuery
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public string? StoreIds { get; set; }
    public Guid? AccountId { get; set; }
    public string? Type { get; set; }
    public string? Types { get; set; }
    public Guid? CategoryId { get; set; }
    public string? Source { get; set; }
    public string? CounterpartyType { get; set; }
    public Guid? CounterpartyId { get; set; }
    public string? Status { get; set; }
    public Guid? StaffUserId { get; set; }
    public string? Search { get; set; }
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
    public bool ManualOnly { get; set; }
    public string? Sort { get; set; }
    public string? Dir { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 15;
}

/// <summary>Қолмен операция (§7): кіріс/шығыс. Draft — черновик, Post — бірден проведение.</summary>
public record SaveOperationRequest(
    string? Type, DateTime? Date, Guid? CategoryId, string? Description, decimal Amount, Guid? AccountId,
    string? CounterpartyType, Guid? CounterpartyId, string? CounterpartyName, string? DocumentNumber, string? AttachmentUrl, string? Comment,
    Guid? ClientRequestId, bool Post);

/// <summary>Шоттар арасы аударым (§9.1) не инкассация (§14): екі проводка, P&amp;L-ға әсер етпейді.</summary>
public record TransferRequest(Guid? FromAccountId, Guid? ToAccountId, decimal Amount, DateTime? Date, string? Comment, string? DocumentNumber,
    string? AttachmentUrl, bool Collection, Guid? ClientRequestId);

/// <summary>Қалдық түзетуі (§9): тек арнайы құқықпен, себебі міндетті.</summary>
public record AdjustmentRequest(Guid? AccountId, decimal Amount, DateTime? Date, string? Reason, string? DocumentNumber, Guid? ClientRequestId);

public record ReverseOperationRequest(string? Reason);

public record StatementRowDto(DateTime Date, Guid OperationId, string Number, string Type, string Description, string? CounterpartyName,
    decimal Amount, decimal BalanceAfter, string? DocumentNumber);

public record AccountStatementDto(FinanceAccountDto Account, DateOnly From, DateOnly To, decimal Opening, decimal Inflow, decimal Outflow, decimal Closing,
    IReadOnlyList<StatementRowDto> Rows);

// ---------- Обзор (§4) ----------

public record FinanceKpiDto(string Key, decimal Value, decimal? Previous, decimal? ChangePercent, bool Available = true, string? Note = null);

public record FinanceDayDto(DateOnly Date, decimal Inflow, decimal Outflow, decimal Balance);

public record ExpenseShareDto(string Key, string Label, decimal Amount, decimal Percent);

public record UpcomingPaymentDto(string Kind, Guid Id, string Counterparty, decimal Amount, DateOnly? DueDate, int? DaysLeft, string Status);

public record DebtsSummaryDto(decimal CustomerDebt, decimal CustomerOverdue, int CustomerCount, decimal SupplierDebt, decimal SupplierOverdue, int SupplierDocuments);

public record FinanceOverviewDto(
    DateOnly From, DateOnly To, DateTime GeneratedAt, IReadOnlyList<FinanceKpiDto> Kpis, IReadOnlyList<FinanceDayDto> Days,
    IReadOnlyList<FinanceAccountDto> Accounts, IReadOnlyList<ExpenseShareDto> Expenses, IReadOnlyList<FinanceOperationDto> Recent,
    IReadOnlyList<UpcomingPaymentDto> Upcoming, DebtsSummaryDto Debts);

// ---------- P&L (§5) ----------

public record PnlRowDto(string Key, int Level, string? Label, decimal Amount, decimal? PercentOfRevenue, decimal? Previous, decimal? Change, decimal? ChangePercent,
    bool IsTotal, string? Link, Guid? CategoryId);

public record PnlDto(DateOnly From, DateOnly To, DateOnly? PrevFrom, DateOnly? PrevTo, bool CostComplete, bool TaxesTracked, IReadOnlyList<PnlRowDto> Rows,
    IReadOnlyList<string> Notes);

// ---------- Cash Flow (§6) ----------

public record CashFlowDto(DateOnly From, DateOnly To, decimal Opening, decimal Inflow, decimal Outflow, decimal Transfers, decimal Adjustments, decimal Closing,
    IReadOnlyList<FinanceDayDto> Days, IReadOnlyList<ExpenseShareDto> InflowByType, IReadOnlyList<ExpenseShareDto> OutflowByType);

// ---------- Тауар пайдасы (§10) ----------

public record ProductProfitRowDto(Guid ProductId, string Name, string? Article, string? Barcode, string? Category, string? Brand, string? Unit,
    decimal Sold, decimal Returned, decimal NetQty, decimal NetRevenue, decimal? Cost, decimal? Profit, decimal? Margin, decimal? AveragePrice, bool CostComplete);

public record ProductProfitDto(int Total, int Page, int PageSize, decimal NetRevenue, decimal? Cost, decimal? Profit, decimal? Margin, bool CostComplete,
    int WithoutCost, IReadOnlyList<ProductProfitRowDto> Rows);

// ---------- Клиент қарызы (§11) ----------

public record CustomerDebtRowDto(Guid CustomerId, string Name, string? Phone, decimal Total, decimal Overdue, DateOnly? NextDue, int Documents, string Status);

public record CustomerDebtDocDto(Guid DebtId, Guid? SaleId, long? SaleNumber, DateTime CreatedAt, DateOnly DueDate, decimal Amount, decimal Paid, decimal Remaining,
    string Status, bool Overdue, string? Comment);

public record CustomerDebtPaymentDto(Guid Id, DateTime CreatedAt, Guid DebtId, long? SaleNumber, decimal Amount, string? Method, bool IsReturn, decimal RemainingAfter,
    string StaffName, Guid? OperationId, string? OperationNumber);

public record CustomerDebtsDto(int Customers, decimal Total, decimal Overdue, decimal DueSoon, IReadOnlyList<CustomerDebtRowDto> Rows, int RowsTotal);

public record CustomerDebtCardDto(Guid CustomerId, string Name, string? Phone, decimal Total, decimal Overdue, DateOnly? NextDue,
    IReadOnlyList<CustomerDebtDocDto> Documents, IReadOnlyList<CustomerDebtPaymentDto> Payments);

public record DebtAllocationRequest(Guid DebtId, decimal Amount);

public record CustomerPaymentRequest(Guid? CustomerId, Guid? AccountId, string? Method, DateTime? Date, string? Comment, string? DocumentNumber,
    IReadOnlyList<DebtAllocationRequest>? Allocations, Guid? ClientRequestId);

// ---------- Жеткізуші қарызы (§12) ----------

public record SupplierDebtRowDto(Guid SupplierId, string Name, string? Phone, DateTime? LastReceipt, decimal Purchases, decimal Paid, decimal Debt, decimal Overdue,
    DateOnly? NextDue, int Documents, string Status);

public record SupplierDebtDocDto(Guid DocumentId, string Number, DateTime Date, string? InvoiceNumber, decimal Total, decimal Paid, decimal Remaining,
    DateOnly? DueDate, bool Overdue, int Lines);

public record SupplierPaymentRowDto(Guid Id, DateTime CreatedAt, DateOnly Date, Guid? DocumentId, string? DocumentNumber, decimal Amount, string Method,
    string? Reference, string Status, string StaffName, Guid? OperationId, string? OperationNumber, string? AccountName);

public record SupplierDebtsDto(decimal Total, decimal Overdue, decimal DueSoon, int Documents, int DocumentsAll, IReadOnlyList<SupplierDebtRowDto> Rows, int RowsTotal);

public record SupplierDebtCardDto(Guid SupplierId, string Name, string? Phone, string? TaxId, string? Address, decimal Debt, decimal Overdue, int OpenDocuments,
    DateOnly? NextDue, IReadOnlyList<SupplierDebtDocDto> Documents, IReadOnlyList<SupplierPaymentRowDto> Payments);

public record DocumentAllocationRequest(Guid DocumentId, decimal Amount);

public record SupplierPaymentRequest(Guid? SupplierId, Guid? AccountId, string? Method, DateTime? Date, string? Reference, string? Comment, string? AttachmentUrl,
    IReadOnlyList<DocumentAllocationRequest>? Allocations, Guid? ClientRequestId);

public record SetDueDateRequest(DateOnly? DueDate);

/// <summary>Салыстыру актісі (§12.1): кезеңдегі кірістер мен төлемдер, басы/соңындағы қалдық.</summary>
public record ReconciliationRowDto(DateTime Date, string Kind, string Number, string? Description, decimal Debit, decimal Credit, decimal Balance);

public record ReconciliationDto(string Counterparty, string? TaxId, DateOnly From, DateOnly To, decimal Opening, decimal Debit, decimal Credit, decimal Closing,
    IReadOnlyList<ReconciliationRowDto> Rows, DateTime GeneratedAt, string StoreName);

// ---------- Көмекші ----------

public record FinanceMetaDto(IReadOnlyList<FinanceAccountDto> Accounts, IReadOnlyList<FinanceCategoryDto> Categories,
    bool CanOperate, bool CanReverse, bool CanManage, bool CanContacts);

public record BackfillResultDto(int Posted, int Skipped);
