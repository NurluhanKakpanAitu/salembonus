namespace SalemBonus.Application.Pos.Catalog;

public interface IProductService
{
    Task<ProductPageDto> ListAsync(string? search, string? status, Guid? nodeId, Guid? brandId, Guid? unitId,
        int page, int pageSize, CancellationToken ct = default);
    Task<ProductDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<ProductListItemDto> FindByBarcodeAsync(string barcode, CancellationToken ct = default);
    Task<BarcodeCheckDto> CheckBarcodeAsync(string barcode, Guid? excludeProductId, CancellationToken ct = default);
    Task<GeneratedBarcodeDto> GenerateBarcodeAsync(CancellationToken ct = default);
    Task<ProductDto> CreateAsync(SaveProductRequest request, CancellationToken ct = default);
    Task<ProductDto> UpdateAsync(Guid id, SaveProductRequest request, CancellationToken ct = default);
    Task<ProductDto> ChangeClassificationAsync(Guid id, Guid? nodeId, CancellationToken ct = default);
    Task ArchiveAsync(Guid id, CancellationToken ct = default);
    Task RestoreAsync(Guid id, CancellationToken ct = default);
}
