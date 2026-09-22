namespace SalemBonus.Infrastructure.Identity;

public class JwtOptions
{
    public const string Section = "Jwt";

    public string Issuer { get; set; } = "salembonus";
    public string Audience { get; set; } = "salembonus-app";
    /// <summary>Кемінде 32 таңба. Продакшнда орта айнымалысынан (Jwt__Key) беріледі.</summary>
    public string Key { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 30;
}
