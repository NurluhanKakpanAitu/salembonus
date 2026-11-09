using Microsoft.EntityFrameworkCore;
using SalemBonus.Application.Common;
using SalemBonus.Domain.Pos.Catalog;
using SalemBonus.Infrastructure.Persistence.Configurations;

namespace SalemBonus.Infrastructure.Persistence.Repositories;

/// <summary>
/// <see cref="ProductSearch"/> ережесі базада: әр сөз — бөлек шарт (ЖӘНЕ), сөздің ішінде — барлық өріс (НЕМЕСЕ).
/// Үлкен-кіші әріп — <see cref="AppDbContext.Fold"/> арқылы (базаның локалі кириллицаны теңестірмейді).
/// </summary>
/// <remarks>
/// Жылдамдық (20 000 тауар, 450 санат): тауардың өз мәтіні — дайын «SearchText» бағаны (Postgres есептеп сақтайды), бір LIKE;
/// санат пен бренд — әр тауар үшін емес, ұйымның сөзге сәйкес санаттары (ішкі санаттарымен) мен брендтері бір рет, тәуелсіз
/// подзапроспен табылады. Бұрын әр тауар × әр сөз үшін барлық санат аралап шығатын — 60 с.
/// </remarks>
internal static class ProductSearchQuery
{
    public static IQueryable<Product> Search(this IQueryable<Product> q, AppDbContext db, Guid orgId, string? text)
    {
        foreach (var token in ProductSearch.Tokens(text))
        {
            var like = $"%{token.ToLowerInvariant().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
            var nodeIds = db.CatalogNodes.Where(a => a.OrganizationId == orgId && EF.Functions.Like(AppDbContext.Fold(a.Name), like))
                .SelectMany(a => db.CatalogNodes.Where(n => n.OrganizationId == orgId && (n.Path == a.Path || n.Path.StartsWith(a.Path + "/"))))
                .Select(n => (Guid?)n.Id);
            var brandIds = db.Brands.Where(b => b.OrganizationId == orgId && EF.Functions.Like(AppDbContext.Fold(b.Name), like)).Select(b => (Guid?)b.Id);
            q = q.Where(p => EF.Functions.Like(EF.Property<string>(p, ProductConfiguration.ProductSearchText), like)
                || p.Barcodes.Any(b => EF.Functions.ILike(b.Barcode, like))
                || brandIds.Contains(p.BrandId)
                || nodeIds.Contains(p.CatalogNodeId)
                || p.Characteristics.Any(c => EF.Functions.Like(AppDbContext.Fold(c.Value), like)));
        }
        return q;
    }
}
