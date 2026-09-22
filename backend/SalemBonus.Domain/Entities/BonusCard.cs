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
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Store? Store { get; set; }
}
