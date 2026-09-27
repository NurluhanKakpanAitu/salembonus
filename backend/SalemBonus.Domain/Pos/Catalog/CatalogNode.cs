namespace SalemBonus.Domain.Pos.Catalog;

/// <summary>
/// Классификация түйіні: санат, топ, топша — бәрі бір кесте, parent_id арқылы шексіз деңгей
/// (ТЗ «Товар» §12). Санат — ата-анасы жоқ түйін, тек оған белгіше беріледі.
///
/// <see cref="Path"/> — түбірден осы түйінге дейінгі идентификаторлар ("a/b/c"). Ол екі нәрсеге керек:
/// ұрпақтарды бір сұраныспен табу және тасымалдағанда цикл тексеру (жаңа ата-ана өз ұрпағы болмауы керек).
/// </summary>
public class CatalogNode
{
    public const int MaxDepth = 12;

    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid? ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>Белгіше кілті (тек санатта міндетті).</summary>
    public string? Icon { get; set; }
    public string? ImageUrl { get; set; }
    public string? Description { get; set; }
    public CatalogStatus Status { get; set; } = CatalogStatus.Active;
    /// <summary>Бір ата-ананың балалары арасындағы реті.</summary>
    public int SortOrder { get; set; }
    public string Path { get; set; } = string.Empty;
    /// <summary>0 — санат, 1 — топ, 2 және одан әрі — топшалар.</summary>
    public int Depth { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public bool IsRoot => ParentId is null;

    /// <summary>Бұл түйін берілген түйіннің ұрпағы ма (немесе өзі ме).</summary>
    public bool IsSelfOrDescendantOf(CatalogNode other) =>
        Path == other.Path || Path.StartsWith(other.Path + "/", StringComparison.Ordinal);
}
