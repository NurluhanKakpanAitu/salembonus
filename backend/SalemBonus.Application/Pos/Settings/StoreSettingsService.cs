using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Application.Platform;
using SalemBonus.Application.Pos.Catalog;
using SalemBonus.Application.Pos.Sales;
using SalemBonus.Domain.Core;
using SalemBonus.Domain.Pos;
using SalemBonus.Domain.Pos.Sales;

namespace SalemBonus.Application.Pos.Settings;

/// <summary>
/// «Настройка»: дүкен профилі мен бизнес деректері, дүкендер, кассалар, дүкен деңгейіндегі касса баптаулары
/// (төлем түрлері, чек үлгісі, хабарламалар), тариф. Құқық — <c>settings.manage</c>; дүкендерді қосу/өшіру мен
/// бизнес деректері — тек иесіне. Әр өзгеріс аудитке ескі/жаңа мәнімен түседі.
/// </summary>
public class StoreSettingsService(
    IPlatformRepository platform,
    IRegisterRepository registers,
    ISalesRepository sales,
    IStaffAdminRepository staff,
    IKatoRepository kato,
    ITokenService tokens,
    CatalogAccess access,
    IUnitOfWork unitOfWork)
{
    private AppLanguage Lang => access.Lang;
    private const string SupportUrl = "https://salemtech.kz/#request";

    // ---------- Дүкен профилі ----------

    public async Task<StoreProfileDto> GetProfileAsync(CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.SettingsManage, ct);
        var store = await platform.StoreAsync(orgId, m.StoreId, ct) ?? throw new NotFoundException(Messages.StoreNotFound(Lang));
        var org = await platform.OrganizationAsync(orgId, ct) ?? throw new NotFoundException(Messages.BusinessNotFound(Lang));
        return await ProfileAsync(store, org, m, ct);
    }

    private async Task<StoreProfileDto> ProfileAsync(Domain.Entities.Store s, Organization org, StoreMembership m, CancellationToken ct)
    {
        var path = s.KatoCode is null ? [] : (await kato.GetPathAsync(s.KatoCode, ct)).Select(n => n.Code).ToList();
        return new StoreProfileDto(s.Id, s.Name, s.Category, s.Description, s.Address, s.Phone, s.KatoCode, s.TimeZone, s.JoinCode, org.Name, org.Bin,
            m.Role == StaffRole.Owner, StoreFactory.TimeZones, path);
    }

    public async Task<StoreProfileDto> UpdateProfileAsync(UpdateStoreProfileRequest r, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.SettingsManage, ct);
        var store = await platform.StoreAsync(orgId, m.StoreId, ct) ?? throw new NotFoundException(Messages.StoreNotFound(Lang));
        var org = await platform.OrganizationAsync(orgId, ct) ?? throw new NotFoundException(Messages.BusinessNotFound(Lang));
        var before = new { store.Name, store.Category, store.Description, store.Address, store.Phone, store.KatoCode, store.TimeZone, Organization = org.Name, org.Bin };

        var name = CatalogAccess.Optional(r.Name, 200);
        if (name is null || name.Length < 2) throw new ValidationException(Messages.BusinessStoreRequired(Lang), "name");
        store.Name = name;
        store.Category = CatalogAccess.Optional(r.Category, 100) ?? store.Category;
        store.Description = CatalogAccess.Optional(r.Description, 500) ?? string.Empty;
        store.Address = CatalogAccess.Optional(r.Address, 300);
        store.Phone = CatalogAccess.Optional(r.Phone, 30);
        store.KatoCode = CatalogAccess.Optional(r.KatoCode, 9);
        store.TimeZone = StoreFactory.TimeZone(r.TimeZone) ?? throw new ValidationException(Messages.BusinessTimeZoneInvalid(Lang), "timeZone");

        var orgName = CatalogAccess.Optional(r.OrganizationName, 200);
        var bin = CatalogAccess.Optional(r.OrganizationBin, 12);
        if (orgName is not null || r.OrganizationBin is not null)
        {
            if (m.Role != StaffRole.Owner)
            {
                if ((orgName ?? org.Name) != org.Name || bin != org.Bin) throw new ForbiddenException(Messages.StoreOwnerOnly(Lang));
            }
            else
            {
                if (orgName is not null && orgName.Length < 2) throw new ValidationException(Messages.BusinessNameRequired(Lang), "organizationName");
                if (bin is not null && (bin.Length != 12 || !bin.All(char.IsAsciiDigit))) throw new ValidationException(Messages.BusinessBinInvalid(Lang), "organizationBin");
                org.Name = orgName ?? org.Name;
                org.Bin = bin;
            }
        }
        access.Audit(orgId, m.StoreId, "settings.store", "store", store.Id, before,
            new { store.Name, store.Category, store.Description, store.Address, store.Phone, store.KatoCode, store.TimeZone, Organization = org.Name, org.Bin });
        await unitOfWork.SaveChangesAsync(ct);
        return await ProfileAsync(store, org, m, ct);
    }

    // ---------- Дүкендер ----------

    public async Task<IReadOnlyList<StoreRowDto>> ListStoresAsync(CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.SettingsManage, ct);
        var stores = await platform.StoresAsync(orgId, ct);
        var regs = await platform.RegisterCountsAsync(stores.Select(s => s.Id).ToList(), ct);
        var users = await staff.ListAsync(orgId, ct);
        return stores.Select(s => new StoreRowDto(s.Id, s.Name, s.Category, s.Address, s.Phone, s.TimeZone, s.IsActive, s.JoinCode,
            regs.GetValueOrDefault(s.Id), users.Count(u => u.IsActive && u.Memberships.Any(x => x.StoreId == s.Id && x.IsActive)),
            s.Id == m.StoreId, s.CreatedAt)).ToList();
    }

    public async Task<StoreRowDto> CreateStoreAsync(CreateStoreRequest r, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.SettingsManage, ct);
        if (m.Role != StaffRole.Owner) throw new ForbiddenException(Messages.StoreOwnerOnly(Lang));
        var name = CatalogAccess.Optional(r.Name, 200);
        if (name is null || name.Length < 2) throw new ValidationException(Messages.BusinessStoreRequired(Lang), "name");
        var timeZone = StoreFactory.TimeZone(r.TimeZone) ?? throw new ValidationException(Messages.BusinessTimeZoneInvalid(Lang), "timeZone");
        var store = StoreFactory.New(orgId, name, CatalogAccess.Optional(r.Category, 100), CatalogAccess.Optional(r.Address, 300),
            CatalogAccess.Optional(r.Phone, 30), CatalogAccess.Optional(r.KatoCode, 9), timeZone, await StoreFactory.NewJoinCodeAsync(platform, ct));
        platform.AddStore(store);
        await platform.EnsureStoreDefaultsAsync(orgId, store.Id, ct);
        // Бизнестің барлық белсенді иесі жаңа дүкенге бірден кіреді.
        foreach (var owner in (await staff.ListAsync(orgId, ct)).Where(u => u.IsActive && u.Memberships.Any(x => x.IsActive && x.Role == StaffRole.Owner)))
            staff.AddMembership(new StoreMembership
            {
                Id = Guid.NewGuid(), StaffUserId = owner.Id, StoreId = store.Id, Role = StaffRole.Owner,
                Permissions = [.. StaffPermissions.DefaultsFor(StaffRole.Owner)], MaxDiscountPercent = 100,
            });
        access.Audit(orgId, m.StoreId, "settings.store.create", "store", store.Id, null, new { store.Name, store.Address });
        await unitOfWork.SaveChangesAsync(ct);
        return (await ListStoresAsync(ct)).First(s => s.Id == store.Id);
    }

    public async Task<StoreRowDto> SetStoreActiveAsync(Guid id, bool active, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.SettingsManage, ct);
        if (m.Role != StaffRole.Owner) throw new ForbiddenException(Messages.StoreOwnerOnly(Lang));
        var store = await platform.StoreAsync(orgId, id, ct) ?? throw new NotFoundException(Messages.BusinessStoreNotFound(Lang));
        if (!active)
        {
            if (store.Id == m.StoreId) throw new ValidationException(Messages.StoreCurrent(Lang), "active");
            if ((await platform.StoresAsync(orgId, ct)).Count(s => s.IsActive && s.Id != store.Id) == 0) throw new ValidationException(Messages.StoreLastActive(Lang), "active");
        }
        store.IsActive = active;
        access.Audit(orgId, m.StoreId, active ? "settings.store.activate" : "settings.store.deactivate", "store", store.Id, null, new { store.Name });
        await unitOfWork.SaveChangesAsync(ct);
        return (await ListStoresAsync(ct)).First(s => s.Id == store.Id);
    }

    // ---------- Кассалар ----------

    public async Task<IReadOnlyList<RegisterRowDto>> ListRegistersAsync(string? deviceToken, CancellationToken ct = default)
    {
        var (_, m) = await access.RequireAsync(StaffPermissions.SettingsManage, ct);
        var list = await registers.ListAllByStoreAsync(m.StoreId, ct);
        var stats = await registers.SalesStatsAsync(m.StoreId, DateTime.UtcNow.AddDays(-30), ct);
        var thisHash = string.IsNullOrEmpty(deviceToken) ? null : tokens.Hash(deviceToken);
        return list.Select(r => Row(r, thisHash, stats)).ToList();
    }

    private static RegisterRowDto Row(Register r, string? thisHash, Dictionary<Guid, (int Sales30, DateTime? LastSaleAt)> stats)
    {
        var s = stats.GetValueOrDefault(r.Id);
        return new RegisterRowDto(r.Id, r.Name, r.AutoLockMinutes, r.DeviceTokenHash is not null, r.ActivatedAt, r.IsActive,
            thisHash is not null && r.DeviceTokenHash == thisHash, s.Sales30, s.LastSaleAt);
    }

    public async Task<RegisterRowDto> CreateRegisterAsync(SaveRegisterRequest r, string? deviceToken, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.SettingsManage, ct);
        var register = new Register { Id = Guid.NewGuid(), StoreId = m.StoreId, Name = access.Name(r.Name, 60), AutoLockMinutes = AutoLock(r.AutoLockMinutes) };
        registers.Add(register);
        access.Audit(orgId, m.StoreId, "register.create", "register", register.Id, null, new { register.Name, register.AutoLockMinutes });
        await unitOfWork.SaveChangesAsync(ct);
        return (await ListRegistersAsync(deviceToken, ct)).First(x => x.Id == register.Id);
    }

    public async Task<RegisterRowDto> UpdateRegisterAsync(Guid id, SaveRegisterRequest r, string? deviceToken, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.SettingsManage, ct);
        var register = await registers.GetAnyForUpdateAsync(m.StoreId, id, ct) ?? throw new NotFoundException(Messages.RegisterNotFound(Lang));
        var before = new { register.Name, register.AutoLockMinutes };
        register.Name = access.Name(r.Name, 60);
        register.AutoLockMinutes = AutoLock(r.AutoLockMinutes);
        access.Audit(orgId, m.StoreId, "register.update", "register", register.Id, before, new { register.Name, register.AutoLockMinutes });
        await unitOfWork.SaveChangesAsync(ct);
        return (await ListRegistersAsync(deviceToken, ct)).First(x => x.Id == register.Id);
    }

    /// <summary>Кассаны өшіру оның құрылғысын да ажыратады; соңғы белсенді касса өшірілмейді.</summary>
    public async Task<RegisterRowDto> SetRegisterActiveAsync(Guid id, bool active, string? deviceToken, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.SettingsManage, ct);
        var register = await registers.GetAnyForUpdateAsync(m.StoreId, id, ct) ?? throw new NotFoundException(Messages.RegisterNotFound(Lang));
        if (!active && register.IsActive && (await registers.ListByStoreAsync(m.StoreId, ct)).Count(x => x.Id != register.Id) == 0)
            throw new ValidationException(Messages.RegisterLastActive(Lang), "active");
        register.IsActive = active;
        if (!active) { register.DeviceTokenHash = null; register.ActivatedAt = null; }
        access.Audit(orgId, m.StoreId, active ? "register.activate" : "register.deactivate", "register", register.Id, null, new { register.Name });
        await unitOfWork.SaveChangesAsync(ct);
        return (await ListRegistersAsync(deviceToken, ct)).First(x => x.Id == register.Id);
    }

    /// <summary>Құрылғыны мәжбүрлеп ажырату (планшет жоғалса/ауысса): сол құрылғыда PIN-мен кіру тоқтайды.</summary>
    public async Task<RegisterRowDto> UnbindRegisterAsync(Guid id, string? deviceToken, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.SettingsManage, ct);
        var register = await registers.GetAnyForUpdateAsync(m.StoreId, id, ct) ?? throw new NotFoundException(Messages.RegisterNotFound(Lang));
        register.DeviceTokenHash = null;
        register.ActivatedAt = null;
        access.Audit(orgId, m.StoreId, "register.unbind", "register", register.Id, null, new { register.Name });
        await unitOfWork.SaveChangesAsync(ct);
        return (await ListRegistersAsync(deviceToken, ct)).First(x => x.Id == register.Id);
    }

    private int? AutoLock(int? minutes) => minutes is { } min
        ? min is >= 1 and <= 240 ? min : throw new ValidationException(Messages.ValueOutOfRange(Lang, 1, 240), "autoLockMinutes")
        : null;

    // ---------- Касса баптаулары (дүкен деңгейі, құрылғыға тәуелсіз) ----------

    public async Task<StoreCashierSettingsDto> GetCashierSettingsAsync(CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.SettingsManage, ct);
        var s = await sales.GetSettingsAsync(m.StoreId, ct) ?? new StoreCashierSettings { StoreId = m.StoreId };
        return await CashierDtoAsync(orgId, m.StoreId, s, ct);
    }

    private async Task<StoreCashierSettingsDto> CashierDtoAsync(Guid orgId, Guid storeId, StoreCashierSettings s, CancellationToken ct)
    {
        var store = await platform.StoreAsync(orgId, storeId, ct) ?? throw new NotFoundException(Messages.StoreNotFound(Lang));
        var org = await platform.OrganizationAsync(orgId, ct);
        var recipients = (await sales.ListRecipientsAsync(storeId, ct)).Where(r => r.IsActive).ToList();
        return new StoreCashierSettingsDto(
            s.EnabledMethods.Select(x => x.ToString()).ToList(), s.MixedEnabled, s.AutoPrint, s.ElectronicReceipt,
            s.NotifyOutOfStock, s.NotifyLowStock, s.NotifyDebt, s.NotifyReturn, s.NotifyCashierChange, s.LowStockThreshold,
            s.ReceiptHeader, s.ReceiptFooter, s.ReceiptShowAddress, s.ReceiptShowPhone, s.ReceiptShowBin,
            recipients.Select(r => new TransferRecipientDto(r.Id, r.BankName, r.Account, r.HolderName)).ToList(),
            store.Name, store.Address, store.Phone, org?.Bin);
    }

    public async Task<StoreCashierSettingsDto> UpdateCashierSettingsAsync(UpdateStoreCashierSettingsRequest r, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.SettingsManage, ct);
        var s = await sales.GetSettingsForUpdateAsync(m.StoreId, ct);
        if (s is null)
        {
            s = new StoreCashierSettings { StoreId = m.StoreId };
            sales.AddSettings(s);
        }
        var before = Snapshot(s);

        var methods = new List<PaymentMethod>();
        foreach (var raw in r.EnabledMethods ?? [])
            if (Enum.TryParse<PaymentMethod>(raw, ignoreCase: true, out var pm) && Enum.IsDefined(pm) && !methods.Contains(pm)) methods.Add(pm);
        if (!methods.Any(x => x != PaymentMethod.Debt)) throw new ValidationException(Messages.SettingsNeedPaymentMethod(Lang), "enabledMethods");
        s.EnabledMethods = StoreCashierSettings.AllMethods.Where(methods.Contains).ToList();
        s.MixedEnabled = r.MixedEnabled;
        s.AutoPrint = r.AutoPrint;
        s.ElectronicReceipt = r.ElectronicReceipt;
        s.NotifyOutOfStock = r.NotifyOutOfStock;
        s.NotifyLowStock = r.NotifyLowStock;
        s.NotifyDebt = r.NotifyDebt;
        s.NotifyReturn = r.NotifyReturn;
        s.NotifyCashierChange = r.NotifyCashierChange;
        s.LowStockThreshold = r.LowStockThreshold is >= 0 and <= 10000 ? r.LowStockThreshold : throw new ValidationException(Messages.ValueOutOfRange(Lang, 0, 10000), "lowStockThreshold");
        s.ReceiptHeader = ReceiptText(r.ReceiptHeader, "receiptHeader");
        s.ReceiptFooter = ReceiptText(r.ReceiptFooter, "receiptFooter");
        s.ReceiptShowAddress = r.ReceiptShowAddress;
        s.ReceiptShowPhone = r.ReceiptShowPhone;
        s.ReceiptShowBin = r.ReceiptShowBin;
        s.UpdatedAt = DateTime.UtcNow;

        access.Audit(orgId, m.StoreId, "cashier.settings", "store", m.StoreId, before, Snapshot(s));
        await unitOfWork.SaveChangesAsync(ct);
        return await CashierDtoAsync(orgId, m.StoreId, s, ct);
    }

    private string? ReceiptText(string? raw, string field)
    {
        var value = raw?.Trim();
        if (string.IsNullOrEmpty(value)) return null;
        if (value.Length > 300) throw new ValidationException(Messages.ReceiptTextTooLong(Lang), field);
        return value;
    }

    private static object Snapshot(StoreCashierSettings s) => new
    {
        Methods = s.EnabledMethods.Select(x => x.ToString()).ToList(), s.MixedEnabled, s.AutoPrint, s.ElectronicReceipt,
        s.NotifyOutOfStock, s.NotifyLowStock, s.NotifyDebt, s.NotifyReturn, s.NotifyCashierChange, s.LowStockThreshold,
        s.ReceiptHeader, s.ReceiptFooter, s.ReceiptShowAddress, s.ReceiptShowPhone, s.ReceiptShowBin,
    };

    // ---------- Тариф ----------

    public async Task<PlanInfoDto> GetPlanAsync(CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.SettingsManage, ct);
        var org = await platform.OrganizationAsync(orgId, ct) ?? throw new NotFoundException(Messages.BusinessNotFound(Lang));
        var plan = org.PlanId is { } pid ? await platform.PlanAsync(pid, ct) : null;
        var store = await platform.StoreAsync(orgId, m.StoreId, ct);
        var today = StoreTime.Today(StoreTime.Zone(store ?? new Domain.Entities.Store()));
        var end = org.PaidUntil ?? org.TrialUntil;
        return new PlanInfoDto(plan?.Name, PlatformAccess.Fee(org, plan), PlatformAccess.Status(org, today), org.PaidUntil, org.TrialUntil,
            end is { } e && e >= today ? e.DayNumber - today.DayNumber : null, PlatformAccess.DaysOverdue(org, today),
            PlatformModules.All.Select(k => new PlanModuleDto(k, org.Modules.Contains(k))).ToList(), SupportUrl);
    }
}
