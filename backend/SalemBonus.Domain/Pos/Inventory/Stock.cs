namespace SalemBonus.Domain.Pos.Inventory;

public enum StockMovementType
{
    /// <summary>Тауар жасалғанда енгізілген бастапқы қалдық.</summary>
    Opening,
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
    public Guid? StaffUserId { get; set; }
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
