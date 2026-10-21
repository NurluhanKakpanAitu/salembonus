namespace SalemBonus.Domain.Core;

/// <summary>
/// Бизнеске қосылатын модульдер (SalemTech әкімшілігі басқарады). Модуль өшірулі болса — оның бөлімі мен API-ы
/// иесіне де жабық: рұқсат тексерісі (<see cref="StoreMembership.Has"/>) модульді де қарайды.
/// «Сотрудники», «Настройки» және клиент байланыстары — ядро, әрқашан қосулы.
/// </summary>
public static class PlatformModules
{
    public const string Cashier = "cashier";
    public const string Products = "products";
    public const string Warehouse = "warehouse";
    public const string Sales = "sales";
    public const string Statistics = "statistics";
    public const string Finance = "finance";
    public const string Bonus = "bonus";
    public const string Clients = "clients";

    public static readonly IReadOnlyList<string> All = [Cashier, Products, Warehouse, Sales, Statistics, Finance, Bonus, Clients];

    /// <summary>Рұқсат қай модульге жатады; null — ядро (әрқашан қолжетімді).</summary>
    public static string? ForPermission(string permission) => permission switch
    {
        StaffPermissions.SalesCreate or StaffPermissions.SalesReturn or StaffPermissions.SalesDebt or StaffPermissions.SalesNegativeStock
            or StaffPermissions.DiscountApprove => Cashier,
        StaffPermissions.SalesView or StaffPermissions.SalesExport => Sales,
        StaffPermissions.StatisticsView or StaffPermissions.StatisticsExport => Statistics,
        _ when permission.StartsWith("products.") || permission.StartsWith("catalog.") => Products,
        _ when permission.StartsWith("inventory.") => Warehouse,
        _ when permission.StartsWith("finance.") => Finance,
        _ when permission.StartsWith("bonus.") => Bonus,
        _ when permission.StartsWith("clients.") => Clients,
        _ => null,
    };
}
