using SalemBonus.Application.Kato.Dtos;

namespace SalemBonus.Application.Kato;

public interface IKatoService
{
    Task<IReadOnlyList<KatoNodeDto>> GetChildrenAsync(string? parentCode, CancellationToken ct = default);
    /// <summary>Ауданның (немесе қаланың) ішіндегі елді мекендер — әкімшілік бірліктерсіз.</summary>
    Task<IReadOnlyList<KatoNodeDto>> GetSettlementsAsync(string parentCode, CancellationToken ct = default);
    Task<IReadOnlyList<KatoMatchDto>> SearchAsync(string query, int take, CancellationToken ct = default);
    /// <summary>Кодтың облыстан бастап толық жолы. Код жарамсыз болса — бос тізім.</summary>
    Task<IReadOnlyList<KatoNodeDto>> GetPathAsync(string? code, CancellationToken ct = default);
}
