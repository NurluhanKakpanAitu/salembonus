using Microsoft.EntityFrameworkCore;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Domain.Entities;

namespace SalemBonus.Infrastructure.Persistence.Repositories;

public class KatoRepository(AppDbContext db) : IKatoRepository
{
    public async Task<IReadOnlyList<KatoEntry>> GetChildrenAsync(string? parentCode, CancellationToken ct = default) =>
        await db.Kato.AsNoTracking()
            .Where(x => parentCode == null ? x.ParentCode == null : x.ParentCode == parentCode)
            .OrderBy(x => x.NameKk)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<string>> GetCodesWithChildrenAsync(
        IReadOnlyCollection<string> codes, CancellationToken ct = default) =>
        await db.Kato.AsNoTracking()
            .Where(x => x.ParentCode != null && codes.Contains(x.ParentCode))
            .Select(x => x.ParentCode!)
            .Distinct()
            .ToListAsync(ct);

    public Task<KatoEntry?> GetAsync(string code, CancellationToken ct = default) =>
        db.Kato.AsNoTracking().FirstOrDefaultAsync(x => x.Code == code, ct);

    public async Task<IReadOnlyList<KatoEntry>> GetPathAsync(string code, CancellationToken ct = default)
    {
        var path = new List<KatoEntry>();
        var current = await GetAsync(code, ct);
        // КАТО-да ең көбі төрт деңгей, сондықтан цикл қысқа.
        while (current is not null)
        {
            path.Insert(0, current);
            current = current.ParentCode is null ? null : await GetAsync(current.ParentCode, ct);
        }
        return path;
    }

    public async Task<IReadOnlyList<KatoEntry>> GetDescendantsAsync(string parentCode, CancellationToken ct = default)
    {
        var all = new List<KatoEntry>();
        var frontier = new List<string> { parentCode };
        // КАТО терең емес: ең көбі бірнеше айналым.
        while (frontier.Count > 0)
        {
            var next = await db.Kato.AsNoTracking()
                .Where(x => x.ParentCode != null && frontier.Contains(x.ParentCode))
                .ToListAsync(ct);
            if (next.Count == 0) break;
            all.AddRange(next);
            frontier = next.Select(x => x.Code).ToList();
        }
        return all;
    }

    public async Task<IReadOnlyList<KatoEntry>> SearchAsync(string query, int take, CancellationToken ct = default)
    {
        var q = query.Trim();
        return await db.Kato.AsNoTracking()
            .Where(x => EF.Functions.ILike(x.NameKk, $"{q}%") || EF.Functions.ILike(x.NameRu, $"{q}%"))
            .OrderBy(x => x.Level)
            .ThenBy(x => x.NameKk)
            .Take(take)
            .ToListAsync(ct);
    }
}
