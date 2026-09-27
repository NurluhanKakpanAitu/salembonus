namespace SalemBonus.Api;

/// <summary>Қызметкер (SalemPos, кейін SalemZapis) аутентификациясының атаулары.</summary>
public static class StaffAuth
{
    public const string Scheme = "Staff";
    public const string Policy = "Staff";
    public const string LoginRateLimit = "staff-login";
    /// <summary>Refresh токені тек осы cookie-де: JavaScript оны оқи алмайды.</summary>
    public const string RefreshCookie = "salem_staff_rt";
    public const string RefreshCookiePath = "/api/staff/v1/auth";
}
