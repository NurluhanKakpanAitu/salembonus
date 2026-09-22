namespace SalemBonus.Application.Auth.Dtos;

public record RequestCodeRequest(string Phone);

/// <summary>DevCode тек Development ортасында, SMS провайдері "Log" болғанда толады.</summary>
public record RequestCodeResult(string Phone, int ExpiresInSeconds, int RetryAfterSeconds, string? DevCode);

public record VerifyCodeRequest(string Phone, string Code, string? Device = null);

public record RefreshRequest(string RefreshToken);

public record LogoutRequest(string RefreshToken);

public record AuthTokens(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    bool IsNewCustomer,
    bool ProfileCompleted);
