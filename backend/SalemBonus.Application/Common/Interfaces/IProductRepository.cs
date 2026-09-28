using SalemBonus.Domain.Pos.Catalog;

namespace SalemBonus.Application.Common.Interfaces;

/// <summary>Тауар тізімінің сүзгісі. <see cref="NodePath"/> берілсе — сол түйін мен оның барлық ұрпағындағы тауарлар.</summary>
public record ProductFilter(string? Search, CatalogStatus? Status, string? NodePath, Guid? BrandId, Guid? UnitId, int Skip, int Take);

public record BarcodeOwner(string Barcode, Guid ProductId, string ProductName);

public interface IProductRepository
{
    Task<(IReadOnlyList<Product> Items, int Total)> SearchAsync(Guid orgId, ProductFilter filter, CancellationToken ct = default);
    /// <summary>Экспорт: сүзгі бойынша барлығы (Skip/Take ескерілмейді), штрихкод пен сипаттамаларымен.</summary>
    Task<IReadOnlyList<Product>> ListForExportAsync(Guid orgId, ProductFilter filter, CancellationToken ct = default);
    /// <summary>Импорт: бизнестің барлық тауары, өзгерту үшін (штрихкод пен сипаттамаларымен).</summary>
    Task<IReadOnlyList<Product>> ListAllForUpdateAsync(Guid orgId, CancellationToken ct = default);
    Task<Product?> GetAsync(Guid orgId, Guid id, CancellationToken ct = default);
    Task<Product?> GetForUpdateAsync(Guid orgId, Guid id, CancellationToken ct = default);
    Task<Product?> FindByBarcodeAsync(Guid orgId, string barcode, CancellationToken ct = default);
    /// <summary>Берілген кодтардың қайсысы басқа тауарға тиесілі.</summary>
    Task<IReadOnlyList<BarcodeOwner>> BarcodeOwnersAsync(Guid orgId, IReadOnlyCollection<string> barcodes, Guid? excludeProductId, CancellationToken ct = default);
    void Add(Product product);
    // Бақыланатын тауардың тізіміне Guid-пен қосылған бала INSERT емес, UPDATE болып кетеді — сондықтан нақты қосамыз.
    void AddBarcode(ProductBarcode barcode);
    void RemoveBarcode(ProductBarcode barcode);
    void AddImage(ProductImage image);
    void RemoveImage(ProductImage image);
    void AddCharacteristicValue(ProductCharacteristicValue value);
    void RemoveCharacteristicValue(ProductCharacteristicValue value);
}
