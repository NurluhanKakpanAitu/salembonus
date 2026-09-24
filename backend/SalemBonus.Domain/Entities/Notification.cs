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
    /// <summary>
    /// Мәтін үлгісінің кілті (bonus_accrued, level_up, ...). Қосымша мәтінді осы кілт пен төмендегі
    /// өрістерден өз тілінде құрастырады. Дүкен жазған еркін мәтінде бос болады — сонда Title/Body көрсетіледі.
    /// </summary>
    public string? TemplateKey { get; set; }
    /// <summary>Есептелген/шегерілген бонус.</summary>
    public int? Amount { get; set; }
    /// <summary>Сатып алу сомасы.</summary>
    public decimal? PurchaseAmount { get; set; }
    /// <summary>Жаңа деңгей кілті (New, Regular, Favorite, Vip).</summary>
    public string? LevelKey { get; set; }
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
