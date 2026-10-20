using SalemBonus.Domain.Pos.Finance;
using SalemBonus.Domain.Pos.Inventory;
using SalemBonus.Domain.Pos.Sales;

namespace SalemBonus.Application.Common.Interfaces;

/// <summary>Операциялар тізімінің сүзгісі (ТЗ «Финансы» §6, §8).</summary>
public record FinanceOperationFilter(
    IReadOnlyCollection<Guid> StoreIds, DateTime? FromUtc, DateTime? ToUtc, Guid? AccountId, FinanceOperationType? Type,
    IReadOnlyCollection<FinanceOperationType>? Types, Guid? CategoryId, string? SourceModule, CounterpartyType? CounterpartyType, Guid? CounterpartyId,
    FinanceOperationStatus? Status, Guid? StaffUserId, string? Search, decimal? MinAmount, decimal? MaxAmount, bool ManualOnly,
    string? Sort, bool Desc, int Skip, int Take);

public record FinanceEntryRow(FinanceEntry Entry, FinanceOperation Operation);

/// <summary>Модуль қосылғанға дейінгі ақша қозғалысы (тарихты шоттарға бір рет жазу үшін): әр жол — бір бастапқы операция.</summary>
public record BackfillSource(string Module, FinanceOperationType Type, Guid StoreId, Guid SourceOperationId, Guid? SourceDocumentId, DateTime Date,
    IReadOnlyList<(PaymentMethod Method, decimal Amount)> Lines, string Description, CounterpartyType? CounterpartyType, Guid? CounterpartyId, string? CounterpartyName,
    string? DocumentNumber);

public interface IFinanceRepository
{
    // Шоттар
    Task<IReadOnlyList<FinanceAccount>> ListAccountsAsync(IReadOnlyCollection<Guid> storeIds, CancellationToken ct = default);
    Task<FinanceAccount?> GetAccountAsync(Guid orgId, Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<FinanceAccount>> GetAccountsForUpdateAsync(Guid orgId, IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
    Task<bool> AccountNameExistsAsync(Guid storeId, string name, Guid? excludeId, CancellationToken ct = default);
    void AddAccount(FinanceAccount account);
    Task<bool> AccountHasEntriesAsync(Guid accountId, CancellationToken ct = default);
    /// <summary>Берілген сәтке дейінгі проводкалар қосындысы (кезең басындағы қалдық).</summary>
    Task<Dictionary<Guid, decimal>> BalancesBeforeAsync(IReadOnlyCollection<Guid> accountIds, DateTime beforeUtc, CancellationToken ct = default);
    Task<Dictionary<Guid, decimal>> SumEntriesAsync(IReadOnlyCollection<Guid> accountIds, CancellationToken ct = default);

    // Санаттар
    Task<IReadOnlyList<FinanceCategory>> ListCategoriesAsync(Guid orgId, CancellationToken ct = default);
    Task<FinanceCategory?> GetCategoryAsync(Guid orgId, Guid id, CancellationToken ct = default);
    Task<bool> CategoryNameExistsAsync(Guid orgId, string name, FinanceCategoryType type, Guid? excludeId, CancellationToken ct = default);
    void AddCategory(FinanceCategory category);

    // Операциялар
    Task<long> NextNumberAsync(Guid orgId, CancellationToken ct = default);
    Task<FinanceOperation?> GetOperationAsync(Guid orgId, Guid id, CancellationToken ct = default);
    Task<FinanceOperation?> GetOperationForUpdateAsync(Guid orgId, Guid id, CancellationToken ct = default);
    Task<FinanceOperation?> FindBySourceAsync(Guid orgId, Guid sourceOperationId, CancellationToken ct = default);
    Task<FinanceOperation?> FindByClientRequestAsync(Guid orgId, Guid clientRequestId, CancellationToken ct = default);
    void AddOperation(FinanceOperation operation);
    Task<(IReadOnlyList<FinanceOperation> Items, int Total)> SearchOperationsAsync(FinanceOperationFilter f, CancellationToken ct = default);
    /// <summary>Кезеңдегі проведённый операциялар (есептер үшін, беттеусіз).</summary>
    Task<IReadOnlyList<FinanceOperation>> PostedOperationsAsync(IReadOnlyCollection<Guid> storeIds, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    Task<IReadOnlyList<FinanceEntryRow>> StatementAsync(Guid accountId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> PostedSourceOperationIdsAsync(Guid orgId, CancellationToken ct = default);
    /// <summary>Сүзгі бойынша жиын: кіріс, шығыс, аударым (тек күшіндегі операциялар — Posted және сторно емес).</summary>
    Task<(decimal Inflow, decimal Outflow, decimal Transfers, int Count)> SummarizeOperationsAsync(FinanceOperationFilter f, CancellationToken ct = default);
    /// <summary>Бастапқы операция id → қаржы операциясы (төлем жолында «ФД-…» сілтемесі үшін).</summary>
    Task<Dictionary<Guid, (Guid Id, string Number)>> OperationsBySourceAsync(IReadOnlyCollection<Guid> sourceIds, CancellationToken ct = default);
    /// <summary>Түпнұсқа id → оны сторнолаған операция id.</summary>
    Task<Dictionary<Guid, Guid>> ReversalsOfAsync(IReadOnlyCollection<Guid> originalIds, CancellationToken ct = default);
    void RemoveOperation(FinanceOperation operation);

    // Қарыздар (бастапқы құжаттар)
    Task<IReadOnlyList<Debt>> DebtsAsync(IReadOnlyCollection<Guid> storeIds, bool openOnly, CancellationToken ct = default);
    Task<IReadOnlyList<Debt>> DebtsForUpdateAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
    Task<IReadOnlyList<DebtPayment>> DebtPaymentsAsync(IReadOnlyCollection<Guid> debtIds, CancellationToken ct = default);
    Task<IReadOnlyList<StockDocument>> SupplierDocumentsAsync(IReadOnlyCollection<Guid> storeIds, CancellationToken ct = default);
    Task<IReadOnlyList<SupplierPayment>> SupplierPaymentsAsync(IReadOnlyCollection<Guid> storeIds, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct = default);
    Task<Dictionary<Guid, (string Name, string? Phone, string? TaxId, string? Address)>> SuppliersAsync(Guid orgId, CancellationToken ct = default);
    Task<Dictionary<Guid, (string Name, string Phone)>> CustomersAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
    Task<Dictionary<Guid, string>> StaffNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
    Task<Dictionary<Guid, string>> StoreNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
    Task<IReadOnlyList<BackfillSource>> BackfillSourcesAsync(Guid orgId, CancellationToken ct = default);
}
