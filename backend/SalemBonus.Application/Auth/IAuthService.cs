using SalemBonus.Application.Auth.Dtos;

namespace SalemBonus.Application.Auth;

public interface IAuthService
{
    Task<RequestCodeResult> RequestCodeAsync(string phone, CancellationToken ct = default);
    Task<AuthTokens> VerifyCodeAsync(VerifyCodeRequest request, CancellationToken ct = default);
    Task<AuthTokens> RefreshAsync(string refreshToken, CancellationToken ct = default);
    Task LogoutAsync(string refreshToken, CancellationToken ct = default);
}
