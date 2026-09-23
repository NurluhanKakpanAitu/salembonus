namespace SalemBonus.Application.Auth;

public class AuthOptions
{
    public const string Section = "Auth";

    public int OtpLength { get; set; } = 4;
    public int OtpLifetimeSeconds { get; set; } = 300;
    public int OtpResendSeconds { get; set; } = 60;
    public int OtpMaxAttempts { get; set; } = 5;
    /// <summary>Development-те кодты жауапта қайтару (демо мен тест үшін).</summary>
    public bool ReturnCodeInResponse { get; set; }
    /// <summary>Демо үшін тұрақты код (мысалы "1234"). Бос болса кездейсоқ код жасалады. Продакшнда бос болуы керек.</summary>
    public string? StaticOtpCode { get; set; }
}
