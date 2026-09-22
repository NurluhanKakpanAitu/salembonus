namespace SalemBonus.Domain.Entities;

/// <summary>Refresh токен. Мәні хэштеліп сақталады, әр жаңартуда айналдырылады.</summary>
public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? Device { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive(DateTime now) => RevokedAt is null && ExpiresAt > now;
}
