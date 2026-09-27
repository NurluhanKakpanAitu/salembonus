namespace SalemBonus.Domain.Core;

/// <summary>
/// Рұқсат кілттері. Рөл әдепкі жиынтықты береді, иесі оны әр қызметкерге кеңейте не тарылта алады.
/// Барлық тексеріс серверде — фронт тек батырманы жасырады.
/// </summary>
public static class StaffPermissions
{
    public const string StatisticsView = "statistics.view";
    public const string FinanceView = "finance.view";

    public const string ProductsView = "products.view";
    public const string ProductsCreate = "products.create";
    public const string ProductsEdit = "products.edit";
    /// <summary>Санат, топ, топша құрылымын өзгерту және тасымалдау.</summary>
    public const string CatalogStructure = "catalog.structure";
    /// <summary>Архивтеу, қалпына келтіру, өшіру.</summary>
    public const string CatalogLifecycle = "catalog.lifecycle";
    /// <summary>Бренд, өлшем бірлігі, сипаттамалар анықтамалықтары.</summary>
    public const string CatalogDictionaries = "catalog.dictionaries";

    public const string SalesCreate = "sales.create";
    public const string SalesReturn = "sales.return";
    public const string SalesDebt = "sales.debt";
    /// <summary>Қалдық жетпесе де сату.</summary>
    public const string SalesNegativeStock = "sales.negative_stock";
    /// <summary>Жеңілдік шегінен асқанда басқаның сатылымын растау.</summary>
    public const string DiscountApprove = "discount.approve";

    public const string StaffManage = "staff.manage";
    public const string SettingsManage = "settings.manage";

    public static readonly IReadOnlyList<string> All =
    [
        StatisticsView, FinanceView,
        ProductsView, ProductsCreate, ProductsEdit, CatalogStructure, CatalogLifecycle, CatalogDictionaries,
        SalesCreate, SalesReturn, SalesDebt, SalesNegativeStock, DiscountApprove,
        StaffManage, SettingsManage,
    ];

    /// <summary>Рөлдің әдепкі рұқсаттары.</summary>
    public static IReadOnlyList<string> DefaultsFor(StaffRole role) => role switch
    {
        StaffRole.Owner => All,
        StaffRole.Admin =>
        [
            StatisticsView, ProductsView, ProductsCreate, ProductsEdit, CatalogStructure, CatalogLifecycle,
            CatalogDictionaries, SalesCreate, SalesReturn, SalesDebt, DiscountApprove,
        ],
        StaffRole.Cashier => [ProductsView, SalesCreate],
        _ => [],
    };
}
