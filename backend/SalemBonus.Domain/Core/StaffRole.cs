namespace SalemBonus.Domain.Core;

/// <summary>Қызметкердің дүкендегі рөлі. Нақты рұқсаттар <see cref="StaffPermissions"/> ішінде.</summary>
public enum StaffRole
{
    /// <summary>Бизнестің иесі — барлығына рұқсат.</summary>
    Owner,
    /// <summary>Әкімші — иесі берген рұқсаттар шегінде.</summary>
    Admin,
    /// <summary>Кассир — сату және кассаға қатысты ғана.</summary>
    Cashier,
}
