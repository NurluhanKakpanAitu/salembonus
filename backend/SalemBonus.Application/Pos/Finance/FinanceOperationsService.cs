using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Application.Pos.Catalog;
using SalemBonus.Application.Pos.Sales;
using SalemBonus.Domain.Core;
using SalemBonus.Domain.Pos.Finance;
using SalemBonus.Domain.Pos.Sales;

namespace SalemBonus.Application.Pos.Finance;

/// <summary>Сұраныс контексі: ұйым, рұқсат етілген дүкендер, уақыт белдеуі, кезең (UTC), құқықтар.</summary>
public sealed record FinanceCtx(Guid OrgId, StoreMembership Current, IReadOnlyList<Guid> StoreIds, TimeZoneInfo Zone, DateOnly Start, DateOnly End,
    DateTime FromUtc, DateTime ToUtc, DateTime NowUtc, bool CanOperate, bool CanReverse, bool CanManage, bool CanContacts)
{
    public DateOnly LocalDate(DateTime utc) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, Zone));
    public DateOnly Today => LocalDate(NowUtc);
}

/// <summary>
/// «Финансы» ядросының қызметі (ТЗ «Финансы» §6–9, §13–15): шоттар, санаттар, қолмен операциялар (черновик → проведение → сторно),
/// аударым мен инкассация, қалдық түзетуі, шот выпискасы, журнал. Автоматты операциялар (сатылым, қайтарым, қарыз төлемі, жеткізушіге төлем)
/// бастапқы модульден <see cref="FinanceLedger"/> арқылы түседі — мұнда қайталанбайды (§3.1).
/// </summary>
public class FinanceOperationsService(
    IFinanceRepository repo,
    IStaffRepository staff,
    IStoreRepository stores,
    FinanceLedger ledger,
    CatalogAccess access,
    IUnitOfWork unitOfWork)
{
    private const int MaxPageSize = 100;
    private AppLanguage Lang => access.Lang;

    // ---------- Контекст ----------

    public async Task<FinanceCtx> ContextAsync(string? storeIds, DateOnly? from, DateOnly? to, CancellationToken ct, string permission = StaffPermissions.FinanceView)
    {
        var (orgId, current) = await access.RequireAsync(permission, ct);
        var memberships = new List<StoreMembership> { current };
        var requested = (storeIds ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => Guid.TryParse(x, out var g) ? g : throw new ValidationException(Messages.AnalyticsStoreForbidden(Lang), "storeIds")).Distinct().ToList();
        if (requested.Count > 0)
        {
            memberships.Clear();
            foreach (var id in requested)
            {
                var m = id == current.StoreId ? current : await staff.GetMembershipAsync(access.StaffUserId, id, ct);
                if (m is null || m.Store?.OrganizationId != orgId || !m.Has(StaffPermissions.FinanceView)) throw new ForbiddenException(Messages.AnalyticsStoreForbidden(Lang));
                memberships.Add(m);
            }
        }
        // Әдепкі шоттар (§9): дүкеннің бірінші қаржы сұранысында-ақ жасалады — қолмен шот бұрын ашылса да.
        await ledger.EnsureAccountsAsync(orgId, current.StoreId, ct);
        var store = await stores.GetByIdAsync(current.StoreId, ct) ?? throw new NotFoundException(Messages.StoreNotFound(Lang));
        var zone = StoreTime.Zone(store);
        var today = StoreTime.Today(zone);
        var start = from ?? new DateOnly(today.Year, today.Month, 1);
        var end = to ?? today;
        if (end < start) (start, end) = (end, start);
        bool All(string p) => memberships.All(x => x.Has(p));
        return new FinanceCtx(orgId, current, memberships.Select(x => x.StoreId).ToList(), zone, start, end,
            StoreTime.StartOfDayUtc(start, zone), StoreTime.StartOfDayUtc(end.AddDays(1), zone), DateTime.UtcNow,
            All(StaffPermissions.FinanceOperate), All(StaffPermissions.FinanceReverse), All(StaffPermissions.FinanceAccounts), All(StaffPermissions.CustomersContacts));
    }

    public async Task<FinanceMetaDto> MetaAsync(string? storeIds, CancellationToken ct = default)
    {
        var c = await ContextAsync(storeIds, null, null, ct);
        var accounts = new List<FinanceAccount>();
        foreach (var sid in c.StoreIds) accounts.AddRange(await ledger.EnsureAccountsAsync(c.OrgId, sid, ct));
        var categories = await ledger.EnsureCategoriesAsync(c.OrgId, ct);
        await unitOfWork.SaveChangesAsync(ct);
        var storeNames = await repo.StoreNamesAsync(c.StoreIds, ct);
        return new FinanceMetaDto(accounts.Select(a => ToDto(a, storeNames, false)).ToList(), categories.Select(ToDto).ToList(),
            c.CanOperate, c.CanReverse, c.CanManage, c.CanContacts);
    }

    // ---------- Шоттар (§9) ----------

    public async Task<AccountsSummaryDto> AccountsAsync(string? storeIds, bool includeInactive, CancellationToken ct = default)
    {
        var c = await ContextAsync(storeIds, null, null, ct);
        var all = new List<FinanceAccount>();
        foreach (var sid in c.StoreIds) all.AddRange(await ledger.EnsureAccountsAsync(c.OrgId, sid, ct));
        await unitOfWork.SaveChangesAsync(ct);
        var storeNames = await repo.StoreNamesAsync(c.StoreIds, ct);
        var withEntries = new HashSet<Guid>();
        foreach (var a in all) if (await repo.AccountHasEntriesAsync(a.Id, ct)) withEntries.Add(a.Id);
        var active = all.Where(a => a.IsActive).ToList();
        decimal Sum(params FinanceAccountType[] types) => active.Where(a => types.Contains(a.Type)).Sum(a => a.Balance);
        return new AccountsSummaryDto(active.Sum(a => a.Balance), Sum(FinanceAccountType.Cash, FinanceAccountType.Safe),
            Sum(FinanceAccountType.Bank, FinanceAccountType.Kaspi, FinanceAccountType.Other), Sum(FinanceAccountType.InTransit),
            all.Where(a => includeInactive || a.IsActive).Select(a => ToDto(a, storeNames, withEntries.Contains(a.Id))).ToList());
    }

    public async Task<FinanceAccountDto> CreateAccountAsync(SaveAccountRequest r, CancellationToken ct = default)
    {
        var c = await ContextAsync(null, null, null, ct, StaffPermissions.FinanceAccounts);
        var storeId = c.Current.StoreId;
        var name = access.Name(r.Name, 120);
        if (await repo.AccountNameExistsAsync(storeId, name, null, ct)) throw new ValidationException(Messages.CatalogDuplicate(Lang), "name");
        var account = new FinanceAccount
        {
            Id = Guid.NewGuid(), OrganizationId = c.OrgId, StoreId = storeId, Name = name, Type = ParseType(r.Type), Methods = ParseMethods(r.Methods),
            Comment = CatalogAccess.Optional(r.Comment, 500), SortOrder = r.SortOrder ?? 100,
        };
        await ReassignMethodsAsync(c, storeId, account, ct);
        repo.AddAccount(account);
        await unitOfWork.InTransactionAsync(async () =>
        {
            await unitOfWork.SaveChangesAsync(ct);
            await OpeningAsync(c, account, r, ct);
            access.Audit(c.OrgId, storeId, "finance.account.create", "finance_account", account.Id, null, new { account.Name, Type = account.Type.ToString(), account.Methods, r.OpeningBalance });
            await unitOfWork.SaveChangesAsync(ct);
            return account;
        }, ct);
        return ToDto(account, await repo.StoreNamesAsync([storeId], ct), account.OpeningBalance != 0);
    }

    public async Task<FinanceAccountDto> UpdateAccountAsync(Guid id, SaveAccountRequest r, CancellationToken ct = default)
    {
        var c = await ContextAsync(null, null, null, ct, StaffPermissions.FinanceAccounts);
        var account = await repo.GetAccountAsync(c.OrgId, id, ct) ?? throw new NotFoundException(Messages.FinanceAccountNotFound(Lang));
        var name = access.Name(r.Name, 120);
        if (await repo.AccountNameExistsAsync(account.StoreId, name, account.Id, ct)) throw new ValidationException(Messages.CatalogDuplicate(Lang), "name");
        var old = new { account.Name, Type = account.Type.ToString(), account.Methods };
        account.Name = name;
        account.Type = ParseType(r.Type);
        account.Methods = ParseMethods(r.Methods);
        account.Comment = CatalogAccess.Optional(r.Comment, 500);
        if (r.SortOrder is { } so) account.SortOrder = so;
        account.UpdatedAt = DateTime.UtcNow;
        await ReassignMethodsAsync(c, account.StoreId, account, ct);
        var hasEntries = await repo.AccountHasEntriesAsync(account.Id, ct);
        await unitOfWork.InTransactionAsync(async () =>
        {
            if (!hasEntries) await OpeningAsync(c, account, r, ct);
            else if (r.OpeningBalance is { } ob && Math.Round(ob, 2) != account.OpeningBalance) throw new ValidationException(Messages.FinanceOpeningLocked(Lang), "openingBalance");
            access.Audit(c.OrgId, account.StoreId, "finance.account.update", "finance_account", account.Id, old, new { account.Name, Type = account.Type.ToString(), account.Methods });
            await unitOfWork.SaveChangesAsync(ct);
            return account;
        }, ct);
        return ToDto(account, await repo.StoreNamesAsync([account.StoreId], ct), hasEntries || account.OpeningBalance != 0);
    }

    /// <summary>Бастапқы қалдық — «Корректировка» операциясымен (§9): тарихта қалады, негізі — «Начальный остаток».</summary>
    private async Task OpeningAsync(FinanceCtx c, FinanceAccount account, SaveAccountRequest r, CancellationToken ct)
    {
        if (r.OpeningBalance is not { } opening || opening == 0) return;
        if (r.OpeningDate is null) throw new ValidationException(Messages.FinanceOpeningDateRequired(Lang), "openingDate");
        if (await repo.AccountHasEntriesAsync(account.Id, ct)) throw new ValidationException(Messages.FinanceOpeningLocked(Lang), "openingBalance");
        account.OpeningBalance = Math.Round(opening, 2);
        account.OpeningDate = r.OpeningDate;
        var date = StoreTime.StartOfDayUtc(r.OpeningDate.Value, c.Zone);
        await ledger.PostAsync(c.OrgId, account.StoreId, new FinancePosting(FinanceOperationType.Adjustment, date, [new FinanceLine(account.Id, account.OpeningBalance)],
            Lang == AppLanguage.Ru ? "Начальный остаток" : "Бастапқы қалдық", AllowNegative: true), ct);
    }

    /// <summary>Бір дүкенде бір төлем әдісі — бір шот: жаңа шотқа берілген әдіс басқа шоттардан алынады.</summary>
    private async Task ReassignMethodsAsync(FinanceCtx c, Guid storeId, FinanceAccount account, CancellationToken ct)
    {
        if (account.Methods.Count == 0) return;
        var others = (await repo.GetAccountsForUpdateAsync(c.OrgId, (await repo.ListAccountsAsync([storeId], ct)).Select(a => a.Id).Where(id => id != account.Id).ToList(), ct));
        foreach (var o in others.Where(o => o.Methods.Any(account.Methods.Contains)))
        {
            o.Methods = o.Methods.Where(m => !account.Methods.Contains(m)).ToList();
            o.UpdatedAt = DateTime.UtcNow;
        }
    }

    public async Task SetAccountActiveAsync(Guid id, bool active, CancellationToken ct = default)
    {
        var c = await ContextAsync(null, null, null, ct, StaffPermissions.FinanceAccounts);
        var account = await repo.GetAccountAsync(c.OrgId, id, ct) ?? throw new NotFoundException(Messages.FinanceAccountNotFound(Lang));
        if (!active && account.Balance != 0) throw new ValidationException(Messages.FinanceAccountHasBalance(Lang, account.Balance));
        account.IsActive = active;
        if (!active) account.Methods = [];
        account.UpdatedAt = DateTime.UtcNow;
        access.Audit(c.OrgId, account.StoreId, active ? "finance.account.activate" : "finance.account.deactivate", "finance_account", account.Id, null, new { account.Name });
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<AccountStatementDto> StatementAsync(Guid id, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var c = await ContextAsync(null, from, to, ct);
        var account = await repo.GetAccountAsync(c.OrgId, id, ct) ?? throw new NotFoundException(Messages.FinanceAccountNotFound(Lang));
        if (!c.StoreIds.Contains(account.StoreId))
        {
            var other = await staff.GetMembershipAsync(access.StaffUserId, account.StoreId, ct);
            if (other is null || !other.Has(StaffPermissions.FinanceView)) throw new ForbiddenException(Messages.AnalyticsStoreForbidden(Lang));
        }
        var opening = (await repo.BalancesBeforeAsync([account.Id], c.FromUtc, ct)).GetValueOrDefault(account.Id);
        var rows = await repo.StatementAsync(account.Id, c.FromUtc, c.ToUtc, ct);
        var running = opening;
        var list = rows.Select(r =>
        {
            running += r.Entry.Amount;
            return new StatementRowDto(r.Entry.Date, r.Operation.Id, r.Operation.Number, r.Operation.Type.ToString(), r.Operation.Description,
                r.Operation.CounterpartyName, r.Entry.Amount, running, r.Operation.DocumentNumber);
        }).ToList();
        var storeNames = await repo.StoreNamesAsync([account.StoreId], ct);
        return new AccountStatementDto(ToDto(account, storeNames, rows.Count > 0), c.Start, c.End, opening,
            rows.Where(r => r.Entry.Amount > 0).Sum(r => r.Entry.Amount), -rows.Where(r => r.Entry.Amount < 0).Sum(r => r.Entry.Amount), running, list);
    }

    // ---------- Санаттар (§15.1) ----------

    public async Task<IReadOnlyList<FinanceCategoryDto>> CategoriesAsync(CancellationToken ct = default)
    {
        var (orgId, _) = await access.RequireAsync(StaffPermissions.FinanceView, ct);
        var list = await ledger.EnsureCategoriesAsync(orgId, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return list.Select(ToDto).ToList();
    }

    public async Task<FinanceCategoryDto> CreateCategoryAsync(SaveCategoryRequest r, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAnyAsync(ct, StaffPermissions.FinanceAccounts, StaffPermissions.FinanceOperate);
        var name = access.Name(r.Name, 120);
        var type = Enum.TryParse<FinanceCategoryType>(r.Type, true, out var t) ? t : throw new ValidationException(Messages.FinanceTypeInvalid(Lang), "type");
        if (await repo.CategoryNameExistsAsync(orgId, name, type, null, ct)) throw new ValidationException(Messages.CatalogDuplicate(Lang), "name");
        var category = new FinanceCategory
        {
            Id = Guid.NewGuid(), OrganizationId = orgId, Name = name, Type = type, PnlClass = ParsePnl(r.PnlClass, type), SortOrder = r.SortOrder ?? 100, CreatedBy = access.StaffUserId,
        };
        repo.AddCategory(category);
        access.Audit(orgId, m.StoreId, "finance.category.create", "finance_category", category.Id, null, new { category.Name, Type = type.ToString(), PnlClass = category.PnlClass.ToString() });
        await unitOfWork.SaveChangesAsync(ct);
        return ToDto(category);
    }

    public async Task<FinanceCategoryDto> UpdateCategoryAsync(Guid id, SaveCategoryRequest r, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.FinanceAccounts, ct);
        var category = await repo.GetCategoryAsync(orgId, id, ct) ?? throw new NotFoundException(Messages.FinanceCategoryInvalid(Lang));
        var name = access.Name(r.Name, 120);
        if (await repo.CategoryNameExistsAsync(orgId, name, category.Type, category.Id, ct)) throw new ValidationException(Messages.CatalogDuplicate(Lang), "name");
        var old = new { category.Name, PnlClass = category.PnlClass.ToString() };
        category.Name = name;
        category.PnlClass = ParsePnl(r.PnlClass, category.Type);
        if (r.SortOrder is { } so) category.SortOrder = so;
        access.Audit(orgId, m.StoreId, "finance.category.update", "finance_category", category.Id, old, new { category.Name, PnlClass = category.PnlClass.ToString() });
        await unitOfWork.SaveChangesAsync(ct);
        return ToDto(category);
    }

    public async Task SetCategoryActiveAsync(Guid id, bool active, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.FinanceAccounts, ct);
        var category = await repo.GetCategoryAsync(orgId, id, ct) ?? throw new NotFoundException(Messages.FinanceCategoryInvalid(Lang));
        category.IsActive = active;
        access.Audit(orgId, m.StoreId, active ? "finance.category.restore" : "finance.category.archive", "finance_category", category.Id, null, new { category.Name });
        await unitOfWork.SaveChangesAsync(ct);
    }

    // ---------- Операциялар журналы (§6, §8, §13) ----------

    public async Task<FinanceOperationsPageDto> ListAsync(FinanceListQuery q, CancellationToken ct = default)
    {
        var c = await ContextAsync(q.StoreIds, q.From, q.To, ct);
        var page = Math.Max(1, q.Page);
        var pageSize = Math.Clamp(q.PageSize, 1, MaxPageSize);
        var filter = Filter(c, q, (page - 1) * pageSize, pageSize);
        var (items, total) = await repo.SearchOperationsAsync(filter, ct);
        var (inflow, outflow, transfers, count) = await repo.SummarizeOperationsAsync(filter, ct);
        return new FinanceOperationsPageDto(await ToDtosAsync(c, items, ct), total, page, pageSize, inflow, outflow, transfers, count);
    }

    /// <summary>Экспорт үшін: сүзгінің барлық жолдары (§6, §8).</summary>
    public async Task<(FinanceCtx Ctx, IReadOnlyList<FinanceOperationDto> Rows)> AllAsync(FinanceListQuery q, int max, CancellationToken ct = default)
    {
        var c = await ContextAsync(q.StoreIds, q.From, q.To, ct);
        var (items, total) = await repo.SearchOperationsAsync(Filter(c, q, 0, max + 1), ct);
        if (total > max) throw new ValidationException(Messages.SalesExportTooLarge(Lang, max));
        return (c, await ToDtosAsync(c, items, ct));
    }

    private FinanceOperationFilter Filter(FinanceCtx c, FinanceListQuery q, int skip, int take)
    {
        FinanceOperationType? type = Enum.TryParse<FinanceOperationType>(q.Type, true, out var t) ? t : null;
        var types = (q.Types ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => Enum.TryParse<FinanceOperationType>(x, true, out var v) ? v : (FinanceOperationType?)null).Where(x => x != null).Select(x => x!.Value).ToList();
        CounterpartyType? ctype = Enum.TryParse<CounterpartyType>(q.CounterpartyType, true, out var cp) ? cp : null;
        FinanceOperationStatus? status = Enum.TryParse<FinanceOperationStatus>(q.Status, true, out var st) ? st : null;
        var sort = q.Sort is "amount" or "number" or "type" or "status" ? q.Sort : "date";
        return new FinanceOperationFilter(c.StoreIds, c.FromUtc, c.ToUtc, q.AccountId, type, types.Count > 0 ? types : null, q.CategoryId,
            CatalogAccess.Optional(q.Source, 32), ctype, q.CounterpartyId, status, q.StaffUserId, CatalogAccess.Optional(q.Search, 100),
            q.MinAmount, q.MaxAmount, q.ManualOnly, sort, !string.Equals(q.Dir, "asc", StringComparison.OrdinalIgnoreCase), skip, take);
    }

    public async Task<FinanceOperationDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var c = await ContextAsync(null, null, null, ct);
        var op = await repo.GetOperationAsync(c.OrgId, id, ct) ?? throw new NotFoundException(Messages.FinanceOperationNotFound(Lang));
        await RequireStoreAsync(c, op.StoreId, ct);
        return (await ToDtosAsync(c, [op], ct))[0];
    }

    private async Task RequireStoreAsync(FinanceCtx c, Guid storeId, CancellationToken ct)
    {
        if (c.StoreIds.Contains(storeId)) return;
        var m = await staff.GetMembershipAsync(access.StaffUserId, storeId, ct);
        if (m is null || !m.Has(StaffPermissions.FinanceView)) throw new ForbiddenException(Messages.AnalyticsStoreForbidden(Lang));
    }

    // ---------- Қолмен операциялар (§7) ----------

    public async Task<FinanceOperationDto> CreateAsync(SaveOperationRequest r, CancellationToken ct = default)
    {
        var c = await ContextAsync(null, null, null, ct, StaffPermissions.FinanceOperate);
        if (r.ClientRequestId is { } key && await repo.FindByClientRequestAsync(c.OrgId, key, ct) is { } done) return await GetAsync(done.Id, ct);
        var storeId = c.Current.StoreId;
        var op = new FinanceOperation { Id = Guid.NewGuid(), OrganizationId = c.OrgId, StoreId = storeId, CreatedBy = access.StaffUserId, ClientRequestId = r.ClientRequestId };
        var account = await FillAsync(c, op, r, ct);
        await unitOfWork.InTransactionAsync(async () =>
        {
            if (r.Post)
            {
                var posted = await ledger.PostAsync(c.OrgId, storeId, Posting(op, account!, r.ClientRequestId), ct);
                op = posted;
            }
            else
            {
                op.Number = "—";
                repo.AddOperation(op);
            }
            access.Audit(c.OrgId, storeId, r.Post ? "finance.operation.post" : "finance.operation.draft", "finance_operation", op.Id, null,
                new { op.Number, Type = op.Type.ToString(), op.Amount, op.Description, op.CategoryId, AccountId = account?.Id });
            await unitOfWork.SaveChangesAsync(ct);
            return op;
        }, ct);
        return await GetAsync(op.Id, ct);
    }

    public async Task<FinanceOperationDto> UpdateDraftAsync(Guid id, SaveOperationRequest r, CancellationToken ct = default)
    {
        var c = await ContextAsync(null, null, null, ct, StaffPermissions.FinanceOperate);
        var op = await repo.GetOperationForUpdateAsync(c.OrgId, id, ct) ?? throw new NotFoundException(Messages.FinanceOperationNotFound(Lang));
        if (op.Status != FinanceOperationStatus.Draft) throw new ValidationException(Messages.FinanceNotDraft(Lang));
        var account = await FillAsync(c, op, r, ct);
        await unitOfWork.InTransactionAsync(async () =>
        {
            if (r.Post)
            {
                if (account is null) throw new ValidationException(Messages.FinanceAccountNotFound(Lang), "accountId");
                await ledger.PostDraftAsync(c.OrgId, op, [new FinanceLine(account.Id, op.Type == FinanceOperationType.Income ? op.Amount : -op.Amount)], ct);
            }
            access.Audit(c.OrgId, op.StoreId, r.Post ? "finance.operation.post" : "finance.operation.update", "finance_operation", op.Id, null,
                new { op.Number, Type = op.Type.ToString(), op.Amount, op.Description });
            await unitOfWork.SaveChangesAsync(ct);
            return op;
        }, ct);
        return await GetAsync(op.Id, ct);
    }

    public async Task<FinanceOperationDto> PostDraftAsync(Guid id, CancellationToken ct = default)
    {
        var c = await ContextAsync(null, null, null, ct, StaffPermissions.FinanceOperate);
        var op = await repo.GetOperationForUpdateAsync(c.OrgId, id, ct) ?? throw new NotFoundException(Messages.FinanceOperationNotFound(Lang));
        if (op.Status == FinanceOperationStatus.Posted) return await GetAsync(op.Id, ct);
        var accountId = op.ToAccountId ?? op.FromAccountId ?? throw new ValidationException(Messages.FinanceAccountNotFound(Lang), "accountId");
        await unitOfWork.InTransactionAsync(async () =>
        {
            await ledger.PostDraftAsync(c.OrgId, op, [new FinanceLine(accountId, op.Type == FinanceOperationType.Income ? op.Amount : -op.Amount)], ct);
            access.Audit(c.OrgId, op.StoreId, "finance.operation.post", "finance_operation", op.Id, null, new { op.Number, Type = op.Type.ToString(), op.Amount });
            await unitOfWork.SaveChangesAsync(ct);
            return op;
        }, ct);
        return await GetAsync(op.Id, ct);
    }

    public async Task DeleteDraftAsync(Guid id, CancellationToken ct = default)
    {
        var c = await ContextAsync(null, null, null, ct, StaffPermissions.FinanceOperate);
        var op = await repo.GetOperationForUpdateAsync(c.OrgId, id, ct) ?? throw new NotFoundException(Messages.FinanceOperationNotFound(Lang));
        if (op.Status != FinanceOperationStatus.Draft) throw new ValidationException(Messages.FinanceNotDraft(Lang));
        repo.RemoveOperation(op);
        access.Audit(c.OrgId, op.StoreId, "finance.operation.delete_draft", "finance_operation", op.Id, new { op.Description, op.Amount }, null);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<FinanceAccount?> FillAsync(FinanceCtx c, FinanceOperation op, SaveOperationRequest r, CancellationToken ct)
    {
        op.Type = r.Type?.ToLowerInvariant() switch
        {
            "income" => FinanceOperationType.Income,
            "expense" => FinanceOperationType.Expense,
            _ => throw new ValidationException(Messages.FinanceTypeInvalid(Lang), "type"),
        };
        var description = (r.Description ?? string.Empty).Trim();
        if (description.Length is < 3 or > 250) throw new ValidationException(Messages.FinanceDescriptionInvalid(Lang), "description");
        op.Description = description;
        op.Amount = Math.Round(r.Amount, 2);
        if (op.Amount <= 0) throw new ValidationException(Messages.FinanceAmountInvalid(Lang), "amount");
        op.Date = r.Date is { } d ? (d.Kind == DateTimeKind.Utc ? d : DateTime.SpecifyKind(d, DateTimeKind.Utc)) : DateTime.UtcNow;
        if (op.Date > DateTime.UtcNow.AddMinutes(5)) throw new ValidationException(Messages.FinanceDateInFuture(Lang), "date");
        var category = await repo.GetCategoryAsync(c.OrgId, r.CategoryId ?? Guid.Empty, ct) ?? throw new ValidationException(Messages.FinanceCategoryRequired(Lang), "categoryId");
        var expected = op.Type == FinanceOperationType.Income ? FinanceCategoryType.Income : FinanceCategoryType.Expense;
        if (!category.IsActive || category.Type != expected) throw new ValidationException(Messages.FinanceCategoryInvalid(Lang), "categoryId");
        op.CategoryId = category.Id;
        op.CounterpartyType = Enum.TryParse<CounterpartyType>(r.CounterpartyType, true, out var cp) ? cp : null;
        op.CounterpartyId = op.CounterpartyType is null ? null : r.CounterpartyId;
        op.CounterpartyName = CatalogAccess.Optional(r.CounterpartyName, 200);
        if (op.CounterpartyName is null && op.CounterpartyType is not null && r.CounterpartyId is null) throw new ValidationException(Messages.FinanceCounterpartyRequired(Lang), "counterpartyName");
        op.DocumentNumber = CatalogAccess.Optional(r.DocumentNumber, 64);
        op.AttachmentUrl = CatalogAccess.Optional(r.AttachmentUrl, 500);
        op.Comment = CatalogAccess.Optional(r.Comment, 1000);
        op.UpdatedAt = DateTime.UtcNow;
        FinanceAccount? account = null;
        if (r.AccountId is { } aid)
        {
            account = await repo.GetAccountAsync(c.OrgId, aid, ct) ?? throw new ValidationException(Messages.FinanceAccountNotFound(Lang), "accountId");
            if (!account.IsActive) throw new ValidationException(Messages.FinanceAccountInactive(Lang, account.Name), "accountId");
            if (op.Type == FinanceOperationType.Income) { op.ToAccountId = account.Id; op.FromAccountId = null; } else { op.FromAccountId = account.Id; op.ToAccountId = null; }
        }
        else if (r.Post) throw new ValidationException(Messages.FinanceAccountNotFound(Lang), "accountId");
        return account;
    }

    private static FinancePosting Posting(FinanceOperation op, FinanceAccount account, Guid? clientRequestId) =>
        new(op.Type, op.Date, [new FinanceLine(account.Id, op.Type == FinanceOperationType.Income ? op.Amount : -op.Amount)], op.Description,
            op.CategoryId, op.CounterpartyType, op.CounterpartyId, op.CounterpartyName, null, op.DocumentNumber, op.AttachmentUrl, op.Comment,
            ClientRequestId: clientRequestId);

    // ---------- Аударым, инкассация, түзету (§9.1, §14) ----------

    public async Task<FinanceOperationDto> TransferAsync(TransferRequest r, CancellationToken ct = default)
    {
        var c = await ContextAsync(null, null, null, ct, StaffPermissions.FinanceOperate);
        if (r.ClientRequestId is { } key && await repo.FindByClientRequestAsync(c.OrgId, key, ct) is { } done) return await GetAsync(done.Id, ct);
        var amount = Math.Round(r.Amount, 2);
        if (amount <= 0) throw new ValidationException(Messages.FinanceAmountInvalid(Lang), "amount");
        if (r.FromAccountId is null || r.ToAccountId is null) throw new ValidationException(Messages.FinanceAccountNotFound(Lang), "fromAccountId");
        if (r.FromAccountId == r.ToAccountId) throw new ValidationException(Messages.FinanceSameAccount(Lang), "toAccountId");
        var from = await repo.GetAccountAsync(c.OrgId, r.FromAccountId.Value, ct) ?? throw new ValidationException(Messages.FinanceAccountNotFound(Lang), "fromAccountId");
        var to = await repo.GetAccountAsync(c.OrgId, r.ToAccountId.Value, ct) ?? throw new ValidationException(Messages.FinanceAccountNotFound(Lang), "toAccountId");
        if (!from.IsActive) throw new ValidationException(Messages.FinanceAccountInactive(Lang, from.Name), "fromAccountId");
        if (!to.IsActive) throw new ValidationException(Messages.FinanceAccountInactive(Lang, to.Name), "toAccountId");
        // Инкассация — қолма-қол ақшаның кассадан (не сейфтен) сейфке, банкке не «Жолдағы ақшаға» кетуі; банктен не Kaspi-ге «инкассация» болмайды.
        if (r.Collection && from.Type is not (FinanceAccountType.Cash or FinanceAccountType.Safe))
            throw new ValidationException(Messages.FinanceCollectionSource(Lang), "fromAccountId");
        if (r.Collection && to.Type is (FinanceAccountType.Cash or FinanceAccountType.Kaspi))
            throw new ValidationException(Messages.FinanceCollectionTarget(Lang), "toAccountId");
        await RequireStoreAsync(c, from.StoreId, ct);
        await RequireStoreAsync(c, to.StoreId, ct);
        var date = r.Date is { } d ? DateTime.SpecifyKind(d, DateTimeKind.Utc) : DateTime.UtcNow;
        var type = r.Collection ? FinanceOperationType.Collection : FinanceOperationType.Transfer;
        var description = (r.Collection ? "Инкассация: " : Lang == AppLanguage.Ru ? "Перевод: " : "Аударым: ") + $"{from.Name} → {to.Name}";
        FinanceOperation op = null!;
        await unitOfWork.InTransactionAsync(async () =>
        {
            op = await ledger.PostAsync(c.OrgId, from.StoreId, new FinancePosting(type, date, [new FinanceLine(from.Id, -amount), new FinanceLine(to.Id, amount)],
                description, DocumentNumber: CatalogAccess.Optional(r.DocumentNumber, 64), AttachmentUrl: CatalogAccess.Optional(r.AttachmentUrl, 500),
                Comment: CatalogAccess.Optional(r.Comment, 1000), ClientRequestId: r.ClientRequestId), ct);
            op.FromAccountId = from.Id;
            op.ToAccountId = to.Id;
            access.Audit(c.OrgId, from.StoreId, r.Collection ? "finance.collection" : "finance.transfer", "finance_operation", op.Id, null,
                new { op.Number, From = from.Name, To = to.Name, Amount = amount });
            await unitOfWork.SaveChangesAsync(ct);
            return op;
        }, ct);
        return await GetAsync(op.Id, ct);
    }

    public async Task<FinanceOperationDto> AdjustAsync(AdjustmentRequest r, CancellationToken ct = default)
    {
        var c = await ContextAsync(null, null, null, ct, StaffPermissions.FinanceReverse);
        if (r.ClientRequestId is { } key && await repo.FindByClientRequestAsync(c.OrgId, key, ct) is { } done) return await GetAsync(done.Id, ct);
        var amount = Math.Round(r.Amount, 2);
        if (amount == 0) throw new ValidationException(Messages.FinanceAmountInvalid(Lang), "amount");
        var reason = CatalogAccess.Optional(r.Reason, 500) ?? throw new ValidationException(Messages.FinanceReasonRequired(Lang), "reason");
        var account = await repo.GetAccountAsync(c.OrgId, r.AccountId ?? Guid.Empty, ct) ?? throw new ValidationException(Messages.FinanceAccountNotFound(Lang), "accountId");
        await RequireStoreAsync(c, account.StoreId, ct);
        var date = r.Date is { } d ? DateTime.SpecifyKind(d, DateTimeKind.Utc) : DateTime.UtcNow;
        FinanceOperation op = null!;
        await unitOfWork.InTransactionAsync(async () =>
        {
            op = await ledger.PostAsync(c.OrgId, account.StoreId, new FinancePosting(FinanceOperationType.Adjustment, date, [new FinanceLine(account.Id, amount)],
                (Lang == AppLanguage.Ru ? "Корректировка остатка: " : "Қалдық түзетуі: ") + account.Name, Comment: reason,
                DocumentNumber: CatalogAccess.Optional(r.DocumentNumber, 64), ClientRequestId: r.ClientRequestId, AllowNegative: true), ct);
            access.Audit(c.OrgId, account.StoreId, "finance.adjustment", "finance_operation", op.Id, new { Before = account.Balance - amount }, new { After = account.Balance, Reason = reason });
            await unitOfWork.SaveChangesAsync(ct);
            return op;
        }, ct);
        return await GetAsync(op.Id, ct);
    }

    /// <summary>Сторно (§3.2): тек қолмен жасалған операциялар, аударым, инкассация, түзету. Бастапқы модульдікі — сол модульде.</summary>
    public async Task<FinanceOperationDto> ReverseAsync(Guid id, ReverseOperationRequest r, CancellationToken ct = default)
    {
        var c = await ContextAsync(null, null, null, ct, StaffPermissions.FinanceReverse);
        var reason = CatalogAccess.Optional(r.Reason, 500) ?? throw new ValidationException(Messages.FinanceReasonRequired(Lang), "reason");
        var op = await repo.GetOperationForUpdateAsync(c.OrgId, id, ct) ?? throw new NotFoundException(Messages.FinanceOperationNotFound(Lang));
        await RequireStoreAsync(c, op.StoreId, ct);
        if (op.SourceModule is not null) throw new ValidationException(Messages.FinanceSourceReverse(Lang));
        FinanceOperation reversal = null!;
        await unitOfWork.InTransactionAsync(async () =>
        {
            reversal = await ledger.ReverseAsync(c.OrgId, op, reason, ct);
            access.Audit(c.OrgId, op.StoreId, "finance.operation.reverse", "finance_operation", op.Id, null, new { op.Number, Reversal = reversal.Number, Reason = reason });
            await unitOfWork.SaveChangesAsync(ct);
            return reversal;
        }, ct);
        return await GetAsync(reversal.Id, ct);
    }

    // ---------- DTO ----------

    public async Task<IReadOnlyList<FinanceOperationDto>> ToDtosAsync(FinanceCtx c, IReadOnlyList<FinanceOperation> ops, CancellationToken ct)
    {
        if (ops.Count == 0) return [];
        var accountIds = ops.SelectMany(o => o.Entries.Select(e => e.AccountId)).Concat(ops.SelectMany(o => new[] { o.FromAccountId, o.ToAccountId }).Where(x => x != null).Select(x => x!.Value)).Distinct().ToList();
        var accounts = (await repo.ListAccountsAsync(ops.Select(o => o.StoreId).Distinct().ToList(), ct)).ToDictionary(a => a.Id, a => a.Name);
        var categories = (await repo.ListCategoriesAsync(c.OrgId, ct)).ToDictionary(x => x.Id, x => x.Name);
        var staffNames = await repo.StaffNamesAsync(ops.SelectMany(o => new[] { o.CreatedBy, o.PostedBy, o.ReversedBy }).Where(x => x != null).Select(x => x!.Value).Distinct().ToList(), ct);
        var storeNames = await repo.StoreNamesAsync(ops.Select(o => o.StoreId).Distinct().ToList(), ct);
        var reversals = await repo.ReversalsOfAsync(ops.Where(o => o.Status == FinanceOperationStatus.Reversed).Select(o => o.Id).ToList(), ct);
        _ = accountIds;
        return ops.Select(o => new FinanceOperationDto(
            o.Id, o.Number, o.Type.ToString(), o.Status.ToString(), o.Date, o.Amount, o.StoreId, storeNames.GetValueOrDefault(o.StoreId) ?? "",
            o.CategoryId, o.CategoryId is { } cid ? categories.GetValueOrDefault(cid) : null, o.Description, o.CounterpartyType?.ToString(), o.CounterpartyId, o.CounterpartyName,
            o.FromAccountId, o.FromAccountId is { } f ? accounts.GetValueOrDefault(f) : null, o.ToAccountId, o.ToAccountId is { } t ? accounts.GetValueOrDefault(t) : null,
            o.Method?.ToString(), o.DocumentNumber, o.AttachmentUrl, o.Comment, o.SourceModule, o.SourceDocumentId, SourceLink(o), o.ReversalOfId, reversals.TryGetValue(o.Id, out var reversedBy) ? reversedBy : null,
            staffNames.GetValueOrDefault(o.CreatedBy) ?? "", o.CreatedAt, o.PostedAt, o.PostedBy is { } pb ? staffNames.GetValueOrDefault(pb) : null,
            o.ReversedAt, o.ReversedBy is { } rb ? staffNames.GetValueOrDefault(rb) : null, o.ReversalReason,
            o.Entries.OrderBy(e => e.CreatedAt).Select(e => new FinanceEntryDto(e.AccountId, accounts.GetValueOrDefault(e.AccountId) ?? "", e.Amount, e.BalanceAfter)).ToList(),
            c.CanReverse && o.Status == FinanceOperationStatus.Posted && o.ReversalOfId is null && o.SourceModule is null)).ToList();
    }

    private static string? SourceLink(FinanceOperation o) => o.SourceModule switch
    {
        "sales" when o.SourceDocumentId is { } s => $"/sales?sale={s}&period=all",
        "inventory" when o.SourceDocumentId is { } d => $"/warehouse/receipts/{d}",
        _ => null,
    };

    internal static FinanceAccountDto ToDto(FinanceAccount a, Dictionary<Guid, string> storeNames, bool hasEntries) =>
        new(a.Id, a.StoreId, storeNames.GetValueOrDefault(a.StoreId) ?? "", a.Name, a.Type.ToString(), a.Currency, a.Methods.Select(m => m.ToString()).ToList(),
            a.Balance, a.OpeningBalance, a.OpeningDate, a.IsActive, a.SortOrder, a.Comment, hasEntries, a.CreatedAt);

    internal static FinanceCategoryDto ToDto(FinanceCategory c) => new(c.Id, c.Name, c.Type.ToString(), c.PnlClass.ToString(), c.IsActive, c.SortOrder, c.CreatedAt);

    private FinanceAccountType ParseType(string? raw) =>
        Enum.TryParse<FinanceAccountType>(raw, true, out var t) ? t : throw new ValidationException(Messages.FinanceTypeInvalid(Lang), "type");

    private List<PaymentMethod> ParseMethods(IReadOnlyList<string>? raw) =>
        (raw ?? []).Select(x => Enum.TryParse<PaymentMethod>(x, true, out var m) && m != PaymentMethod.Debt ? m : throw new ValidationException(Messages.PaymentMethodInvalid(Lang), "methods"))
            .Distinct().ToList();

    private PnlClass ParsePnl(string? raw, FinanceCategoryType type) =>
        raw is null ? (type == FinanceCategoryType.Income ? PnlClass.OtherIncome : PnlClass.OperatingExpense)
        : Enum.TryParse<PnlClass>(raw, true, out var p) ? p : throw new ValidationException(Messages.FinanceTypeInvalid(Lang), "pnlClass");
    // ---------- Тарихты жүктеу ----------

    /// <summary>
    /// Модуль қосылғанға дейінгі сатылым, қайтарым, қарыз төлемдері мен жеткізушіге төлемдерді шоттарға жазу (§3.1). Бір рет: SourceOperationId
    /// бойынша қайталанбайды, қайта шақырса — тек жаңа жолдар. Тарихи жазбаға овердрафт рұқсат (ол кезде шот болмаған). Құқық: finance.accounts.
    /// </summary>
    public async Task<BackfillResultDto> BackfillAsync(CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.FinanceAccounts, ct);
        var sources = await repo.BackfillSourcesAsync(orgId, ct);
        var done = (await repo.PostedSourceOperationIdsAsync(orgId, ct)).ToHashSet();
        var posted = 0; var skipped = 0;
        await unitOfWork.InTransactionAsync(async () =>
        {
            foreach (var s in sources)
            {
                if (done.Contains(s.SourceOperationId)) { skipped++; continue; }
                var lines = new List<FinanceLine>();
                foreach (var (method, amount) in s.Lines)
                    lines.Add(new FinanceLine((await ledger.AccountForMethodAsync(orgId, s.StoreId, method, ct)).Id, amount));
                await ledger.PostAsync(orgId, s.StoreId, new FinancePosting(s.Type, s.Date, lines, s.Description, CounterpartyType: s.CounterpartyType,
                    CounterpartyId: s.CounterpartyId, CounterpartyName: s.CounterpartyName, Method: s.Lines.Count == 1 ? s.Lines[0].Method : null,
                    DocumentNumber: s.DocumentNumber, SourceModule: s.Module, SourceDocumentId: s.SourceDocumentId, SourceOperationId: s.SourceOperationId,
                    AllowNegative: true), ct);
                posted++;
            }
            access.Audit(orgId, m.StoreId, "finance.backfill", "finance", orgId, null, new { Posted = posted, Skipped = skipped });
            await unitOfWork.SaveChangesAsync(ct);
            return posted;
        }, ct);
        return new BackfillResultDto(posted, skipped);
    }
}
