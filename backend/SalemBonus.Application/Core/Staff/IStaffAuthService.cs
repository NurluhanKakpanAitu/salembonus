using SalemBonus.Application.Core.Staff.Dtos;

namespace SalemBonus.Application.Core.Staff;

public interface IStaffAuthService
{
    Task<StaffSession> LoginAsync(StaffLoginRequest request, CancellationToken ct = default);
    Task<StaffSession> RefreshAsync(string? refreshToken, CancellationToken ct = default);
    Task LogoutAsync(string? refreshToken, CancellationToken ct = default);
    Task<StaffMeDto> GetMeAsync(CancellationToken ct = default);
    Task<StaffMeDto> SetLanguageAsync(string language, CancellationToken ct = default);
}
