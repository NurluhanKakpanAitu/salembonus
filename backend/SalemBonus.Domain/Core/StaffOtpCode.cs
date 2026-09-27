namespace SalemBonus.Domain.Core;

/// <summary>
/// Қызметкерге WhatsApp-пен жіберілген бір реттік код (қазір — құпиясөзді қалпына келтіру).
/// Код дұрыс енгізілсе, жаңа құпиясөз қоюға рұқсат беретін қысқа мерзімді токен шығарылады:
/// жаңа құпиясөз тек сол токенмен қабылданады.
/// </summary>
public class StaffOtpCode
{
    public const string PasswordReset = "password_reset";

    public Guid Id { get; set; }
    public Guid StaffUserId { get; set; }
    public string Purpose { get; set; } = PasswordReset;
    public string CodeHash { get; set; } = string.Empty;
    public int Attempts { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
    /// <summary>Код расталғаннан кейін берілетін рұқсат токенінің хеші.</summary>
    public string? ResetTokenHash { get; set; }
    public DateTime? ResetTokenExpiresAt { get; set; }
    public DateTime? ResetTokenUsedAt { get; set; }
    public string? Ip { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive(DateTime now) => ConsumedAt is null && ExpiresAt > now;

    public bool CanReset(DateTime now) =>
        ResetTokenHash is not null && ResetTokenUsedAt is null && ResetTokenExpiresAt > now;
}
