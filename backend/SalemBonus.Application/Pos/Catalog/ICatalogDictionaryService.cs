namespace SalemBonus.Application.Pos.Catalog;

/// <summary>
/// Анықтамалықтар: брендтер, өлшем бірліктері, сипаттамалар (ТЗ «Товар» §9–11). Қолданыстағы
/// жазба өшірілмейді — архивтеледі: тарихы сақталады, жаңа тауарға ұсынылмайды.
/// </summary>
public interface ICatalogDictionaryService
{
    Task<IReadOnlyList<BrandDto>> ListBrandsAsync(CancellationToken ct = default);
    Task<BrandDto> CreateBrandAsync(SaveBrandRequest request, CancellationToken ct = default);
    Task<BrandDto> UpdateBrandAsync(Guid id, SaveBrandRequest request, CancellationToken ct = default);
    Task ReorderBrandAsync(Guid id, string direction, CancellationToken ct = default);

    Task<IReadOnlyList<UnitDto>> ListUnitsAsync(CancellationToken ct = default);
    Task<UnitDto> CreateUnitAsync(SaveUnitRequest request, CancellationToken ct = default);
    Task<UnitDto> UpdateUnitAsync(Guid id, SaveUnitRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<CharacteristicDto>> ListCharacteristicsAsync(CancellationToken ct = default);
    Task<CharacteristicDto> CreateCharacteristicAsync(SaveCharacteristicRequest request, CancellationToken ct = default);
    Task<CharacteristicDto> UpdateCharacteristicAsync(Guid id, SaveCharacteristicRequest request, CancellationToken ct = default);
    Task ReorderCharacteristicAsync(Guid id, string direction, CancellationToken ct = default);
}
