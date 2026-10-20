namespace SalemBonus.Domain.Core;

/// <summary>
/// Рұқсат кілттері. Рөл әдепкі жиынтықты береді, иесі оны әр қызметкерге кеңейте не тарылта алады.
/// Барлық тексеріс серверде — фронт тек батырманы жасырады.
/// </summary>
public static class StaffPermissions
{
    public const string StatisticsView = "statistics.view";
    /// <summary>«Статистика» экспорты (Excel, CSV, PDF).</summary>
    public const string StatisticsExport = "statistics.export";
    /// <summary>Клиенттің телефоны мен жеке деректері (статистикада, экспортта).</summary>
    public const string CustomersContacts = "customers.contacts";
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

    /// <summary>«Продажи» бөлімі: тарих, чек карточкасы, KPI (ТЗ «Продажи» §13).</summary>
    public const string SalesView = "sales.view";
    /// <summary>Сатылым тізімін Excel-ге экспорттау.</summary>
    public const string SalesExport = "sales.export";
    public const string SalesCreate = "sales.create";
    public const string SalesReturn = "sales.return";
    public const string SalesDebt = "sales.debt";
    /// <summary>Қалдық жетпесе де сату.</summary>
    public const string SalesNegativeStock = "sales.negative_stock";
    /// <summary>Жеңілдік шегінен асқанда басқаның сатылымын растау.</summary>
    public const string DiscountApprove = "discount.approve";

    public const string StaffManage = "staff.manage";
    public const string SettingsManage = "settings.manage";

    // «Склад» (ТЗ «Склад» §16)
    public const string InventoryView = "inventory.view";
    /// <summary>Приёмка жасау мен черновикті өңдеу.</summary>
    public const string InventoryReceive = "inventory.receive";
    /// <summary>Приёмканы проведение/сторно, төлем тіркеу.</summary>
    public const string InventoryPost = "inventory.post";
    /// <summary>Перемещение: жасау, жіберу, қабылдау.</summary>
    public const string InventoryTransfer = "inventory.transfer";
    public const string InventoryWriteOff = "inventory.writeoff";
    public const string InventoryWarehouses = "inventory.warehouses";
    public const string InventorySuppliers = "inventory.suppliers";

    public static readonly IReadOnlyList<string> All =
    [
        StatisticsView, FinanceView, StatisticsExport, CustomersContacts,
        ProductsView, ProductsCreate, ProductsEdit, CatalogStructure, CatalogLifecycle, CatalogDictionaries,
        SalesCreate, SalesReturn, SalesDebt, SalesNegativeStock, DiscountApprove, SalesView, SalesExport,
        StaffManage, SettingsManage,
        InventoryView, InventoryReceive, InventoryPost, InventoryTransfer, InventoryWriteOff, InventoryWarehouses, InventorySuppliers,
    ];

    /// <summary>Рөлдің әдепкі рұқсаттары.</summary>
    public static IReadOnlyList<string> DefaultsFor(StaffRole role) => role switch
    {
        StaffRole.Owner => All,
        StaffRole.Admin =>
        [
            StatisticsView, ProductsView, ProductsCreate, ProductsEdit, CatalogStructure, CatalogLifecycle,
            CatalogDictionaries, SalesCreate, SalesReturn, SalesDebt, DiscountApprove, SalesView, SalesExport,
            InventoryView, InventoryReceive, InventoryPost, InventoryTransfer, InventoryWriteOff, InventoryWarehouses, InventorySuppliers,
            StatisticsExport, CustomersContacts,
        ],
        // Кассир тек сатады: касса экранының каталогы өз эндпоинтімен (SalesCreate) жүреді,
        // «Товар» бөлімі мен оның API-і кассирге ашылмайды. Иесі қажет болса жеке кеңейтеді.
        // Кассир қалдықты өз дүкені бойынша көреді (ТЗ «Склад» §16), құжат жасамайды.
        StaffRole.Cashier => [SalesCreate, InventoryView],
        _ => [],
    };
}
