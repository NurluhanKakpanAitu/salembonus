using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Application.Pos.Catalog;
using SalemBonus.Application.Pos.Registers;
using SalemBonus.Domain.Core;
using SalemBonus.Domain.Pos.Sales;

namespace SalemBonus.Application.Pos.Sales;

/// <summary>
/// Кассаның «Финансы» экраны (ТЗ «Касса» §14): сатылым, төлем түрлері, аралас төлемдер, жеңілдік,
/// бонус, қайтарым, қарыз. <c>finance.view</c> құқығы барға — бүкіл дүкен, кассирге — тек өз сатылымдары.
/// </summary>
public class FinanceService(ISalesRepository sales, IStoreRepository stores, CatalogAccess access)
{
    private static readonly PaymentMethod[] MoneyMethods = [PaymentMethod.Cash, PaymentMethod.Card, PaymentMethod.Qr, PaymentMethod.Transfer];

    public async Task<FinanceDto> GetAsync(DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var (_, m) = await access.RequireAsync(StaffPermissions.SalesCreate, ct);
        var store = await stores.GetByIdAsync(m.StoreId, ct) ?? throw new NotFoundException(Messages.StoreNotFound(access.Lang));
        var zone = StoreTime.Zone(store);
        var start = from ?? StoreTime.Today(zone);
        var end = to ?? start;
        if (end < start) (start, end) = (end, start);
        var fromUtc = StoreTime.StartOfDayUtc(start, zone);
        var toUtc = StoreTime.StartOfDayUtc(end.AddDays(1), zone);

        var wholeStore = m.Has(StaffPermissions.FinanceView);
        Guid? staffId = wholeStore ? null : access.StaffUserId;
        var list = await sales.ListSalesInRangeAsync(m.StoreId, fromUtc, toUtc, staffId, ct);
        var returns = await sales.ListReturnsInRangeAsync(m.StoreId, fromUtc, toUtc, staffId, ct);
        var repaid = await sales.ListDebtPaymentsInRangeAsync(m.StoreId, fromUtc, toUtc, staffId, ct);

        var payments = list.SelectMany(s => s.Payments).ToList();
        var methods = MoneyMethods.Append(PaymentMethod.Debt)
            .Select(x => new FinanceMethodDto(x.ToString(), payments.Where(p => p.Method == x).Sum(p => p.Amount),
                payments.Count(p => p.Method == x)))
            .ToList();
        var mixed = list.Where(s => s.Payments.Count > 1).ToList();
        var refunds = MoneyMethods
            .Select(x => new FinanceMethodDto(x.ToString(), returns.Where(r => r.RefundMethod == x).Sum(r => r.Refunded),
                returns.Count(r => r.RefundMethod == x)))
            .ToList();
        var repaidByMethod = MoneyMethods
            .Select(x => new FinanceMethodDto(x.ToString(), repaid.Where(p => p.Method == x).Sum(p => p.Amount), repaid.Count(p => p.Method == x)))
            .ToList();

        var salesTotal = list.Sum(s => s.Total);
        var returnsTotal = returns.Sum(r => r.Amount);
        var cash = payments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount)
                   + repaid.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount)
                   - returns.Where(r => r.RefundMethod == PaymentMethod.Cash).Sum(r => r.Refunded);

        return new FinanceDto(
            start, end, wholeStore,
            salesTotal, list.Count,
            methods,
            mixed.Sum(s => s.Total), mixed.Count,
            list.Sum(s => s.DiscountAmount), list.Count(s => s.DiscountAmount > 0),
            list.Sum(s => s.BonusRedeemed) - returns.Sum(r => r.BonusRestored),
            list.Sum(s => s.BonusAccrued) - returns.Sum(r => r.BonusReversed),
            returnsTotal, returns.Count, refunds,
            methods.First(x => x.Method == nameof(PaymentMethod.Debt)).Amount, repaid.Sum(p => p.Amount), repaidByMethod,
            salesTotal - returnsTotal, cash);
    }
}

/// <summary>
/// Касса баптаулары (ТЗ «Касса» §17). Дүкен атауы мен чек нөмірі өзгермейді; бонус пайыздары
/// SalemBonus-та. Дүкен деңгейіндегі баптауларды тек <c>settings.manage</c> құқығы барлар өзгертеді.
/// </summary>
public class CashierSettingsService(
    ISalesRepository sales,
    IStoreRepository stores,
    IRegisterRepository registers,
    IRegisterService registerService,
    CatalogAccess access,
    IUnitOfWork unitOfWork)
{
    private AppLanguage Lang => access.Lang;

    public async Task<CashierSettingsDto> GetAsync(string? deviceToken, CancellationToken ct = default)
    {
        var (_, m) = await access.RequireAsync(StaffPermissions.SalesCreate, ct);
        var register = await registerService.RequireCurrentAsync(deviceToken, ct);
        var store = await stores.GetByIdAsync(m.StoreId, ct) ?? throw new NotFoundException(Messages.StoreNotFound(Lang));
        var s = await sales.GetSettingsAsync(m.StoreId, ct) ?? new StoreCashierSettings { StoreId = m.StoreId };
        var recipients = (await sales.ListRecipientsAsync(m.StoreId, ct)).Where(r => r.IsActive).ToList();
        return new CashierSettingsDto(
            store.Name, store.Address, register.Id, register.Name, register.AutoLockMinutes,
            s.EnabledMethods.Select(x => x.ToString()).ToList(), s.MixedEnabled, s.AutoPrint, s.ElectronicReceipt,
            s.NotifyOutOfStock, s.NotifyLowStock, s.NotifyDebt, s.NotifyReturn, s.NotifyCashierChange, s.LowStockThreshold,
            recipients.Select(r => new TransferRecipientDto(r.Id, r.BankName, r.Account, r.HolderName)).ToList(),
            m.Has(StaffPermissions.SettingsManage));
    }

    public async Task<CashierSettingsDto> UpdateAsync(UpdateCashierSettingsRequest request, string? deviceToken, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.SettingsManage, ct);
        var current = await registerService.RequireCurrentAsync(deviceToken, ct);
        var register = await registers.GetForUpdateAsync(m.StoreId, current.Id, ct) ?? throw new NotFoundException(Messages.RegisterNotFound(Lang));

        var s = await sales.GetSettingsForUpdateAsync(m.StoreId, ct);
        if (s is null)
        {
            s = new StoreCashierSettings { StoreId = m.StoreId };
            sales.AddSettings(s);
        }
        var before = new
        {
            register.Name, register.AutoLockMinutes, Methods = s.EnabledMethods.Select(x => x.ToString()).ToList(), s.MixedEnabled,
            s.AutoPrint, s.ElectronicReceipt, s.NotifyOutOfStock, s.NotifyLowStock, s.NotifyDebt, s.NotifyReturn, s.NotifyCashierChange,
            s.LowStockThreshold,
        };

        register.Name = access.Name(request.RegisterName, 60, "registerName");
        register.AutoLockMinutes = request.AutoLockMinutes is { } min
            ? min is >= 1 and <= 240 ? min : throw new ValidationException(Messages.ValueOutOfRange(Lang, 1, 240), "autoLockMinutes")
            : null;

        var methods = new List<PaymentMethod>();
        foreach (var raw in request.EnabledMethods ?? [])
            if (Enum.TryParse<PaymentMethod>(raw, ignoreCase: true, out var pm) && Enum.IsDefined(pm) && !methods.Contains(pm)) methods.Add(pm);
        if (!methods.Any(x => x != PaymentMethod.Debt)) throw new ValidationException(Messages.SettingsNeedPaymentMethod(Lang), "enabledMethods");
        s.EnabledMethods = StoreCashierSettings.AllMethods.Where(methods.Contains).ToList();
        s.MixedEnabled = request.MixedEnabled;
        s.AutoPrint = request.AutoPrint;
        s.ElectronicReceipt = request.ElectronicReceipt;
        s.NotifyOutOfStock = request.NotifyOutOfStock;
        s.NotifyLowStock = request.NotifyLowStock;
        s.NotifyDebt = request.NotifyDebt;
        s.NotifyReturn = request.NotifyReturn;
        s.NotifyCashierChange = request.NotifyCashierChange;
        s.LowStockThreshold = request.LowStockThreshold is >= 0 and <= 10000
            ? request.LowStockThreshold : throw new ValidationException(Messages.ValueOutOfRange(Lang, 0, 10000), "lowStockThreshold");
        s.UpdatedAt = DateTime.UtcNow;

        // ТЗ §16.7: кім, қашан, не өзгертті — ескі және жаңа мәнімен.
        access.Audit(orgId, m.StoreId, "cashier.settings", "store", m.StoreId, before, new
        {
            register.Name, register.AutoLockMinutes, Methods = s.EnabledMethods.Select(x => x.ToString()).ToList(), s.MixedEnabled,
            s.AutoPrint, s.ElectronicReceipt, s.NotifyOutOfStock, s.NotifyLowStock, s.NotifyDebt, s.NotifyReturn, s.NotifyCashierChange,
            s.LowStockThreshold,
        });
        await unitOfWork.SaveChangesAsync(ct);
        return await GetAsync(deviceToken, ct);
    }

    public async Task<TransferRecipientDto> SaveRecipientAsync(Guid? id, SaveRecipientRequest request, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.SettingsManage, ct);
        var bank = access.Name(request.BankName, 60, "bankName");
        var account = access.Name(request.Account, 40, "account");
        var holder = access.Name(request.HolderName, 100, "holderName");

        TransferRecipient r;
        if (id is { } rid)
        {
            r = await sales.GetRecipientForUpdateAsync(m.StoreId, rid, ct) ?? throw new NotFoundException(Messages.TransferRecipientRequired(Lang));
            access.Audit(orgId, m.StoreId, "cashier.recipient.update", "transfer_recipient", r.Id,
                new { r.BankName, r.Account, r.HolderName }, new { BankName = bank, Account = account, HolderName = holder });
        }
        else
        {
            var existing = await sales.ListRecipientsAsync(m.StoreId, ct);
            r = new TransferRecipient { Id = Guid.NewGuid(), StoreId = m.StoreId, SortOrder = existing.Count + 1 };
            sales.AddRecipient(r);
            access.Audit(orgId, m.StoreId, "cashier.recipient.create", "transfer_recipient", r.Id, null,
                new { BankName = bank, Account = account, HolderName = holder });
        }
        r.BankName = bank;
        r.Account = account;
        r.HolderName = holder;
        r.IsActive = true;
        await unitOfWork.SaveChangesAsync(ct);
        return new TransferRecipientDto(r.Id, r.BankName, r.Account, r.HolderName);
    }

    /// <summary>Реквизит өшірілмейді, тек жасырылады: бұрынғы чектерде оның мәтіні сақталған.</summary>
    public async Task RemoveRecipientAsync(Guid id, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.SettingsManage, ct);
        var r = await sales.GetRecipientForUpdateAsync(m.StoreId, id, ct) ?? throw new NotFoundException(Messages.TransferRecipientRequired(Lang));
        r.IsActive = false;
        access.Audit(orgId, m.StoreId, "cashier.recipient.remove", "transfer_recipient", r.Id, new { r.BankName, r.Account, r.HolderName }, null);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
