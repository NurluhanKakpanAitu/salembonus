namespace SalemBonus.Api;

/// <summary>Қызметкер (SalemPos, кейін SalemZapis) аутентификациясының атаулары.</summary>
public static class StaffAuth
{
    public const string Scheme = "Staff";
    public const string Policy = "Staff";
    public const string LoginRateLimit = "staff-login";
    /// <summary>Құпиясөзді қалпына келтіру сұраныстары (код жіберу, тексеру, жаңа құпиясөз).</summary>
    public const string ResetRateLimit = "staff-reset";
    /// <summary>PIN-мен кіру мен кассаны ашу.</summary>
    public const string PinRateLimit = "staff-pin";
    /// <summary>Касса құрылғысының кілті — тек осы cookie-де, JavaScript оны оқи алмайды.</summary>
    public const string RegisterCookie = "salem_register";
    /// <summary>Бүкіл POS API-ға: сатылым да қай кассадан жасалғанын білуі керек.</summary>
    public const string RegisterCookiePath = "/api/pos/v1";
    /// <summary>Бұрынғы тар жол — ескі cookie-ді жаңа жолға көшіру үшін.</summary>
    public const string LegacyRegisterCookiePath = "/api/pos/v1/registers";
    /// <summary>Refresh токені тек осы cookie-де: JavaScript оны оқи алмайды.</summary>
    public const string RefreshCookie = "salem_staff_rt";
    public const string RefreshCookiePath = "/api/staff/v1/auth";
}
