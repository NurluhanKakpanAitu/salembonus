namespace SalemBonus.Domain.Core;

/// <summary>
/// Бизнес (SalemPos клиенті). SalemTech әкімшілігі басқарады: қосылған модульдер, тариф пен ай сайынғы төлем,
/// сынақ мерзімі, қай күнге дейін төленгені, байланыс адамы. <see cref="IsActive"/> = false — бизнес тоқтатылған:
/// қызметкерлері кіре алмайды, ашық сеанстары келесі сұраныста жабылады.
/// </summary>
public class Organization
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Bin { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Қосылған модульдер (<see cref="PlatformModules"/>). Әдепкіде — бәрі.</summary>
    public List<string> Modules { get; set; } = [.. PlatformModules.All];
    public Guid? PlanId { get; set; }
    /// <summary>Жеке баға (айына, ₸). Бос болса — тарифтің бағасы.</summary>
    public decimal? MonthlyFee { get; set; }
    public DateOnly? TrialUntil { get; set; }
    /// <summary>Төленген соңғы күн — жойылмаған төлемдердің ең кеш кезеңінен есептеледі.</summary>
    public DateOnly? PaidUntil { get; set; }
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
    public string? City { get; set; }
    public string? Comment { get; set; }
    public DateTime? SuspendedAt { get; set; }
    public string? SuspendReason { get; set; }

    /// <summary>Рұқсаттың модулі қосулы ма (ядро рұқсаттары — әрқашан).</summary>
    public bool HasModuleFor(string permission) => PlatformModules.ForPermission(permission) is not { } module || Modules.Contains(module);
}
