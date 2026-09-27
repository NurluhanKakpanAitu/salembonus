namespace SalemBonus.Application.Pos.Catalog;

public record CatalogNodeDto(
    Guid Id,
    Guid? ParentId,
    string Name,
    string? Icon,
    string? ImageUrl,
    string? Description,
    string Status,
    int SortOrder,
    int Depth,
    /// <summary>Түбірден осы түйінге дейінгі атаулар: «Автозапчасти → Тормозная система → …».</summary>
    IReadOnlyList<string> PathNames,
    /// <summary>Тікелей балалары.</summary>
    int ChildCount,
    /// <summary>Барлық ұрпағы.</summary>
    int DescendantCount,
    /// <summary>Осы түйін мен оның барлық ұрпағындағы тауарлар.</summary>
    int ProductCount,
    DateTime UpdatedAt);

public record SaveCatalogNodeRequest(
    Guid? ParentId,
    string? Name,
    string? Icon,
    string? ImageUrl,
    string? Description,
    string? Status,
    int? SortOrder);

public record ReorderRequest(string Direction);

public record MoveContentRequest(Guid TargetId);

public record BrandDto(Guid Id, string Name, string? LogoUrl, string Status, int SortOrder, int ProductCount);

public record SaveBrandRequest(string? Name, string? LogoUrl, string? Status);

public record UnitDto(Guid Id, string Name, string ShortName, string Status, int ProductCount);

public record SaveUnitRequest(string? Name, string? ShortName, string? Status);

public record CharacteristicDto(
    Guid Id, string Name, string Type, bool IsRequired, string Status, int SortOrder,
    IReadOnlyList<string> Options, int ProductCount);

public record SaveCharacteristicRequest(string? Name, string? Type, bool IsRequired, IReadOnlyList<string>? Options, string? Status);
