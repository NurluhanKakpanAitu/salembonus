using SalemBonus.Domain.Pos.Sales;

namespace SalemBonus.Domain.Pos.Finance;

/// <summary>Ақша орны (ТЗ «Финансы» §9): касса, сейф, банк, Kaspi/төлем сервисі, жолдағы ақша, басқа. Қарыз — ақша шоты емес.</summary>
public enum FinanceAccountType
{
    Cash,
    Safe,
    Bank,
    Kaspi,
    InTransit,
    Other,
}

public class FinanceAccount
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid StoreId { get; set; }
    public string Name { get; set; } = string.Empty;
    public FinanceAccountType Type { get; set; }
    public string Currency { get; set; } = "KZT";
    /// <summary>Касса/сатылым төлемдері осы шотқа автоматты түседі (бір дүкенде бір әдіс — бір шот).</summary>
    public List<PaymentMethod> Methods { get; set; } = [];
    /// <summary>Проводкалар журналының кэшталған қосындысы; қайта есептеуге болады.</summary>
    public decimal Balance { get; set; }
    public decimal OpeningBalance { get; set; }
    public DateOnly? OpeningDate { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum FinanceCategoryType
{
    Income,
    Expense,
}

/// <summary>P&amp;L-дағы орны (ТЗ §5, §15.1).</summary>
public enum PnlClass
{
    OperatingExpense,
    OtherIncome,
    OtherExpense,
    Tax,
    /// <summary>P&amp;L-ға кірмейді (мысалы, иесінің жеке алымы, несие денесі).</summary>
    NotInPnl,
}

public class FinanceCategory
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public FinanceCategoryType Type { get; set; }
    public PnlClass PnlClass { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Операция түрі. Бастапқы модульден автоматты түсетіндер: SalePayment, Refund, CustomerPayment, SupplierPayment.
/// Қолмен: Income, Expense, Transfer, Collection (инкассация), Adjustment (қалдық түзету — тек арнайы құқықпен).
/// </summary>
public enum FinanceOperationType
{
    Income,
    Expense,
    Transfer,
    Collection,
    Adjustment,
    SalePayment,
    Refund,
    CustomerPayment,
    SupplierPayment,
}

public enum FinanceOperationStatus
{
    Draft,
    Posted,
    Reversed,
}

public enum CounterpartyType
{
    Customer,
    Supplier,
    Staff,
    Other,
}

/// <summary>
/// Қаржы операциясы (ТЗ §16): бір операция — бір не бірнеше проводка, атомарлы. Проведённый операция өшірілмейді —
/// тек сторно (кері жазба + түпнұсқада Reversed). SourceOperationId — бастапқы құжат бойынша қайталанбау кілті.
/// </summary>
public class FinanceOperation
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid StoreId { get; set; }
    public string Number { get; set; } = string.Empty;
    public long Seq { get; set; }
    public FinanceOperationType Type { get; set; }
    public FinanceOperationStatus Status { get; set; } = FinanceOperationStatus.Draft;
    /// <summary>Операция күні (UTC); PostedAt — проведение сәті.</summary>
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public decimal Amount { get; set; }
    public Guid? CategoryId { get; set; }
    public string Description { get; set; } = string.Empty;
    public CounterpartyType? CounterpartyType { get; set; }
    public Guid? CounterpartyId { get; set; }
    public string? CounterpartyName { get; set; }
    public Guid? FromAccountId { get; set; }
    public Guid? ToAccountId { get; set; }
    /// <summary>Төлем әдісі (автоматты операцияларда — чектегі әдіс).</summary>
    public PaymentMethod? Method { get; set; }
    public string? DocumentNumber { get; set; }
    public string? AttachmentUrl { get; set; }
    public string? Comment { get; set; }
    public string? SourceModule { get; set; }
    public Guid? SourceDocumentId { get; set; }
    public Guid? SourceOperationId { get; set; }
    public Guid? ClientRequestId { get; set; }
    /// <summary>Сторно жазбасы болса — түпнұсқа операция.</summary>
    public Guid? ReversalOfId { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PostedAt { get; set; }
    public Guid? PostedBy { get; set; }
    public DateTime? ReversedAt { get; set; }
    public Guid? ReversedBy { get; set; }
    public string? ReversalReason { get; set; }

    public List<FinanceEntry> Entries { get; set; } = [];

    public bool IsInternal => Type is FinanceOperationType.Transfer or FinanceOperationType.Collection;
    public bool IsManual => Type is FinanceOperationType.Income or FinanceOperationType.Expense;
}

/// <summary>Проводка: шот, сома (+ кіріс, − шығыс), осыдан кейінгі қалдық.</summary>
public class FinanceEntry
{
    public Guid Id { get; set; }
    public Guid OperationId { get; set; }
    public Guid AccountId { get; set; }
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public DateTime Date { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
