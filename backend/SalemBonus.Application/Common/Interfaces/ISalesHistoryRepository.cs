using SalemBonus.Domain.Pos.Sales;

namespace SalemBonus.Application.Common.Interfaces;

/// <summary>
/// «Продажи» бөлімінің сүзгісі (ТЗ «Продажи» §6). Status: Completed / PartiallyReturned / Returned / Debt
/// (ашық қарызы бар чек). Sort: number / date / total / returned / net / status.
/// </summary>
public record SalesHistoryFilter(
    Guid StoreId, DateTime? FromUtc, DateTime? ToUtc, string? Search, Guid? RegisterId, Guid? StaffUserId,
    PaymentMethod? Method, string? Status, bool? HasReturn, Guid? CustomerId, bool NoCustomer,
    string? Sort, bool Desc, int Skip, int Take);

/// <summary>Тізімнің жиынтығы — сол сүзгімен (KPI мен кесте бір дерек, ТЗ §3).</summary>
public record SalesHistoryStats(
    int Count, decimal Total, decimal Returned, decimal Units,
    decimal Discounts, int DiscountCount, int BonusRedeemed, int BonusCount, int WithReturn,
    int DebtCount, decimal DebtAmount, decimal DebtOpen);

/// <summary>Қайтарымдар — қайтару күні бойынша (ТЗ §11: «Доля возвратов» операция күнімен).</summary>
public record ReturnsStats(int Count, decimal Amount);

public record OpenDebtInfo(DebtStatus Status, decimal Remaining);

public interface ISalesHistoryRepository
{
    Task<(IReadOnlyList<Sale> Items, int Total)> SearchAsync(SalesHistoryFilter f, CancellationToken ct = default);
    Task<SalesHistoryStats> StatsAsync(SalesHistoryFilter f, CancellationToken ct = default);
    Task<ReturnsStats> ReturnsStatsAsync(Guid storeId, DateTime? fromUtc, DateTime? toUtc, Guid? registerId, Guid? staffUserId, CancellationToken ct = default);
    /// <summary>Чектегі бірлік саны (тауар × саны) — тізім бағаны үшін.</summary>
    Task<Dictionary<Guid, decimal>> UnitsBySaleAsync(IReadOnlyCollection<Guid> saleIds, CancellationToken ct = default);
    Task<Dictionary<Guid, OpenDebtInfo>> DebtsBySaleAsync(IReadOnlyCollection<Guid> saleIds, CancellationToken ct = default);
    Task<IReadOnlyList<DebtPayment>> DebtPaymentsBySaleAsync(Guid saleId, CancellationToken ct = default);
    /// <summary>Дүкеннің белсенді қызметкерлері — «Кассир» сүзгісі үшін.</summary>
    Task<IReadOnlyList<(Guid Id, string Name)>> StoreStaffAsync(Guid storeId, CancellationToken ct = default);
}
