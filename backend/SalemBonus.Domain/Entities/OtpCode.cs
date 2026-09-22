namespace SalemBonus.Domain.Entities;

/// <summary>Телефонға жіберілген бір реттік код. Код хэштеліп сақталады.</summary>
public class OtpCode
{
    public Guid Id { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string CodeHash { get; set; } = string.Empty;
    public int Attempts { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive(DateTime now) => ConsumedAt is null && ExpiresAt > now;
}
