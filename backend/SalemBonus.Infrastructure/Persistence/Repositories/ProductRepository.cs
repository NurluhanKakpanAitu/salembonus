using Microsoft.EntityFrameworkCore;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Domain.Pos.Catalog;

namespace SalemBonus.Infrastructure.Persistence.Repositories;

public class ProductRepository(AppDbContext db) : IProductRepository
{
    public async Task<(IReadOnlyList<Product> Items, int Total)> SearchAsync(Guid orgId, ProductFilter f, CancellationToken ct = default)
    {
        var q = db.Products.AsNoTracking().Where(p => p.OrganizationId == orgId);
        if (f.Status is { } status) q = q.Where(p => p.Status == status);
        if (f.BrandId is { } brandId) q = q.Where(p => p.BrandId == brandId);
        if (f.UnitId is { } unitId) q = q.Where(p => p.UnitId == unitId);
        if (f.NodePath is { } path)
        {
            var prefix = path + "/";
            q = q.Where(p => db.CatalogNodes.Any(n => n.Id == p.CatalogNodeId && (n.Path == path || n.Path.StartsWith(prefix))));
        }
        if (f.Search is { } term)
        {
            var like = $"%{Escape(term)}%";
            q = q.Where(p => EF.Functions.ILike(p.Name, like, "\\")
                || (p.Article != null && EF.Functions.ILike(p.Article, like, "\\"))
                || p.Barcodes.Any(b => b.Barcode.StartsWith(term)));
        }

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Name)
            .Skip(f.Skip).Take(f.Take)
            .Include(p => p.Barcodes).Include(p => p.Images)
            .AsSplitQuery()
            .ToListAsync(ct);
        return (items, total);
    }

    public Task<Product?> GetAsync(Guid orgId, Guid id, CancellationToken ct = default) =>
        WithChildren(db.Products.AsNoTracking()).FirstOrDefaultAsync(p => p.OrganizationId == orgId && p.Id == id, ct);

    public Task<Product?> GetForUpdateAsync(Guid orgId, Guid id, CancellationToken ct = default) =>
        WithChildren(db.Products).FirstOrDefaultAsync(p => p.OrganizationId == orgId && p.Id == id, ct);

    public Task<Product?> FindByBarcodeAsync(Guid orgId, string barcode, CancellationToken ct = default) =>
        db.Products.AsNoTracking().Include(p => p.Barcodes).Include(p => p.Images).AsSplitQuery()
            .FirstOrDefaultAsync(p => p.OrganizationId == orgId && p.Barcodes.Any(b => b.Barcode == barcode), ct);

    public async Task<IReadOnlyList<BarcodeOwner>> BarcodeOwnersAsync(Guid orgId, IReadOnlyCollection<string> barcodes,
        Guid? excludeProductId, CancellationToken ct = default)
    {
        if (barcodes.Count == 0) return [];
        return await (from b in db.ProductBarcodes
                      join p in db.Products on b.ProductId equals p.Id
                      where b.OrganizationId == orgId && barcodes.Contains(b.Barcode) && b.ProductId != excludeProductId
                      select new BarcodeOwner(b.Barcode, p.Id, p.Name)).ToListAsync(ct);
    }

    public void Add(Product product) => db.Products.Add(product);
    public void AddBarcode(ProductBarcode barcode) => db.ProductBarcodes.Add(barcode);
    public void RemoveBarcode(ProductBarcode barcode) => db.ProductBarcodes.Remove(barcode);
    public void AddImage(ProductImage image) => db.ProductImages.Add(image);
    public void RemoveImage(ProductImage image) => db.ProductImages.Remove(image);
    public void AddCharacteristicValue(ProductCharacteristicValue value) => db.ProductCharacteristics.Add(value);
    public void RemoveCharacteristicValue(ProductCharacteristicValue value) => db.ProductCharacteristics.Remove(value);

    private static IQueryable<Product> WithChildren(IQueryable<Product> q) =>
        q.Include(p => p.Barcodes).Include(p => p.Images).Include(p => p.Characteristics).AsSplitQuery();

    /// <summary>ILIKE үлгісіндегі арнайы таңбалар (%, _) әріп ретінде ізделсін.</summary>
    private static string Escape(string term) => term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
