namespace SalemBonus.Domain.Pos.Catalog;

public enum ProductType
{
    /// <summary>Кәдімгі тауар: қалдығы есептеледі.</summary>
    Goods,
    /// <summary>Қызмет: қалдығы жоқ.</summary>
    Service,
}

/// <summary>
/// Тауар — каталогтың объектісі (ТЗ «Товар» §1). Қалдық, қозғалыс және қоймалық бағалар мұнда
/// емес, «Склад» контурында. Санаты өшірілсе де тауар жойылмайды — ол бөлек объект.
/// Каталог бүкіл бизнеске ортақ: бір тауар барлық филиалда бір рет енгізіледі.
/// </summary>
public class Product
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Article { get; set; }
    public Guid UnitId { get; set; }
    public Guid? BrandId { get; set; }
    public Guid? CatalogNodeId { get; set; }
    public ProductType Type { get; set; } = ProductType.Goods;
    public CatalogStatus Status { get; set; } = CatalogStatus.Active;
    public string? Description { get; set; }

    // «Дополнительно» (ТЗ §6.6). Жеткізуші мен өндіруші әзірше мәтін: анықтамалықтары кейін.
    public string? Supplier { get; set; }
    public string? Manufacturer { get; set; }
    public string? Country { get; set; }
    public int? WarrantyMonths { get; set; }
    public int? ShelfLifeDays { get; set; }
    /// <summary>ҚҚС мөлшерлемесі (%). Бос — ҚҚС-сыз.</summary>
    public decimal? VatRate { get; set; }
    public bool IsMarked { get; set; }
    public bool IsRecommended { get; set; }
    public bool HideInCashier { get; set; }
    /// <summary>Бонус жүйесіне қатыса ма (есептеу мен шегеру).</summary>
    public bool BonusEligible { get; set; } = true;
    /// <summary>Бұл тауарға берілетін ең үлкен жеңілдік (%). Бос — дүкеннің жалпы ережесі.</summary>
    public decimal? MaxDiscountPercent { get; set; }
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<ProductBarcode> Barcodes { get; set; } = [];
    public List<ProductImage> Images { get; set; } = [];
    public List<ProductCharacteristicValue> Characteristics { get; set; } = [];
}

/// <summary>Штрихкод. Бір бизнесте бір штрихкод тек бір тауарға тиесілі.</summary>
public class ProductBarcode
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid OrganizationId { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public string? Description { get; set; }
}

public class ProductImage
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string Url { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public int SortOrder { get; set; }
}

public class ProductCharacteristicValue
{
    public Guid ProductId { get; set; }
    public Guid DefinitionId { get; set; }
    public string Value { get; set; } = string.Empty;
}
