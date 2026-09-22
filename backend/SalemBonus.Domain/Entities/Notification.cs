using SalemBonus.Domain.Enums;

namespace SalemBonus.Domain.Entities;

public class Notification
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Guid? StoreId { get; set; }
    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    /// <summary>Қосымша жол: сатып алу сомасы, мерзімі және т.б.</summary>
    public string? Detail { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Store? Store { get; set; }

    public NotificationCategory Category => Type switch
    {
        NotificationType.BonusAccrued or NotificationType.BonusRedeemed or NotificationType.Birthday => NotificationCategory.Bonus,
        NotificationType.Promo => NotificationCategory.Promo,
        NotificationType.StoreAdded => NotificationCategory.Store,
        _ => NotificationCategory.System,
    };
}
