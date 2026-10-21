using SalemBonus.Domain.Enums;

namespace SalemBonus.Domain.Entities;

/// <summary>
/// Клиенттің push алатын құрылғысы (App Store / Google Play қосымшасы). Токенді FCM не APNs береді;
/// бір токен — бір құрылғы, сондықтан басқа аккаунт сол телефоннан кірсе, токен соған көшеді.
/// </summary>
public class PushDevice
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public PushPlatform Platform { get; set; }
    public string Token { get; set; } = string.Empty;
    /// <summary>Хабарлама мәтінінің тілі (kk, ru) — қосымшада таңдалғаны.</summary>
    public string Language { get; set; } = "kk";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>Қосымша токенді соңғы рет тіркеген уақыт (әр кірген сайын жаңарады).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
