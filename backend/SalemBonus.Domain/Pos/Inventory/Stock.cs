namespace SalemBonus.Domain.Pos.Inventory;

public enum StockMovementType
{
    /// <summary>Тауар жасалғанда енгізілген бастапқы қалдық.</summary>
    Opening,
    /// <summary>Кассадағы сатылым (теріс).</summary>
    Sale,
    /// <summary>Сатылымды қайтару (оң).</summary>
    Return,
    /// <summary>Приёмка: жеткізушіден кіріс (оң).</summary>
    Receipt,
    /// <summary>Списание (теріс).</summary>
    WriteOff,
    /// <summary>Перемещение: жіберуші қоймадан шығыс (теріс).</summary>
    TransferOut,
    /// <summary>Перемещение: алушы қоймаға кіріс (оң).</summary>
    TransferIn,
    /// <summary>Инвентаризациялық түзету.</summary>
    Adjustment,
    /// <summary>Сторно: проведённый құжаттың қозғалысын кері қайтару.</summary>
    Reversal,
}

/// <summary>
/// Қалдық қозғалысы — өзгермейтін журнал. Қалдық тек осы жазбалар арқылы өзгереді,
/// сондықтан кез келген сәттегі санды қайта есептеуге болады.
/// </summary>
public class StockMovement
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid ProductId { get; set; }
    public StockMovementType Type { get; set; }
    /// <summary>Оң — кіріс, теріс — шығыс.</summary>
    public decimal Quantity { get; set; }
    /// <summary>Бір бірліктің өзіндік құны (кіріс кезінде).</summary>
    public decimal? UnitCost { get; set; }
    /// <summary>Осы қозғалыстан кейінгі қалдық (тауар + қойма) — журналда бірден көрінеді (ТЗ «Склад» §14).</summary>
    public decimal BalanceAfter { get; set; }
    public Guid? StaffUserId { get; set; }
    /// <summary>Құжат: сатылым, қайтару, приёмка, списание, перемещение.</summary>
    public Guid? DocumentId { get; set; }
    /// <summary>Құжат нөмірі журналда көрсету үшін («Чек №12», «ПР-000012»).</summary>
    public string? DocumentNumber { get; set; }
    /// <summary>Себеп/түсініктеме: списание, түзету, сторно.</summary>
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Қоймадағы ағымдағы қалдық: касса мен тізімдер журналды әр жолы қоспау үшін.</summary>
public class StockBalance
{
    public Guid WarehouseId { get; set; }
    public Guid ProductId { get; set; }
    public decimal Quantity { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Тауардың дүкендегі бағасы. Каталог бизнеске ортақ, ал баға әр филиалда әртүрлі болуы мүмкін.
/// </summary>
public class ProductPrice
{
    public Guid ProductId { get; set; }
    public Guid StoreId { get; set; }
    public decimal SalePrice { get; set; }
    /// <summary>Соңғы кіріс бағасы (пайданы есептеу үшін).</summary>
    public decimal? PurchasePrice { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
