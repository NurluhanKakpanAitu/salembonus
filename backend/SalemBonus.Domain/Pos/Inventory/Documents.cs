using SalemBonus.Domain.Pos.Catalog;
using SalemBonus.Domain.Pos.Sales;

namespace SalemBonus.Domain.Pos.Inventory;

/// <summary>Контрагент-жеткізуші (ТЗ «Склад» §12). Бизнеске ортақ: бір жеткізуші барлық дүкенге жеткізеді.</summary>
public enum SupplierType
{
    Company,
    Entrepreneur,
    Person,
}

public class Supplier
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public SupplierType Type { get; set; } = SupplierType.Company;
    /// <summary>БИН / ИИН.</summary>
    public string? TaxId { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Comment { get; set; }
    public CatalogStatus Status { get; set; } = CatalogStatus.Active;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum StockDocumentType
{
    /// <summary>Приёмка: жеткізушіден кіріс.</summary>
    Receipt,
    /// <summary>Списание: себеппен шығыс.</summary>
    WriteOff,
}

public enum StockDocumentStatus
{
    /// <summary>Черновик: қалдыққа әсер етпейді.</summary>
    Draft,
    /// <summary>Проведён: қозғалыстар жазылды, қалдық өзгерді (бір-ақ рет).</summary>
    Posted,
    /// <summary>Черновик жойылды (физикалық өшірілмейді, тарих қалады).</summary>
    Cancelled,
    /// <summary>Проведённый құжат сторно құжатымен жойылды: қозғалыстар кері қайтарылды.</summary>
    Reversed,
}

/// <summary>Списание себептері (ТЗ §11). «Другое» — түсініктеме міндетті.</summary>
public enum WriteOffReason
{
    Damage,
    Expired,
    Loss,
    InternalUse,
    Adjustment,
    Other,
}

/// <summary>
/// Қойма құжаты: приёмка немесе списание (ТЗ «Склад» §7, §11, §17). Нөмірі бизнес ішінде сквозной
/// («ПР-000123», «СП-000045»), сервер береді. Черновик қалдықты өзгертпейді; проведение қозғалыстарды
/// бір транзакцияда, бір-ақ рет жазады. Проведённый құжат өшірілмейді — тек сторно.
/// Жолдардағы баға мен атау құжат сәтіндегі күйінде сақталады.
/// </summary>
public class StockDocument
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid StoreId { get; set; }
    public Guid WarehouseId { get; set; }
    public StockDocumentType Type { get; set; }
    public string Number { get; set; } = string.Empty;
    public long Seq { get; set; }
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public StockDocumentStatus Status { get; set; } = StockDocumentStatus.Draft;

    // Приёмка
    public Guid? SupplierId { get; set; }
    public string? SupplierInvoiceNumber { get; set; }
    /// <summary>Негіздеме: шарт, шот-фактура т.б.</summary>
    public string? Basis { get; set; }
    /// <summary>«Быстрая приёмка» — реквизитсіз қысқа форма (ТЗ §7.5).</summary>
    public bool IsQuick { get; set; }

    // Списание
    public WriteOffReason? Reason { get; set; }

    public string? AttachmentUrl { get; set; }
    public string? Comment { get; set; }

    /// <summary>Құжат сомасы — закупочная бағамен (ТЗ §7.5, §13).</summary>
    public decimal Total { get; set; }
    /// <summary>Тіркелген төлемдер (тек приёмка). Долг = Total − Paid.</summary>
    public decimal Paid { get; set; }
    /// <summary>Жеткізушіге төлеу мерзімі (ТЗ «Финансы» §12): өтсе — просрочка; бос — мерзімсіз.</summary>
    public DateOnly? PaymentDueDate { get; set; }

    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PostedAt { get; set; }
    public Guid? PostedBy { get; set; }
    public DateTime? ReversedAt { get; set; }
    public Guid? ReversedBy { get; set; }
    public string? ReversalReason { get; set; }

    public decimal Remaining => Total - Paid;

    public List<StockDocumentLine> Lines { get; set; } = [];
}

public class StockDocumentLine
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public Guid ProductId { get; set; }
    /// <summary>Атау мен артикул құжат сәтіндегі күйінде — тауар кейін өзгерсе де, құжат өзгермейді.</summary>
    public string Name { get; set; } = string.Empty;
    public string? Article { get; set; }
    public decimal Quantity { get; set; }
    /// <summary>Бір бірліктің закупочная бағасы (приёмка) не өзіндік құны (списание, анықтама үшін).</summary>
    public decimal PurchasePrice { get; set; }
    /// <summary>Приёмкада қойылған розничная баға (дүкенге жазылады); бос — өзгермейді.</summary>
    public decimal? RetailPrice { get; set; }
    public decimal LineTotal { get; set; }
    public int SortOrder { get; set; }
}

public enum TransferStatus
{
    Draft,
    /// <summary>Жіберілді: жіберуші қоймадан алынды, алушы әлі растамады (ТЗ §10.3).</summary>
    InTransit,
    Completed,
    Cancelled,
}

/// <summary>
/// Перемещение (ТЗ «Склад» §10): бір бизнестің қоймалары не дүкендері арасында. Жіберу мен қабылдау —
/// бөлек қадамдар: жіберуде шығыс, растауда нақты қабылданған санға кіріс. Айырмашылық жолда сақталады.
/// </summary>
public class StockTransfer
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Number { get; set; } = string.Empty;
    public long Seq { get; set; }
    public Guid SourceStoreId { get; set; }
    public Guid SourceWarehouseId { get; set; }
    public Guid DestinationStoreId { get; set; }
    public Guid DestinationWarehouseId { get; set; }
    public TransferStatus Status { get; set; } = TransferStatus.Draft;
    public string? Comment { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
    public Guid? SentBy { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public Guid? ReceivedBy { get; set; }

    public List<StockTransferLine> Lines { get; set; } = [];
}

public class StockTransferLine
{
    public Guid Id { get; set; }
    public Guid TransferId { get; set; }
    public Guid ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Article { get; set; }
    public decimal QuantitySent { get; set; }
    /// <summary>Алушы растаған нақты сан; бос — әлі қабылданбаған.</summary>
    public decimal? QuantityReceived { get; set; }
    public string? DiscrepancyReason { get; set; }
    public int SortOrder { get; set; }
}

public enum SupplierPaymentStatus
{
    Registered,
    Reversed,
}

/// <summary>
/// Жеткізушіге төлем (ТЗ «Склад» §13). Приёмка мен төлем — бөлек оқиғалар: приёмка міндеттеме жасайды,
/// төлем оны азайтады. Өзгерту/жою — тек сторно жазбасымен. Банк интеграциясы жоқ: тек есептік жазба.
/// </summary>
public class SupplierPayment
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid StoreId { get; set; }
    public Guid SupplierId { get; set; }
    /// <summary>Қай приёмкаға; бос — жалпы қарызға.</summary>
    public Guid? DocumentId { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    /// <summary>Төлем тапсырмасының нөмірі т.б.</summary>
    public string? Reference { get; set; }
    public DateOnly Date { get; set; }
    public string? Comment { get; set; }
    public SupplierPaymentStatus Status { get; set; } = SupplierPaymentStatus.Registered;
    /// <summary>Қайталап жіберуден қорғайтын кілт (идемпотенттілік).</summary>
    public Guid? ClientRequestId { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReversedAt { get; set; }
    public Guid? ReversedBy { get; set; }
    public string? ReversalReason { get; set; }
}

/// <summary>Құжат нөмірлерінің санағышы: бизнес + түр («receipt», «writeoff», «transfer»).</summary>
public class DocumentCounter
{
    public Guid OrganizationId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public long LastNumber { get; set; }
}
