using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SalemBonus.Domain.Entities;

namespace SalemBonus.Infrastructure.Persistence;

/// <summary>
/// КАТО анықтамалығын бір рет базаға құяды. Дерек ресми ҚР жіктеуішінен алынған
/// (Persistence/Data/kato.json) және жинақтың ішінде бірге жүреді.
/// </summary>
public static class KatoSeeder
{
    private const string ResourceName = "SalemBonus.Infrastructure.Persistence.Data.kato.json";

    private record Row(string c, string? p, int l, string k, string r);

    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (await db.Kato.AnyAsync(ct)) return;

        await using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"КАТО дерегі табылмады: {ResourceName}");
        var rows = await JsonSerializer.DeserializeAsync<List<Row>>(stream, cancellationToken: ct) ?? [];

        var tracking = db.ChangeTracker.AutoDetectChangesEnabled;
        db.ChangeTracker.AutoDetectChangesEnabled = false;
        try
        {
            foreach (var chunk in rows.Chunk(2000))
            {
                db.Kato.AddRange(chunk.Select(x => new KatoEntry
                {
                    Code = x.c,
                    ParentCode = x.p,
                    Level = x.l,
                    NameKk = x.k,
                    NameRu = x.r,
                }));
                await db.SaveChangesAsync(ct);
                db.ChangeTracker.Clear();
            }
        }
        finally
        {
            db.ChangeTracker.AutoDetectChangesEnabled = tracking;
        }
    }
}
