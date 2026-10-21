namespace SalemBonus.Infrastructure.Identity;

public class JwtOptions
{
    public const string Section = "Jwt";

    public string Issuer { get; set; } = "salembonus";
    public string Audience { get; set; } = "salembonus-app";
    /// <summary>Қызметкер токенінің audience-і. Барлық өнімге ортақ (SalemPos, кейін SalemZapis).</summary>
    public string StaffAudience { get; set; } = "salem-staff";
    /// <summary>SalemTech әкімшісінің токені (admin.salemtech.kz) — бизнес API-ына жарамайды.</summary>
    public string AdminAudience { get; set; } = "salem-admin";
    /// <summary>Әкімші сеансы refresh-сіз: жұмыс күні бойы жарамды, сосын қайта кіреді.</summary>
    public int AdminAccessTokenHours { get; set; } = 10;
    /// <summary>Кемінде 32 таңба. Продакшнда орта айнымалысынан (Jwt__Key) беріледі.</summary>
    public string Key { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 30;
}
