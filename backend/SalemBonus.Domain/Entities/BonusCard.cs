using SalemBonus.Domain.Enums;

namespace SalemBonus.Domain.Entities;

/// <summary>
/// Тұтынушының нақты бір дүкендегі бонус шоты. Бонус дүкендер арасында ортақ емес.
/// </summary>
public class BonusCard
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Guid StoreId { get; set; }
    public int Balance { get; set; }
    public decimal TotalSpent { get; set; }
    public CustomerLevel Level { get; set; } = CustomerLevel.New;
    /// <summary>SalemPos бағдарламасының мәртебесі (ТЗ «Бонусы» §3); бос — ескі баспалдақ (<see cref="Level"/>).</summary>
    public Guid? StatusId { get; set; }
    /// <summary>Мәртебе қолмен берілген — ережелер оны автоматты өзгертпейді.</summary>
    public bool StatusManual { get; set; }
    public string? StatusReason { get; set; }
    public DateTime? StatusAssignedAt { get; set; }
    /// <summary>Мәртебе мерзімі; өткенде автотөмендету шарттарды қайта тексереді.</summary>
    public DateTime? StatusExpiresAt { get; set; }
    /// <summary>Клиент осы дүкеннің таратуларынан бас тартты (§6.3, §16).</summary>
    public bool NotificationsOptOut { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Store? Store { get; set; }
}
