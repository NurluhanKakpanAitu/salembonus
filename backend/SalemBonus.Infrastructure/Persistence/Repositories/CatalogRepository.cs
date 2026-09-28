using Microsoft.EntityFrameworkCore;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Domain.Pos.Catalog;

namespace SalemBonus.Infrastructure.Persistence.Repositories;

public class CatalogRepository(AppDbContext db) : ICatalogRepository
{
    // ---------- Классификация ----------

    public async Task<IReadOnlyList<CatalogNode>> ListNodesAsync(Guid orgId, CancellationToken ct = default) =>
        await db.CatalogNodes.AsNoTracking().Where(n => n.OrganizationId == orgId).ToListAsync(ct);

    public Task<CatalogNode?> GetNodeForUpdateAsync(Guid orgId, Guid id, CancellationToken ct = default) =>
        db.CatalogNodes.FirstOrDefaultAsync(n => n.OrganizationId == orgId && n.Id == id, ct);

    public async Task<IReadOnlyList<CatalogNode>> ListDescendantsForUpdateAsync(Guid orgId, string path, CancellationToken ct = default)
    {
        var prefix = path + "/";
        return await db.CatalogNodes.Where(n => n.OrganizationId == orgId && n.Path.StartsWith(prefix)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CatalogNode>> ListSiblingsForUpdateAsync(Guid orgId, Guid? parentId, CancellationToken ct = default) =>
        await db.CatalogNodes.Where(n => n.OrganizationId == orgId && n.ParentId == parentId).ToListAsync(ct);

    public Task<bool> NodeNameExistsAsync(Guid orgId, Guid? parentId, string name, Guid? excludeId, CancellationToken ct = default)
    {
        var lower = name.ToLower();
        return db.CatalogNodes.AnyAsync(n =>
            n.OrganizationId == orgId && n.ParentId == parentId && n.Name.ToLower() == lower && n.Id != excludeId, ct);
    }

    public async Task<int> MaxNodeSortOrderAsync(Guid orgId, Guid? parentId, CancellationToken ct = default) =>
        await db.CatalogNodes.Where(n => n.OrganizationId == orgId && n.ParentId == parentId)
            .MaxAsync(n => (int?)n.SortOrder, ct) ?? 0;

    public async Task<Dictionary<Guid, int>> ProductCountsByNodeAsync(Guid orgId, CancellationToken ct = default) =>
        await db.Products.Where(p => p.OrganizationId == orgId && p.CatalogNodeId != null)
            .GroupBy(p => p.CatalogNodeId!.Value)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

    public Task<int> CountProductsInNodeAsync(Guid nodeId, CancellationToken ct = default) =>
        db.Products.CountAsync(p => p.CatalogNodeId == nodeId, ct);

    public Task MoveProductsAsync(Guid fromNodeId, Guid toNodeId, CancellationToken ct = default) =>
        db.Products.Where(p => p.CatalogNodeId == fromNodeId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.CatalogNodeId, toNodeId)
                .SetProperty(p => p.UpdatedAt, DateTime.UtcNow), ct);

    public async Task<IReadOnlyList<Product>> ListProductsInNodesForUpdateAsync(IReadOnlyCollection<Guid> nodeIds, CancellationToken ct = default) =>
        await db.Products.Where(p => p.CatalogNodeId != null && nodeIds.Contains(p.CatalogNodeId.Value)).ToListAsync(ct);

    public void AddNode(CatalogNode node) => db.CatalogNodes.Add(node);

    public void RemoveNode(CatalogNode node) => db.CatalogNodes.Remove(node);

    // ---------- Брендтер ----------

    public async Task<IReadOnlyList<Brand>> ListBrandsAsync(Guid orgId, CancellationToken ct = default) =>
        await db.Brands.AsNoTracking().Where(b => b.OrganizationId == orgId)
            .OrderBy(b => b.SortOrder).ThenBy(b => b.Name).ToListAsync(ct);

    public Task<Brand?> GetBrandForUpdateAsync(Guid orgId, Guid id, CancellationToken ct = default) =>
        db.Brands.FirstOrDefaultAsync(b => b.OrganizationId == orgId && b.Id == id, ct);

    public async Task<IReadOnlyList<Brand>> ListBrandsForUpdateAsync(Guid orgId, CancellationToken ct = default) =>
        await db.Brands.Where(b => b.OrganizationId == orgId).ToListAsync(ct);

    public Task<bool> BrandNameExistsAsync(Guid orgId, string name, Guid? excludeId, CancellationToken ct = default)
    {
        var lower = name.ToLower();
        return db.Brands.AnyAsync(b => b.OrganizationId == orgId && b.Name.ToLower() == lower && b.Id != excludeId, ct);
    }

    public async Task<Dictionary<Guid, int>> ProductCountsByBrandAsync(Guid orgId, CancellationToken ct = default) =>
        await db.Products.Where(p => p.OrganizationId == orgId && p.BrandId != null)
            .GroupBy(p => p.BrandId!.Value)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

    public void AddBrand(Brand brand) => db.Brands.Add(brand);

    // ---------- Өлшем бірліктері ----------

    public async Task<IReadOnlyList<MeasureUnit>> ListUnitsAsync(Guid orgId, CancellationToken ct = default) =>
        await db.Units.AsNoTracking().Where(u => u.OrganizationId == orgId).OrderBy(u => u.CreatedAt).ThenBy(u => u.Name).ToListAsync(ct);

    public Task<MeasureUnit?> GetUnitForUpdateAsync(Guid orgId, Guid id, CancellationToken ct = default) =>
        db.Units.FirstOrDefaultAsync(u => u.OrganizationId == orgId && u.Id == id, ct);

    public Task<bool> UnitNameExistsAsync(Guid orgId, string name, Guid? excludeId, CancellationToken ct = default)
    {
        var lower = name.ToLower();
        return db.Units.AnyAsync(u => u.OrganizationId == orgId && u.Name.ToLower() == lower && u.Id != excludeId, ct);
    }

    public async Task<Dictionary<Guid, int>> ProductCountsByUnitAsync(Guid orgId, CancellationToken ct = default) =>
        await db.Products.Where(p => p.OrganizationId == orgId)
            .GroupBy(p => p.UnitId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

    public void AddUnit(MeasureUnit unit) => db.Units.Add(unit);

    // ---------- Сипаттамалар ----------

    public async Task<IReadOnlyList<CharacteristicDefinition>> ListCharacteristicsAsync(Guid orgId, CancellationToken ct = default) =>
        await db.Characteristics.AsNoTracking().Include(c => c.Options).Where(c => c.OrganizationId == orgId)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToListAsync(ct);

    public Task<CharacteristicDefinition?> GetCharacteristicForUpdateAsync(Guid orgId, Guid id, CancellationToken ct = default) =>
        db.Characteristics.Include(c => c.Options).FirstOrDefaultAsync(c => c.OrganizationId == orgId && c.Id == id, ct);

    public async Task<IReadOnlyList<CharacteristicDefinition>> ListCharacteristicsForUpdateAsync(Guid orgId, CancellationToken ct = default) =>
        await db.Characteristics.Where(c => c.OrganizationId == orgId).ToListAsync(ct);

    public Task<bool> CharacteristicNameExistsAsync(Guid orgId, string name, Guid? excludeId, CancellationToken ct = default)
    {
        var lower = name.ToLower();
        return db.Characteristics.AnyAsync(c => c.OrganizationId == orgId && c.Name.ToLower() == lower && c.Id != excludeId, ct);
    }

    public async Task<Dictionary<Guid, int>> ProductCountsByCharacteristicAsync(Guid orgId, CancellationToken ct = default) =>
        await db.ProductCharacteristics
            .Where(v => db.Characteristics.Any(c => c.Id == v.DefinitionId && c.OrganizationId == orgId))
            .GroupBy(v => v.DefinitionId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

    public void AddCharacteristic(CharacteristicDefinition definition) => db.Characteristics.Add(definition);

    public void AddCharacteristicOption(CharacteristicOption option) => db.CharacteristicOptions.Add(option);

    public void RemoveCharacteristicOption(CharacteristicOption option) => db.CharacteristicOptions.Remove(option);
}
