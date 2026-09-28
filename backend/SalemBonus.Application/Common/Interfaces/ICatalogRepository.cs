using SalemBonus.Domain.Pos.Catalog;

namespace SalemBonus.Application.Common.Interfaces;

/// <summary>Каталогтың анықтамалықтары мен классификациясы. Бәрі бизнес (ұйым) деңгейінде.</summary>
public interface ICatalogRepository
{
    // Классификация
    Task<IReadOnlyList<CatalogNode>> ListNodesAsync(Guid orgId, CancellationToken ct = default);
    Task<CatalogNode?> GetNodeForUpdateAsync(Guid orgId, Guid id, CancellationToken ct = default);
    /// <summary>Түйіннің барлық ұрпағы (өзінсіз), өзгерту үшін.</summary>
    Task<IReadOnlyList<CatalogNode>> ListDescendantsForUpdateAsync(Guid orgId, string path, CancellationToken ct = default);
    Task<IReadOnlyList<CatalogNode>> ListSiblingsForUpdateAsync(Guid orgId, Guid? parentId, CancellationToken ct = default);
    Task<bool> NodeNameExistsAsync(Guid orgId, Guid? parentId, string name, Guid? excludeId, CancellationToken ct = default);
    Task<int> MaxNodeSortOrderAsync(Guid orgId, Guid? parentId, CancellationToken ct = default);
    /// <summary>Әр түйінге тікелей байланған тауар саны.</summary>
    Task<Dictionary<Guid, int>> ProductCountsByNodeAsync(Guid orgId, CancellationToken ct = default);
    Task<int> CountProductsInNodeAsync(Guid nodeId, CancellationToken ct = default);
    /// <summary>Бір түйіннің тауарларын басқа түйінге көшіру.</summary>
    Task MoveProductsAsync(Guid fromNodeId, Guid toNodeId, CancellationToken ct = default);
    /// <summary>Берілген түйіндерге тікелей байланған тауарлар, өзгерту үшін.</summary>
    Task<IReadOnlyList<Product>> ListProductsInNodesForUpdateAsync(IReadOnlyCollection<Guid> nodeIds, CancellationToken ct = default);
    void AddNode(CatalogNode node);
    void RemoveNode(CatalogNode node);

    // Брендтер
    Task<IReadOnlyList<Brand>> ListBrandsAsync(Guid orgId, CancellationToken ct = default);
    Task<Brand?> GetBrandForUpdateAsync(Guid orgId, Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Brand>> ListBrandsForUpdateAsync(Guid orgId, CancellationToken ct = default);
    Task<bool> BrandNameExistsAsync(Guid orgId, string name, Guid? excludeId, CancellationToken ct = default);
    Task<Dictionary<Guid, int>> ProductCountsByBrandAsync(Guid orgId, CancellationToken ct = default);
    void AddBrand(Brand brand);

    // Өлшем бірліктері
    Task<IReadOnlyList<MeasureUnit>> ListUnitsAsync(Guid orgId, CancellationToken ct = default);
    Task<MeasureUnit?> GetUnitForUpdateAsync(Guid orgId, Guid id, CancellationToken ct = default);
    Task<bool> UnitNameExistsAsync(Guid orgId, string name, Guid? excludeId, CancellationToken ct = default);
    Task<Dictionary<Guid, int>> ProductCountsByUnitAsync(Guid orgId, CancellationToken ct = default);
    void AddUnit(MeasureUnit unit);

    // Сипаттамалар
    Task<IReadOnlyList<CharacteristicDefinition>> ListCharacteristicsAsync(Guid orgId, CancellationToken ct = default);
    Task<CharacteristicDefinition?> GetCharacteristicForUpdateAsync(Guid orgId, Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<CharacteristicDefinition>> ListCharacteristicsForUpdateAsync(Guid orgId, CancellationToken ct = default);
    Task<bool> CharacteristicNameExistsAsync(Guid orgId, string name, Guid? excludeId, CancellationToken ct = default);
    Task<Dictionary<Guid, int>> ProductCountsByCharacteristicAsync(Guid orgId, CancellationToken ct = default);
    void AddCharacteristic(CharacteristicDefinition definition);
    /// <summary>
    /// Жаңа мәнді нақты «қосылды» деп белгілеу. Бақыланатын ата-ананың тізіміне Guid-пен қосылған бала
    /// EF үшін «бар жазба» болып көрінеді де, INSERT-тің орнына UPDATE жасалады.
    /// </summary>
    void AddCharacteristicOption(CharacteristicOption option);
    void RemoveCharacteristicOption(CharacteristicOption option);
}
