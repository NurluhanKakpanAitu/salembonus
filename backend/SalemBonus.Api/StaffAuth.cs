namespace SalemBonus.Api;

/// <summary>Қызметкер (SalemPos, кейін SalemZapis) аутентификациясының атаулары.</summary>
public static class StaffAuth
{
    public const string Scheme = "Staff";
    public const string Policy = "Staff";
    public const string LoginRateLimit = "staff-login";
    /// <summary>Құпиясөзді қалпына келтіру сұраныстары (код жіберу, тексеру, жаңа құпиясөз).</summary>
    public const string ResetRateLimit = "staff-reset";
    /// <summary>Refresh токені тек осы cookie-де: JavaScript оны оқи алмайды.</summary>
    public const string RefreshCookie = "salem_staff_rt";
    public const string RefreshCookiePath = "/api/staff/v1/auth";
}
