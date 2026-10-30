using SalemBonus.Domain.Enums;
using SalemBonus.Domain.Pos.Inventory;
using SalemBonus.Domain.Pos.Sales;

namespace SalemBonus.Application.Common.Interfaces;

public record AnalyticsProduct(Guid Id, string Name, string? Article, Guid? NodeId, Guid? BrandId, string? BrandName, string? Unit,
    string? ImageUrl, decimal? MinStock, bool IsGoods, bool IsActive);

public record AnalyticsBonusTx(DateTime CreatedAt, BonusTransactionType Type, int Amount, Guid? ReceiptId, Guid StoreId);

public record AnalyticsDebtPayment(DateTime CreatedAt, decimal Amount, PaymentMethod? Method, bool IsReturn, Guid StaffUserId, Guid? RegisterId);

/// <summary>Клиенттің ұйым бойынша бүкіл тарихы (ТЗ «Статистика» §13: «жаңа» — компанияда алғаш сатып алған).</summary>
public record AnalyticsCustomerHistory(Guid CustomerId, DateTime FirstAt, DateTime LastAt, int Purchases, decimal Amount);

public record AnalyticsCustomer(Guid Id, string Name, string Phone);

public record AnalyticsBalance(Guid WarehouseId, Guid ProductId, decimal Quantity);

public record AnalyticsMovement(DateTime CreatedAt, Guid WarehouseId, Guid ProductId, StockMovementType Type, decimal Quantity);

public record AnalyticsDocument(Guid Id, Guid StoreId, Guid WarehouseId, StockDocumentType Type, StockDocumentStatus Status, DateTime Date, DateTime? PostedAt,
    Guid? SupplierId, decimal Total, decimal Paid, int Lines);

public record AnalyticsSupplierPayment(DateTime CreatedAt, DateOnly Date, Guid SupplierId, Guid? DocumentId, decimal Amount, PaymentMethod Method);

public record AnalyticsTransfer(DateTime? SentAt, DateTime? ReceivedAt, TransferStatus Status, Guid SourceWarehouseId, Guid DestinationWarehouseId, decimal Quantity);

/// <summary>
/// «Статистика» тек оқиды (ТЗ §1). Әр әдіс бір бизнес-фактіні бір рет әкеледі — JOIN-мен көбейтпейді (§26):
/// сатылым төлемімен бірге, жолдары бөлек жиын.
/// </summary>
public interface IAnalyticsRepository
{
    Task<IReadOnlyList<Sale>> SalesAsync(IReadOnlyCollection<Guid> storeIds, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    Task<IReadOnlyList<SaleReturn>> ReturnsAsync(IReadOnlyCollection<Guid> storeIds, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    Task<IReadOnlyList<AnalyticsDebtPayment>> DebtPaymentsAsync(IReadOnlyCollection<Guid> storeIds, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    /// <summary>Ағымдағы ашық қарыз (сан, қалдық) — кезең айналымы емес, «қазіргі сәтке» (§25).</summary>
    Task<(int Count, decimal Remaining)> OpenDebtsAsync(IReadOnlyCollection<Guid> storeIds, CancellationToken ct = default);

    Task<Dictionary<Guid, AnalyticsCustomerHistory>> CustomerHistoryAsync(Guid orgId, IReadOnlyCollection<Guid> customerIds, CancellationToken ct = default);
    /// <summary>Таңдалған дүкендерде бұрын сатып алған, бірақ берілген сәттен бері алмаған клиенттер (§13 «давно не покупали»).</summary>
    Task<IReadOnlyList<AnalyticsCustomerHistory>> InactiveCustomersAsync(IReadOnlyCollection<Guid> storeIds, DateTime lastBeforeUtc, int take, CancellationToken ct = default);
    Task<int> InactiveCustomersCountAsync(IReadOnlyCollection<Guid> storeIds, DateTime lastBeforeUtc, CancellationToken ct = default);
    /// <summary>Дүкендердің клиент базасы — бонус картасы ашылғандар (бір клиент бір рет).</summary>
    Task<int> CustomersInBaseAsync(IReadOnlyCollection<Guid> storeIds, CancellationToken ct = default);
    Task<Dictionary<Guid, AnalyticsCustomer>> CustomersAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);

    Task<IReadOnlyList<AnalyticsBonusTx>> BonusTransactionsAsync(IReadOnlyCollection<Guid> storeIds, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    Task<int> BonusBalanceAsync(IReadOnlyCollection<Guid> storeIds, CancellationToken ct = default);

    Task<IReadOnlyList<AnalyticsProduct>> ProductsAsync(Guid orgId, IReadOnlyCollection<Guid>? ids, CancellationToken ct = default);
    /// <summary>Әр түйін → оның түбір санаты (id, атауы): «Продажи по категориям» мен санат сүзгісі үшін.</summary>
    Task<Dictionary<Guid, (Guid RootId, string RootName)>> NodeRootsAsync(Guid orgId, CancellationToken ct = default);
    /// <summary>Іздеуге сәйкес тауарлар (ProductSearch: сөздер кез келген ретпен, барлық өріс — штрихкод, бренд, санат…).</summary>
    Task<HashSet<Guid>> ProductIdsMatchingAsync(Guid orgId, string search, CancellationToken ct = default);
    /// <summary>Кіріс бағасы дүкен бойынша: (дүкен, тауар) → баға. Белгісіз болса — жоқ.</summary>
    Task<Dictionary<(Guid StoreId, Guid ProductId), decimal>> PurchasePricesAsync(IReadOnlyCollection<Guid> storeIds, CancellationToken ct = default);

    Task<IReadOnlyList<(Guid Id, Guid StoreId, string Name, bool IsActive)>> WarehousesAsync(IReadOnlyCollection<Guid> storeIds, CancellationToken ct = default);
    Task<IReadOnlyList<AnalyticsBalance>> BalancesAsync(IReadOnlyCollection<Guid> warehouseIds, CancellationToken ct = default);
    Task<IReadOnlyList<AnalyticsMovement>> MovementsAsync(IReadOnlyCollection<Guid> warehouseIds, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    Task<Dictionary<(Guid WarehouseId, Guid ProductId), DateTime>> LastMovementsAsync(IReadOnlyCollection<Guid> warehouseIds, CancellationToken ct = default);
    Task<IReadOnlyList<AnalyticsTransfer>> TransfersAsync(IReadOnlyCollection<Guid> warehouseIds, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);

    /// <summary>Приёмка мен списание (черновиктер де — сүзгіні сервис қояды). Приёмка қарызы үшін — барлық уақыт.</summary>
    Task<IReadOnlyList<AnalyticsDocument>> DocumentsAsync(IReadOnlyCollection<Guid> storeIds, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct = default);
    Task<IReadOnlyList<AnalyticsSupplierPayment>> SupplierPaymentsAsync(IReadOnlyCollection<Guid> storeIds, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    Task<Dictionary<Guid, string>> SupplierNamesAsync(Guid orgId, CancellationToken ct = default);

    Task<Dictionary<Guid, string>> StaffNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
    Task<Dictionary<Guid, (string Name, Guid StoreId)>> RegistersAsync(IReadOnlyCollection<Guid> storeIds, CancellationToken ct = default);
}
