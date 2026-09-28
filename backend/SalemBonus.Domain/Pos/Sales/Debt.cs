namespace SalemBonus.Domain.Pos.Sales;

public enum DebtStatus
{
    Open,
    Paid,
}

/// <summary>
/// Клиент қарызы (ТЗ «Касса» §11). Тек анықталған клиентке, сатылымның «Долг» төлемінен туады.
/// Өтеулер бөлек жазба ретінде сақталады: әр өтеу — күні, сомасы, түрі, кассир, қалдығы.
/// </summary>
public class Debt
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid StoreId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid? SaleId { get; set; }
    public long? SaleNumber { get; set; }
    public decimal Amount { get; set; }
    public decimal Paid { get; set; }
    public DateOnly DueDate { get; set; }
    public string? Comment { get; set; }
    public DebtStatus Status { get; set; } = DebtStatus.Open;
    /// <summary>Рәсімдеген кассир.</summary>
    public Guid StaffUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }

    public decimal Remaining => Amount - Paid;

    public List<DebtPayment> Payments { get; set; } = [];
}

public class DebtPayment
{
    public Guid Id { get; set; }
    public Guid DebtId { get; set; }
    public decimal Amount { get; set; }
    /// <summary>Ақшамен өтеу түрі. Тауар қайтарумен жабылса — null (<see cref="IsReturn"/>).</summary>
    public PaymentMethod? Method { get; set; }
    public bool IsReturn { get; set; }
    public string? TransferRecipient { get; set; }
    public decimal RemainingAfter { get; set; }
    public Guid StaffUserId { get; set; }
    public Guid? RegisterId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Қайтару (ТЗ «Касса» §13): чектің кей не барлық позициясы. Бір чекке бірнеше қайтару болуы мүмкін,
/// бірақ әр позицияның қайтарылғаны сатылғанынан аспайды.
/// </summary>
public class SaleReturn
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid StoreId { get; set; }
    public Guid SaleId { get; set; }
    public Guid RegisterId { get; set; }
    public Guid StaffUserId { get; set; }
    /// <summary>Касса жіберген кілт: «Оформить возврат» екі рет басылса да, бір қайтару.</summary>
    public Guid ClientRequestId { get; set; }
    /// <summary>Қайтарылған тауарлардың клиент төлеген құны (ақша + қарызды азайту).</summary>
    public decimal Amount { get; set; }
    /// <summary>Қолға берілген ақша (қарызды азайтқан бөлігінсіз).</summary>
    public decimal Refunded { get; set; }
    public PaymentMethod? RefundMethod { get; set; }
    /// <summary>Сатылымның ашық қарызынан азайтылғаны.</summary>
    public decimal DebtReduced { get; set; }
    public int BonusRestored { get; set; }
    public int BonusReversed { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<SaleReturnItem> Items { get; set; } = [];
}

public class SaleReturnItem
{
    public Guid Id { get; set; }
    public Guid ReturnId { get; set; }
    public Guid SaleItemId { get; set; }
    public Guid ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal Amount { get; set; }
}
