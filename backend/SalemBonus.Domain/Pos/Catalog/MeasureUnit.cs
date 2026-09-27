namespace SalemBonus.Domain.Pos.Catalog;

/// <summary>Өлшем бірлігі: атауы және қысқа белгісі (шт, кг, л…).</summary>
public class MeasureUnit
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public CatalogStatus Status { get; set; } = CatalogStatus.Active;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Жаңа бизнеске бірден берілетін бірліктер.</summary>
    public static readonly (string Name, string Short)[] Defaults =
    [
        ("Штука", "шт"), ("Килограмм", "кг"), ("Грамм", "г"), ("Литр", "л"), ("Метр", "м"),
        ("Комплект", "компл"), ("Упаковка", "уп"), ("Пара", "пар"),
    ];
}
