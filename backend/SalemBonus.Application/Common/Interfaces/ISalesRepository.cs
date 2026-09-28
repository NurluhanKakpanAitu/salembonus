using SalemBonus.Domain.Entities;
using SalemBonus.Domain.Pos.Catalog;
using SalemBonus.Domain.Pos.Inventory;
using SalemBonus.Domain.Pos.Sales;

namespace SalemBonus.Application.Common.Interfaces;

public enum CashierSort { Popular, PriceAsc, PriceDesc, Stock, Name }

public record CashierCatalogFilter(string? Search, string? NodePath, CashierSort Sort, int Skip, int Take);

/// <summary>Касса каталогының жолы: тауар + осы дүкендегі баға мен қалдық.</summary>
public record CashierCatalogRow(Product Product, decimal? Price, decimal Stock);

public interface ISalesRepository
{
    /// <summary>Кассаға көрінетін тауарлар (белсенді, «кассада жасыру» емес), баға мен қалдығымен.</summary>
    Task<(IReadOnlyList<CashierCatalogRow> Items, int Total)> SearchCatalogAsync(Guid orgId, Guid storeId, Guid warehouseId,
        CashierCatalogFilter filter, CancellationToken ct = default);
    /// <summary>Кез келген байланған штрихкод бойынша (сканер).</summary>
    Task<CashierCatalogRow?> FindByBarcodeAsync(Guid orgId, Guid storeId, Guid warehouseId, string barcode, CancellationToken ct = default);

    Task<IReadOnlyList<Product>> ListProductsAsync(Guid orgId, IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
    Task<Dictionary<Guid, decimal>> PricesAsync(Guid storeId, IReadOnlyCollection<Guid> productIds, CancellationToken ct = default);
    Task<Dictionary<Guid, StockBalance>> BalancesForUpdateAsync(Guid warehouseId, IReadOnlyCollection<Guid> productIds, CancellationToken ct = default);

    /// <summary>Келесі чек нөмірі. Транзакция ішінде шақырылады: жол бұғатталады, параллель сатылым күтеді.</summary>
    Task<long> NextReceiptNumberAsync(Guid storeId, CancellationToken ct = default);
    Task<Sale?> FindByClientRequestAsync(Guid storeId, Guid clientRequestId, CancellationToken ct = default);
    Task<Sale?> GetSaleAsync(Guid storeId, Guid id, CancellationToken ct = default);
    void AddSale(Sale sale);

    Task<StoreCashierSettings?> GetSettingsAsync(Guid storeId, CancellationToken ct = default);
    Task<IReadOnlyList<TransferRecipient>> ListRecipientsAsync(Guid storeId, CancellationToken ct = default);

    /// <summary>Аты-жөні бойынша іздеу — тек осы дүкенде картасы бар клиенттер (бөтен бизнестің клиенті көрінбейді).</summary>
    Task<IReadOnlyList<Customer>> SearchStoreCustomersAsync(Guid storeId, string term, int take, CancellationToken ct = default);
    /// <summary>Растай алатын қызметкерлер (жеңілдік шегінен асқанда).</summary>
    Task<IReadOnlyList<(Guid Id, string Name)>> ListApproversAsync(Guid storeId, string permission, CancellationToken ct = default);
}
