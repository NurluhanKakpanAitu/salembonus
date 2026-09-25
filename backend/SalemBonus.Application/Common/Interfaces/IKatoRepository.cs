using SalemBonus.Domain.Entities;

namespace SalemBonus.Application.Common.Interfaces;

public interface IKatoRepository
{
    /// <summary>Тікелей бағынышты тармақтар. parentCode бос болса — облыстар мен республикалық қалалар.</summary>
    Task<IReadOnlyList<KatoEntry>> GetChildrenAsync(string? parentCode, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetCodesWithChildrenAsync(IReadOnlyCollection<string> codes, CancellationToken ct = default);
    Task<KatoEntry?> GetAsync(string code, CancellationToken ct = default);
    /// <summary>Кодтан бастап жоғары қарай толық жол (облысқа дейін).</summary>
    Task<IReadOnlyList<KatoEntry>> GetPathAsync(string code, CancellationToken ct = default);
    /// <summary>Берілген тармақтың барлық ұрпақтары (барлық деңгейде).</summary>
    Task<IReadOnlyList<KatoEntry>> GetDescendantsAsync(string parentCode, CancellationToken ct = default);
    Task<IReadOnlyList<KatoEntry>> SearchAsync(string query, int take, CancellationToken ct = default);
}
