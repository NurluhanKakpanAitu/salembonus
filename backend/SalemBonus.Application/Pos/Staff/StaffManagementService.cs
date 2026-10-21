using SalemBonus.Application.BonusCards;
using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Application.Core.Staff;
using SalemBonus.Application.Platform;
using SalemBonus.Application.Pos.Catalog;
using SalemBonus.Domain.Core;

namespace SalemBonus.Application.Pos.Staff;

/// <summary>
/// «Сотрудники»: бизнестің қызметкерлері — қосу, рөлі мен дүкендері, рұқсаттары, жеңілдік шегі, PIN мен құпиясөз,
/// өшіру, сеанстары, әрекет журналы. Құқық — <c>staff.manage</c>; иелерді тек иесі қоса/өзгерте алады.
/// Қорғаныс: өзін өшіруге не өз қолжетімділігін өзгертуге болмайды, соңғы белсенді иесі өшірілмейді.
/// </summary>
public class StaffManagementService(
    IStaffAdminRepository repo,
    IPlatformRepository platform,
    IPasswordHasher hasher,
    ITokenService tokens,
    CatalogAccess access,
    ICurrentStaff current,
    IUnitOfWork unitOfWork)
{
    private AppLanguage Lang => access.Lang;

    // ---------- Тізім мен карточка ----------

    public async Task<StaffListDto> ListAsync(CancellationToken ct = default)
    {
        var (orgId, actor) = await access.RequireAsync(StaffPermissions.StaffManage, ct);
        var users = await repo.ListAsync(orgId, ct);
        var stats = await repo.SalesStatsAsync(orgId, DateTime.UtcNow.AddDays(-30), ct);
        var stores = await platform.StoresAsync(orgId, ct);
        var rows = users.Select(u => Row(u, actor, stats.GetValueOrDefault(u.Id)))
            .OrderByDescending(r => r.IsActive).ThenBy(r => Rank(r.Role)).ThenBy(r => r.FirstName).ThenBy(r => r.LastName).ToList();
        return new StaffListDto(rows, stores.Select(s => new StaffStoreOptionDto(s.Id, s.Name, s.IsActive)).ToList(), actor.Role == StaffRole.Owner);
    }

    public async Task<StaffDetailDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var (orgId, actor) = await access.RequireAsync(StaffPermissions.StaffManage, ct);
        var user = await repo.GetForUpdateAsync(orgId, id, ct) ?? throw new NotFoundException(Messages.PlatformStaffNotFound(Lang));
        return await DetailAsync(orgId, user, actor, ct);
    }

    private async Task<StaffDetailDto> DetailAsync(Guid orgId, StaffUser user, StoreMembership actor, CancellationToken ct)
    {
        var stats = await repo.SalesStatsAsync(orgId, DateTime.UtcNow.AddDays(-30), ct);
        var stores = await platform.StoresAsync(orgId, ct);
        var sessions = await repo.SessionsAsync(user.Id, ct);
        var primary = Primary(user);
        var accessRows = stores.Where(s => s.IsActive || user.Memberships.Any(m => m.StoreId == s.Id)).Select(s =>
        {
            var m = user.Memberships.FirstOrDefault(x => x.StoreId == s.Id);
            return new StaffStoreAccessDto(s.Id, s.Name, s.IsActive, (m?.Role ?? StaffRole.Cashier).ToString(), m?.Permissions ?? [],
                m?.MaxDiscountPercent ?? 0, m is { IsActive: true });
        }).ToList();
        return new StaffDetailDto(Row(user, actor, stats.GetValueOrDefault(user.Id)),
            primary?.Role == StaffRole.Owner ? StaffPermissions.All : primary?.Permissions ?? [],
            primary?.MaxDiscountPercent ?? 0, accessRows,
            sessions.Select(s => new StaffSessionDto(s.Id, s.Device, s.CreatedAt, s.ExpiresAt, false)).ToList());
    }

    private StaffRowDto Row(StaffUser u, StoreMembership actor, StaffSalesStats? stats)
    {
        var active = u.Memberships.Where(m => m.IsActive && m.Store is { IsActive: true }).OrderBy(m => m.Store!.Name).ToList();
        var role = Primary(u)?.Role ?? StaffRole.Cashier;
        var now = DateTime.UtcNow;
        return new StaffRowDto(u.Id, u.FirstName, u.LastName, u.Phone, u.Language, role.ToString(),
            active.Select(m => m.StoreId).ToList(), active.Select(m => m.Store!.Name).ToList(),
            u.IsActive, u.HasPin, u.HasPassword, u.IsLockedOut(now) || u.IsPinLockedOut(now), u.LastLoginAt, u.CreatedAt,
            stats?.Sales ?? 0, stats?.Revenue ?? 0, stats?.LastSaleAt,
            u.Id == current.StaffUserId, role != StaffRole.Owner || actor.Role == StaffRole.Owner);
    }

    /// <summary>Негізгі мүшелік: белсенді, ең жоғары рөлі (иесі > әкімші > кассир); болмаса — кез келгені.</summary>
    private static StoreMembership? Primary(StaffUser u) =>
        u.Memberships.Where(m => m.IsActive).OrderBy(m => Rank(m.Role.ToString())).FirstOrDefault()
        ?? u.Memberships.OrderBy(m => Rank(m.Role.ToString())).FirstOrDefault();

    private static int Rank(string role) => role switch { "Owner" => 0, "Admin" => 1, _ => 2 };

    // ---------- Қосу / өзгерту ----------

    public async Task<StaffSavedDto> CreateAsync(SaveStaffRequest r, CancellationToken ct = default)
    {
        var (orgId, actor) = await access.RequireAsync(StaffPermissions.StaffManage, ct);
        var role = Role(r.Role);
        RequireOwnerFor(role, actor);
        var phone = Phone(r.Phone);
        if (await repo.PhoneExistsAsync(phone, ct)) throw new ValidationException(Messages.PlatformPhoneExists(Lang), "phone");
        var first = access.Name(r.FirstName, 60, "firstName");
        var stores = await platform.StoresAsync(orgId, ct);
        var targets = Targets(role, r.StoreIds, stores);
        var permissions = Permissions(role, r.Permissions, StaffPermissions.DefaultsFor(role));
        var discount = Discount(role, r.MaxDiscountPercent, role == StaffRole.Cashier ? 0 : 0);

        string? password = null;
        var id = Guid.NewGuid();
        var user = new StaffUser
        {
            Id = id, OrganizationId = orgId, Phone = phone, FirstName = first, LastName = CatalogAccess.Optional(r.LastName, 60) ?? string.Empty,
            Language = Language(r.Language), CreatedAt = DateTime.UtcNow,
            Memberships = targets.Select(s => new StoreMembership
            {
                Id = Guid.NewGuid(), StaffUserId = id, StoreId = s.Id, Role = role, Permissions = [.. permissions], MaxDiscountPercent = discount,
            }).ToList(),
        };
        // Кассир — тек PIN-мен (құпиясөзі жоқ); иесі мен әкімші — құпиясөзбен, PIN қалауынша.
        if (role == StaffRole.Cashier)
        {
            if (string.IsNullOrWhiteSpace(r.Pin)) throw new ValidationException(Messages.StaffCashierNeedsPin(Lang), "pin");
        }
        else if (r.GeneratePassword ?? true)
        {
            password = PlatformAccess.TempPassword();
            user.PasswordHash = hasher.Hash(password);
        }
        if (!string.IsNullOrWhiteSpace(r.Pin))
        {
            CredentialRules.ValidatePin(r.Pin.Trim(), Lang);
            user.PinHash = hasher.Hash(r.Pin.Trim());
        }
        repo.Add(user);
        access.Audit(orgId, actor.StoreId, "staff.create", "staff", user.Id, null,
            new { user.Phone, Name = user.FullName, Role = role.ToString(), Stores = targets.Select(s => s.Name).ToList(), MaxDiscount = discount });
        await unitOfWork.SaveChangesAsync(ct);
        var saved = await repo.GetForUpdateAsync(orgId, user.Id, ct) ?? user;
        return new StaffSavedDto(await DetailAsync(orgId, saved, actor, ct), password);
    }

    public async Task<StaffSavedDto> UpdateAsync(Guid id, SaveStaffRequest r, CancellationToken ct = default)
    {
        var (orgId, actor) = await access.RequireAsync(StaffPermissions.StaffManage, ct);
        var user = await repo.GetForUpdateAsync(orgId, id, ct) ?? throw new NotFoundException(Messages.PlatformStaffNotFound(Lang));
        var currentRole = Primary(user)?.Role ?? StaffRole.Cashier;
        RequireOwnerFor(currentRole, actor);
        var role = Role(r.Role ?? currentRole.ToString());
        RequireOwnerFor(role, actor);
        var self = user.Id == current.StaffUserId;

        var stores = await platform.StoresAsync(orgId, ct);
        var primary = Primary(user);
        var before = new
        {
            user.Phone, Name = user.FullName, user.Language, Role = currentRole.ToString(),
            Stores = user.Memberships.Where(m => m.IsActive).Select(m => m.Store?.Name ?? m.StoreId.ToString()).ToList(),
            Permissions = primary?.Permissions ?? [], MaxDiscount = primary?.MaxDiscountPercent ?? 0,
        };

        var phone = Phone(r.Phone ?? user.Phone);
        if (phone != user.Phone && await repo.PhoneExistsAsync(phone, ct)) throw new ValidationException(Messages.PlatformPhoneExists(Lang), "phone");
        user.Phone = phone;
        user.FirstName = access.Name(r.FirstName ?? user.FirstName, 60, "firstName");
        user.LastName = CatalogAccess.Optional(r.LastName, 60) ?? (r.LastName is null ? user.LastName : string.Empty);
        user.Language = Language(r.Language ?? user.Language);

        var targets = Targets(role, r.StoreIds ?? user.Memberships.Where(m => m.IsActive).Select(m => m.StoreId).ToList(), stores);
        var permissions = Permissions(role, r.Permissions, role == currentRole && primary is not null ? primary.Permissions : StaffPermissions.DefaultsFor(role));
        var discount = Discount(role, r.MaxDiscountPercent ?? primary?.MaxDiscountPercent, 0);
        if (self)
        {
            // Өз қолжетімділігін өзгертуге болмайды — бұл кездейсоқ өзін-өзі бизнестен шығарудан қорғайды.
            var activeStores = user.Memberships.Where(m => m.IsActive).Select(m => m.StoreId).OrderBy(x => x).ToList();
            if (role != currentRole || !targets.Select(s => s.Id).OrderBy(x => x).SequenceEqual(activeStores)
                || !permissions.OrderBy(x => x).SequenceEqual((primary?.Permissions ?? []).OrderBy(x => x)) || discount != (primary?.MaxDiscountPercent ?? 0))
                throw new ValidationException(Messages.StaffSelfAccess(Lang), "role");
        }
        else
        {
            if (currentRole == StaffRole.Owner && role != StaffRole.Owner && user.IsActive && await repo.OtherActiveOwnersAsync(orgId, user.Id, ct) == 0)
                throw new ValidationException(Messages.StaffLastOwner(Lang), "role");
            SyncMemberships(user, role, targets, permissions, discount, stores);
            // Кассирге айналғанда құпиясөз алынады (кассир тек PIN-мен кіреді); кассирден өскенде — құпиясөз қалпына келтірумен беріледі.
            if (role == StaffRole.Cashier && user.PasswordHash is not null) user.PasswordHash = null;
        }
        if (!string.IsNullOrWhiteSpace(r.Pin))
        {
            CredentialRules.ValidatePin(r.Pin.Trim(), Lang);
            user.PinHash = hasher.Hash(r.Pin.Trim());
            user.PinFailedCount = 0;
            user.PinLockedUntil = null;
        }
        if (role == StaffRole.Cashier && !user.HasPin) throw new ValidationException(Messages.StaffCashierNeedsPin(Lang), "pin");

        access.Audit(orgId, actor.StoreId, "staff.update", "staff", user.Id, before, new
        {
            user.Phone, Name = user.FullName, user.Language, Role = role.ToString(), Stores = targets.Select(s => s.Name).ToList(),
            Permissions = permissions, MaxDiscount = discount, PinChanged = !string.IsNullOrWhiteSpace(r.Pin),
        });
        await unitOfWork.SaveChangesAsync(ct);
        var saved = await repo.GetForUpdateAsync(orgId, user.Id, ct) ?? user;
        return new StaffSavedDto(await DetailAsync(orgId, saved, actor, ct), null);
    }

    /// <summary>Мүшеліктерді мақсатты дүкендерге келтіру: барына рөл/рұқсат/шек, жоғына жаңасы, қалғаны өшіріледі.</summary>
    private void SyncMemberships(StaffUser user, StaffRole role, IReadOnlyList<Domain.Entities.Store> targets, IReadOnlyList<string> permissions, decimal discount,
        IReadOnlyList<Domain.Entities.Store> stores)
    {
        var targetIds = targets.Select(s => s.Id).ToHashSet();
        foreach (var s in stores)
        {
            var m = user.Memberships.FirstOrDefault(x => x.StoreId == s.Id);
            if (targetIds.Contains(s.Id))
            {
                if (m is null)
                {
                    m = new StoreMembership { Id = Guid.NewGuid(), StaffUserId = user.Id, StoreId = s.Id, Store = s };
                    user.Memberships.Add(m);
                    repo.AddMembership(m);
                }
                m.Role = role;
                m.Permissions = [.. permissions];
                m.MaxDiscountPercent = discount;
                m.IsActive = true;
            }
            else if (m is not null) m.IsActive = false;
        }
    }

    // ---------- Күйі, құпиясөз, PIN, сеанстар ----------

    public async Task<StaffDetailDto> SetActiveAsync(Guid id, bool active, CancellationToken ct = default)
    {
        var (orgId, actor) = await access.RequireAsync(StaffPermissions.StaffManage, ct);
        var user = await repo.GetForUpdateAsync(orgId, id, ct) ?? throw new NotFoundException(Messages.PlatformStaffNotFound(Lang));
        var role = Primary(user)?.Role ?? StaffRole.Cashier;
        RequireOwnerFor(role, actor);
        if (user.Id == current.StaffUserId) throw new ValidationException(Messages.StaffSelfAccess(Lang), "active");
        if (!active && user.IsActive && role == StaffRole.Owner && await repo.OtherActiveOwnersAsync(orgId, user.Id, ct) == 0)
            throw new ValidationException(Messages.StaffLastOwner(Lang), "active");
        user.IsActive = active;
        if (active) { user.FailedLoginCount = 0; user.LockedUntil = null; user.PinFailedCount = 0; user.PinLockedUntil = null; }
        else await repo.RevokeSessionsAsync(user.Id, null, ct);
        access.Audit(orgId, actor.StoreId, active ? "staff.activate" : "staff.deactivate", "staff", user.Id, null, new { user.Phone, Name = user.FullName });
        await unitOfWork.SaveChangesAsync(ct);
        return await DetailAsync(orgId, user, actor, ct);
    }

    /// <summary>Жаңа уақытша құпиясөз (иесі/әкімші): бұғат алынады, барлық сеансы жабылады. Кассирге — PIN қою.</summary>
    public async Task<StaffSavedDto> ResetPasswordAsync(Guid id, CancellationToken ct = default)
    {
        var (orgId, actor) = await access.RequireAsync(StaffPermissions.StaffManage, ct);
        var user = await repo.GetForUpdateAsync(orgId, id, ct) ?? throw new NotFoundException(Messages.PlatformStaffNotFound(Lang));
        var role = Primary(user)?.Role ?? StaffRole.Cashier;
        RequireOwnerFor(role, actor);
        if (role == StaffRole.Cashier) throw new ValidationException(Messages.StaffHasNoPassword(Lang));
        var password = PlatformAccess.TempPassword();
        user.PasswordHash = hasher.Hash(password);
        user.FailedLoginCount = 0;
        user.LockedUntil = null;
        await repo.RevokeSessionsAsync(user.Id, null, ct);
        access.Audit(orgId, actor.StoreId, "staff.password_reset", "staff", user.Id, null, new { user.Phone, Name = user.FullName });
        await unitOfWork.SaveChangesAsync(ct);
        return new StaffSavedDto(await DetailAsync(orgId, user, actor, ct), password);
    }

    public async Task<StaffDetailDto> SetPinAsync(Guid id, string? pin, CancellationToken ct = default)
    {
        var (orgId, actor) = await access.RequireAsync(StaffPermissions.StaffManage, ct);
        var user = await repo.GetForUpdateAsync(orgId, id, ct) ?? throw new NotFoundException(Messages.PlatformStaffNotFound(Lang));
        RequireOwnerFor(Primary(user)?.Role ?? StaffRole.Cashier, actor);
        var value = pin?.Trim() ?? string.Empty;
        if (value.Length == 0) throw new ValidationException(Messages.StaffPinRequired(Lang), "pin");
        CredentialRules.ValidatePin(value, Lang);
        user.PinHash = hasher.Hash(value);
        user.PinFailedCount = 0;
        user.PinLockedUntil = null;
        access.Audit(orgId, actor.StoreId, "staff.pin_set", "staff", user.Id, null, new { user.Phone, Name = user.FullName });
        await unitOfWork.SaveChangesAsync(ct);
        return await DetailAsync(orgId, user, actor, ct);
    }

    public async Task<StaffDetailDto> RevokeSessionsAsync(Guid id, CancellationToken ct = default)
    {
        var (orgId, actor) = await access.RequireAsync(StaffPermissions.StaffManage, ct);
        var user = await repo.GetForUpdateAsync(orgId, id, ct) ?? throw new NotFoundException(Messages.PlatformStaffNotFound(Lang));
        RequireOwnerFor(Primary(user)?.Role ?? StaffRole.Cashier, actor);
        await repo.RevokeSessionsAsync(user.Id, null, ct);
        access.Audit(orgId, actor.StoreId, "staff.sessions_revoked", "staff", user.Id, null, new { user.Phone, Name = user.FullName });
        await unitOfWork.SaveChangesAsync(ct);
        return await DetailAsync(orgId, user, actor, ct);
    }

    // ---------- Өз сеанстарым («Настройка → Безопасность») ----------

    public async Task<IReadOnlyList<StaffSessionDto>> MySessionsAsync(string? currentToken, CancellationToken ct = default)
    {
        var hash = string.IsNullOrEmpty(currentToken) ? null : tokens.Hash(currentToken);
        var list = await repo.SessionsAsync(current.StaffUserId, ct);
        return list.Select(s => new StaffSessionDto(s.Id, s.Device, s.CreatedAt, s.ExpiresAt, s.TokenHash == hash)).ToList();
    }

    public async Task RevokeMySessionAsync(Guid id, CancellationToken ct = default)
    {
        if (!await repo.RevokeSessionAsync(current.StaffUserId, id, ct)) throw new NotFoundException(Messages.StaffSessionNotFound(Lang));
    }

    public Task RevokeOtherSessionsAsync(string? currentToken, CancellationToken ct = default) =>
        repo.RevokeSessionsAsync(current.StaffUserId, string.IsNullOrEmpty(currentToken) ? null : tokens.Hash(currentToken), ct);

    // ---------- Әрекет журналы ----------

    public async Task<AuditPageDto> AuditAsync(AuditQuery q, CancellationToken ct = default)
    {
        var (orgId, _) = await access.RequireAnyAsync(ct, StaffPermissions.StaffManage, StaffPermissions.SettingsManage);
        var (rows, total) = await repo.AuditAsync(orgId, q, ct);
        var users = (await repo.ListAsync(orgId, ct)).ToDictionary(u => u.Id, u => u.FullName);
        var stores = (await platform.StoresAsync(orgId, ct)).ToDictionary(s => s.Id, s => s.Name);
        var registers = await repo.RegisterNamesAsync(rows.Where(a => a.RegisterId is not null).Select(a => a.RegisterId!.Value).Distinct().ToList(), ct);
        var actions = await repo.AuditActionsAsync(orgId, ct);
        return new AuditPageDto(rows.Select(a => new AuditRowDto(a.Id, a.CreatedAt, a.Action, a.Entity, a.EntityId,
            a.StaffUserId, a.StaffUserId is { } sid ? users.GetValueOrDefault(sid) : null,
            a.StoreId, a.StoreId is { } st ? stores.GetValueOrDefault(st) : null,
            a.RegisterId is { } rid ? registers.GetValueOrDefault(rid) : null,
            a.Success, a.Details, a.OldValues, a.NewValues, a.Amount, a.Ip)).ToList(), total, actions);
    }

    // ---------- Тексерулер ----------

    private void RequireOwnerFor(StaffRole target, StoreMembership actor)
    {
        if (target == StaffRole.Owner && actor.Role != StaffRole.Owner) throw new ForbiddenException(Messages.StaffOwnerOnly(Lang));
    }

    private StaffRole Role(string? raw) => raw?.Trim().ToLowerInvariant() switch
    {
        "owner" => StaffRole.Owner,
        "admin" => StaffRole.Admin,
        "cashier" => StaffRole.Cashier,
        _ => throw new ValidationException(Messages.StaffRoleInvalid(Lang), "role"),
    };

    private string Phone(string? raw)
    {
        var value = raw?.Trim() ?? string.Empty;
        if (!BonusRules.LooksLikePhone(value)) throw new ValidationException(Messages.StaffPhoneInvalid(Lang), "phone");
        var phone = BonusRules.NormalizePhone(value);
        if (phone.Length != 12 || !phone.StartsWith("+7")) throw new ValidationException(Messages.StaffPhoneInvalid(Lang), "phone");
        return phone;
    }

    private string Language(string? raw) => raw?.Trim().ToLowerInvariant() switch
    {
        null or "" or "ru" => "ru",
        "kk" => "kk",
        _ => throw new ValidationException(Messages.StaffLanguageInvalid(Lang), "language"),
    };

    /// <summary>Иесі — бизнестің барлық белсенді дүкенінде; қалғаны — таңдалған дүкендерде (кемінде бір).</summary>
    private IReadOnlyList<Domain.Entities.Store> Targets(StaffRole role, IReadOnlyList<Guid>? ids, IReadOnlyList<Domain.Entities.Store> stores)
    {
        var active = stores.Where(s => s.IsActive).ToList();
        if (role == StaffRole.Owner) return active;
        var set = (ids ?? []).ToHashSet();
        var targets = active.Where(s => set.Contains(s.Id)).ToList();
        if (targets.Count == 0) throw new ValidationException(Messages.StaffStoresRequired(Lang), "storeIds");
        return targets;
    }

    private IReadOnlyList<string> Permissions(StaffRole role, IReadOnlyList<string>? raw, IReadOnlyList<string> fallback)
    {
        if (role == StaffRole.Owner) return StaffPermissions.All;
        if (raw is null) return fallback;
        var list = new List<string>();
        foreach (var p in raw)
        {
            var key = p.Trim();
            if (!StaffPermissions.All.Contains(key)) throw new ValidationException(Messages.StaffPermissionUnknown(Lang, p), "permissions");
            if (!list.Contains(key)) list.Add(key);
        }
        // Қызметкерлер мен баптауларды басқару — тек иесі мен әкімшіге (кассирге берілмейді).
        if (role == StaffRole.Cashier) list.RemoveAll(p => p is StaffPermissions.StaffManage or StaffPermissions.SettingsManage);
        return StaffPermissions.All.Where(list.Contains).ToList();
    }

    private decimal Discount(StaffRole role, decimal? raw, decimal fallback)
    {
        if (role == StaffRole.Owner) return 100;
        var value = raw ?? fallback;
        if (value is < 0 or > 100) throw new ValidationException(Messages.ValueOutOfRange(Lang, 0, 100), "maxDiscountPercent");
        return Math.Round(value, 2);
    }
}
