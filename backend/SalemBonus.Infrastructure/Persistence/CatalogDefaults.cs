using Microsoft.EntityFrameworkCore;
using SalemBonus.Domain.Pos.Catalog;

namespace SalemBonus.Infrastructure.Persistence;

/// <summary>Жаңа бизнеске бірден берілетін каталог деректері.</summary>
public static class CatalogDefaults
{
    public static async Task EnsureUnitsAsync(AppDbContext db, Guid orgId, CancellationToken ct = default)
    {
        if (await db.Units.AnyAsync(u => u.OrganizationId == orgId, ct)) return;
        var now = DateTime.UtcNow;
        var i = 0;
        foreach (var (name, shortName) in MeasureUnit.Defaults)
        {
            db.Units.Add(new MeasureUnit
            {
                Id = Guid.NewGuid(),
                OrganizationId = orgId,
                Name = name,
                ShortName = shortName,
                // Реті сақталсын: тізім CreatedAt бойынша сұрыпталады.
                CreatedAt = now.AddMilliseconds(i++),
            });
        }
    }
}
