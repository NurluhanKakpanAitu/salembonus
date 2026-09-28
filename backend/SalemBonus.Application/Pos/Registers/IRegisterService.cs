using SalemBonus.Application.Core.Staff.Dtos;

namespace SalemBonus.Application.Pos.Registers;

/// <summary>
/// Касса (құрылғы). Иесі не әкімші компьютерді бір рет тіркейді; содан кейін онда кассирлер
/// PIN-мен ауысады және касса әрекетсіз тұрса бұғатталады. PIN тек тіркелген кассада жарайды.
/// </summary>
public interface IRegisterService
{
    Task<IReadOnlyList<RegisterDto>> ListAsync(CancellationToken ct = default);
    Task<RegisterActivation> ActivateAsync(Guid registerId, CancellationToken ct = default);
    Task DeactivateAsync(string? deviceToken, CancellationToken ct = default);

    /// <summary>Бұл құрылғы тіркелген касса болса — оның мәліметі, әйтпесе бос.</summary>
    Task<RegisterDto?> GetCurrentAsync(string? deviceToken, CancellationToken ct = default);
    /// <summary>Осы құрылғының кассасы; тіркелмеген болса — 403 (сатылым тек кассадан).</summary>
    Task<RegisterDto> RequireCurrentAsync(string? deviceToken, CancellationToken ct = default);
    Task<IReadOnlyList<RegisterCashierDto>> GetCashiersAsync(string? deviceToken, CancellationToken ct = default);
    Task<StaffSession> PinLoginAsync(string? deviceToken, PinLoginRequest request, string? device, CancellationToken ct = default);
    Task UnlockAsync(string? deviceToken, string? pin, CancellationToken ct = default);
    Task LockAsync(string? deviceToken, CancellationToken ct = default);
}
