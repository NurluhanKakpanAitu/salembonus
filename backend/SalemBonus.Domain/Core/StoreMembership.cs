using SalemBonus.Domain.Entities;

namespace SalemBonus.Domain.Core;

/// <summary>Қызметкердің нақты дүкендегі рөлі мен рұқсаттары. Бір адам бірнеше дүкенде әртүрлі рөлде бола алады.</summary>
public class StoreMembership
{
    public Guid Id { get; set; }
    public Guid StaffUserId { get; set; }
    public Guid StoreId { get; set; }
    public StaffRole Role { get; set; }
    /// <summary>Нақты рұқсаттар. Жасалғанда рөлдің әдепкісімен толтырылады.</summary>
    public List<string> Permissions { get; set; } = [];
    /// <summary>Кассирдің өзі бере алатын ең үлкен жеңілдік (%). Асса — растау керек.</summary>
    public decimal MaxDiscountPercent { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public StaffUser? StaffUser { get; set; }
    public Store? Store { get; set; }

    public bool Has(string permission) => Role == StaffRole.Owner || Permissions.Contains(permission);
}
