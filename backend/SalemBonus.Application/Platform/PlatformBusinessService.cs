using System.Security.Cryptography;
using SalemBonus.Application.BonusCards;
using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Application.Pos.Catalog;
using SalemBonus.Domain.Core;
using SalemBonus.Domain.Entities;
using SalemBonus.Domain.Enums;
using SalemBonus.Domain.Platform;

namespace SalemBonus.Application.Platform;

/// <summary>
/// Бизнестер (admin.salemtech.kz): тізім (төлем күйі, қолданыс), карточка, қосу (ұйым + дүкен + иесінің аккаунты — уақытша құпиясөз
/// бір рет көрсетіледі), модульдерді қосу/өшіру, тоқтату/қайта қосу, дүкен мен иесі/әкімші аккаунттары, құпиясөзді қалпына келтіру.
/// Әр өзгеріс әкімшілік журналына жазылады.
/// </summary>
public class PlatformBusinessService(PlatformAccess access, IPlatformRepository repo, IPasswordHasher hasher, IUnitOfWork unitOfWork)
{
    private AppLanguage Lang => access.Lang;

    // ---------- Тізім ----------

    public static BusinessRowDto Row(Organization o, IReadOnlyList<PlatformPlan> plans, OrgStats? s, DateOnly today)
    {
        var plan = o.PlanId is { } pid ? plans.FirstOrDefault(p => p.Id == pid) : null;
        return new BusinessRowDto(o.Id, o.Name, o.Bin, o.City, o.ContactName, o.ContactPhone, o.PlanId, plan?.Name, PlatformAccess.Fee(o, plan),
            o.MonthlyFee is not null, PlatformAccess.Status(o, today), o.PaidUntil, o.TrialUntil, PlatformAccess.DaysOverdue(o, today), o.Modules,
            s?.Stores ?? 0, s?.Staff ?? 0, s?.Sales30 ?? 0, s?.Revenue30 ?? 0, s?.LastSaleAt, s?.Clients ?? 0, s?.OwnerName, s?.OwnerPhone, s?.LastLoginAt, o.CreatedAt);
    }

    public async Task<IReadOnlyList<BusinessRowDto>> RowsAsync(CancellationToken ct)
    {
        var orgs = await repo.OrganizationsAsync(ct);
        var plans = await repo.PlansAsync(ct);
        var stats = await repo.StatsAsync(orgs.Select(o => o.Id).ToList(), DateTime.UtcNow.AddDays(-30), ct);
        var today = access.Today;
        return orgs.Select(o => Row(o, plans, stats.GetValueOrDefault(o.Id), today)).ToList();
    }

    public async Task<BusinessPageDto> ListAsync(string? search, string? status, Guid? planId, string? module, string? sort, int page, int pageSize,
        CancellationToken ct = default)
    {
        await access.RequireAsync(ct);
        var rows = await RowsAsync(ct);
        var counts = rows.GroupBy(r => r.Status).ToDictionary(g => g.Key, g => g.Count());
        counts["all"] = rows.Count;
        IEnumerable<BusinessRowDto> q = rows;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            var digits = new string(s.Where(char.IsDigit).ToArray());
            q = q.Where(r => r.Name.Contains(s, StringComparison.CurrentCultureIgnoreCase)
                             || (r.ContactName?.Contains(s, StringComparison.CurrentCultureIgnoreCase) ?? false)
                             || (r.OwnerName?.Contains(s, StringComparison.CurrentCultureIgnoreCase) ?? false)
                             || (r.City?.Contains(s, StringComparison.CurrentCultureIgnoreCase) ?? false)
                             || (digits.Length >= 4 && ((r.Bin?.Contains(digits) ?? false) || (r.OwnerPhone?.Contains(digits) ?? false)
                                                         || (r.ContactPhone?.Replace(" ", "").Contains(digits) ?? false))));
        }
        if (!string.IsNullOrWhiteSpace(status) && status != "all") q = q.Where(r => r.Status == status);
        if (planId is { } pid) q = q.Where(r => r.PlanId == pid);
        if (!string.IsNullOrWhiteSpace(module)) q = q.Where(r => r.Modules.Contains(module));
        q = sort switch
        {
            "name" => q.OrderBy(r => r.Name),
            "paidUntil" => q.OrderBy(r => r.PaidUntil ?? r.TrialUntil ?? DateOnly.MinValue),
            "revenue" => q.OrderByDescending(r => r.Revenue30),
            "fee" => q.OrderByDescending(r => r.Fee),
            _ => q.OrderByDescending(r => r.CreatedAt),
        };
        var list = q.ToList();
        var size = Math.Clamp(pageSize, 1, 200);
        return new BusinessPageDto(list.Skip((Math.Max(1, page) - 1) * size).Take(size).ToList(), list.Count, counts);
    }

    // ---------- Карточка ----------

    public async Task<BusinessDetailDto> DetailAsync(Guid id, CancellationToken ct = default)
    {
        await access.RequireAsync(ct);
        return await BuildDetailAsync(id, ct);
    }

    private async Task<BusinessDetailDto> BuildDetailAsync(Guid id, CancellationToken ct)
    {
        var org = await repo.OrganizationAsync(id, ct) ?? throw new NotFoundException(Messages.BusinessNotFound(Lang));
        var plans = await repo.PlansAsync(ct);
        var stats = await repo.StatsAsync([id], DateTime.UtcNow.AddDays(-30), ct);
        var stores = await repo.StoresAsync(id, ct);
        var registers = await repo.RegisterCountsAsync(stores.Select(s => s.Id).ToList(), ct);
        var staff = await repo.StaffAsync(id, ct);
        var (payments, _, paid) = await repo.PaymentsAsync(new PlatformPaymentQuery(id, null, null, null, true, 0, 500), ct);
        var history = await repo.AuditAsync(id, 100, ct);
        var adminIds = payments.Select(p => p.CreatedBy).Concat(payments.Where(p => p.VoidedBy != null).Select(p => p.VoidedBy!.Value))
            .Concat(history.Where(h => h.AdminId != null).Select(h => h.AdminId!.Value)).Distinct().ToList();
        var admins = await repo.AdminNamesAsync(adminIds, ct);
        return new BusinessDetailDto(Row(org, plans, stats.GetValueOrDefault(id), access.Today), org.ContactEmail, org.Comment, org.SuspendedAt, org.SuspendReason,
            stores.Select(s => new BusinessStoreDto(s.Id, s.Name, s.Category, s.Address, s.Phone, s.KatoCode, s.TimeZone, s.IsActive, s.JoinCode,
                registers.GetValueOrDefault(s.Id), s.CreatedAt)).ToList(),
            staff.Select(Account).ToList(),
            payments.Select(p => PlatformBillingService.Payment(p, org.Name, admins)).ToList(), paid,
            history.Select(h => new PlatformAuditDto(h.Id, h.CreatedAt, h.AdminId is { } a ? admins.GetValueOrDefault(a) ?? "—" : "—", h.Action, h.Data)).ToList());
    }

    private static StaffAccountDto Account(StaffUser u) =>
        new(u.Id, u.FullName, u.Phone, u.IsActive, u.HasPassword, u.HasPin, u.LastLoginAt, u.CreatedAt,
            u.Memberships.OrderBy(m => m.Store?.Name).Select(m => new StaffRoleDto(m.StoreId, m.Store?.Name ?? "—", m.Role.ToString(), m.IsActive)).ToList());

    // ---------- Қосу ----------

    private string Name(string? raw)
    {
        var name = CatalogAccess.Optional(raw, 200);
        if (name is null || name.Length < 2) throw new ValidationException(Messages.BusinessNameRequired(Lang), "name");
        return name;
    }

    private string? Bin(string? raw)
    {
        var bin = CatalogAccess.Optional(raw, 12);
        if (bin is not null && (bin.Length != 12 || !bin.All(char.IsAsciiDigit))) throw new ValidationException(Messages.BusinessBinInvalid(Lang), "bin");
        return bin;
    }

    private decimal? Fee(decimal? fee)
    {
        if (fee is { } f && (f < 0 || f > 10_000_000)) throw new ValidationException(Messages.BusinessFeeInvalid(Lang), "monthlyFee");
        return fee;
    }

    private string Phone(string? raw, string field)
    {
        var value = raw?.Trim() ?? string.Empty;
        if (!BonusRules.LooksLikePhone(value)) throw new ValidationException(Messages.PhoneInvalid(Lang), field);
        var phone = BonusRules.NormalizePhone(value);
        if (phone.Length != 12 || !phone.StartsWith("+7")) throw new ValidationException(Messages.PhoneNotKz(Lang), field);
        return phone;
    }

    private const string CodeChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    /// <summary>Жаңа дүкен: SalemBonus кілттері (терминал API кілті, клиенттің қосылу коды), әдепкі бонус баспалдағы, «Касса №1».</summary>
    private async Task<Store> NewStoreAsync(Guid orgId, CreateStoreRequest? r, string fallbackName, CancellationToken ct)
    {
        var name = CatalogAccess.Optional(r?.Name, 200) ?? fallbackName;
        if (name.Length < 2) throw new ValidationException(Messages.BusinessStoreRequired(Lang), "store.name");
        var timeZone = CatalogAccess.Optional(r?.TimeZone, 64) ?? "Asia/Almaty";
        try { TimeZoneInfo.FindSystemTimeZoneById(timeZone); }
        catch (TimeZoneNotFoundException) { throw new ValidationException(Messages.BusinessTimeZoneInvalid(Lang), "store.timeZone"); }
        string code;
        do code = new string(Enumerable.Range(0, 7).Select(_ => CodeChars[RandomNumberGenerator.GetInt32(CodeChars.Length)]).ToArray());
        while (await repo.JoinCodeExistsAsync(code, ct));
        var store = new Store
        {
            Id = Guid.NewGuid(), Name = name, Category = CatalogAccess.Optional(r?.Category, 100) ?? "Магазин", Description = string.Empty, CashbackPercent = 2,
            Address = CatalogAccess.Optional(r?.Address, 300), Phone = CatalogAccess.Optional(r?.Phone, 30), KatoCode = CatalogAccess.Optional(r?.KatoCode, 9),
            TimeZone = timeZone, ApiKey = "sk_live_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant(), JoinCode = code,
            OrganizationId = orgId, MaxRedeemPercent = 30, BonusLifetimeDays = 365, CreatedAt = DateTime.UtcNow,
            Levels =
            [
                Level(CustomerLevel.New, 0, 2), Level(CustomerLevel.Regular, 50_000, 4), Level(CustomerLevel.Favorite, 150_000, 6), Level(CustomerLevel.Vip, 400_000, 8),
            ],
        };
        repo.AddStore(store);
        await repo.EnsureStoreDefaultsAsync(orgId, store.Id, ct);
        return store;
    }

    private static StoreLevel Level(CustomerLevel level, decimal from, decimal percent) =>
        new() { Id = Guid.NewGuid(), Level = level, FromAmount = from, CashbackPercent = percent };

    private async Task<(StaffUser User, string Password)> NewAccountAsync(Guid orgId, CreateAccountRequest? r, IReadOnlyList<Guid> storeIds, Guid defaultStore,
        CancellationToken ct, string field = "owner")
    {
        var phone = Phone(r?.Phone, $"{field}.phone");
        if (await repo.StaffPhoneExistsAsync(phone, ct)) throw new ValidationException(Messages.PlatformPhoneExists(Lang), $"{field}.phone");
        var first = CatalogAccess.Optional(r?.FirstName, 60) ?? throw new ValidationException(Messages.CatalogNameRequired(Lang), $"{field}.firstName");
        var last = CatalogAccess.Optional(r?.LastName, 60) ?? string.Empty;
        var role = (r?.Role ?? "Owner").Trim() switch
        {
            "Owner" or "owner" => StaffRole.Owner,
            "Admin" or "admin" => StaffRole.Admin,
            _ => throw new ValidationException(Messages.PlatformStaffRoleInvalid(Lang), $"{field}.role"),
        };
        var store = r?.StoreId ?? defaultStore;
        if (!storeIds.Contains(store)) throw new ValidationException(Messages.BusinessStoreNotFound(Lang), $"{field}.storeId");
        var password = PlatformAccess.TempPassword();
        var id = Guid.NewGuid();
        // Иесі — бизнестің барлық дүкенінде; әкімші — таңдалған дүкенде, рөлдің әдепкі рұқсаттарымен.
        var targets = role == StaffRole.Owner ? storeIds : [store];
        var user = new StaffUser
        {
            Id = id, OrganizationId = orgId, Phone = phone, FirstName = first, LastName = last, PasswordHash = hasher.Hash(password), CreatedAt = DateTime.UtcNow,
            Memberships = targets.Select(s => new StoreMembership
            {
                Id = Guid.NewGuid(), StaffUserId = id, StoreId = s, Role = role, Permissions = [.. StaffPermissions.DefaultsFor(role)],
                MaxDiscountPercent = role == StaffRole.Owner ? 100 : 0,
            }).ToList(),
        };
        repo.AddStaff(user);
        return (user, password);
    }

    public async Task<BusinessCreatedDto> CreateAsync(CreateBusinessRequest r, CancellationToken ct = default)
    {
        var admin = await access.RequireAsync(ct);
        var name = Name(r.Name);
        var plan = r.PlanId is { } pid ? await repo.PlanAsync(pid, ct) ?? throw new ValidationException(Messages.PlanNotFound(Lang), "planId") : null;
        var trialDays = r.TrialDays ?? 14;
        if (trialDays is < 0 or > 90) throw new ValidationException(Messages.BusinessTrialInvalid(Lang), "trialDays");
        var modules = r.Modules is not null ? access.Modules(r.Modules) : plan is not null ? access.Modules(plan.Modules) : [.. PlatformModules.All];
        var org = new Organization
        {
            Id = Guid.NewGuid(), Name = name, Bin = Bin(r.Bin), City = CatalogAccess.Optional(r.City, 100), ContactName = CatalogAccess.Optional(r.ContactName, 120),
            ContactPhone = CatalogAccess.Optional(r.ContactPhone, 20), ContactEmail = r.ContactEmail is { Length: > 0 } e ? access.Email(e) : null,
            Comment = CatalogAccess.Optional(r.Comment, 1000), PlanId = plan?.Id, MonthlyFee = Fee(r.MonthlyFee),
            TrialUntil = trialDays > 0 ? access.Today.AddDays(trialDays) : null, Modules = modules, CreatedAt = DateTime.UtcNow,
        };
        var (owner, password) = await unitOfWork.InTransactionAsync(async () =>
        {
            repo.AddOrganization(org);
            var store = await NewStoreAsync(org.Id, r.Store, name, ct);
            var (user, pass) = await NewAccountAsync(org.Id, (r.Owner ?? new CreateAccountRequest(null, null, null, null, null)) with { Role = "Owner", StoreId = store.Id },
                [store.Id], store.Id, ct);
            access.Audit(admin, "business.create", org.Id, "organization", org.Id,
                new { org.Name, Store = store.Name, Owner = user.Phone, org.Modules, Plan = plan?.Name, Fee = PlatformAccess.Fee(org, plan), org.TrialUntil });
            await unitOfWork.SaveChangesAsync(ct);
            return (user, pass);
        }, ct);
        return new BusinessCreatedDto(await BuildDetailAsync(org.Id, ct), owner.Phone, password);
    }

    // ---------- Өзгерту ----------

    private async Task<Organization> OrgAsync(Guid id, CancellationToken ct) =>
        await repo.OrganizationAsync(id, ct) ?? throw new NotFoundException(Messages.BusinessNotFound(Lang));

    public async Task<BusinessDetailDto> UpdateAsync(Guid id, UpdateBusinessRequest r, CancellationToken ct = default)
    {
        var admin = await access.RequireAsync(ct);
        var org = await OrgAsync(id, ct);
        var plan = r.PlanId is { } pid ? await repo.PlanAsync(pid, ct) ?? throw new ValidationException(Messages.PlanNotFound(Lang), "planId") : null;
        var before = new { org.Name, org.Bin, org.City, org.ContactName, org.ContactPhone, org.ContactEmail, org.PlanId, org.MonthlyFee, org.TrialUntil, Modules = org.Modules.ToList() };
        org.Name = Name(r.Name);
        org.Bin = Bin(r.Bin);
        org.City = CatalogAccess.Optional(r.City, 100);
        org.ContactName = CatalogAccess.Optional(r.ContactName, 120);
        org.ContactPhone = CatalogAccess.Optional(r.ContactPhone, 20);
        org.ContactEmail = r.ContactEmail is { Length: > 0 } e ? access.Email(e) : null;
        org.Comment = CatalogAccess.Optional(r.Comment, 1000);
        org.PlanId = plan?.Id;
        if (r.ApplyPlanModules && plan is not null) org.Modules = access.Modules(plan.Modules);
        org.MonthlyFee = r.CustomFee ? Fee(r.MonthlyFee ?? 0) : null;
        org.TrialUntil = r.TrialUntil;
        access.Audit(admin, "business.update", org.Id, "organization", org.Id,
            new { Before = before, After = new { org.Name, org.Bin, org.City, org.ContactName, org.ContactPhone, org.ContactEmail, org.PlanId, org.MonthlyFee, org.TrialUntil, org.Modules } });
        await unitOfWork.SaveChangesAsync(ct);
        return await BuildDetailAsync(id, ct);
    }

    /// <summary>Модульдер: өшірілген модульдің бөлімі мен API-ы бизнестің барлық қызметкеріне келесі сұраныстан бастап жабық.</summary>
    public async Task<BusinessDetailDto> SetModulesAsync(Guid id, SetModulesRequest r, CancellationToken ct = default)
    {
        var admin = await access.RequireAsync(ct);
        var org = await OrgAsync(id, ct);
        var before = org.Modules.ToList();
        org.Modules = access.Modules(r.Modules);
        access.Audit(admin, "business.modules", org.Id, "organization", org.Id,
            new { Enabled = org.Modules.Except(before).ToList(), Disabled = before.Except(org.Modules).ToList(), Modules = org.Modules });
        await unitOfWork.SaveChangesAsync(ct);
        return await BuildDetailAsync(id, ct);
    }

    /// <summary>Тоқтату: қызметкерлер кіре алмайды, барлық сеанс жабылады. Деректер сақталады.</summary>
    public async Task<BusinessDetailDto> SuspendAsync(Guid id, ReasonRequest r, CancellationToken ct = default)
    {
        var admin = await access.RequireAsync(ct);
        var reason = CatalogAccess.Optional(r.Reason, 500);
        if (reason is null || reason.Length < 3) throw new ValidationException(Messages.PlatformReasonRequired(Lang), "reason");
        var org = await OrgAsync(id, ct);
        org.IsActive = false;
        org.SuspendedAt = DateTime.UtcNow;
        org.SuspendReason = reason;
        foreach (var u in await repo.StaffAsync(id, ct)) await repo.RevokeStaffSessionsAsync(u.Id, ct);
        access.Audit(admin, "business.suspend", org.Id, "organization", org.Id, new { Reason = reason });
        await unitOfWork.SaveChangesAsync(ct);
        return await BuildDetailAsync(id, ct);
    }

    public async Task<BusinessDetailDto> ActivateAsync(Guid id, CancellationToken ct = default)
    {
        var admin = await access.RequireAsync(ct);
        var org = await OrgAsync(id, ct);
        if (!org.IsActive)
        {
            access.Audit(admin, "business.activate", org.Id, "organization", org.Id, new { Reason = org.SuspendReason });
            org.IsActive = true;
            org.SuspendedAt = null;
            org.SuspendReason = null;
            await unitOfWork.SaveChangesAsync(ct);
        }
        return await BuildDetailAsync(id, ct);
    }

    // ---------- Дүкендер мен аккаунттар ----------

    public async Task<BusinessDetailDto> AddStoreAsync(Guid id, CreateStoreRequest r, CancellationToken ct = default)
    {
        var admin = await access.RequireAsync(ct);
        var org = await OrgAsync(id, ct);
        await unitOfWork.InTransactionAsync(async () =>
        {
            var store = await NewStoreAsync(org.Id, r, string.Empty, ct);
            // Бизнес иелері жаңа дүкенге де бірден кіреді.
            foreach (var owner in (await repo.StaffAsync(org.Id, ct)).Where(u => u.Memberships.Any(m => m.Role == StaffRole.Owner)))
                repo.AddMembership(new StoreMembership
                {
                    Id = Guid.NewGuid(), StaffUserId = owner.Id, StoreId = store.Id, Role = StaffRole.Owner,
                    Permissions = [.. StaffPermissions.DefaultsFor(StaffRole.Owner)], MaxDiscountPercent = 100,
                });
            access.Audit(admin, "business.store.create", org.Id, "store", store.Id, new { store.Name, store.Address });
            await unitOfWork.SaveChangesAsync(ct);
            return true;
        }, ct);
        return await BuildDetailAsync(id, ct);
    }

    public async Task<BusinessDetailDto> SetStoreActiveAsync(Guid id, Guid storeId, bool active, CancellationToken ct = default)
    {
        var admin = await access.RequireAsync(ct);
        var store = await repo.StoreAsync(id, storeId, ct) ?? throw new NotFoundException(Messages.BusinessStoreNotFound(Lang));
        store.IsActive = active;
        access.Audit(admin, active ? "business.store.activate" : "business.store.deactivate", id, "store", store.Id, new { store.Name });
        await unitOfWork.SaveChangesAsync(ct);
        return await BuildDetailAsync(id, ct);
    }

    public async Task<StaffCredentialsDto> AddAccountAsync(Guid id, CreateAccountRequest r, CancellationToken ct = default)
    {
        var admin = await access.RequireAsync(ct);
        var org = await OrgAsync(id, ct);
        var stores = (await repo.StoresAsync(org.Id, ct)).Select(s => s.Id).ToList();
        if (stores.Count == 0) throw new ValidationException(Messages.BusinessStoreNotFound(Lang), "storeId");
        var (user, password) = await NewAccountAsync(org.Id, r, stores, stores[0], ct, "account");
        access.Audit(admin, "business.account.create", org.Id, "staff", user.Id, new { user.Phone, Name = user.FullName, Role = r.Role ?? "Owner" });
        await unitOfWork.SaveChangesAsync(ct);
        var saved = await repo.StaffUserAsync(org.Id, user.Id, ct) ?? user;
        return new StaffCredentialsDto(Account(saved), password);
    }

    /// <summary>Құпиясөзді қалпына келтіру: жаңа уақытша құпиясөз, PIN мен барлық сеанс өшеді (CLI-дегі reset-password сияқты).</summary>
    public async Task<StaffCredentialsDto> ResetStaffPasswordAsync(Guid id, Guid staffId, CancellationToken ct = default)
    {
        var admin = await access.RequireAsync(ct);
        var user = await repo.StaffUserAsync(id, staffId, ct) ?? throw new NotFoundException(Messages.PlatformStaffNotFound(Lang));
        if (!user.HasPassword) throw new ValidationException(Messages.PlatformStaffPinOnly(Lang));
        var password = PlatformAccess.TempPassword();
        user.PasswordHash = hasher.Hash(password);
        user.IsActive = true;
        user.FailedLoginCount = 0;
        user.LockedUntil = null;
        user.PinHash = null;
        user.PinFailedCount = 0;
        user.PinLockedUntil = null;
        await repo.RevokeStaffSessionsAsync(user.Id, ct);
        access.Audit(admin, "business.account.password", id, "staff", user.Id, new { user.Phone, Name = user.FullName });
        await unitOfWork.SaveChangesAsync(ct);
        return new StaffCredentialsDto(Account(user), password);
    }

    public async Task<StaffAccountDto> SetStaffActiveAsync(Guid id, Guid staffId, bool active, CancellationToken ct = default)
    {
        var admin = await access.RequireAsync(ct);
        var user = await repo.StaffUserAsync(id, staffId, ct) ?? throw new NotFoundException(Messages.PlatformStaffNotFound(Lang));
        user.IsActive = active;
        if (!active) await repo.RevokeStaffSessionsAsync(user.Id, ct);
        access.Audit(admin, active ? "business.account.activate" : "business.account.deactivate", id, "staff", user.Id, new { user.Phone, Name = user.FullName });
        await unitOfWork.SaveChangesAsync(ct);
        return Account(user);
    }
}
