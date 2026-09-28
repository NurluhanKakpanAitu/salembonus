namespace SalemBonus.Application.Pos.Catalog;

/// <summary>«Список товаров» жолы (ТЗ «Товар» §5.1): қалдық пен баға мұнда жоқ — олар «Склад»-та.</summary>
public record ProductListItemDto(
    Guid Id,
    string Name,
    string? Article,
    string? Barcode,
    string? ImageUrl,
    Guid? NodeId,
    IReadOnlyList<string> PathNames,
    Guid? BrandId,
    string? BrandName,
    Guid UnitId,
    string? UnitShortName,
    string Status,
    DateTime UpdatedAt);

public record ProductPageDto(IReadOnlyList<ProductListItemDto> Items, int Total, int Page, int PageSize);

public record ProductBarcodeDto(string Barcode, bool IsPrimary);

public record ProductImageDto(string Url, bool IsPrimary);

public record ProductCharacteristicDto(Guid DefinitionId, string Value);

public record ProductStockDto(Guid WarehouseId, string WarehouseName, string StoreName, decimal Quantity);

/// <summary>Тауар карточкасы. Бағалар — ағымдағы дүкендікі; кіріс бағасы тек құқығы барға.</summary>
public record ProductDto(
    Guid Id,
    string Name,
    string? Article,
    Guid UnitId,
    Guid? BrandId,
    Guid? NodeId,
    IReadOnlyList<string> PathNames,
    string Status,
    string? Description,
    string? Supplier,
    string? Manufacturer,
    string? Country,
    int? WarrantyMonths,
    int? ShelfLifeDays,
    decimal? VatRate,
    bool IsMarked,
    string? Notes,
    IReadOnlyList<ProductBarcodeDto> Barcodes,
    IReadOnlyList<ProductImageDto> Images,
    IReadOnlyList<ProductCharacteristicDto> Characteristics,
    IReadOnlyList<ProductStockDto> Stock,
    decimal? SalePrice,
    decimal? PurchasePrice,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>Бастапқы қалдық пен баға — тек тауар жасалғанда (ТЗ §6.2). Кейінгі өзгерістер «Склад» арқылы.</summary>
public record OpeningStockRequest(Guid? WarehouseId, decimal? Quantity, decimal? PurchasePrice, decimal? SalePrice);

public record SaveProductRequest(
    string? Name,
    string? Article,
    Guid? UnitId,
    Guid? BrandId,
    Guid? NodeId,
    string? Description,
    string? Status,
    string? Supplier,
    string? Manufacturer,
    string? Country,
    int? WarrantyMonths,
    int? ShelfLifeDays,
    decimal? VatRate,
    bool IsMarked,
    string? Notes,
    IReadOnlyList<ProductBarcodeDto>? Barcodes,
    IReadOnlyList<ProductImageDto>? Images,
    IReadOnlyList<ProductCharacteristicDto>? Characteristics,
    OpeningStockRequest? Opening,
    /// <summary>
    /// Тек өзгертуде: ағымдағы дүкеннің сату бағасы. «Склад» модулі жасалғанша баға карточкада
    /// өзгертіледі; қалдық пен кіріс бағасы — тек «Склад» арқылы. null — өзгермейді.
    /// </summary>
    decimal? SalePrice = null);

public record ChangeClassificationRequest(Guid? NodeId);

public record BarcodeCheckDto(bool Available, Guid? ProductId, string? ProductName);

public record GeneratedBarcodeDto(string Barcode);

public record WarehouseDto(Guid Id, string Name, bool IsDefault);

public record UploadRequest(string? Kind, string? ContentType, long Size);

public record UploadDto(string UploadUrl, string PublicUrl, DateTime ExpiresAt);
