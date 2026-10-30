using Microsoft.EntityFrameworkCore;
using SalemBonus.Application.Common;
using SalemBonus.Domain.Pos.Catalog;

namespace SalemBonus.Infrastructure.Persistence.Repositories;

/// <summary>
/// <see cref="ProductSearch"/> ережесі базада: әр сөз — бөлек шарт (ЖӘНЕ), сөздің ішінде — барлық өріс (НЕМЕСЕ).
/// Санат — тауардың өз санаты және оның барлық жоғарғы санаттары (жол бойынша). Үлкен-кіші әріп — <see cref="AppDbContext.Fold"/>
/// арқылы (базаның локалі кириллицаны теңестірмейді): өріс те, сөз де кіші әріпке келтіріліп, LIKE-пен салыстырылады.
/// </summary>
internal static class ProductSearchQuery
{
    public static IQueryable<Product> Search(this IQueryable<Product> q, AppDbContext db, string? text)
    {
        foreach (var token in ProductSearch.Tokens(text))
        {
            var like = $"%{token.ToLowerInvariant().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
            q = q.Where(p => EF.Functions.Like(AppDbContext.Fold(p.Name), like)
                || (p.Article != null && EF.Functions.Like(AppDbContext.Fold(p.Article), like))
                || p.Barcodes.Any(b => EF.Functions.Like(AppDbContext.Fold(b.Barcode), like))
                || p.Characteristics.Any(c => EF.Functions.Like(AppDbContext.Fold(c.Value), like))
                || (p.BrandId != null && db.Brands.Any(b => b.Id == p.BrandId && EF.Functions.Like(AppDbContext.Fold(b.Name), like)))
                || (p.CatalogNodeId != null && db.CatalogNodes.Any(a => a.OrganizationId == p.OrganizationId && EF.Functions.Like(AppDbContext.Fold(a.Name), like)
                    && db.CatalogNodes.Any(n => n.Id == p.CatalogNodeId && (n.Path == a.Path || n.Path.StartsWith(a.Path + "/")))))
                || (p.Supplier != null && EF.Functions.Like(AppDbContext.Fold(p.Supplier), like))
                || (p.Manufacturer != null && EF.Functions.Like(AppDbContext.Fold(p.Manufacturer), like))
                || (p.Country != null && EF.Functions.Like(AppDbContext.Fold(p.Country), like))
                || (p.Description != null && EF.Functions.Like(AppDbContext.Fold(p.Description), like)));
        }
        return q;
    }
}
