namespace SalemBonus.Domain.Pos.Sales;

public enum PaymentMethod
{
    Cash,
    Card,
    Qr,
    Transfer,
    Debt,
}

public enum SaleStatus
{
    Completed,
    PartiallyReturned,
    Returned,
}

public enum DiscountKind
{
    Percent,
    Amount,
}

/// <summary>
/// Сатылым (чек). Нөмірін сервер береді: дүкен ішінде сквозной, кассир өзгерте алмайды (ТЗ «Касса» §1.4, §12.5).
/// Бағалар мен атаулар сатылған сәттегі күйінде сақталады — кейін тауар өзгерсе де, чек өзгермейді.
///
/// Сомалар: <see cref="Subtotal"/> − <see cref="DiscountAmount"/> − <see cref="BonusRedeemed"/> = <see cref="Total"/>,
/// ал <see cref="Total"/> төлемдердің қосындысына тең.
/// </summary>
public class Sale
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid StoreId { get; set; }
    public Guid RegisterId { get; set; }
    /// <summary>Сатқан кассир.</summary>
    public Guid StaffUserId { get; set; }
    public long Number { get; set; }
    /// <summary>Касса жіберген кілт: «Оплатить» екі рет басылса, екінші сатылым жасалмайды.</summary>
    public Guid ClientRequestId { get; set; }

    public Guid? CustomerId { get; set; }
    public Guid? BonusCardId { get; set; }

    public decimal Subtotal { get; set; }
    public DiscountKind? DiscountKind { get; set; }
    public decimal? DiscountValue { get; set; }
    public decimal DiscountAmount { get; set; }
    /// <summary>Жеңілдік кассир шегінен асса — растаған қызметкер.</summary>
    public Guid? DiscountApprovedBy { get; set; }
    public int BonusRedeemed { get; set; }
    public int BonusAccrued { get; set; }
    public decimal Total { get; set; }

    public SaleStatus Status { get; set; } = SaleStatus.Completed;
    public decimal ReturnedAmount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<SaleItem> Items { get; set; } = [];
    public List<SalePayment> Payments { get; set; } = [];
}

public class SaleItem
{
    public Guid Id { get; set; }
    public Guid SaleId { get; set; }
    public Guid ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Article { get; set; }
    public string? UnitShortName { get; set; }
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    /// <summary>Баға × саны.</summary>
    public decimal LineTotal { get; set; }
    /// <summary>
    /// Чек жеңілдігі мен бонустың осы жолға тиесілі үлесі. Қайтарғанда клиентке нақты төлегені
    /// қайтарылсын деп сақталады.
    /// </summary>
    public decimal Discount { get; set; }
    public decimal ReturnedQuantity { get; set; }
    public int SortOrder { get; set; }
}

public class SalePayment
{
    public Guid Id { get; set; }
    public Guid SaleId { get; set; }
    public PaymentMethod Method { get; set; }
    public decimal Amount { get; set; }
    /// <summary>Қолма-қол: клиент берген сома және қайтарым.</summary>
    public decimal? Received { get; set; }
    public decimal? Change { get; set; }
    public Guid? TransferRecipientId { get; set; }
    /// <summary>Аударым алушысының сатылым сәтіндегі мәтіні: «Kaspi Bank · +7 777 … · Максатбек».</summary>
    public string? TransferRecipient { get; set; }
}

/// <summary>Дүкеннің чек нөмірлегіші: бір жол, жаңа сатылымда блокпен +1 (параллель сатылымда да қайталанбайды).</summary>
public class ReceiptCounter
{
    public Guid StoreId { get; set; }
    public long LastNumber { get; set; }
}

/// <summary>Аударым реквизиті (ТЗ «Касса» §9.4): банк, нөмір, алушы.</summary>
public class TransferRecipient
{
    public Guid Id { get; set; }
    public Guid StoreId { get; set; }
    public string BankName { get; set; } = string.Empty;
    public string Account { get; set; } = string.Empty;
    public string HolderName { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string Display => $"{BankName} · {Account} · {HolderName}";
}

/// <summary>
/// Касса баптаулары дүкен деңгейінде (ТЗ «Касса» §17). Жолы жоқ болса — әдепкі: бәрі қосулы.
/// Бонус пайыздары мұнда жоқ — олар SalemBonus-та.
/// </summary>
public class StoreCashierSettings
{
    public static readonly PaymentMethod[] AllMethods =
        [PaymentMethod.Cash, PaymentMethod.Card, PaymentMethod.Qr, PaymentMethod.Transfer, PaymentMethod.Debt];

    public Guid StoreId { get; set; }
    public List<PaymentMethod> EnabledMethods { get; set; } = [.. AllMethods];
    public bool MixedEnabled { get; set; } = true;
    public bool AutoPrint { get; set; }
    public bool ElectronicReceipt { get; set; } = true;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
