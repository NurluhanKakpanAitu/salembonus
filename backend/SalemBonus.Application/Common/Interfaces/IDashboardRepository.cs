using SalemBonus.Domain.Enums;
using SalemBonus.Domain.Pos.Sales;

namespace SalemBonus.Application.Common.Interfaces;

public record DashboardProduct(Guid Id, string Name, string? ImageUrl, Guid? NodeId);

public record DashboardBonusTx(DateTime CreatedAt, BonusTransactionType Type, int Amount);

/// <summary>Статистика үшін оқу сұраныстары (тек оқу, ештеңе өзгермейді).</summary>
public interface IDashboardRepository
{
    Task<IReadOnlyList<Sale>> SalesWithItemsAsync(Guid storeId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    Task<IReadOnlyList<DashboardBonusTx>> BonusTransactionsAsync(Guid storeId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    /// <summary>Осы дүкенде картасы ашылған клиенттер саны (дүкен үшін «жаңа клиент»).</summary>
    Task<int> NewCustomersAsync(Guid storeId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    Task<Dictionary<Guid, CustomerLevel>> LevelsAsync(Guid storeId, IReadOnlyCollection<Guid> customerIds, CancellationToken ct = default);
    Task<int> TotalBonusBalanceAsync(Guid storeId, CancellationToken ct = default);
    Task<IReadOnlyList<DashboardProduct>> ProductsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
    /// <summary>Белсенді тауарлар (қызметтен басқа) бойынша: аз қалғаны және мүлдем жоқтары.</summary>
    Task<(int Low, int Out)> StockAlertsAsync(Guid orgId, Guid warehouseId, decimal threshold, CancellationToken ct = default);
    Task<(int Count, decimal Sum, int Overdue)> OpenDebtsAsync(Guid storeId, DateOnly today, CancellationToken ct = default);
}
