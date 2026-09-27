namespace SalemBonus.Domain.Pos.Catalog;

/// <summary>
/// Каталог объектінің күйі. Архивтегі объект жаңа тауарға ұсынылмайды, бірақ тарихы сақталады
/// (ТЗ «Товар» §16).
/// </summary>
public enum CatalogStatus
{
    Active,
    Archived,
}
