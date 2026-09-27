namespace SalemBonus.Application.Core.Staff;

/// <summary>Қызметкер кіруінің баптаулары. ТЗ бойынша құпиясөз талаптары серверде бапталады.</summary>
public class StaffAuthOptions
{
    public const string Section = "StaffAuth";

    public int OtpLength { get; set; } = 4;
    public int OtpLifetimeSeconds { get; set; } = 300;
    public int OtpResendSeconds { get; set; } = 60;
    public int OtpMaxAttempts { get; set; } = 5;
    /// <summary>Бір қызметкерге сағатына ең көп код сұрауы.</summary>
    public int OtpMaxPerHour { get; set; } = 5;
    /// <summary>Код расталғаннан кейін жаңа құпиясөз қоюға берілетін уақыт.</summary>
    public int ResetTokenMinutes { get; set; } = 10;
    public int MinPasswordLength { get; set; } = 8;
    public bool RequireLetterAndDigit { get; set; } = true;
    /// <summary>Тек Development: кодты жауапта қайтару (WhatsApp шаблоны бекітілгенше тест үшін).</summary>
    public bool ReturnCodeInResponse { get; set; }
}
