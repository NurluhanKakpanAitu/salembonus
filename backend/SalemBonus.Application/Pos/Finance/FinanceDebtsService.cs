using SalemBonus.Application.Common;
using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Application.Pos.Catalog;
using SalemBonus.Domain.Core;
using SalemBonus.Domain.Pos.Finance;
using SalemBonus.Domain.Pos.Inventory;
using SalemBonus.Domain.Pos.Sales;

namespace SalemBonus.Application.Pos.Finance;

/// <summary>
/// Қарыздар (ТЗ «Финансы» §11–12): клиент қарызы — проведённый қарызға сатылымнан, жеткізуші қарызы — проведённый приёмкадан.
/// Дәптердегі қарызды қолмен енгізуге болады (клиентке — сатылымсыз қарыз, жеткізушіге — «Начальный долг» құжаты), «Закрыть» — ақшасыз жабу, себебімен тарихта. Төлем: бір не бірнеше құжатқа айқын бөлу (сумма бөлулерінің қосындысы = төлем), қарыздан аспайды,
/// ақшаны азайтады/көбейтеді, бірақ выручка мен COGS-қа тимейді (§3.1). Қайталанбау — ClientRequestId.
/// </summary>
public class FinanceDebtsService(
    IFinanceRepository repo,
    ISalesRepository sales,
    IInventoryRepository inventory,
    Inventory.WarehouseService warehouses,
    FinanceOperationsService operations,
    FinanceLedger ledger,
    Sales.DebtBonusPayer bonusPayer,
    IBonusRepository bonusRepo,
    CatalogAccess access,
    IUnitOfWork unitOfWork)
{
    private AppLanguage Lang => access.Lang;

    // ---------- Клиент қарызы (§11) ----------

    public async Task<CustomerDebtsDto> CustomerDebtsAsync(string? storeIds, string? segment, string? search, int page, int pageSize, CancellationToken ct = default)
    {
        var c = await operations.ContextAsync(storeIds, null, null, ct);
        return await BuildCustomerDebtsAsync(c.StoreIds, c.Today, c.CanContacts, segment, search, page, pageSize, ct);
    }

    /// <summary>Клиент қарыздарының тізімі — «Финансы» мен «Клиенты» бөлімдеріне ортақ (құқықты шақырушы тексереді).</summary>
    public async Task<CustomerDebtsDto> BuildCustomerDebtsAsync(IReadOnlyList<Guid> storeIds, DateOnly today, bool canContacts, string? segment, string? search,
        int page, int pageSize, CancellationToken ct = default)
    {
        var debts = await repo.DebtsAsync(storeIds, segment != "paid", ct);
        var customers = await repo.CustomersAsync(debts.Select(d => d.CustomerId).Distinct().ToList(), ct);
        var rows = debts.GroupBy(d => d.CustomerId).Select(g =>
        {
            var open = g.Where(d => d.Status == DebtStatus.Open).ToList();
            var overdue = open.Where(d => d.DueDate < today).Sum(d => d.Remaining);
            var status = open.Count == 0 ? "Paid" : overdue > 0 ? "Overdue" : open.Any(d => d.Paid > 0) ? "Partial" : "Open";
            var (name, phone) = customers.GetValueOrDefault(g.Key);
            return new CustomerDebtRowDto(g.Key, string.IsNullOrWhiteSpace(name) ? "—" : name, Phone(phone, canContacts), open.Sum(d => d.Remaining), overdue,
                open.Count == 0 ? null : open.Min(d => d.DueDate), open.Count, status);
        }).ToList();
        var all = rows.Where(r => r.Total > 0).ToList();
        var filtered = segment switch
        {
            "overdue" => rows.Where(r => r.Overdue > 0),
            "due" => rows.Where(r => r.Total > 0 && r.Overdue == 0),
            "paid" => rows.Where(r => r.Total == 0),
            _ => rows.Where(r => r.Total > 0),
        };
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var phone = PhoneSearch.Parse(term, 4);
            filtered = filtered.Where(r => r.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase)
                                           || (phone?.Matches(customers.GetValueOrDefault(r.CustomerId).Phone) ?? false));
        }
        var list = filtered.OrderByDescending(r => r.Overdue).ThenByDescending(r => r.Total).ToList();
        var size = Math.Clamp(pageSize, 1, 100);
        var p = Math.Max(1, page);
        return new CustomerDebtsDto(all.Count, all.Sum(r => r.Total), all.Sum(r => r.Overdue),
            debts.Where(d => d.Status == DebtStatus.Open && d.DueDate >= today && d.DueDate <= today.AddDays(7)).Sum(d => d.Remaining),
            list.Skip((p - 1) * size).Take(size).ToList(), list.Count);
    }

    public async Task<CustomerDebtCardDto> CustomerCardAsync(Guid customerId, string? storeIds, CancellationToken ct = default)
    {
        var c = await operations.ContextAsync(storeIds, null, null, ct);
        return await BuildCustomerCardAsync(c.StoreIds, c.Today, c.CanContacts, customerId, ct);
    }

    /// <summary>Клиенттің қарыз карточкасы — «Финансы» мен «Клиенты» бөлімдеріне ортақ (құқықты шақырушы тексереді).</summary>
    public async Task<CustomerDebtCardDto> BuildCustomerCardAsync(IReadOnlyList<Guid> storeIds, DateOnly today, bool canContacts, Guid customerId, CancellationToken ct = default)
    {
        var debts = (await repo.DebtsAsync(storeIds, false, ct)).Where(d => d.CustomerId == customerId).OrderByDescending(d => d.CreatedAt).ToList();
        var (name, phone) = (await repo.CustomersAsync([customerId], ct)).GetValueOrDefault(customerId);
        var payments = await repo.DebtPaymentsAsync(debts.Select(d => d.Id).ToList(), ct);
        var staff = await repo.StaffNamesAsync(payments.Select(p => p.StaffUserId).Distinct().ToList(), ct);
        var ops = await repo.OperationsBySourceAsync(payments.Select(p => p.Id).ToList(), ct);
        var numbers = debts.ToDictionary(d => d.Id, d => d.SaleNumber);
        var bonusInfo = new Dictionary<Guid, (int Balance, decimal MaxPercent)>();
        foreach (var storeId in debts.Where(d => d.Status == DebtStatus.Open).Select(d => d.StoreId).Distinct())
            bonusInfo[storeId] = await bonusPayer.InfoAsync(customerId, storeId, ct);
        var open = debts.Where(d => d.Status == DebtStatus.Open).ToList();
        return new CustomerDebtCardDto(customerId, string.IsNullOrWhiteSpace(name) ? "—" : name, Phone(phone, canContacts),
            open.Sum(d => d.Remaining), open.Where(d => d.DueDate < today).Sum(d => d.Remaining), open.Count == 0 ? null : open.Min(d => d.DueDate),
            debts.Select(d => new CustomerDebtDocDto(d.Id, d.SaleId, d.SaleNumber, d.CreatedAt, d.DueDate, d.Amount, d.Paid, d.Remaining,
                d.Status == DebtStatus.Paid ? "Paid" : d.DueDate < today ? "Overdue" : d.Paid > 0 ? "Partial" : "Open", d.Status == DebtStatus.Open && d.DueDate < today, d.Comment, d.StoreId, d.Status == DebtStatus.Open ? Sales.DebtBonusPayer.Limit(d, bonusInfo.GetValueOrDefault(d.StoreId).MaxPercent) : 0)).ToList(),
            payments.Select(p => new CustomerDebtPaymentDto(p.Id, p.CreatedAt, p.DebtId, numbers.GetValueOrDefault(p.DebtId), p.Amount, p.Method?.ToString(), p.IsReturn,
                p.RemainingAfter, staff.GetValueOrDefault(p.StaffUserId) ?? "", ops.GetValueOrDefault(p.Id).Id is { } oid && oid != Guid.Empty ? oid : null,
                ops.GetValueOrDefault(p.Id).Number, p.IsWriteOff, p.Comment, p.IsBonus)).ToList(),
            bonusInfo.Select(b => new CustomerBonusBalanceDto(b.Key, b.Value.Balance, b.Value.MaxPercent)).ToList());
    }

    /// <summary>«Принять оплату» (§11.1): бөлулер бойынша, FIFO жоқ; әр қарызға өз төлемі мен өз қаржы операциясы.</summary>
    public async Task<CustomerDebtCardDto> CustomerPaymentAsync(CustomerPaymentRequest r, CancellationToken ct = default)
    {
        var c = await operations.ContextAsync(null, null, null, ct, StaffPermissions.FinanceOperate);
        if (r.CustomerId is not { } customerId) throw new ValidationException(Messages.FinanceCounterpartyRequired(Lang), "customerId");
        if (r.ClientRequestId is { } key && await repo.FindByClientRequestAsync(c.OrgId, key, ct) is not null) return await BuildCustomerCardAsync(c.StoreIds, c.Today, c.CanContacts, customerId, ct);
        var allocations = Allocations(r.Allocations?.Select(a => (a.DebtId, a.Amount)));
        if (string.Equals(r.Method, "Bonus", StringComparison.OrdinalIgnoreCase)) return await CustomerBonusPaymentAsync(c, customerId, allocations, r, ct);
        var method = ParseMethod(r.Method);
        var account = await AccountAsync(c, r.AccountId, method, ct);
        var date = r.Date is { } d ? DateTime.SpecifyKind(d, DateTimeKind.Utc) : DateTime.UtcNow;
        var comment = CatalogAccess.Optional(r.Comment, 500);
        var (name, _) = (await repo.CustomersAsync([customerId], ct)).GetValueOrDefault(customerId);

        await unitOfWork.InTransactionAsync(async () =>
        {
            var debts = (await repo.DebtsForUpdateAsync(allocations.Keys.ToList(), ct)).ToDictionary(x => x.Id);
            var first = true;
            foreach (var (debtId, amount) in allocations)
            {
                var debt = debts.GetValueOrDefault(debtId) ?? throw new ValidationException(Messages.DebtNotFound(Lang), "allocations");
                if (debt.CustomerId != customerId || !c.StoreIds.Contains(debt.StoreId)) throw new ValidationException(Messages.DebtNotFound(Lang), "allocations");
                if (debt.Status != DebtStatus.Open) throw new ValidationException(Messages.DebtAlreadyPaid(Lang), "allocations");
                if (amount > debt.Remaining) throw new ValidationException(Messages.FinanceAllocationTooMuch(Lang, debt.Remaining), "allocations");
                debt.Paid += amount;
                var payment = new DebtPayment
                {
                    Id = Guid.NewGuid(), DebtId = debt.Id, Amount = amount, Method = method, RemainingAfter = debt.Remaining, StaffUserId = access.StaffUserId, CreatedAt = date,
                };
                sales.AddDebtPayment(payment);
                if (debt.Remaining <= 0) { debt.Status = DebtStatus.Paid; debt.ClosedAt = date; }
                await ledger.PostAsync(c.OrgId, debt.StoreId, new FinancePosting(FinanceOperationType.CustomerPayment, date, [new FinanceLine(account.Id, amount)],
                    (Lang == AppLanguage.Ru ? "Погашение долга клиента" : "Клиент қарызын өтеу") + (debt.SaleNumber is { } n ? $" · чек №{n}" : ""),
                    CounterpartyType: CounterpartyType.Customer, CounterpartyId: customerId, CounterpartyName: name, Method: method,
                    DocumentNumber: CatalogAccess.Optional(r.DocumentNumber, 64), Comment: comment, SourceModule: "sales", SourceDocumentId: debt.SaleId,
                    SourceOperationId: payment.Id, ClientRequestId: first ? r.ClientRequestId : null), ct);
                first = false;
                access.Audit(c.OrgId, debt.StoreId, "finance.customer.payment", "debt", debt.Id, null, new { Amount = amount, Method = method.ToString(), Account = account.Name, Remaining = debt.Remaining });
            }
            await unitOfWork.SaveChangesAsync(ct);
            return true;
        }, ct);
        return await BuildCustomerCardAsync(c.StoreIds, c.Today, c.CanContacts, customerId, ct);
    }

    /// <summary>Бонуспен өтеу (1 Б = 1 ₸): әр қарыз өз дүкенінің бонус картасынан, сатылымдағы шекпен; ақша шоттарына тимейді, қаржы операциясы жоқ.</summary>
    private async Task<CustomerDebtCardDto> CustomerBonusPaymentAsync(FinanceCtx c, Guid customerId, IReadOnlyDictionary<Guid, decimal> allocations, CustomerPaymentRequest r, CancellationToken ct)
    {
        if (r.ClientRequestId is { } key && await bonusRepo.ByIdempotencyKeyAsync(key, ct) is not null) return await BuildCustomerCardAsync(c.StoreIds, c.Today, c.CanContacts, customerId, ct);
        var comment = CatalogAccess.Optional(r.Comment, 500);
        await unitOfWork.InTransactionAsync(async () =>
        {
            var debts = (await repo.DebtsForUpdateAsync(allocations.Keys.ToList(), ct)).ToDictionary(x => x.Id);
            var first = true;
            var now = DateTime.UtcNow;
            foreach (var (debtId, amount) in allocations)
            {
                var debt = debts.GetValueOrDefault(debtId) ?? throw new ValidationException(Messages.DebtNotFound(Lang), "allocations");
                if (debt.CustomerId != customerId || !c.StoreIds.Contains(debt.StoreId)) throw new ValidationException(Messages.DebtNotFound(Lang), "allocations");
                if (debt.Status != DebtStatus.Open) throw new ValidationException(Messages.DebtAlreadyPaid(Lang), "allocations");
                if (amount > debt.Remaining) throw new ValidationException(Messages.FinanceAllocationTooMuch(Lang, debt.Remaining), "allocations");
                var payment = await bonusPayer.PayAsync(debt, amount, null, comment, now, ct, first ? r.ClientRequestId : null);
                sales.AddDebtPayment(payment);
                first = false;
                access.Audit(c.OrgId, debt.StoreId, "finance.customer.payment", "debt", debt.Id, null, new { Amount = amount, Method = "Bonus", Remaining = debt.Remaining });
            }
            await unitOfWork.SaveChangesAsync(ct);
            return true;
        }, ct);
        return await BuildCustomerCardAsync(c.StoreIds, c.Today, c.CanContacts, customerId, ct);
    }

    /// <summary>
    /// «Добавить долг» (дәптерден): сатылымсыз қарыз — клиент, сома, пайда болған күні, мерзімі. Ақшаға да, выручкаға да тимейді:
    /// тауар бұрын (жүйеге дейін) берілген, мұнда тек клиенттің қарызы тіркеледі. Аудитке жазылады.
    /// </summary>
    public async Task<CustomerDebtCardDto> AddCustomerDebtAsync(ManualCustomerDebtRequest r, CancellationToken ct = default)
    {
        var c = await operations.ContextAsync(null, null, null, ct, StaffPermissions.FinanceOperate);
        if (r.CustomerId is not { } customerId) throw new ValidationException(Messages.FinanceCounterpartyRequired(Lang), "customerId");
        if (!(await repo.CustomersAsync([customerId], ct)).ContainsKey(customerId))
            throw new ValidationException(Messages.CustomerNotFound(Lang), "customerId");
        var amount = Math.Round(r.Amount, 2);
        if (amount <= 0) throw new ValidationException(Messages.FinanceAmountInvalid(Lang), "amount");
        var (createdAt, date) = ManualDate(c, r.Date);
        var due = r.DueDate ?? date.AddDays(30);
        if (due < date) throw new ValidationException(Messages.DebtDueBeforeDate(Lang), "dueDate");
        var debt = new Debt
        {
            Id = Guid.NewGuid(), OrganizationId = c.OrgId, StoreId = c.Current.StoreId, CustomerId = customerId, Amount = amount, DueDate = due,
            Comment = CatalogAccess.Optional(r.Comment, 500), StaffUserId = access.StaffUserId, CreatedAt = createdAt,
        };
        sales.AddDebt(debt);
        access.Audit(c.OrgId, debt.StoreId, "finance.customer.debt.add", "debt", debt.Id, null, new { Amount = amount, Date = date, DueDate = due, debt.Comment });
        await unitOfWork.SaveChangesAsync(ct);
        return await BuildCustomerCardAsync(c.StoreIds, c.Today, c.CanContacts, customerId, ct);
    }

    /// <summary>«Закрыть долг»: ақшасыз жабу (дәптер бойынша өтелген, кешірілді т.б.). Тарихқа «Закрыт» жазбасы, себеп міндетті; шот, выручка, касса өзгермейді.</summary>
    public async Task<CustomerDebtCardDto> CloseCustomerDebtAsync(Guid debtId, CloseDebtRequest r, CancellationToken ct = default)
    {
        var c = await operations.ContextAsync(null, null, null, ct, StaffPermissions.FinanceOperate);
        var reason = CloseReason(r);
        var customerId = Guid.Empty;
        await unitOfWork.InTransactionAsync(async () =>
        {
            var debt = (await repo.DebtsForUpdateAsync([debtId], ct)).FirstOrDefault();
            if (debt is null || !c.StoreIds.Contains(debt.StoreId)) throw new NotFoundException(Messages.DebtNotFound(Lang));
            if (debt.Status != DebtStatus.Open || debt.Remaining <= 0) throw new ValidationException(Messages.DebtAlreadyPaid(Lang), "debtId");
            customerId = debt.CustomerId;
            var amount = debt.Remaining;
            var now = DateTime.UtcNow;
            debt.Paid += amount;
            debt.Status = DebtStatus.Paid;
            debt.ClosedAt = now;
            sales.AddDebtPayment(new DebtPayment
            {
                Id = Guid.NewGuid(), DebtId = debt.Id, Amount = amount, Method = null, IsWriteOff = true, Comment = reason, RemainingAfter = 0,
                StaffUserId = access.StaffUserId, CreatedAt = now,
            });
            access.Audit(c.OrgId, debt.StoreId, "finance.customer.debt.close", "debt", debt.Id, new { Remaining = amount }, new { Remaining = 0, Reason = reason });
            await unitOfWork.SaveChangesAsync(ct);
            return true;
        }, ct);
        return await BuildCustomerCardAsync(c.StoreIds, c.Today, c.CanContacts, customerId, ct);
    }

    // ---------- Жеткізуші қарызы (§12) ----------

    public async Task<SupplierDebtsDto> SupplierDebtsAsync(string? storeIds, DateOnly? from, DateOnly? to, string? segment, string? search, int page, int pageSize, CancellationToken ct = default)
    {
        var c = await operations.ContextAsync(storeIds, from, to, ct);
        var today = c.Today;
        var docs = await repo.SupplierDocumentsAsync(c.StoreIds, ct);
        var payments = await repo.SupplierPaymentsAsync(c.StoreIds, c.FromUtc, c.ToUtc, ct);
        var suppliers = await repo.SuppliersAsync(c.OrgId, ct);
        var rows = docs.GroupBy(d => d.SupplierId!.Value).Select(g =>
        {
            var open = g.Where(d => d.Remaining > 0).ToList();
            var overdue = open.Where(d => d.PaymentDueDate < today).Sum(d => d.Remaining);
            var s = suppliers.GetValueOrDefault(g.Key);
            var inPeriod = g.Where(d => d.Type == StockDocumentType.Receipt && d.Date >= c.FromUtc && d.Date < c.ToUtc).ToList();
            return new SupplierDebtRowDto(g.Key, s.Name ?? "—", Phone(s.Phone, c.CanContacts), g.Max(d => (DateTime?)d.Date), inPeriod.Sum(d => d.Total),
                payments.Where(p => p.SupplierId == g.Key && p.Status == SupplierPaymentStatus.Registered && !p.IsWriteOff).Sum(p => p.Amount),
                open.Sum(d => d.Remaining), overdue, open.Where(d => d.PaymentDueDate != null).Min(d => d.PaymentDueDate), open.Count,
                open.Count == 0 ? "Paid" : overdue > 0 ? "Overdue" : open.Any(d => d.Paid > 0) ? "Partial" : "Open");
        }).ToList();
        var open = docs.Where(d => d.Remaining > 0).ToList();
        var filtered = segment switch
        {
            "overdue" => rows.Where(r => r.Overdue > 0),
            "due" => rows.Where(r => r.Debt > 0 && r.Overdue == 0),
            "paid" => rows.Where(r => r.Debt == 0),
            "all" => rows,
            _ => rows.Where(r => r.Debt > 0),
        };
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var phone = PhoneSearch.Parse(term, 4);
            filtered = filtered.Where(r => r.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase)
                                           || (suppliers.GetValueOrDefault(r.SupplierId).TaxId?.Contains(term) ?? false)
                                           || (phone?.Matches(suppliers.GetValueOrDefault(r.SupplierId).Phone) ?? false)
                                           || docs.Any(d => d.SupplierId == r.SupplierId && (d.Number.Contains(term, StringComparison.OrdinalIgnoreCase) || (d.SupplierInvoiceNumber?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false))));
        }
        var list = filtered.OrderByDescending(r => r.Overdue).ThenByDescending(r => r.Debt).ToList();
        var size = Math.Clamp(pageSize, 1, 100);
        var p = Math.Max(1, page);
        return new SupplierDebtsDto(open.Sum(d => d.Remaining), open.Where(d => d.PaymentDueDate < today).Sum(d => d.Remaining),
            open.Where(d => d.PaymentDueDate >= today && d.PaymentDueDate <= today.AddDays(7)).Sum(d => d.Remaining), open.Count, docs.Count,
            list.Skip((p - 1) * size).Take(size).ToList(), list.Count);
    }

    public async Task<SupplierDebtCardDto> SupplierCardAsync(Guid supplierId, string? storeIds, CancellationToken ct = default)
    {
        var c = await operations.ContextAsync(storeIds, null, null, ct);
        var today = c.Today;
        var docs = (await repo.SupplierDocumentsAsync(c.StoreIds, ct)).Where(d => d.SupplierId == supplierId).ToList();
        var payments = (await repo.SupplierPaymentsAsync(c.StoreIds, null, null, ct)).Where(p => p.SupplierId == supplierId).ToList();
        var s = (await repo.SuppliersAsync(c.OrgId, ct)).GetValueOrDefault(supplierId);
        if (s.Name is null) throw new NotFoundException(Messages.SupplierNotFound(Lang));
        var staff = await repo.StaffNamesAsync(payments.Select(p => p.CreatedBy).Distinct().ToList(), ct);
        var ops = await repo.OperationsBySourceAsync(payments.Select(p => p.Id).ToList(), ct);
        var opIds = ops.Values.Select(v => v.Id).ToList();
        var accountNames = new Dictionary<Guid, string>();
        foreach (var oid in opIds)
        {
            var op = await repo.GetOperationAsync(c.OrgId, oid, ct);
            if (op?.FromAccountId is { } fa) accountNames[oid] = (await repo.GetAccountAsync(c.OrgId, fa, ct))?.Name ?? "";
        }
        var numbers = docs.ToDictionary(d => d.Id, d => d.Number);
        var open = docs.Where(d => d.Remaining > 0).ToList();
        return new SupplierDebtCardDto(supplierId, s.Name, Phone(s.Phone, c.CanContacts), s.TaxId, s.Address, open.Sum(d => d.Remaining),
            open.Where(d => d.PaymentDueDate < today).Sum(d => d.Remaining), open.Count, open.Where(d => d.PaymentDueDate != null).Min(d => d.PaymentDueDate),
            docs.OrderByDescending(d => d.Date).Select(d => new SupplierDebtDocDto(d.Id, d.Number, d.Date, d.SupplierInvoiceNumber, d.Total, d.Paid, d.Remaining, d.PaymentDueDate,
                d.Remaining > 0 && d.PaymentDueDate < today, d.Lines.Count, d.Type == StockDocumentType.OpeningDebt ? "opening" : "receipt", d.WrittenOff, d.Comment)).ToList(),
            payments.OrderByDescending(p => p.CreatedAt).Select(p => new SupplierPaymentRowDto(p.Id, p.CreatedAt, p.Date, p.DocumentId,
                p.DocumentId is { } did ? numbers.GetValueOrDefault(did) : null, p.Amount, p.Method?.ToString() ?? "WriteOff", p.Reference, p.Status.ToString(),
                staff.GetValueOrDefault(p.CreatedBy) ?? "", ops.GetValueOrDefault(p.Id).Id is { } oid && oid != Guid.Empty ? oid : null, ops.GetValueOrDefault(p.Id).Number,
                ops.GetValueOrDefault(p.Id).Id is { } oid2 && accountNames.TryGetValue(oid2, out var an) ? an : null, p.IsWriteOff, p.Comment)).ToList());
    }

    /// <summary>«Оплатить поставщику» (§12.2): бірнеше накладнаяға бөлу; әр накладнаяға өз төлем жазбасы мен қаржы операциясы (сторно — жеке).</summary>
    public async Task<SupplierDebtCardDto> SupplierPaymentAsync(SupplierPaymentRequest r, CancellationToken ct = default)
    {
        var c = await operations.ContextAsync(null, null, null, ct, StaffPermissions.FinanceOperate);
        if (r.SupplierId is not { } supplierId) throw new ValidationException(Messages.FinanceCounterpartyRequired(Lang), "supplierId");
        if (r.ClientRequestId is { } key && await repo.FindByClientRequestAsync(c.OrgId, key, ct) is not null) return await SupplierCardAsync(supplierId, null, ct);
        var allocations = Allocations(r.Allocations?.Select(a => (a.DocumentId, a.Amount)));
        var method = ParseMethod(r.Method);
        var account = await AccountAsync(c, r.AccountId, method, ct);
        var date = r.Date is { } d ? DateTime.SpecifyKind(d, DateTimeKind.Utc) : DateTime.UtcNow;
        var supplierName = (await repo.SuppliersAsync(c.OrgId, ct)).GetValueOrDefault(supplierId).Name ?? throw new NotFoundException(Messages.SupplierNotFound(Lang));
        var reference = CatalogAccess.Optional(r.Reference, 64);
        var comment = CatalogAccess.Optional(r.Comment, 500);

        await unitOfWork.InTransactionAsync(async () =>
        {
            var first = true;
            foreach (var (docId, amount) in allocations)
            {
                var doc = await inventory.GetDocumentForUpdateAsync(c.OrgId, docId, ct) ?? throw new ValidationException(Messages.DocumentNotFound(Lang), "allocations");
                if (doc.SupplierId != supplierId || doc.Type is not (StockDocumentType.Receipt or StockDocumentType.OpeningDebt) || doc.Status != StockDocumentStatus.Posted || !c.StoreIds.Contains(doc.StoreId))
                    throw new ValidationException(Messages.DocumentNotPosted(Lang), "allocations");
                // Параллель төлем (§17): құжат бұғатталған күйде қалдық қайта тексеріледі.
                if (amount > doc.Remaining) throw new ValidationException(Messages.PaymentExceedsDebt(Lang, doc.Remaining), "allocations");
                var payment = new SupplierPayment
                {
                    Id = Guid.NewGuid(), OrganizationId = c.OrgId, StoreId = doc.StoreId, SupplierId = supplierId, DocumentId = doc.Id, Amount = amount, Method = method,
                    Reference = reference, Date = c.LocalDate(date), Comment = comment, ClientRequestId = first ? r.ClientRequestId : null, CreatedBy = access.StaffUserId, CreatedAt = date,
                };
                inventory.AddPayment(payment);
                doc.Paid += amount;
                doc.UpdatedAt = date;
                await ledger.PostAsync(c.OrgId, doc.StoreId, new FinancePosting(FinanceOperationType.SupplierPayment, date, [new FinanceLine(account.Id, -amount)],
                    (Lang == AppLanguage.Ru ? "Оплата поставщику" : "Жеткізушіге төлем") + $" · {doc.Number}", CounterpartyType: CounterpartyType.Supplier,
                    CounterpartyId: supplierId, CounterpartyName: supplierName, Method: method, DocumentNumber: reference, AttachmentUrl: CatalogAccess.Optional(r.AttachmentUrl, 500),
                    Comment: comment, SourceModule: "inventory", SourceDocumentId: doc.Id, SourceOperationId: payment.Id, ClientRequestId: first ? r.ClientRequestId : null), ct);
                first = false;
                access.Audit(c.OrgId, doc.StoreId, "inventory.supplier.payment", "supplier_payment", payment.Id, null,
                    new { doc.Number, payment.Amount, Method = method.ToString(), Account = account.Name, Remaining = doc.Remaining });
            }
            await unitOfWork.SaveChangesAsync(ct);
            return true;
        }, ct);
        return await SupplierCardAsync(supplierId, null, ct);
    }

    /// <summary>
    /// «Добавить долг» поставщику (дәптерден): тауарсыз «Начальный долг» құжаты (ДЛГ-…). Қалдыққа, закупкаға, приёмкалар тізіміне тимейді,
    /// тек жеткізуші қарызына қосылады; оны кәдімгідей төлеуге не «Закрыть»-пен жабуға болады.
    /// </summary>
    public async Task<SupplierDebtCardDto> AddSupplierDebtAsync(ManualSupplierDebtRequest r, CancellationToken ct = default)
    {
        var c = await operations.ContextAsync(null, null, null, ct, StaffPermissions.FinanceOperate);
        if (r.SupplierId is not { } supplierId) throw new ValidationException(Messages.FinanceCounterpartyRequired(Lang), "supplierId");
        if ((await repo.SuppliersAsync(c.OrgId, ct)).GetValueOrDefault(supplierId).Name is null) throw new ValidationException(Messages.SupplierNotFound(Lang), "supplierId");
        var amount = Math.Round(r.Amount, 2);
        if (amount <= 0) throw new ValidationException(Messages.FinanceAmountInvalid(Lang), "amount");
        var (createdAt, date) = ManualDate(c, r.Date);
        if (r.DueDate is { } due && due < date) throw new ValidationException(Messages.DebtDueBeforeDate(Lang), "dueDate");
        // Құжат дүкен қоймасына байланады (схема талабы), бірақ жолсыз — қалдыққа әсері жоқ.
        var warehouse = await warehouses.EnsureDefaultAsync(c.OrgId, c.Current.StoreId, ct);
        var doc = new StockDocument
        {
            Id = Guid.NewGuid(), OrganizationId = c.OrgId, StoreId = c.Current.StoreId, WarehouseId = warehouse.Id, Type = StockDocumentType.OpeningDebt,
            Status = StockDocumentStatus.Posted, Date = createdAt, SupplierId = supplierId, SupplierInvoiceNumber = CatalogAccess.Optional(r.DocumentNumber, 64),
            Comment = CatalogAccess.Optional(r.Comment, 1000), Total = amount, PaymentDueDate = r.DueDate,
            CreatedBy = access.StaffUserId, PostedAt = DateTime.UtcNow, PostedBy = access.StaffUserId,
        };
        doc.Seq = await inventory.NextDocumentNumberAsync(c.OrgId, "supplierdebt", ct);
        doc.Number = $"ДЛГ-{doc.Seq:D6}";
        inventory.AddDocument(doc);
        access.Audit(c.OrgId, doc.StoreId, "finance.supplier.debt.add", "stock_document", doc.Id, null,
            new { doc.Number, Amount = amount, Date = date, r.DueDate, doc.SupplierInvoiceNumber, doc.Comment });
        await unitOfWork.SaveChangesAsync(ct);
        return await SupplierCardAsync(supplierId, null, ct);
    }

    /// <summary>«Закрыть долг» поставщику: қалдықты ақшасыз жабу, себеп міндетті. Төлем емес — шот пен ақша ағынына тимейді, тарихта «Закрыт» болып көрінеді.</summary>
    public async Task<SupplierDebtCardDto> CloseSupplierDebtAsync(Guid documentId, CloseDebtRequest r, CancellationToken ct = default)
    {
        var c = await operations.ContextAsync(null, null, null, ct, StaffPermissions.FinanceOperate);
        var reason = CloseReason(r);
        var supplierId = Guid.Empty;
        await unitOfWork.InTransactionAsync(async () =>
        {
            var doc = await inventory.GetDocumentForUpdateAsync(c.OrgId, documentId, ct);
            if (doc is null || doc.SupplierId is null || doc.Type is not (StockDocumentType.Receipt or StockDocumentType.OpeningDebt)
                || doc.Status != StockDocumentStatus.Posted || !c.StoreIds.Contains(doc.StoreId))
                throw new NotFoundException(Messages.DocumentNotFound(Lang));
            if (doc.Remaining <= 0) throw new ValidationException(Messages.DebtAlreadyPaid(Lang), "documentId");
            supplierId = doc.SupplierId.Value;
            var amount = doc.Remaining;
            var now = DateTime.UtcNow;
            inventory.AddPayment(new SupplierPayment
            {
                Id = Guid.NewGuid(), OrganizationId = c.OrgId, StoreId = doc.StoreId, SupplierId = supplierId, DocumentId = doc.Id, Amount = amount, Method = null,
                IsWriteOff = true, Date = c.LocalDate(now), Comment = reason, CreatedBy = access.StaffUserId, CreatedAt = now,
            });
            doc.WrittenOff += amount;
            doc.UpdatedAt = now;
            access.Audit(c.OrgId, doc.StoreId, "finance.supplier.debt.close", "stock_document", doc.Id, new { doc.Number, Remaining = amount }, new { Remaining = 0, Reason = reason });
            await unitOfWork.SaveChangesAsync(ct);
            return true;
        }, ct);
        return await SupplierCardAsync(supplierId, null, ct);
    }

    public async Task SetDueDateAsync(Guid documentId, SetDueDateRequest r, CancellationToken ct = default)
    {
        var c = await operations.ContextAsync(null, null, null, ct, StaffPermissions.FinanceOperate);
        var doc = await inventory.GetDocumentForUpdateAsync(c.OrgId, documentId, ct) ?? throw new NotFoundException(Messages.DocumentNotFound(Lang));
        var old = doc.PaymentDueDate;
        doc.PaymentDueDate = r.DueDate;
        doc.UpdatedAt = DateTime.UtcNow;
        access.Audit(c.OrgId, doc.StoreId, "finance.supplier.due_date", "stock_document", doc.Id, new { DueDate = old }, new { r.DueDate });
        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>Салыстыру актісі (§12.1): кезеңдегі накладнаялар (дебет — біз қарызданамыз) мен төлемдер (кредит), басы/соңындағы сальдо.</summary>
    public async Task<ReconciliationDto> SupplierReconciliationAsync(Guid supplierId, string? storeIds, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var c = await operations.ContextAsync(storeIds, from, to, ct);
        var s = (await repo.SuppliersAsync(c.OrgId, ct)).GetValueOrDefault(supplierId);
        if (s.Name is null) throw new NotFoundException(Messages.SupplierNotFound(Lang));
        var docs = (await repo.SupplierDocumentsAsync(c.StoreIds, ct)).Where(d => d.SupplierId == supplierId).ToList();
        var payments = (await repo.SupplierPaymentsAsync(c.StoreIds, null, null, ct)).Where(p => p.SupplierId == supplierId && p.Status == SupplierPaymentStatus.Registered).ToList();
        var opening = docs.Where(d => d.Date < c.FromUtc).Sum(d => d.Total) - payments.Where(p => p.CreatedAt < c.FromUtc).Sum(p => p.Amount);
        var rows = docs.Where(d => d.Date >= c.FromUtc && d.Date < c.ToUtc)
            .Select(d => (d.Date, Kind: d.Type == StockDocumentType.OpeningDebt ? "opening" : "receipt", d.Number, Description: d.SupplierInvoiceNumber ?? d.Comment, Debit: d.Total, Credit: 0m))
            .Concat(payments.Where(p => p.CreatedAt >= c.FromUtc && p.CreatedAt < c.ToUtc)
                .Select(p => (Date: p.CreatedAt, Kind: p.IsWriteOff ? "writeoff" : "payment", Number: p.Reference ?? "", Description: p.IsWriteOff ? p.Comment : (string?)$"{p.Method}", Debit: 0m, Credit: p.Amount)))
            .OrderBy(x => x.Date).ToList();
        var balance = opening;
        var list = rows.Select(x => { balance += x.Debit - x.Credit; return new ReconciliationRowDto(x.Date, x.Kind, x.Number, x.Description, x.Debit, x.Credit, balance); }).ToList();
        var storeNames = await repo.StoreNamesAsync(c.StoreIds, ct);
        return new ReconciliationDto(s.Name, s.TaxId, c.Start, c.End, opening, rows.Sum(x => x.Debit), rows.Sum(x => x.Credit), balance, list, c.NowUtc,
            string.Join(", ", storeNames.Values));
    }

    // ---------- Көмекші ----------

    /// <summary>Қолмен енгізілген қарыздың күні: бүгін болса — қазіргі сәт, бұрынғы күн болса — сол күннің түсі (дүкен уақыт белдеуі). Болашақ — қате.</summary>
    private (DateTime Utc, DateOnly Date) ManualDate(FinanceCtx c, DateOnly? raw)
    {
        var date = raw ?? c.Today;
        if (date > c.Today) throw new ValidationException(Messages.DebtDateInFuture(Lang), "date");
        if (date == c.Today) return (c.NowUtc, date);
        var local = DateTime.SpecifyKind(date.ToDateTime(new TimeOnly(12, 0)), DateTimeKind.Unspecified);
        return (TimeZoneInfo.ConvertTimeToUtc(local, c.Zone), date);
    }

    private string CloseReason(CloseDebtRequest r) =>
        CatalogAccess.Optional(r.Reason, 500) is { Length: >= 3 } reason ? reason : throw new ValidationException(Messages.DebtCloseReasonRequired(Lang), "reason");

    private Dictionary<Guid, decimal> Allocations(IEnumerable<(Guid Id, decimal Amount)>? raw)
    {
        var list = (raw ?? []).Select(a => (a.Id, Amount: Math.Round(a.Amount, 2))).Where(a => a.Amount != 0).ToList();
        if (list.Count == 0 || list.Any(a => a.Amount < 0)) throw new ValidationException(Messages.FinanceAmountInvalid(Lang), "allocations");
        return list.GroupBy(a => a.Id).ToDictionary(g => g.Key, g => g.Sum(a => a.Amount));
    }

    private PaymentMethod ParseMethod(string? raw) =>
        Enum.TryParse<PaymentMethod>(raw, true, out var m) && m != PaymentMethod.Debt ? m : throw new ValidationException(Messages.PaymentMethodInvalid(Lang), "method");

    private async Task<FinanceAccount> AccountAsync(FinanceCtx c, Guid? accountId, PaymentMethod method, CancellationToken ct)
    {
        if (accountId is { } id)
        {
            var a = await repo.GetAccountAsync(c.OrgId, id, ct) ?? throw new ValidationException(Messages.FinanceAccountNotFound(Lang), "accountId");
            if (!a.IsActive) throw new ValidationException(Messages.FinanceAccountInactive(Lang, a.Name), "accountId");
            return a;
        }
        var byMethod = await ledger.AccountForMethodAsync(c.OrgId, c.Current.StoreId, method, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return byMethod;
    }

    internal static string? Phone(string? phone, bool canContacts) => Analytics.AnalyticsService.Phone(phone, canContacts);
}
