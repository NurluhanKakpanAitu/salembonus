using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Application.Pos.Catalog;
using SalemBonus.Application.Pos.Sales;
using SalemBonus.Domain.Core;
using SalemBonus.Domain.Entities;
using SalemBonus.Domain.Enums;
using SalemBonus.Domain.Pos.Bonus;

namespace SalemBonus.Application.Pos.Bonus;

/// <summary>Сұраныс контексі: ұйым, ағымдағы дүкен (бағдарлама), таңдалған дүкендер, кезең, құқықтар.</summary>
public record BonusCtx(Guid OrgId, StoreMembership Current, Store Store, IReadOnlyList<Guid> StoreIds, TimeZoneInfo Zone, DateOnly Start, DateOnly End,
    DateTime FromUtc, DateTime ToUtc, BonusPermissionsDto Can)
{
    public DateOnly LocalDate(DateTime utc) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, Zone));
    public DateOnly Today => StoreTime.Today(Zone);
}

/// <summary>
/// «Бонусы» бөлімінің ортақ кіруі (ТЗ «Бонусы» §12): құқық серверде әр шақыруда тексеріледі; бағдарлама — ағымдағы дүкендікі,
/// шолу/статистика/журнал бірнеше дүкенді (әрқайсысында bonus.view болса) біріктіре алады. Телефон — customers.contacts-сыз жасырын (§16).
/// </summary>
public class BonusAccess(CatalogAccess access, IStaffRepository staff, IStoreRepository stores, BonusProgram program, IUnitOfWork unitOfWork)
{
    public AppLanguage Lang => access.Lang;
    public Guid StaffUserId => access.StaffUserId;
    public CatalogAccess Catalog => access;

    public async Task<BonusCtx> ContextAsync(string permission, CancellationToken ct, string? storeIds = null, DateOnly? from = null, DateOnly? to = null)
    {
        var (orgId, current) = await access.RequireAsync(permission, ct);
        var ids = new List<Guid> { current.StoreId };
        var requested = (storeIds ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => Guid.TryParse(x, out var g) ? g : throw new ValidationException(Messages.AnalyticsStoreForbidden(Lang), "storeIds")).Distinct().ToList();
        if (requested.Count > 0)
        {
            ids.Clear();
            foreach (var id in requested)
            {
                var m = id == current.StoreId ? current : await staff.GetMembershipAsync(access.StaffUserId, id, ct);
                if (m is null || m.Store?.OrganizationId != orgId || !m.Has(StaffPermissions.BonusView)) throw new ForbiddenException(Messages.AnalyticsStoreForbidden(Lang));
                ids.Add(id);
            }
        }
        var store = await stores.GetByIdAsync(current.StoreId, ct) ?? throw new NotFoundException(Messages.StoreNotFound(Lang));
        var zone = StoreTime.Zone(store);
        var today = StoreTime.Today(zone);
        var start = from ?? new DateOnly(today.Year, today.Month, 1);
        var end = to ?? today;
        if (end < start) (start, end) = (end, start);
        var can = new BonusPermissionsDto(current.Has(StaffPermissions.BonusManage), current.Has(StaffPermissions.BonusCampaigns),
            current.Has(StaffPermissions.BonusAccrue), current.Has(StaffPermissions.BonusWriteOff), current.Has(StaffPermissions.BonusReverse),
            current.Has(StaffPermissions.BonusTerms), current.Has(StaffPermissions.StatisticsExport), current.Has(StaffPermissions.CustomersContacts));
        return new BonusCtx(orgId, current, store, ids, zone, start, end, StoreTime.StartOfDayUtc(start, zone), StoreTime.StartOfDayUtc(end.AddDays(1), zone), can);
    }

    /// <summary>Бағдарлама мәртебелері мен баптаулары (бірінші ашылғанда іске қосылады) — сақталған күйінде.</summary>
    public async Task<(IReadOnlyList<LoyaltyStatus> Statuses, BonusSettings Settings)> ProgramAsync(BonusCtx c, CancellationToken ct)
    {
        var (list, settings) = await program.EnsureAsync(c.Store, access.StaffUserId, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return (list, settings);
    }

    public void Require(bool allowed)
    {
        if (!allowed) throw new ForbiddenException(Messages.StaffPermissionDenied(Lang));
    }

    public static string? Phone(string? phone, bool canContacts) =>
        phone is null ? null : canContacts || phone.Length < 6 ? phone : $"{phone[..Math.Min(5, phone.Length)]} *** ** {phone[^2..]}";

    public static string FullName(string first, string last) => string.Join(' ', new[] { first, last }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim() is { Length: > 0 } n ? n : "—";

    public static (string Name, string Color) StatusOf(Guid? statusId, CustomerLevel level, IReadOnlyList<LoyaltyStatus> list, AppLanguage lang)
    {
        var s = statusId is { } id ? list.FirstOrDefault(x => x.Id == id) : null;
        s ??= list.Where(x => x.Level == level).OrderBy(x => x.SortOrder).FirstOrDefault();
        return s is null ? (BonusCards.CustomerLevels.Name(level, lang), "#94A3B8") : (s.Name, s.Color);
    }

    public string Reason(string? raw, string field = "reason")
    {
        var r = CatalogAccess.Optional(raw, 500);
        if (r is null || r.Length < 3) throw new ValidationException(Messages.BonusReasonRequired(Lang), field);
        return r;
    }

    public void Audit(BonusCtx c, string action, string entity, Guid id, object? before, object? after) =>
        access.Audit(c.OrgId, c.Store.Id, action, entity, id, before, after);
}
