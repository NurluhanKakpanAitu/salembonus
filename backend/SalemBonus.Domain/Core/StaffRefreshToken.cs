namespace SalemBonus.Domain.Core;

/// <summary>Қызметкер сеансы. Мәні тек httpOnly cookie-де, базада — хеші.</summary>
public class StaffRefreshToken
{
    public Guid Id { get; set; }
    public Guid StaffUserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? Device { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive(DateTime now) => RevokedAt is null && ExpiresAt > now;
}
