using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Application.Pos.Media;
using SalemBonus.Domain.Core;
using SalemBonus.Domain.Pos.Catalog;

namespace SalemBonus.Application.Pos.Catalog;

public class CatalogDictionaryService(ICatalogRepository repo, CatalogAccess access, IUnitOfWork unitOfWork, IFileStorage storage)
    : ICatalogDictionaryService
{
    private AppLanguage Lang => access.Lang;

    // ---------- Брендтер ----------

    public async Task<IReadOnlyList<BrandDto>> ListBrandsAsync(CancellationToken ct = default)
    {
        var (orgId, _) = await access.RequireAsync(StaffPermissions.ProductsView, ct);
        var counts = await repo.ProductCountsByBrandAsync(orgId, ct);
        return (await repo.ListBrandsAsync(orgId, ct)).Select(b => ToDto(b, counts)).ToList();
    }

    public async Task<BrandDto> CreateBrandAsync(SaveBrandRequest request, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.CatalogDictionaries, ct);
        var name = access.Name(request.Name, 100);
        if (await repo.BrandNameExistsAsync(orgId, name, null, ct))
            throw new ValidationException(Messages.CatalogDuplicate(Lang), "name");

        var existing = await repo.ListBrandsAsync(orgId, ct);
        var brand = new Brand
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            Name = name,
            LogoUrl = Logo(orgId, request.LogoUrl),
            Status = access.Status(request.Status, CatalogStatus.Active),
            SortOrder = existing.Count == 0 ? 1 : existing.Max(b => b.SortOrder) + 1,
        };
        repo.AddBrand(brand);
        access.Audit(orgId, m.StoreId, "catalog.brand.create", "brand", brand.Id, null, new { brand.Name });
        await unitOfWork.SaveChangesAsync(ct);
        return ToDto(brand, new Dictionary<Guid, int>());
    }

    public async Task<BrandDto> UpdateBrandAsync(Guid id, SaveBrandRequest request, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.CatalogDictionaries, ct);
        var brand = await repo.GetBrandForUpdateAsync(orgId, id, ct) ?? throw new NotFoundException(Messages.BrandNotFound(Lang));
        var before = new { brand.Name, brand.LogoUrl, Status = brand.Status.ToString() };

        var name = access.Name(request.Name, 100);
        if (await repo.BrandNameExistsAsync(orgId, name, brand.Id, ct))
            throw new ValidationException(Messages.CatalogDuplicate(Lang), "name");
        brand.Name = name;
        brand.LogoUrl = Logo(orgId, request.LogoUrl);
        brand.Status = access.Status(request.Status, brand.Status);
        brand.UpdatedAt = DateTime.UtcNow;

        access.Audit(orgId, m.StoreId, "catalog.brand.update", "brand", brand.Id, before,
            new { brand.Name, brand.LogoUrl, Status = brand.Status.ToString() });
        await unitOfWork.SaveChangesAsync(ct);
        return ToDto(brand, await repo.ProductCountsByBrandAsync(orgId, ct));
    }

    public async Task ReorderBrandAsync(Guid id, string direction, CancellationToken ct = default)
    {
        var (orgId, _) = await access.RequireAsync(StaffPermissions.CatalogDictionaries, ct);
        var list = (await repo.ListBrandsForUpdateAsync(orgId, ct)).OrderBy(b => b.SortOrder).ThenBy(b => b.Name).ToList();
        if (Swap(list, b => b.Id == id, direction))
        {
            for (var i = 0; i < list.Count; i++) list[i].SortOrder = i + 1;
            await unitOfWork.SaveChangesAsync(ct);
        }
    }

    /// <summary>Логотип тек осы бизнестің бумасынан (бөтен сілтеме сақталмайды).</summary>
    private string? Logo(Guid orgId, string? raw)
    {
        var url = CatalogAccess.Optional(raw, 500);
        if (url is not null && !storage.IsOwnUrl(url, MediaService.BrandPrefix(orgId)))
            throw new ValidationException(Messages.ProductImageInvalid(Lang), "logoUrl");
        return url;
    }

    private static BrandDto ToDto(Brand b, IReadOnlyDictionary<Guid, int> counts) =>
        new(b.Id, b.Name, b.LogoUrl, b.Status.ToString(), b.SortOrder, counts.GetValueOrDefault(b.Id));

    // ---------- Өлшем бірліктері ----------

    public async Task<IReadOnlyList<UnitDto>> ListUnitsAsync(CancellationToken ct = default)
    {
        var (orgId, _) = await access.RequireAsync(StaffPermissions.ProductsView, ct);
        var counts = await repo.ProductCountsByUnitAsync(orgId, ct);
        return (await repo.ListUnitsAsync(orgId, ct)).Select(u => ToDto(u, counts)).ToList();
    }

    public async Task<UnitDto> CreateUnitAsync(SaveUnitRequest request, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.CatalogDictionaries, ct);
        var (name, shortName) = UnitNames(request);
        if (await repo.UnitNameExistsAsync(orgId, name, null, ct))
            throw new ValidationException(Messages.CatalogDuplicate(Lang), "name");

        var unit = new MeasureUnit
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            Name = name,
            ShortName = shortName,
            Status = access.Status(request.Status, CatalogStatus.Active),
        };
        repo.AddUnit(unit);
        access.Audit(orgId, m.StoreId, "catalog.unit.create", "unit", unit.Id, null, new { unit.Name, unit.ShortName });
        await unitOfWork.SaveChangesAsync(ct);
        return ToDto(unit, new Dictionary<Guid, int>());
    }

    public async Task<UnitDto> UpdateUnitAsync(Guid id, SaveUnitRequest request, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.CatalogDictionaries, ct);
        var unit = await repo.GetUnitForUpdateAsync(orgId, id, ct) ?? throw new NotFoundException(Messages.UnitNotFound(Lang));
        var before = new { unit.Name, unit.ShortName, Status = unit.Status.ToString() };

        var (name, shortName) = UnitNames(request);
        if (await repo.UnitNameExistsAsync(orgId, name, unit.Id, ct))
            throw new ValidationException(Messages.CatalogDuplicate(Lang), "name");
        unit.Name = name;
        unit.ShortName = shortName;
        unit.Status = access.Status(request.Status, unit.Status);
        unit.UpdatedAt = DateTime.UtcNow;

        access.Audit(orgId, m.StoreId, "catalog.unit.update", "unit", unit.Id, before,
            new { unit.Name, unit.ShortName, Status = unit.Status.ToString() });
        await unitOfWork.SaveChangesAsync(ct);
        return ToDto(unit, await repo.ProductCountsByUnitAsync(orgId, ct));
    }

    private (string Name, string Short) UnitNames(SaveUnitRequest request)
    {
        var name = access.Name(request.Name, 50);
        var shortName = CatalogAccess.Optional(request.ShortName, 12)
            ?? throw new ValidationException(Messages.UnitShortRequired(Lang), "shortName");
        return (name, shortName);
    }

    private static UnitDto ToDto(MeasureUnit u, IReadOnlyDictionary<Guid, int> counts) =>
        new(u.Id, u.Name, u.ShortName, u.Status.ToString(), counts.GetValueOrDefault(u.Id));

    // ---------- Сипаттамалар ----------

    public async Task<IReadOnlyList<CharacteristicDto>> ListCharacteristicsAsync(CancellationToken ct = default)
    {
        var (orgId, _) = await access.RequireAsync(StaffPermissions.ProductsView, ct);
        var counts = await repo.ProductCountsByCharacteristicAsync(orgId, ct);
        return (await repo.ListCharacteristicsAsync(orgId, ct)).Select(c => ToDto(c, counts)).ToList();
    }

    public async Task<CharacteristicDto> CreateCharacteristicAsync(SaveCharacteristicRequest request, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.CatalogDictionaries, ct);
        var name = access.Name(request.Name, 100);
        var type = ParseType(request.Type);
        var options = Options(type, request.Options);
        if (await repo.CharacteristicNameExistsAsync(orgId, name, null, ct))
            throw new ValidationException(Messages.CatalogDuplicate(Lang), "name");

        var existing = await repo.ListCharacteristicsAsync(orgId, ct);
        var id = Guid.NewGuid();
        var definition = new CharacteristicDefinition
        {
            Id = id,
            OrganizationId = orgId,
            Name = name,
            Type = type,
            IsRequired = request.IsRequired,
            Status = access.Status(request.Status, CatalogStatus.Active),
            SortOrder = existing.Count == 0 ? 1 : existing.Max(c => c.SortOrder) + 1,
            Options = options.Select((v, i) => new CharacteristicOption { Id = Guid.NewGuid(), DefinitionId = id, Value = v, SortOrder = i + 1 }).ToList(),
        };
        repo.AddCharacteristic(definition);
        access.Audit(orgId, m.StoreId, "catalog.characteristic.create", "characteristic", id, null,
            new { definition.Name, Type = type.ToString(), Options = options });
        await unitOfWork.SaveChangesAsync(ct);
        return ToDto(definition, new Dictionary<Guid, int>());
    }

    public async Task<CharacteristicDto> UpdateCharacteristicAsync(Guid id, SaveCharacteristicRequest request, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.CatalogDictionaries, ct);
        var definition = await repo.GetCharacteristicForUpdateAsync(orgId, id, ct)
            ?? throw new NotFoundException(Messages.CharacteristicNotFound(Lang));
        var before = new
        {
            definition.Name, Type = definition.Type.ToString(), definition.IsRequired,
            Options = definition.Options.OrderBy(o => o.SortOrder).Select(o => o.Value).ToList(),
        };

        var name = access.Name(request.Name, 100);
        var type = ParseType(request.Type);
        var options = Options(type, request.Options);
        if (await repo.CharacteristicNameExistsAsync(orgId, name, definition.Id, ct))
            throw new ValidationException(Messages.CatalogDuplicate(Lang), "name");

        definition.Name = name;
        definition.Type = type;
        definition.IsRequired = request.IsRequired;
        definition.Status = access.Status(request.Status, definition.Status);
        definition.UpdatedAt = DateTime.UtcNow;

        // Мәндер тізімін синхрондау: сақталғандары орнында қалады, реті жаңарады.
        foreach (var removed in definition.Options.Where(o => !options.Contains(o.Value)).ToList())
        {
            definition.Options.Remove(removed);
            repo.RemoveCharacteristicOption(removed);
        }
        for (var i = 0; i < options.Count; i++)
        {
            var existing = definition.Options.FirstOrDefault(o => o.Value == options[i]);
            if (existing is not null) existing.SortOrder = i + 1;
            else
            {
                var option = new CharacteristicOption { Id = Guid.NewGuid(), DefinitionId = definition.Id, Value = options[i], SortOrder = i + 1 };
                repo.AddCharacteristicOption(option);
                // EF жаңа мәнді ата-ананың тізіміне өзі қосады (relationship fix-up) — екі рет қоспау үшін.
                if (!definition.Options.Contains(option)) definition.Options.Add(option);
            }
        }

        access.Audit(orgId, m.StoreId, "catalog.characteristic.update", "characteristic", definition.Id, before,
            new { definition.Name, Type = type.ToString(), definition.IsRequired, Options = options });
        await unitOfWork.SaveChangesAsync(ct);
        return ToDto(definition, await repo.ProductCountsByCharacteristicAsync(orgId, ct));
    }

    public async Task ReorderCharacteristicAsync(Guid id, string direction, CancellationToken ct = default)
    {
        var (orgId, _) = await access.RequireAsync(StaffPermissions.CatalogDictionaries, ct);
        var list = (await repo.ListCharacteristicsForUpdateAsync(orgId, ct)).OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToList();
        if (Swap(list, c => c.Id == id, direction))
        {
            for (var i = 0; i < list.Count; i++) list[i].SortOrder = i + 1;
            await unitOfWork.SaveChangesAsync(ct);
        }
    }

    private CharacteristicType ParseType(string? raw) =>
        Enum.TryParse<CharacteristicType>(raw, ignoreCase: true, out var type)
            ? type
            : throw new ValidationException(Messages.CharacteristicTypeInvalid(Lang), "type");

    /// <summary>Тек «Тізім» түрінде мәндер керек: бос емес, қайталанбайтын, реті сақталған.</summary>
    private List<string> Options(CharacteristicType type, IReadOnlyList<string>? raw)
    {
        if (type != CharacteristicType.List) return [];
        var values = (raw ?? [])
            .Select(v => v.Trim()).Where(v => v.Length > 0).Select(v => v.Length > 100 ? v[..100] : v)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (values.Count == 0) throw new ValidationException(Messages.CharacteristicOptionsRequired(Lang), "options");
        return values;
    }

    private static CharacteristicDto ToDto(CharacteristicDefinition c, IReadOnlyDictionary<Guid, int> counts) =>
        new(c.Id, c.Name, c.Type.ToString(), c.IsRequired, c.Status.ToString(), c.SortOrder,
            c.Options.OrderBy(o => o.SortOrder).Select(o => o.Value).ToList(), counts.GetValueOrDefault(c.Id));

    /// <summary>Тізімде элементті көршісімен ауыстыру (жоғары/төмен).</summary>
    private static bool Swap<T>(List<T> list, Func<T, bool> match, string direction)
    {
        var index = list.FindIndex(x => match(x));
        var target = direction.Equals("up", StringComparison.OrdinalIgnoreCase) ? index - 1 : index + 1;
        if (index < 0 || target < 0 || target >= list.Count) return false;
        (list[index], list[target]) = (list[target], list[index]);
        return true;
    }
}
