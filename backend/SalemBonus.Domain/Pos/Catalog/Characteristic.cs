namespace SalemBonus.Domain.Pos.Catalog;

/// <summary>
/// Сипаттама түрі (ТЗ «Товар» §11). Салаға байланбайды: киімге өлшем/түс, сусынға көлем,
/// автобөлшекке марка/модель — бәрі осы әмбебап анықтамалықпен.
/// </summary>
public enum CharacteristicType
{
    Text,
    Number,
    List,
    Boolean,
    Date,
    Range,
}

public class CharacteristicDefinition
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public CharacteristicType Type { get; set; }
    public bool IsRequired { get; set; }
    public CatalogStatus Status { get; set; } = CatalogStatus.Active;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Тек «Тізім» түрінде: таңдауға болатын мәндер.</summary>
    public List<CharacteristicOption> Options { get; set; } = [];
}

public class CharacteristicOption
{
    public Guid Id { get; set; }
    public Guid DefinitionId { get; set; }
    public string Value { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
