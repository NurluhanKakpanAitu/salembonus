using System.Text.RegularExpressions;
using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Application.Pos.Catalog;
using SalemBonus.Domain.Core;
using SalemBonus.Domain.Enums;
using SalemBonus.Domain.Pos.Bonus;

namespace SalemBonus.Application.Pos.Bonus;

/// <summary>
/// «Статусы клиентов» (ТЗ «Бонусы» §3) және клиенттің бонус карточкасы (§10): мәртебелер (қосу, өзгерту, көшірме, өшіру, жою — қолданылмаса),
/// клиенттер тізімі, карточка (партиялар, операциялар), мәртебені қолмен/ереже бойынша өзгерту, таратудан бас тарту.
/// Мәртебе өзгергенде бонус есептелмейді (§3.4).
/// </summary>
public partial class BonusStatusService(
    IBonusRepository repo,
    BonusAccess access,
    BonusProgram program,
    BonusMapper mapper,
    IUnitOfWork unitOfWork)
{
    private const int MaxPageSize = 100;
    private AppLanguage Lang => access.Lang;

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColor();

    private static readonly string[] Icons = ["sprout", "star", "crown", "gem", "heart", "award", "shield", "handshake", "flame", "trophy"];

    public async Task<BonusMetaDto> MetaAsync(CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.BonusView, ct);
        var (statuses, settings) = await access.ProgramAsync(c, ct);
        var usage = await repo.StatusUsageAsync(c.Store.Id, ct);
        return new BonusMetaDto(c.Store.Id, c.Store.Name, c.Store.TimeZone, statuses.Select(s => ToDto(s, usage)).ToList(), c.Can, Channels(Lang),
            c.Store.MaxRedeemPercent, settings.ExpiryEnabled, settings.BirthdayEnabled && settings.BirthdayAmount > 0);
    }

    /// <summary>Арналар: SalemBonus қосымшасы — әрқашан қосулы (клиент картасы бар), қалғаны — интеграция жоқ (§6.4).</summary>
    public static IReadOnlyList<BonusChannelDto> Channels(AppLanguage lang) =>
    [
        new("app", true, null),
        new("whatsapp", false, lang == AppLanguage.Ru ? "Интеграция WhatsApp не подключена" : "WhatsApp интеграциясы қосылмаған"),
        new("sms", false, lang == AppLanguage.Ru ? "SMS-провайдер не подключён" : "SMS провайдері қосылмаған"),
        new("email", false, lang == AppLanguage.Ru ? "Почтовый сервис не подключён" : "Пошта сервисі қосылмаған"),
    ];

    // ---------- Мәртебелер ----------

    public async Task<IReadOnlyList<BonusStatusDto>> ListAsync(CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.BonusView, ct);
        var (statuses, _) = await access.ProgramAsync(c, ct);
        var usage = await repo.StatusUsageAsync(c.Store.Id, ct);
        return statuses.OrderBy(s => s.SortOrder).Select(s => ToDto(s, usage)).ToList();
    }

    public async Task<BonusStatusDto> CreateAsync(SaveStatusRequest r, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.BonusManage, ct);
        var (statuses, _) = await access.ProgramAsync(c, ct);
        var status = new LoyaltyStatus { Id = Guid.NewGuid(), StoreId = c.Store.Id, CreatedBy = access.StaffUserId, CreatedAt = DateTime.UtcNow };
        await FillAsync(c, status, r, ct);
        var all = statuses.Append(status).ToList();
        BonusProgram.Rank(all);
        repo.AddStatus(status);
        await program.SyncLadderAsync(c.Store, all, ct);
        access.Audit(c, "bonus.status.create", "loyalty_status", status.Id, null, Snapshot(status));
        await unitOfWork.SaveChangesAsync(ct);
        program.Forget(c.Store.Id);
        return ToDto(status, await repo.StatusUsageAsync(c.Store.Id, ct));
    }

    public async Task<BonusStatusDto> UpdateAsync(Guid id, SaveStatusRequest r, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.BonusManage, ct);
        var (statuses, _) = await access.ProgramAsync(c, ct);
        var status = statuses.FirstOrDefault(s => s.Id == id) ?? throw new NotFoundException(Messages.BonusStatusNotFound(Lang));
        var before = Snapshot(status);
        await FillAsync(c, status, r, ct);
        EnsureRegistrationLeft(statuses);
        var all = statuses.ToList();
        BonusProgram.Rank(all);
        status.UpdatedAt = DateTime.UtcNow;
        await program.SyncLadderAsync(c.Store, all, ct);
        access.Audit(c, "bonus.status.update", "loyalty_status", status.Id, before, Snapshot(status));
        await unitOfWork.SaveChangesAsync(ct);
        program.Forget(c.Store.Id);
        return ToDto(status, await repo.StatusUsageAsync(c.Store.Id, ct));
    }

    /// <summary>Көшірме (§3.3): «(копия)» атауымен, өшірулі черновик — атауын өзгертіп, белсендіру керек.</summary>
    public async Task<BonusStatusDto> DuplicateAsync(Guid id, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.BonusManage, ct);
        var (statuses, _) = await access.ProgramAsync(c, ct);
        var src = statuses.FirstOrDefault(s => s.Id == id) ?? throw new NotFoundException(Messages.BonusStatusNotFound(Lang));
        var suffix = Lang == AppLanguage.Ru ? " (копия)" : " (көшірме)";
        var name = src.Name.Length + suffix.Length > 60 ? src.Name[..(60 - suffix.Length)] + suffix : src.Name + suffix;
        for (var i = 2; await repo.StatusNameExistsAsync(c.Store.Id, name, null, ct); i++) name = $"{src.Name[..Math.Min(src.Name.Length, 50)]}{suffix} {i}";
        var copy = new LoyaltyStatus
        {
            Id = Guid.NewGuid(), StoreId = c.Store.Id, Name = name, Icon = src.Icon, Color = src.Color, Rule = src.Rule == StatusRule.Registration ? StatusRule.Manual : src.Rule,
            ThresholdAmount = src.ThresholdAmount, ThresholdCount = src.ThresholdCount, Period = src.Period, AccrualPercent = src.AccrualPercent,
            CountReturns = src.CountReturns, ValidityDays = src.ValidityDays, AutoDowngrade = src.AutoDowngrade, Description = src.Description,
            IsActive = false, CreatedBy = access.StaffUserId,
        };
        var all = statuses.Append(copy).ToList();
        BonusProgram.Rank(all);
        repo.AddStatus(copy);
        access.Audit(c, "bonus.status.duplicate", "loyalty_status", copy.Id, new { From = src.Id }, Snapshot(copy));
        await unitOfWork.SaveChangesAsync(ct);
        program.Forget(c.Store.Id);
        return ToDto(copy, await repo.StatusUsageAsync(c.Store.Id, ct));
    }

    /// <summary>Өшіру: ереже автоматты қолданылмайды, клиенттер өз мәртебесінде қалады, тарих сақталады (§3.3, тест 3).</summary>
    public async Task<BonusStatusDto> SetActiveAsync(Guid id, bool active, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.BonusManage, ct);
        var (statuses, _) = await access.ProgramAsync(c, ct);
        var status = statuses.FirstOrDefault(s => s.Id == id) ?? throw new NotFoundException(Messages.BonusStatusNotFound(Lang));
        status.IsActive = active;
        status.UpdatedAt = DateTime.UtcNow;
        EnsureRegistrationLeft(statuses);
        await program.SyncLadderAsync(c.Store, statuses, ct);
        access.Audit(c, active ? "bonus.status.activate" : "bonus.status.deactivate", "loyalty_status", status.Id, null, new { status.Name });
        await unitOfWork.SaveChangesAsync(ct);
        program.Forget(c.Store.Id);
        return ToDto(status, await repo.StatusUsageAsync(c.Store.Id, ct));
    }

    /// <summary>Жою — тек ешкімде жоқ және баптауларда сілтемесі жоқ мәртебе (§3.3); әйтпесе өшіруді ұсынамыз.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.BonusManage, ct);
        var (statuses, settings) = await access.ProgramAsync(c, ct);
        var status = statuses.FirstOrDefault(s => s.Id == id) ?? throw new NotFoundException(Messages.BonusStatusNotFound(Lang));
        var used = (await repo.StatusUsageAsync(c.Store.Id, ct)).GetValueOrDefault(id);
        if (used > 0) throw new ValidationException(Messages.BonusStatusInUse(Lang, used));
        var rest = statuses.Where(s => s.Id != id).ToList();
        EnsureRegistrationLeft(rest);
        settings.ExpiryStatusIds = settings.ExpiryStatusIds.Where(x => x != id).ToList();
        settings.BirthdayStatusIds = settings.BirthdayStatusIds.Where(x => x != id).ToList();
        repo.RemoveStatus(status);
        BonusProgram.Rank(rest);
        await program.SyncLadderAsync(c.Store, rest, ct);
        access.Audit(c, "bonus.status.delete", "loyalty_status", status.Id, Snapshot(status), null);
        await unitOfWork.SaveChangesAsync(ct);
        program.Forget(c.Store.Id);
    }

    private void EnsureRegistrationLeft(IEnumerable<LoyaltyStatus> statuses)
    {
        if (!statuses.Any(s => s.IsActive && s.Rule == StatusRule.Registration)) throw new ValidationException(Messages.BonusStatusLastRegistration(Lang), "rule");
    }

    private async Task FillAsync(BonusCtx c, LoyaltyStatus s, SaveStatusRequest r, CancellationToken ct)
    {
        var name = (r.Name ?? string.Empty).Trim();
        if (name.Length is < 2 or > 60) throw new ValidationException(Messages.BonusStatusNameInvalid(Lang), "name");
        if (await repo.StatusNameExistsAsync(c.Store.Id, name, s.Id, ct)) throw new ValidationException(Messages.BonusStatusDuplicate(Lang), "name");
        var rule = Enum.TryParse<StatusRule>(r.Rule, true, out var ru) ? ru : throw new ValidationException(Messages.BonusStatusRuleInvalid(Lang), "rule");
        if (r.AccrualPercent is < 0 or > 50) throw new ValidationException(Messages.BonusStatusPercentInvalid(Lang), "accrualPercent");
        if (r.ValidityDays is { } v && v is < 1 or > 3650) throw new ValidationException(Messages.BonusStatusValidityInvalid(Lang), "validityDays");
        s.ThresholdAmount = null;
        s.ThresholdCount = null;
        switch (rule)
        {
            case StatusRule.PurchaseAmount:
                if (r.ThresholdAmount is not { } a || a < 0) throw new ValidationException(Messages.BonusStatusThresholdRequired(Lang), "thresholdAmount");
                s.ThresholdAmount = Math.Round(a, 2);
                break;
            case StatusRule.PurchaseCount:
                if (r.ThresholdCount is not { } n || n < 1) throw new ValidationException(Messages.BonusStatusThresholdRequired(Lang), "thresholdCount");
                s.ThresholdCount = n;
                break;
        }
        s.Name = name;
        s.Rule = rule;
        s.Icon = Icons.Contains(r.Icon) ? r.Icon! : "star";
        s.Color = r.Color is { } col && HexColor().IsMatch(col) ? col.ToUpperInvariant() : "#1769FF";
        s.Period = Enum.TryParse<StatusPeriod>(r.Period, true, out var p) ? p : StatusPeriod.AllTime;
        s.AccrualPercent = Math.Round(r.AccrualPercent, 2);
        s.CountReturns = r.CountReturns;
        s.ValidityDays = r.ValidityDays;
        s.AutoDowngrade = r.AutoDowngrade && r.ValidityDays is not null;
        s.Description = CatalogAccess.Optional(r.Description, 1000);
        s.IsActive = r.IsActive;
    }

    private static object Snapshot(LoyaltyStatus s) => new
    {
        s.Name, Rule = s.Rule.ToString(), s.ThresholdAmount, s.ThresholdCount, Period = s.Period.ToString(), s.AccrualPercent, s.CountReturns,
        s.ValidityDays, s.AutoDowngrade, s.IsActive,
    };

    public static BonusStatusDto ToDto(LoyaltyStatus s, IReadOnlyDictionary<Guid, int> usage) => new(
        s.Id, s.Name, s.Icon, s.Color, s.Rule.ToString(), s.ThresholdAmount, s.ThresholdCount, s.Period.ToString(), s.AccrualPercent, s.CountReturns,
        s.ValidityDays, s.AutoDowngrade, s.Description, s.IsActive, s.SortOrder, s.Level.ToString(), usage.GetValueOrDefault(s.Id), s.CreatedAt, s.UpdatedAt);

    // ---------- Клиенттер (§10) ----------

    public async Task<BonusCustomersPageDto> CustomersAsync(string? search, Guid? statusId, bool positive, string? sort, string? dir, int page, int pageSize,
        CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.BonusView, ct);
        var (statuses, _) = await access.ProgramAsync(c, ct);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var (rows, total) = await repo.CustomersAsync(new BonusCustomerFilter(c.Store.Id, search, statusId, positive, sort, dir != "asc", (page - 1) * pageSize, pageSize), ct);
        return new BonusCustomersPageDto(rows.Select(r => mapper.Customer(r, statuses, c.Can.Contacts)).ToList(), total, page, pageSize);
    }

    public async Task<BonusCustomerCardDto> CustomerAsync(Guid customerId, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.BonusView, ct);
        var (statuses, _) = await access.ProgramAsync(c, ct);
        var row = await repo.CustomerAsync(c.Store.Id, customerId, ct) ?? throw new NotFoundException(Messages.BonusCustomerNotFound(Lang));
        var lots = await repo.LotsAsync(row.Card.Id, ct);
        var now = DateTime.UtcNow;
        var next = lots.Where(l => l.Remaining > 0 && l.ExpiresAt > now).OrderBy(l => l.ExpiresAt).FirstOrDefault();
        var (ops, _) = await repo.OperationsAsync(new BonusOperationFilter([c.Store.Id], null, null, null, null, null, null, null, null, null, customerId, null, 0, 50), ct);
        var stats = await repo.PurchaseStatsAsync(c.Store.Id, customerId, null, ct);
        var status = BonusProgram.Current(row.Card, statuses);
        return new BonusCustomerCardDto(mapper.Customer(row, statuses, c.Can.Contacts), status?.AccrualPercent ?? 0, next?.Remaining, next?.ExpiresAt,
            row.Card.StatusReason, row.Card.StatusAssignedAt, row.Card.StatusExpiresAt, new PurchaseStatsDto(stats.Amount(true), stats.Purchases(true), stats.LastAt),
            lots.Select(l => LotDto(l, now)).ToList(), await mapper.OperationsAsync(ops, c.Can.Contacts, ct));
    }

    public static BonusLotDto LotDto(Domain.Entities.BonusTransaction l, DateTime now) => new(
        l.Id, l.Type.ToString(), l.Source, l.Amount, l.Remaining, l.Amount - l.Remaining, l.CreatedAt, l.ExpiresAt,
        l.ReversedAt is not null ? "reversed" : l.Remaining <= 0 ? (l.ExpiresAt is { } e && e <= now ? "expired" : "spent") : "active", l.Comment);

    /// <summary>
    /// Мәртебені өзгерту (§3.4): «manual» — таңдалған мәртебе, себеп міндетті, ережелер енді оны өзгертпейді; «rules» — қолмен берілгенін алып,
    /// шарттар бойынша қайта бағалау (төмендеуі мүмкін). Бонус есептелмейді; аудит ескі/жаңа мәртебемен.
    /// </summary>
    public async Task<BonusCustomerCardDto> ChangeStatusAsync(Guid customerId, ChangeStatusRequest r, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.BonusManage, ct);
        var (statuses, _) = await access.ProgramAsync(c, ct);
        await unitOfWork.InTransactionAsync(async () =>
        {
            var card = await repo.CardForUpdateAsync(c.Store.Id, customerId, ct) ?? throw new NotFoundException(Messages.BonusCustomerNotFound(Lang));
            var before = BonusProgram.Current(card, statuses);
            var now = DateTime.UtcNow;
            if (string.Equals(r.Mode, "rules", StringComparison.OrdinalIgnoreCase))
            {
                var reason = CatalogAccess.Optional(r.Reason, 500);
                card.StatusManual = false;
                card.StatusReason = null;
                var registration = BonusProgram.Registration(statuses);
                if (registration is not null) BonusProgram.Assign(card, registration, now, false, null);
                await program.EvaluateAsync(card, c.Store, now, true, 0, 0, ct);
                access.Audit(c, "bonus.customer.status.rules", "bonus_card", card.Id, new { Status = before?.Name }, new { Status = BonusProgram.Current(card, statuses)?.Name, Reason = reason });
            }
            else
            {
                var reason = access.Reason(r.Reason);
                var target = statuses.FirstOrDefault(s => s.Id == r.StatusId && s.IsActive) ?? throw new ValidationException(Messages.BonusStatusManualOnly(Lang), "statusId");
                BonusProgram.Assign(card, target, now, true, reason);
                access.Audit(c, "bonus.customer.status.manual", "bonus_card", card.Id, new { Status = before?.Name }, new { Status = target.Name, Reason = reason });
            }
            await unitOfWork.SaveChangesAsync(ct);
            return card;
        }, ct);
        return await CustomerAsync(customerId, ct);
    }

    /// <summary>Клиент дүкеннің таратуларынан бас тартты (§6.3, §16): таратуда «согласия нет» деп алынып тасталады.</summary>
    public async Task<BonusCustomerCardDto> SetOptOutAsync(Guid customerId, OptOutRequest r, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.BonusCampaigns, ct);
        await unitOfWork.InTransactionAsync(async () =>
        {
            var card = await repo.CardForUpdateAsync(c.Store.Id, customerId, ct) ?? throw new NotFoundException(Messages.BonusCustomerNotFound(Lang));
            card.NotificationsOptOut = r.OptOut;
            access.Audit(c, r.OptOut ? "bonus.customer.optout" : "bonus.customer.optin", "bonus_card", card.Id, null, new { Reason = CatalogAccess.Optional(r.Reason, 500) });
            await unitOfWork.SaveChangesAsync(ct);
            return card;
        }, ct);
        return await CustomerAsync(customerId, ct);
    }
}
