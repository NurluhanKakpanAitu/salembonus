using System.Net.Mail;
using SalemBonus.Application.BonusCards;
using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Application.Pos.Bonus;
using SalemBonus.Application.Pos.Catalog;
using SalemBonus.Application.Pos.Sales;
using SalemBonus.Domain.Core;
using SalemBonus.Domain.Entities;
using SalemBonus.Domain.Enums;
using SalemBonus.Domain.Pos.Bonus;
using SalemBonus.Domain.Pos.Clients;

namespace SalemBonus.Application.Pos.Clients;

/// <summary>
/// Сұраныс контексі: ұйым, ағымдағы дүкен (бонус мәртебесі мен хабар осы дүкен атынан), таңдалған дүкендер (көрсеткіштер),
/// баптаулар (белсенділік шектері), құқықтар. Шектер — «қазір» сәтінен күнмен.
/// </summary>
public record ClientCtx(Guid OrgId, StoreMembership Current, Store Store, IReadOnlyList<Guid> StoreIds, TimeZoneInfo Zone, DateOnly Today,
    ClientSettings Settings, ClientPermissionsDto Can, IReadOnlyList<LoyaltyStatus> Statuses)
{
    public DateTime Now { get; } = DateTime.UtcNow;
    public DateTime ActiveSince => Now.AddDays(-Settings.ActiveDays);
    public DateTime PassiveBefore => Now.AddDays(-Settings.PassiveDays);
    public DateTime NewSince => Now.AddDays(-Settings.NewDays);

    public ClientQuery Query(ClientFilter? filter = null) =>
        new(OrgId, StoreIds, Store.Id, Zone, Today, ActiveSince, PassiveBefore, NewSince, filter ?? new ClientFilter());

    public DateOnly LocalDate(DateTime utc) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, Zone));
}

/// <summary>
/// «Клиенты» бөлімінің ортақ кіруі (ТЗ «Клиенты» §22): құқық әр шақыруда серверде; дүкен өз ұйымының клиенттерін ғана көреді
/// (SALEMPOS.md 4.6). Телефон мен e-mail — customers.contacts-сыз жасырын. Өріс тексерулері — қолмен қосу мен импортқа ортақ.
/// </summary>
public class ClientAccess(CatalogAccess access, IStaffRepository staff, IStoreRepository stores, IClientRepository repo, IBonusRepository bonus)
{
    public AppLanguage Lang => access.Lang;
    public Guid StaffUserId => access.StaffUserId;
    public CatalogAccess Catalog => access;

    public async Task<ClientCtx> ContextAsync(string permission, CancellationToken ct, string? storeIds = null, bool ensure = true)
    {
        var (orgId, current) = await access.RequireAsync(permission, ct);
        if (!current.Has(StaffPermissions.ClientsView)) throw new ForbiddenException(Messages.StaffPermissionDenied(Lang));
        var ids = new List<Guid> { current.StoreId };
        var requested = (storeIds ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => Guid.TryParse(x, out var g) ? g : throw new ValidationException(Messages.AnalyticsStoreForbidden(Lang), "storeIds")).Distinct().ToList();
        if (requested.Count > 0)
        {
            ids.Clear();
            foreach (var id in requested)
            {
                var m = id == current.StoreId ? current : await staff.GetMembershipAsync(access.StaffUserId, id, ct);
                if (m is null || m.Store?.OrganizationId != orgId || !m.Has(StaffPermissions.ClientsView)) throw new ForbiddenException(Messages.AnalyticsStoreForbidden(Lang));
                ids.Add(id);
            }
        }
        var store = await stores.GetByIdAsync(current.StoreId, ct) ?? throw new NotFoundException(Messages.StoreNotFound(Lang));
        var zone = StoreTime.Zone(store);
        var settings = await repo.SettingsAsync(orgId, ct) ?? new ClientSettings { OrganizationId = orgId };
        var statuses = await bonus.StatusesAsync(store.Id, ct);
        if (ensure) await repo.EnsureProfilesAsync(orgId, ct);
        bool Has(string p) => current.Has(p);
        var can = new ClientPermissionsDto(Has(StaffPermissions.ClientsEdit), Has(StaffPermissions.ClientsArchive), Has(StaffPermissions.ClientsExport),
            Has(StaffPermissions.ClientsMerge), Has(StaffPermissions.ClientsDebts) || Has(StaffPermissions.FinanceView), Has(StaffPermissions.CustomersContacts),
            Has(StaffPermissions.SettingsManage), Has(StaffPermissions.BonusView), Has(StaffPermissions.BonusAccrue), Has(StaffPermissions.BonusManage),
            Has(StaffPermissions.BonusCampaigns), Has(StaffPermissions.SalesView), Has(StaffPermissions.SalesCreate), Has(StaffPermissions.FinanceOperate));
        return new ClientCtx(orgId, current, store, ids, zone, StoreTime.Today(zone), settings, can, statuses);
    }

    public void Require(bool allowed)
    {
        if (!allowed) throw new ForbiddenException(Messages.StaffPermissionDenied(Lang));
    }

    public async Task<ClientProfile> ProfileAsync(ClientCtx c, Guid id, CancellationToken ct) =>
        await repo.GetAsync(c.OrgId, id, ct) ?? throw new NotFoundException(Messages.ClientNotFound(Lang));

    public void Audit(ClientCtx c, string action, Guid profileId, object? before, object? after) =>
        access.Audit(c.OrgId, c.Store.Id, action, "client_profile", profileId, before, after);

    // ---------- Жолды көрсету ----------

    public static string Activity(ClientCtx c, ClientRow r) =>
        r.Purchases == 0 || r.LastPurchaseAt is null ? "noPurchases"
        : r.LastPurchaseAt >= c.ActiveSince ? "active"
        : r.LastPurchaseAt >= c.PassiveBefore ? "cooling"
        : "passive";

    public (string Name, string Color) Status(ClientCtx c, Guid? statusId, CustomerLevel? level, bool hasCard) =>
        hasCard ? BonusAccess.StatusOf(statusId, level ?? CustomerLevel.New, c.Statuses, Lang) : ("—", "#94A3B8");

    public ClientRowDto Row(ClientCtx c, ClientRow r)
    {
        var p = r.Profile;
        var (statusName, statusColor) = Status(c, r.StatusId, r.Level, r.HasCurrentCard);
        int? days = r.LastPurchaseAt is { } last ? c.Today.DayNumber - c.LocalDate(last).DayNumber : null;
        return new ClientRowDto(p.Id, p.CustomerId, p.Number, BonusAccess.FullName(r.FirstName, r.LastName), r.FirstName, r.LastName, p.MiddleName,
            BonusAccess.Phone(r.Phone, c.Can.Contacts), c.Can.Contacts ? p.Email : null, r.BirthDate, p.Gender.ToString(), p.Source.ToString(),
            r.HasCurrentCard ? r.StatusId : null, statusName, statusColor, r.Purchases, r.Amount, r.Average, r.FirstPurchaseAt, r.LastPurchaseAt, days, r.Bonus,
            r.Debt, r.Overdue, p.CreatedAt, r.HasApp, r.HasCurrentCard, p.IsArchived, p.CreatedAt >= c.NewSince, Activity(c, r), p.ConsentApp, p.ConsentSms,
            p.ConsentWhatsApp, p.ConsentEmail, p.KatoCode, p.CustomFields);
    }

    public static ClientSettingsDto Settings(ClientSettings s) =>
        new(s.RequireLastName, s.RequireBirthDate, s.ActiveDays, s.PassiveDays, s.NewDays, s.DueSoonDays, s.DefaultColumns, s.CustomFields,
            s.UpdatedAt == default ? null : s.UpdatedAt);

    // ---------- Өріс тексерулері ----------

    /// <summary>Қазақстан нөмірі «+7XXXXXXXXXX» түріне келтіріледі (8-ден басталса да).</summary>
    public string Phone(string? raw, string field = "phone")
    {
        var value = raw?.Trim() ?? string.Empty;
        if (!BonusRules.LooksLikePhone(value)) throw new ValidationException(Messages.PhoneInvalid(Lang), field);
        var phone = BonusRules.NormalizePhone(value);
        if (phone.Length != 12 || !phone.StartsWith("+7")) throw new ValidationException(Messages.PhoneNotKz(Lang), field);
        return phone;
    }

    public static bool TryPhone(string? raw, out string phone)
    {
        phone = string.Empty;
        var value = raw?.Trim() ?? string.Empty;
        if (!BonusRules.LooksLikePhone(value)) return false;
        phone = BonusRules.NormalizePhone(value);
        return phone.Length == 12 && phone.StartsWith("+7");
    }

    public string? Email(string? raw)
    {
        var value = CatalogAccess.Optional(raw, 200);
        if (value is null) return null;
        if (!IsEmail(value)) throw new ValidationException(Messages.ClientEmailInvalid(Lang), "email");
        return value.ToLowerInvariant();
    }

    public static bool IsEmail(string value)
    {
        if (value.Contains(' ') || !value.Contains('@')) return false;
        try { return new MailAddress(value).Address == value; } catch (FormatException) { return false; }
    }

    public DateOnly? BirthDate(DateOnly? value, bool required)
    {
        if (value is null)
        {
            if (required) throw new ValidationException(Messages.BirthDateRequired(Lang), "birthDate");
            return null;
        }
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (value > today || value < today.AddYears(-120)) throw new ValidationException(Messages.BirthDateInvalid(Lang), "birthDate");
        return value;
    }

    public static ClientGender Gender(string? raw) =>
        raw?.Trim().ToLowerInvariant() switch
        {
            "male" or "m" or "м" or "муж" or "мужской" or "ер" => ClientGender.Male,
            "female" or "f" or "ж" or "жен" or "женский" or "әйел" => ClientGender.Female,
            _ => ClientGender.Unknown,
        };

    /// <summary>Қосымша өрістер: тек баптаулардағы кілттер; сан/күн/тізім түріне сай; бос мән — жойылады.</summary>
    public Dictionary<string, string> CustomFields(ClientSettings settings, IReadOnlyDictionary<string, string>? values, IReadOnlyDictionary<string, string>? existing)
    {
        var result = new Dictionary<string, string>(existing ?? new Dictionary<string, string>());
        if (values is null) return result;
        foreach (var (key, raw) in values)
        {
            var def = settings.CustomFields.FirstOrDefault(d => d.Key == key);
            if (def is null) continue;
            var value = CatalogAccess.Optional(raw, 300);
            if (value is null) { result.Remove(key); continue; }
            var ok = def.Type switch
            {
                "number" => decimal.TryParse(value.Replace(',', '.'), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out _),
                "date" => DateOnly.TryParseExact(value, "yyyy-MM-dd", out _),
                "select" => def.Options.Contains(value),
                _ => true,
            };
            if (!ok) throw new ValidationException(Messages.ClientCustomFieldInvalid(Lang, def.Label), $"customFields.{key}");
            result[key] = def.Type == "number" ? value.Replace(',', '.') : value;
        }
        return result;
    }

    public string Reason(string? raw)
    {
        var r = CatalogAccess.Optional(raw, 500);
        if (r is null || r.Length < 3) throw new ValidationException(Messages.ClientReasonRequired(Lang), "reason");
        return r;
    }

    /// <summary>Сүзгіні тазалау: бос тізімдер, іздеу ұзындығы, шекаралар реті.</summary>
    public static ClientFilter Clean(ClientFilter? f)
    {
        var x = f ?? new ClientFilter();
        x.StatusIds ??= [];
        x.Search = CatalogAccess.Optional(x.Search, 100);
        x.Quick = CatalogAccess.Optional(x.Quick, 20);
        x.Kato = CatalogAccess.Optional(x.Kato, 9);
        x.Consent = CatalogAccess.Optional(x.Consent, 16)?.ToLowerInvariant();
        if (x.BirthMonth is < 1 or > 12) x.BirthMonth = null;
        return x;
    }
}
