using Microsoft.EntityFrameworkCore;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Domain.Pos.Finance;
using SalemBonus.Domain.Pos.Inventory;
using SalemBonus.Domain.Pos.Sales;

namespace SalemBonus.Infrastructure.Persistence.Repositories;

public class FinanceRepository(AppDbContext db) : IFinanceRepository
{
    // ---------- Шоттар ----------

    public async Task<IReadOnlyList<FinanceAccount>> ListAccountsAsync(IReadOnlyCollection<Guid> storeIds, CancellationToken ct = default) =>
        await db.FinanceAccounts.AsNoTracking().Where(a => storeIds.Contains(a.StoreId))
            .OrderBy(a => a.SortOrder).ThenBy(a => a.CreatedAt).ToListAsync(ct);

    public Task<FinanceAccount?> GetAccountAsync(Guid orgId, Guid id, CancellationToken ct = default) =>
        db.FinanceAccounts.FirstOrDefaultAsync(a => a.OrganizationId == orgId && a.Id == id, ct);

    public async Task<IReadOnlyList<FinanceAccount>> GetAccountsForUpdateAsync(Guid orgId, IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
        ids.Count == 0 ? [] : await db.FinanceAccounts
            .FromSql($"""SELECT * FROM pos.finance_accounts WHERE "OrganizationId" = {orgId} AND "Id" = ANY({ids.ToArray()}) ORDER BY "Id" FOR UPDATE""")
            .ToListAsync(ct);

    public Task<bool> AccountNameExistsAsync(Guid storeId, string name, Guid? excludeId, CancellationToken ct = default) =>
        db.FinanceAccounts.AnyAsync(a => a.StoreId == storeId && (excludeId == null || a.Id != excludeId) && (a.Name == name || a.Name.ToLower() == name.ToLower()), ct);

    public void AddAccount(FinanceAccount account) => db.FinanceAccounts.Add(account);

    public Task<bool> AccountHasEntriesAsync(Guid accountId, CancellationToken ct = default) =>
        db.FinanceEntries.AnyAsync(e => e.AccountId == accountId, ct);

    public async Task<Dictionary<Guid, decimal>> BalancesBeforeAsync(IReadOnlyCollection<Guid> accountIds, DateTime beforeUtc, CancellationToken ct = default) =>
        accountIds.Count == 0 ? [] : await db.FinanceEntries.Where(e => accountIds.Contains(e.AccountId) && e.Date < beforeUtc)
            .GroupBy(e => e.AccountId).Select(g => new { g.Key, Sum = g.Sum(e => e.Amount) }).ToDictionaryAsync(x => x.Key, x => x.Sum, ct);

    public async Task<Dictionary<Guid, decimal>> SumEntriesAsync(IReadOnlyCollection<Guid> accountIds, CancellationToken ct = default) =>
        accountIds.Count == 0 ? [] : await db.FinanceEntries.Where(e => accountIds.Contains(e.AccountId))
            .GroupBy(e => e.AccountId).Select(g => new { g.Key, Sum = g.Sum(e => e.Amount) }).ToDictionaryAsync(x => x.Key, x => x.Sum, ct);

    // ---------- Санаттар ----------

    public async Task<IReadOnlyList<FinanceCategory>> ListCategoriesAsync(Guid orgId, CancellationToken ct = default) =>
        await db.FinanceCategories.AsNoTracking().Where(c => c.OrganizationId == orgId).OrderBy(c => c.Type).ThenBy(c => c.SortOrder).ThenBy(c => c.Name).ToListAsync(ct);

    public Task<FinanceCategory?> GetCategoryAsync(Guid orgId, Guid id, CancellationToken ct = default) =>
        db.FinanceCategories.FirstOrDefaultAsync(c => c.OrganizationId == orgId && c.Id == id, ct);

    public Task<bool> CategoryNameExistsAsync(Guid orgId, string name, FinanceCategoryType type, Guid? excludeId, CancellationToken ct = default) =>
        db.FinanceCategories.AnyAsync(c => c.OrganizationId == orgId && c.Type == type && (excludeId == null || c.Id != excludeId) && (c.Name == name || c.Name.ToLower() == name.ToLower()), ct);

    public void AddCategory(FinanceCategory category) => db.FinanceCategories.Add(category);

    // ---------- Операциялар ----------

    public async Task<long> NextNumberAsync(Guid orgId, CancellationToken ct = default)
    {
        var numbers = await db.Database.SqlQuery<long>($"""
            INSERT INTO pos.document_counters ("OrganizationId", "Kind", "LastNumber") VALUES ({orgId}, {"finance"}, 1)
            ON CONFLICT ("OrganizationId", "Kind") DO UPDATE SET "LastNumber" = pos.document_counters."LastNumber" + 1
            RETURNING "LastNumber" AS "Value"
            """).ToListAsync(ct);
        return numbers[0];
    }

    public Task<FinanceOperation?> GetOperationAsync(Guid orgId, Guid id, CancellationToken ct = default) =>
        db.FinanceOperations.AsNoTracking().Include(o => o.Entries).FirstOrDefaultAsync(o => o.OrganizationId == orgId && o.Id == id, ct);

    public Task<FinanceOperation?> GetOperationForUpdateAsync(Guid orgId, Guid id, CancellationToken ct = default) =>
        db.FinanceOperations.Include(o => o.Entries).FirstOrDefaultAsync(o => o.OrganizationId == orgId && o.Id == id, ct);

    public Task<FinanceOperation?> FindBySourceAsync(Guid orgId, Guid sourceOperationId, CancellationToken ct = default) =>
        db.FinanceOperations.Include(o => o.Entries).FirstOrDefaultAsync(o => o.OrganizationId == orgId && o.SourceOperationId == sourceOperationId, ct);

    public Task<FinanceOperation?> FindByClientRequestAsync(Guid orgId, Guid clientRequestId, CancellationToken ct = default) =>
        db.FinanceOperations.Include(o => o.Entries).FirstOrDefaultAsync(o => o.OrganizationId == orgId && o.ClientRequestId == clientRequestId, ct);

    public void AddOperation(FinanceOperation operation) => db.FinanceOperations.Add(operation);

    public void RemoveOperation(FinanceOperation operation) => db.FinanceOperations.Remove(operation);

    public async Task<Dictionary<Guid, Guid>> ReversalsOfAsync(IReadOnlyCollection<Guid> originalIds, CancellationToken ct = default) =>
        originalIds.Count == 0 ? [] : await db.FinanceOperations.AsNoTracking().Where(o => o.ReversalOfId != null && originalIds.Contains(o.ReversalOfId.Value))
            .Select(o => new { Original = o.ReversalOfId!.Value, o.Id }).ToDictionaryAsync(x => x.Original, x => x.Id, ct);

    private IQueryable<FinanceOperation> Filtered(FinanceOperationFilter f)
    {
        var q = db.FinanceOperations.AsNoTracking().Where(o => f.StoreIds.Contains(o.StoreId));
        if (f.FromUtc is { } from) q = q.Where(o => o.Date >= from);
        if (f.ToUtc is { } to) q = q.Where(o => o.Date < to);
        if (f.AccountId is { } acc) q = q.Where(o => o.FromAccountId == acc || o.ToAccountId == acc || o.Entries.Any(e => e.AccountId == acc));
        if (f.Type is { } type) q = q.Where(o => o.Type == type);
        if (f.Types is { Count: > 0 }) q = q.Where(o => f.Types.Contains(o.Type));
        if (f.ManualOnly) q = q.Where(o => o.Type == FinanceOperationType.Income || o.Type == FinanceOperationType.Expense);
        if (f.CategoryId is { } cat) q = q.Where(o => o.CategoryId == cat);
        if (!string.IsNullOrWhiteSpace(f.SourceModule)) q = q.Where(o => o.SourceModule == f.SourceModule);
        if (f.CounterpartyType is { } ctype) q = q.Where(o => o.CounterpartyType == ctype);
        if (f.CounterpartyId is { } cid) q = q.Where(o => o.CounterpartyId == cid);
        if (f.Status is { } st) q = q.Where(o => o.Status == st);
        if (f.StaffUserId is { } staff) q = q.Where(o => o.CreatedBy == staff);
        if (f.MinAmount is { } min) q = q.Where(o => o.Amount >= min);
        if (f.MaxAmount is { } max) q = q.Where(o => o.Amount <= max);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var like = $"%{f.Search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
            q = q.Where(o => EF.Functions.ILike(o.Number, like, "\\") || EF.Functions.ILike(o.Description, like, "\\")
                             || (o.CounterpartyName != null && EF.Functions.ILike(o.CounterpartyName, like, "\\"))
                             || (o.DocumentNumber != null && EF.Functions.ILike(o.DocumentNumber, like, "\\")));
        }
        return q;
    }

    public async Task<(IReadOnlyList<FinanceOperation> Items, int Total)> SearchOperationsAsync(FinanceOperationFilter f, CancellationToken ct = default)
    {
        var q = Filtered(f);
        var total = await q.CountAsync(ct);
        IOrderedQueryable<FinanceOperation> sorted = (f.Sort, f.Desc) switch
        {
            ("amount", true) => q.OrderByDescending(o => o.Amount).ThenByDescending(o => o.Seq),
            ("amount", false) => q.OrderBy(o => o.Amount).ThenBy(o => o.Seq),
            ("number", true) => q.OrderByDescending(o => o.Seq),
            ("number", false) => q.OrderBy(o => o.Seq),
            ("type", true) => q.OrderByDescending(o => o.Type).ThenByDescending(o => o.Seq),
            ("type", false) => q.OrderBy(o => o.Type).ThenBy(o => o.Seq),
            ("status", true) => q.OrderByDescending(o => o.Status).ThenByDescending(o => o.Seq),
            ("status", false) => q.OrderBy(o => o.Status).ThenBy(o => o.Seq),
            (_, false) => q.OrderBy(o => o.Date).ThenBy(o => o.Seq),
            _ => q.OrderByDescending(o => o.Date).ThenByDescending(o => o.Seq),
        };
        var items = await sorted.Skip(f.Skip).Take(f.Take).Include(o => o.Entries).ToListAsync(ct);
        return (items, total);
    }

    public async Task<IReadOnlyList<FinanceOperation>> PostedOperationsAsync(IReadOnlyCollection<Guid> storeIds, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default) =>
        await db.FinanceOperations.AsNoTracking().Include(o => o.Entries)
            .Where(o => storeIds.Contains(o.StoreId) && o.Status != FinanceOperationStatus.Draft && o.Date >= fromUtc && o.Date < toUtc)
            .OrderBy(o => o.Date).ThenBy(o => o.Seq).ToListAsync(ct);

    public async Task<IReadOnlyList<FinanceEntryRow>> StatementAsync(Guid accountId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default) =>
        await (from e in db.FinanceEntries.AsNoTracking()
               join o in db.FinanceOperations.AsNoTracking() on e.OperationId equals o.Id
               where e.AccountId == accountId && e.Date >= fromUtc && e.Date < toUtc
               orderby e.Date, e.CreatedAt
               select new FinanceEntryRow(e, o)).ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> PostedSourceOperationIdsAsync(Guid orgId, CancellationToken ct = default) =>
        await db.FinanceOperations.Where(o => o.OrganizationId == orgId && o.SourceOperationId != null).Select(o => o.SourceOperationId!.Value).ToListAsync(ct);

    private static readonly FinanceOperationType[] InTypes = [FinanceOperationType.Income, FinanceOperationType.SalePayment, FinanceOperationType.CustomerPayment];
    private static readonly FinanceOperationType[] OutTypes = [FinanceOperationType.Expense, FinanceOperationType.Refund, FinanceOperationType.SupplierPayment];
    private static readonly FinanceOperationType[] MoveTypes = [FinanceOperationType.Transfer, FinanceOperationType.Collection];

    public async Task<(decimal Inflow, decimal Outflow, decimal Transfers, int Count)> SummarizeOperationsAsync(FinanceOperationFilter f, CancellationToken ct = default)
    {
        var q = Filtered(f).Where(o => o.Status == FinanceOperationStatus.Posted && o.ReversalOfId == null);
        var agg = await q.GroupBy(_ => 1).Select(g => new
        {
            Count = g.Count(),
            Inflow = g.Sum(o => InTypes.Contains(o.Type) ? o.Amount : 0),
            Outflow = g.Sum(o => OutTypes.Contains(o.Type) ? o.Amount : 0),
            Transfers = g.Sum(o => MoveTypes.Contains(o.Type) ? o.Amount : 0),
        }).FirstOrDefaultAsync(ct);
        return agg is null ? (0, 0, 0, 0) : (agg.Inflow, agg.Outflow, agg.Transfers, agg.Count);
    }

    public async Task<Dictionary<Guid, (Guid Id, string Number)>> OperationsBySourceAsync(IReadOnlyCollection<Guid> sourceIds, CancellationToken ct = default) =>
        sourceIds.Count == 0 ? [] : (await db.FinanceOperations.AsNoTracking()
            .Where(o => o.SourceOperationId != null && sourceIds.Contains(o.SourceOperationId.Value) && o.ReversalOfId == null)
            .Select(o => new { Source = o.SourceOperationId!.Value, o.Id, o.Number }).ToListAsync(ct))
        .GroupBy(x => x.Source).ToDictionary(g => g.Key, g => (g.First().Id, g.First().Number));

    // ---------- Қарыздар ----------

    public async Task<IReadOnlyList<Debt>> DebtsAsync(IReadOnlyCollection<Guid> storeIds, bool openOnly, CancellationToken ct = default)
    {
        var q = db.Debts.AsNoTracking().Include(d => d.Payments).Where(d => storeIds.Contains(d.StoreId));
        if (openOnly) q = q.Where(d => d.Status == DebtStatus.Open);
        return await q.OrderBy(d => d.DueDate).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Debt>> DebtsForUpdateAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
        ids.Count == 0 ? [] : await db.Debts.Include(d => d.Payments).Where(d => ids.Contains(d.Id)).ToListAsync(ct);

    public async Task<IReadOnlyList<DebtPayment>> DebtPaymentsAsync(IReadOnlyCollection<Guid> debtIds, CancellationToken ct = default) =>
        debtIds.Count == 0 ? [] : await db.DebtPayments.AsNoTracking().Where(p => debtIds.Contains(p.DebtId)).OrderByDescending(p => p.CreatedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<StockDocument>> SupplierDocumentsAsync(IReadOnlyCollection<Guid> storeIds, CancellationToken ct = default) =>
        await db.StockDocuments.AsNoTracking()
            .Where(d => storeIds.Contains(d.StoreId) && d.Type == StockDocumentType.Receipt && d.Status == StockDocumentStatus.Posted && d.SupplierId != null)
            .OrderByDescending(d => d.Date).ToListAsync(ct);

    public async Task<IReadOnlyList<SupplierPayment>> SupplierPaymentsAsync(IReadOnlyCollection<Guid> storeIds, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct = default)
    {
        var q = db.SupplierPayments.AsNoTracking().Where(p => storeIds.Contains(p.StoreId));
        if (fromUtc is { } from) q = q.Where(p => p.CreatedAt >= from);
        if (toUtc is { } to) q = q.Where(p => p.CreatedAt < to);
        return await q.OrderByDescending(p => p.CreatedAt).ToListAsync(ct);
    }

    public async Task<Dictionary<Guid, (string Name, string? Phone, string? TaxId, string? Address)>> SuppliersAsync(Guid orgId, CancellationToken ct = default) =>
        (await db.Suppliers.AsNoTracking().Where(s => s.OrganizationId == orgId).Select(s => new { s.Id, s.Name, s.Phone, s.TaxId, s.Address }).ToListAsync(ct))
        .ToDictionary(s => s.Id, s => (s.Name, s.Phone, s.TaxId, s.Address));

    public async Task<Dictionary<Guid, (string Name, string Phone)>> CustomersAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
        ids.Count == 0 ? [] : (await db.Customers.AsNoTracking().Where(c => ids.Contains(c.Id)).Select(c => new { c.Id, c.FirstName, c.LastName, c.Phone }).ToListAsync(ct))
            .ToDictionary(c => c.Id, c => ($"{c.FirstName} {c.LastName}".Trim(), c.Phone));

    public async Task<Dictionary<Guid, string>> StaffNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
        ids.Count == 0 ? [] : await db.StaffUsers.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => (u.FirstName + " " + u.LastName).Trim(), ct);

    public async Task<Dictionary<Guid, string>> StoreNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
        ids.Count == 0 ? [] : await db.Stores.AsNoTracking().Where(s => ids.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, ct);
    public async Task<IReadOnlyList<BackfillSource>> BackfillSourcesAsync(Guid orgId, CancellationToken ct = default)
    {
        var rows = new List<BackfillSource>();
        var sales = await db.Sales.AsNoTracking().Include(s => s.Payments).Where(s => s.OrganizationId == orgId).OrderBy(s => s.CreatedAt).ToListAsync(ct);
        var customerIds = sales.Where(s => s.CustomerId != null).Select(s => s.CustomerId!.Value).ToHashSet();
        var debts = await db.Debts.AsNoTracking().Include(d => d.Payments).Where(d => db.Stores.Any(s => s.Id == d.StoreId && s.OrganizationId == orgId)).ToListAsync(ct);
        foreach (var d in debts) customerIds.Add(d.CustomerId);
        var customers = await CustomersAsync(customerIds, ct);
        var suppliers = await SuppliersAsync(orgId, ct);
        string? Customer(Guid? id) => id is { } c && customers.TryGetValue(c, out var v) ? v.Name : null;

        foreach (var s in sales)
        {
            var lines = s.Payments.Where(p => p.Method != PaymentMethod.Debt && p.Amount > 0).Select(p => (p.Method, p.Amount)).ToList();
            if (lines.Count == 0) continue;
            rows.Add(new BackfillSource("sales", FinanceOperationType.SalePayment, s.StoreId, s.Id, s.Id, s.CreatedAt, lines, $"Чек №{s.Number}",
                s.CustomerId is null ? null : CounterpartyType.Customer, s.CustomerId, Customer(s.CustomerId), $"№{s.Number}"));
        }
        var returns = await db.SaleReturns.AsNoTracking().Where(r => r.OrganizationId == orgId && r.Refunded > 0).OrderBy(r => r.CreatedAt).ToListAsync(ct);
        var saleNumbers = sales.ToDictionary(s => s.Id, s => s);
        foreach (var r in returns)
        {
            var sale = saleNumbers.GetValueOrDefault(r.SaleId);
            rows.Add(new BackfillSource("sales", FinanceOperationType.Refund, r.StoreId, r.Id, r.SaleId, r.CreatedAt, [(r.RefundMethod ?? PaymentMethod.Cash, -r.Refunded)],
                $"Возврат по чеку №{sale?.Number}", sale?.CustomerId is null ? null : CounterpartyType.Customer, sale?.CustomerId, Customer(sale?.CustomerId), sale is null ? null : $"№{sale.Number}"));
        }
        foreach (var d in debts)
            foreach (var p in d.Payments.Where(p => !p.IsReturn && p.Method is { } && p.Amount > 0).OrderBy(p => p.CreatedAt))
                rows.Add(new BackfillSource("debts", FinanceOperationType.CustomerPayment, d.StoreId, p.Id, d.Id, p.CreatedAt, [(p.Method!.Value, p.Amount)],
                    "Оплата от клиента", CounterpartyType.Customer, d.CustomerId, Customer(d.CustomerId), null));
        var payments = await db.SupplierPayments.AsNoTracking()
            .Where(p => p.Status == SupplierPaymentStatus.Registered && db.Stores.Any(s => s.Id == p.StoreId && s.OrganizationId == orgId)).OrderBy(p => p.CreatedAt).ToListAsync(ct);
        var docIds = payments.Where(p => p.DocumentId != null).Select(p => p.DocumentId!.Value).ToHashSet();
        var docNumbers = docIds.Count == 0 ? [] : await db.StockDocuments.AsNoTracking().Where(d => docIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Number, ct);
        foreach (var p in payments)
            rows.Add(new BackfillSource("inventory", FinanceOperationType.SupplierPayment, p.StoreId, p.Id, p.DocumentId, p.CreatedAt, [(p.Method, -p.Amount)],
                "Оплата поставщику" + (p.DocumentId is { } did && docNumbers.TryGetValue(did, out var n) ? " · " + n : ""),
                CounterpartyType.Supplier, p.SupplierId, suppliers.GetValueOrDefault(p.SupplierId).Name, p.Reference));
        return rows.OrderBy(r => r.Date).ToList();
    }
}
