using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Kato.Dtos;
using SalemBonus.Domain.Entities;

namespace SalemBonus.Application.Kato;

public class KatoService(IKatoRepository kato, ICurrentLanguage language) : IKatoService
{
    private string Name(KatoEntry e) => KatoNames.ForDisplay(language.Value == AppLanguage.Ru ? e.NameRu : e.NameKk);

    public async Task<IReadOnlyList<KatoNodeDto>> GetChildrenAsync(string? parentCode, CancellationToken ct = default)
    {
        var children = await kato.GetChildrenAsync(parentCode, ct);
        if (children.Count == 0) return [];

        var withChildren = (await kato.GetCodesWithChildrenAsync(children.Select(x => x.Code).ToList(), ct)).ToHashSet();
        return children
            .Select(x => new KatoNodeDto(x.Code, Name(x), x.Level, withChildren.Contains(x.Code)))
            .OrderBy(x => x.Name, StringComparer.CurrentCulture)
            .ToList();
    }

    public async Task<IReadOnlyList<KatoNodeDto>> GetSettlementsAsync(string parentCode, CancellationToken ct = default)
    {
        var descendants = await kato.GetDescendantsAsync(parentCode, ct);
        return descendants
            .Where(x => !KatoNames.IsAdministrative(x.NameKk))
            .Select(x => new KatoNodeDto(x.Code, Name(x), x.Level, false))
            .OrderBy(x => x.Name, StringComparer.CurrentCulture)
            .ToList();
    }

    public async Task<IReadOnlyList<KatoMatchDto>> SearchAsync(string query, int take, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2) return [];
        var found = (await kato.SearchAsync(query, take * 2, ct))
            .Where(x => !KatoNames.IsAdministrative(x.NameKk))
            .Take(take)
            .ToList();

        var result = new List<KatoMatchDto>(found.Count);
        foreach (var entry in found)
        {
            var path = await kato.GetPathAsync(entry.Code, ct);
            result.Add(new KatoMatchDto(
                entry.Code,
                Name(entry),
                entry.Level,
                string.Join(", ", path.SkipLast(1).Where(x => !KatoNames.IsAdministrative(x.NameKk)).Select(Name))));
        }
        return result;
    }

    public async Task<IReadOnlyList<KatoNodeDto>> GetPathAsync(string? code, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code)) return [];
        var path = await kato.GetPathAsync(code, ct);
        // Ауылдық округ сияқты әкімшілік буындар тұтынушыға көрсетілмейді.
        return path
            .Where(x => x.Code == code || !KatoNames.IsAdministrative(x.NameKk))
            .Select(x => new KatoNodeDto(x.Code, Name(x), x.Level, false))
            .ToList();
    }
}
