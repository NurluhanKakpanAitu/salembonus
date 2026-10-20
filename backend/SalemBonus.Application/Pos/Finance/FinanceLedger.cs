using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Application.Pos.Inventory;
using SalemBonus.Domain.Pos.Finance;
using SalemBonus.Domain.Pos.Sales;

namespace SalemBonus.Application.Pos.Finance;

/// <summary>Бір проводка: шот және сома (+ кіріс, − шығыс).</summary>
public record FinanceLine(Guid AccountId, decimal Amount);

public record FinancePosting(
    FinanceOperationType Type, DateTime Date, IReadOnlyList<FinanceLine> Lines, string Description,
    Guid? CategoryId = null, CounterpartyType? CounterpartyType = null, Guid? CounterpartyId = null, string? CounterpartyName = null,
    PaymentMethod? Method = null, string? DocumentNumber = null, string? AttachmentUrl = null, string? Comment = null,
    string? SourceModule = null, Guid? SourceDocumentId = null, Guid? SourceOperationId = null, Guid? ClientRequestId = null,
    /// <summary>Бастапқы модульден түсетін операцияларда теріс қалдыққа рұқсат: кассадағы сатылымды қаржы модулі тоқтатпауы керек.</summary>
    bool AllowNegative = false);

/// <summary>
/// Қаржы ядросы (ТЗ «Финансы» §16): операция мен проводкалар атомарлы; шот қалдығы проводкалар журналының кэшталған қосындысы,
/// проводка жазылғанда шот FOR UPDATE-пен бұғатталады. Қайталанбау: SourceOperationId (бастапқы құжат) не ClientRequestId.
/// Сторно — кері жазба, түпнұсқа Reversed болып қалады (§3.2). Овердрафт әдепкіде жабық (§17).
/// Шақырушы транзакцияның ішінде және SaveChanges-ке дейін жұмыс істейді.
/// </summary>
public class FinanceLedger(IFinanceRepository repo, IStoreRepository stores, ICurrentLanguage language, ICurrentStaff current, IUnitOfWork unitOfWork)
{
    public const string NumberPrefix = "ФД";
    private AppLanguage Lang => language.Value;

    /// <summary>Әдепкі шоттар: бір дүкенге бір рет. Касса, банк (карта + аударым), Kaspi (QR), сейф, жолдағы ақша.</summary>
    public async Task<IReadOnlyList<FinanceAccount>> EnsureAccountsAsync(Guid orgId, Guid storeId, CancellationToken ct)
    {
        var existing = await repo.ListAccountsAsync([storeId], ct);
        // Әдепкі жиын бір рет: дүкенде төлем әдісі бекітілген шот бар болса — жасалған. Қолмен ашылған (әдіссіз) шоттар есепке алынбайды.
        var ru = Lang == AppLanguage.Ru;
        var defaults = new (string Name, FinanceAccountType Type, PaymentMethod[] Methods)[]
        {
            (ru ? "Касса (наличные)" : "Касса (қолма-қол)", FinanceAccountType.Cash, [PaymentMethod.Cash]),
            (ru ? "Банковский счёт" : "Банк шоты", FinanceAccountType.Bank, [PaymentMethod.Card, PaymentMethod.Transfer]),
            ("Kaspi QR", FinanceAccountType.Kaspi, [PaymentMethod.Qr]),
            (ru ? "Сейф" : "Сейф", FinanceAccountType.Safe, []),
            (ru ? "Деньги в пути" : "Жолдағы ақша", FinanceAccountType.InTransit, []),
        };
        if (existing.Any(a => a.Methods.Count > 0 || defaults.Any(d => d.Name == a.Name))) return existing;
        var list = new List<FinanceAccount>(existing);
        var i = 0;
        foreach (var (name, type, methods) in defaults)
        {
            var a = new FinanceAccount { Id = Guid.NewGuid(), OrganizationId = orgId, StoreId = storeId, Name = name, Type = type, Methods = [.. methods], SortOrder = i++ };
            repo.AddAccount(a);
            list.Add(a);
        }
        // Бірден сақталады: сол сұраныста FOR UPDATE-пен оқылатын шот базада болуы керек.
        await unitOfWork.SaveChangesAsync(ct);
        return list;
    }

    /// <summary>Әдепкі санаттар: бір ұйымға бір рет (§15.1).</summary>
    public async Task<IReadOnlyList<FinanceCategory>> EnsureCategoriesAsync(Guid orgId, CancellationToken ct)
    {
        var existing = await repo.ListCategoriesAsync(orgId, ct);
        if (existing.Count > 0) return existing;
        var ru = Lang == AppLanguage.Ru;
        var defaults = new (string Ru, string Kk, FinanceCategoryType Type, PnlClass Class)[]
        {
            ("Аренда", "Жалдау", FinanceCategoryType.Expense, PnlClass.OperatingExpense),
            ("Зарплата", "Жалақы", FinanceCategoryType.Expense, PnlClass.OperatingExpense),
            ("Коммунальные услуги", "Коммуналдық қызмет", FinanceCategoryType.Expense, PnlClass.OperatingExpense),
            ("Маркетинг", "Маркетинг", FinanceCategoryType.Expense, PnlClass.OperatingExpense),
            ("Транспорт и доставка", "Көлік және жеткізу", FinanceCategoryType.Expense, PnlClass.OperatingExpense),
            ("Банковские комиссии", "Банк комиссиясы", FinanceCategoryType.Expense, PnlClass.OperatingExpense),
            ("Офисные расходы", "Кеңсе шығыны", FinanceCategoryType.Expense, PnlClass.OperatingExpense),
            ("Налоги", "Салықтар", FinanceCategoryType.Expense, PnlClass.Tax),
            ("Прочие расходы", "Басқа шығын", FinanceCategoryType.Expense, PnlClass.OtherExpense),
            ("Личные средства владельца", "Иесінің жеке қаражаты", FinanceCategoryType.Expense, PnlClass.NotInPnl),
            ("Прочие доходы", "Басқа кіріс", FinanceCategoryType.Income, PnlClass.OtherIncome),
            ("Взнос владельца", "Иесінің салымы", FinanceCategoryType.Income, PnlClass.NotInPnl),
        };
        var list = new List<FinanceCategory>();
        var i = 0;
        foreach (var (nameRu, nameKk, type, cls) in defaults)
        {
            var c = new FinanceCategory { Id = Guid.NewGuid(), OrganizationId = orgId, Name = ru ? nameRu : nameKk, Type = type, PnlClass = cls, SortOrder = i++ };
            repo.AddCategory(c);
            list.Add(c);
        }
        return list;
    }

    /// <summary>Төлем әдісіне сай шот (касса → қолма-қол шоты, карта → банк, QR → Kaspi). Жоқ болса — сол түрдегі бірінші белсенді шот.</summary>
    public async Task<FinanceAccount> AccountForMethodAsync(Guid orgId, Guid storeId, PaymentMethod method, CancellationToken ct)
    {
        var accounts = await EnsureAccountsAsync(orgId, storeId, ct);
        var active = accounts.Where(a => a.IsActive).ToList();
        var byMethod = active.FirstOrDefault(a => a.Methods.Contains(method));
        if (byMethod is not null) return byMethod;
        var type = method switch
        {
            PaymentMethod.Cash => FinanceAccountType.Cash,
            PaymentMethod.Qr => FinanceAccountType.Kaspi,
            _ => FinanceAccountType.Bank,
        };
        return active.FirstOrDefault(a => a.Type == type) ?? active.FirstOrDefault()
               ?? throw new ValidationException(Messages.FinanceNoAccount(Lang));
    }

    /// <summary>Проведение: нөмір, проводкалар, қалдықтар. Қайталанған сұраныста — бар операция.</summary>
    public async Task<FinanceOperation> PostAsync(Guid orgId, Guid storeId, FinancePosting p, CancellationToken ct)
    {
        if (p.SourceOperationId is { } src && await repo.FindBySourceAsync(orgId, src, ct) is { } bySource) return bySource;
        if (p.ClientRequestId is { } key && await repo.FindByClientRequestAsync(orgId, key, ct) is { } byKey) return byKey;
        var op = new FinanceOperation
        {
            Id = Guid.NewGuid(), OrganizationId = orgId, StoreId = storeId, Type = p.Type, Date = p.Date,
            Amount = OperationAmount(p.Lines),
            CategoryId = p.CategoryId, Description = p.Description, CounterpartyType = p.CounterpartyType, CounterpartyId = p.CounterpartyId,
            CounterpartyName = p.CounterpartyName, Method = p.Method, DocumentNumber = p.DocumentNumber, AttachmentUrl = p.AttachmentUrl, Comment = p.Comment,
            SourceModule = p.SourceModule, SourceDocumentId = p.SourceDocumentId, SourceOperationId = p.SourceOperationId, ClientRequestId = p.ClientRequestId,
            CreatedBy = current.StaffUserId,
        };
        await ApplyAsync(orgId, op, p.Lines, p.AllowNegative, ct);
        repo.AddOperation(op);
        return op;
    }

    /// <summary>Операция сомасы: кіріс проводкалары болса — солардың қосындысы, әйтпесе шығыстардың модулі.</summary>
    public static decimal OperationAmount(IReadOnlyList<FinanceLine> lines)
    {
        var inc = lines.Where(l => l.Amount > 0).Sum(l => l.Amount);
        return Math.Round(inc > 0 ? inc : -lines.Sum(l => l.Amount), 2);
    }

    /// <summary>Черновикті проведение (сол операция объектісі, проводкалар енді ғана жазылады).</summary>
    public async Task PostDraftAsync(Guid orgId, FinanceOperation op, IReadOnlyList<FinanceLine> lines, CancellationToken ct)
    {
        if (op.Status != FinanceOperationStatus.Draft) throw new ValidationException(Messages.FinanceNotDraft(Lang));
        await ApplyAsync(orgId, op, lines, false, ct);
    }

    private async Task ApplyAsync(Guid orgId, FinanceOperation op, IReadOnlyList<FinanceLine> lines, bool allowNegative, CancellationToken ct)
    {
        if (lines.Count == 0 || lines.Any(l => l.Amount == 0)) throw new ValidationException(Messages.FinanceAmountInvalid(Lang), "amount");
        var accounts = (await repo.GetAccountsForUpdateAsync(orgId, lines.Select(l => l.AccountId).Distinct().ToList(), ct)).ToDictionary(a => a.Id);
        foreach (var line in lines)
        {
            var account = accounts.GetValueOrDefault(line.AccountId) ?? throw new ValidationException(Messages.FinanceAccountNotFound(Lang), "accountId");
            if (!account.IsActive) throw new ValidationException(Messages.FinanceAccountInactive(Lang, account.Name), "accountId");
        }
        var now = DateTime.UtcNow;
        op.Seq = await repo.NextNumberAsync(orgId, ct);
        op.Number = StockPosting.FormatNumber(NumberPrefix, op.Seq);
        op.Status = FinanceOperationStatus.Posted;
        op.PostedAt = now;
        op.PostedBy = current.StaffUserId;
        op.UpdatedAt = now;
        op.FromAccountId ??= lines.FirstOrDefault(l => l.Amount < 0)?.AccountId;
        op.ToAccountId ??= lines.FirstOrDefault(l => l.Amount > 0)?.AccountId;
        var tick = 0;
        foreach (var line in lines)
        {
            var account = accounts[line.AccountId];
            var amount = Math.Round(line.Amount, 2);
            if (amount < 0 && !allowNegative && account.Balance + amount < 0)
                throw new ValidationException(Messages.FinanceInsufficientFunds(Lang, account.Name, account.Balance, -amount - account.Balance), "amount");
            account.Balance += amount;
            account.UpdatedAt = now;
            op.Entries.Add(new FinanceEntry
            {
                Id = Guid.NewGuid(), OperationId = op.Id, AccountId = account.Id, Amount = amount, BalanceAfter = account.Balance,
                Date = op.Date, CreatedAt = now.AddTicks(tick++),
            });
        }
    }

    /// <summary>Сторно (§3.2, §17): түпнұсқа Reversed, кері операция — сол түрмен, проводкалары теріс.</summary>
    public async Task<FinanceOperation> ReverseAsync(Guid orgId, FinanceOperation original, string reason, CancellationToken ct)
    {
        if (original.Status == FinanceOperationStatus.Reversed) throw new ValidationException(Messages.FinanceAlreadyReversed(Lang));
        if (original.Status != FinanceOperationStatus.Posted) throw new ValidationException(Messages.FinanceNotPosted(Lang));
        var now = DateTime.UtcNow;
        var reversal = new FinanceOperation
        {
            Id = Guid.NewGuid(), OrganizationId = orgId, StoreId = original.StoreId, Type = original.Type, Date = now, Amount = original.Amount,
            CategoryId = original.CategoryId, Description = $"Сторно {original.Number}: {original.Description}",
            CounterpartyType = original.CounterpartyType, CounterpartyId = original.CounterpartyId, CounterpartyName = original.CounterpartyName,
            Method = original.Method, DocumentNumber = original.DocumentNumber, Comment = reason, SourceModule = original.SourceModule,
            SourceDocumentId = original.SourceDocumentId, ReversalOfId = original.Id, CreatedBy = current.StaffUserId,
        };
        var lines = original.Entries.Select(e => new FinanceLine(e.AccountId, -e.Amount)).ToList();
        await ApplyAsync(orgId, reversal, lines, true, ct);
        // Кері жазбада «қайдан/қайда» ауысады.
        (reversal.FromAccountId, reversal.ToAccountId) = (original.ToAccountId, original.FromAccountId);
        repo.AddOperation(reversal);
        original.Status = FinanceOperationStatus.Reversed;
        original.ReversedAt = now;
        original.ReversedBy = current.StaffUserId;
        original.ReversalReason = reason;
        original.UpdatedAt = now;
        return reversal;
    }

    /// <summary>Бастапқы құжат бойынша сторно (мысалы, жеткізушіге төлем сторносы): операция табылмаса — ештеңе.</summary>
    public async Task ReverseBySourceAsync(Guid orgId, Guid sourceOperationId, string reason, CancellationToken ct)
    {
        var op = await repo.FindBySourceAsync(orgId, sourceOperationId, ct);
        if (op is null || op.Status != FinanceOperationStatus.Posted) return;
        // Tracking-пен қайта алу: FindBySourceAsync tracked (AsNoTracking емес).
        await ReverseAsync(orgId, op, reason, ct);
    }

    public async Task<TimeZoneInfo> ZoneAsync(Guid storeId, CancellationToken ct)
    {
        var store = await stores.GetByIdAsync(storeId, ct);
        return store is null ? TimeZoneInfo.Utc : Sales.StoreTime.Zone(store);
    }
}
