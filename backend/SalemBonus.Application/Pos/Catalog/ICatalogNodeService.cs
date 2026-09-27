namespace SalemBonus.Application.Pos.Catalog;

/// <summary>Классификация: санат → топ → топша → … (ТЗ «Товар» §7, §8, §12–16).</summary>
public interface ICatalogNodeService
{
    Task<IReadOnlyList<CatalogNodeDto>> ListAsync(CancellationToken ct = default);
    Task<CatalogNodeDto> CreateAsync(SaveCatalogNodeRequest request, CancellationToken ct = default);
    Task<CatalogNodeDto> UpdateAsync(Guid id, SaveCatalogNodeRequest request, CancellationToken ct = default);
    Task ReorderAsync(Guid id, string direction, CancellationToken ct = default);
    Task ArchiveAsync(Guid id, CancellationToken ct = default);
    Task RestoreAsync(Guid id, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    /// <summary>Түйіннің балалары мен тауарларын басқа түйінге ауыстыру (өшірер алдында).</summary>
    Task MoveContentAsync(Guid id, Guid targetId, CancellationToken ct = default);
}
