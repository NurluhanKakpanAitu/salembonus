using SalemBonus.Application.Common;
using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Application.Pos.Catalog;
using SalemBonus.Domain.Core;
using SalemBonus.Domain.Enums;
using SalemBonus.Domain.Pos.Bonus;

namespace SalemBonus.Application.Pos.Bonus;

/// <summary>
/// «Сгорание бонусов» (ТЗ «Бонусы» §4) және «Бонусы на день рождения» (§5): баптаулар (тек «Сохранить»-дан кейін, сервер тексереді),
/// жанатын партиялар реестрі, партия мерзімін қолмен өзгерту, жақын туған күндер, сыйлықты алдын ала қарау және қолмен есептеу.
/// </summary>
public class BonusSettingsService(
    IBonusRepository repo,
    BonusAccess access,
    BonusProgram program,
    BonusBirthdays birthdays,
    IUnitOfWork unitOfWork)
{
    private const int MaxPageSize = 100;
    private AppLanguage Lang => access.Lang;

    /// <summary>Жанбайтын етуге болатын түрлер (§4.1 «Исключения»).</summary>
    private static readonly string[] ExcludableTypes = [nameof(BonusTransactionType.Adjustment), nameof(BonusTransactionType.ReturnRestore), nameof(BonusTransactionType.Reversal)];
    private static readonly BonusTransactionType[] GeneralLotTypes =
        [BonusTransactionType.Accrual, BonusTransactionType.ReturnRestore, BonusTransactionType.Adjustment, BonusTransactionType.Reversal];

    // ---------- Жану ----------

    public async Task<ExpirySettingsDto> ExpiryAsync(CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.BonusView, ct);
        var (_, s) = await access.ProgramAsync(c, ct);
        var lots = await repo.OpenLotsOfStoreAsync(c.Store.Id, ct);
        return new ExpirySettingsDto(s.ExpiryEnabled, s.ExpiryValue, s.ExpiryUnit.ToString(), s.ExpiryStatusIds, s.ExpiryExcludedTypes, s.WarnEnabled,
            s.WarnDays, s.WarnChannel, lots.Count, lots.Count(l => l.ExpiresAt is null), s.UpdatedAt);
    }

    /// <summary>
    /// Сақтау (§4.1): өшірілсе — автоматты жану орындалмайды. <c>ApplyToExisting</c> — ашық жалпы партиялардың мерзімі жаңа ережемен
    /// (есептелген күнінен) қайта есептеледі; әйтпесе тек жаңа партияларға. Сыйлық партиялары (туған күн, тарату) өз мерзімімен қалады.
    /// </summary>
    public async Task<ExpirySettingsDto> SaveExpiryAsync(SaveExpirySettingsRequest r, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.BonusManage, ct);
        var (statuses, s) = await access.ProgramAsync(c, ct);
        var unit = Enum.TryParse<ExpiryUnit>(r.Unit, true, out var u) ? u : ExpiryUnit.Months;
        if (r.Value < 1 || (unit == ExpiryUnit.Months ? r.Value > 120 : r.Value > 3650)) throw new ValidationException(Messages.BonusSettingsValueInvalid(Lang), "value");
        var warnDays = (r.WarnDays ?? []).Distinct().OrderByDescending(d => d).ToList();
        if (r.WarnEnabled && (warnDays.Count == 0 || warnDays.Any(d => d is < 1 or > 90))) throw new ValidationException(Messages.BonusWarnDaysInvalid(Lang), "warnDays");
        if (r.WarnEnabled && !string.IsNullOrEmpty(r.WarnChannel) && r.WarnChannel != "app") throw new ValidationException(Messages.BonusChannelUnavailable(Lang), "warnChannel");
        var before = new { s.ExpiryEnabled, s.ExpiryValue, Unit = s.ExpiryUnit.ToString(), s.ExpiryStatusIds, s.ExpiryExcludedTypes, s.WarnEnabled, s.WarnDays };
        s.ExpiryEnabled = r.Enabled;
        s.ExpiryValue = r.Value;
        s.ExpiryUnit = unit;
        s.ExpiryStatusIds = (r.StatusIds ?? []).Where(id => statuses.Any(x => x.Id == id)).Distinct().ToList();
        s.ExpiryExcludedTypes = (r.ExcludedTypes ?? []).Where(ExcludableTypes.Contains).Distinct().ToList();
        s.WarnEnabled = r.WarnEnabled;
        s.WarnDays = warnDays.Count > 0 ? warnDays : [7, 1];
        s.WarnChannel = "app";
        s.UpdatedAt = DateTime.UtcNow;
        s.UpdatedBy = access.StaffUserId;
        // Ескі SalemBonus жолдары (қосымша, терминал) үшін шамамен күнмен.
        c.Store.BonusLifetimeDays = r.Enabled ? (unit == ExpiryUnit.Months ? r.Value * 30 : r.Value) : null;

        var recalculated = 0;
        if (r.ApplyToExisting)
        {
            var cards = (await repo.BalancesAsync([c.Store.Id], ct)).ToDictionary(x => x.CardId, x => x.StatusId);
            foreach (var lot in await repo.OpenLotsOfStoreAsync(c.Store.Id, ct))
            {
                if (!GeneralLotTypes.Contains(lot.Type)) continue;
                var card = new Domain.Entities.BonusCard { Id = lot.BonusCardId, StatusId = cards.GetValueOrDefault(lot.BonusCardId) };
                var expires = BonusProgram.LotExpiresAt(s, c.Store, card, lot.Type, lot.CreatedAt);
                if (expires == lot.ExpiresAt) continue;
                lot.ExpiresAt = expires;
                recalculated++;
            }
        }
        access.Audit(c, "bonus.expiry.settings", "bonus_settings", c.Store.Id, before,
            new { s.ExpiryEnabled, s.ExpiryValue, Unit = unit.ToString(), s.ExpiryStatusIds, s.ExpiryExcludedTypes, s.WarnEnabled, s.WarnDays, r.ApplyToExisting, Recalculated = recalculated });
        await unitOfWork.SaveChangesAsync(ct);
        program.Forget(c.Store.Id);
        return await ExpiryAsync(ct);
    }

    /// <summary>«Скоро сгорят» (§4.2): мерзімі кезеңге түсетін, қалдығы бар партиялар; ескерту күйі — баптау мен жіберілгені бойынша.</summary>
    public async Task<ExpiringPageDto> ExpiringAsync(int days, DateOnly? from, DateOnly? to, string? search, Guid? statusId, string? storeIds, int page, int pageSize,
        CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.BonusView, ct, storeIds);
        var (statuses, s) = await access.ProgramAsync(c, ct);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var now = DateTime.UtcNow;
        var fromUtc = from is { } f ? Sales.StoreTime.StartOfDayUtc(f, c.Zone) : now;
        var toUtc = to is { } t ? Sales.StoreTime.StartOfDayUtc(t.AddDays(1), c.Zone) : now.AddDays(Math.Clamp(days <= 0 ? 30 : days, 1, 366));
        var (rows, total, sum) = await repo.ExpiringLotsAsync(c.StoreIds, fromUtc, toUtc, search, statusId, (page - 1) * pageSize, pageSize, ct);
        var warned = await repo.WarningsAsync(rows.Select(r => r.Lot.Id).ToList(), ct);
        var items = rows.Select(r => ExpiringDto(r, statuses, s, warned, c.Can.Contacts)).ToList();
        return new ExpiringPageDto(items, total, page, pageSize, sum, s.ExpiryEnabled);
    }

    public ExpiringLotDto ExpiringDto(BonusLotRow r, IReadOnlyList<LoyaltyStatus> statuses, BonusSettings s, HashSet<(Guid, int)> warned, bool contacts)
    {
        var (name, color) = BonusAccess.StatusOf(r.StatusId, CustomerLevel.New, statuses, Lang);
        var notification = !s.WarnEnabled ? "none" : warned.Any(w => w.Item1 == r.Lot.Id) ? "sent" : "scheduled";
        var general = GeneralLotTypes.Contains(r.Lot.Type);
        var state = general && !s.ExpiryEnabled ? "disabled" : "pending";
        return new ExpiringLotDto(r.Lot.Id, r.Lot.ExpiresAt!.Value, r.CustomerId, BonusAccess.FullName(r.FirstName, r.LastName), BonusAccess.Phone(r.Phone, contacts),
            name, color, r.Lot.Remaining, r.Lot.Amount, r.Lot.Type.ToString(), r.Lot.CreatedAt, notification, state);
    }

    /// <summary>Партия мерзімін қолмен өзгерту (§4.3): арнайы құқық пен себеп, аудитке ескі/жаңа күн.</summary>
    public async Task SetLotExpiryAsync(Guid lotId, SetLotExpiryRequest r, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.BonusManage, ct);
        var reason = access.Reason(r.Reason);
        var lot = await repo.LotForUpdateAsync(lotId, ct) ?? throw new NotFoundException(Messages.BonusLotNotFound(Lang));
        var card = await repo.CardByIdForUpdateAsync(lot.BonusCardId, ct);
        if (card is null || card.StoreId != c.Store.Id || lot.Amount <= 0) throw new NotFoundException(Messages.BonusLotNotFound(Lang));
        if (r.ExpiresAt is { } at && at.ToUniversalTime() <= DateTime.UtcNow) throw new ValidationException(Messages.BonusLotDateInvalid(Lang), "expiresAt");
        var before = lot.ExpiresAt;
        lot.ExpiresAt = r.ExpiresAt is { } d ? DateTime.SpecifyKind(d.ToUniversalTime(), DateTimeKind.Utc) : null;
        access.Audit(c, "bonus.lot.expiry", "bonus_lot", lot.Id, new { ExpiresAt = before }, new { lot.ExpiresAt, Reason = reason });
        await unitOfWork.SaveChangesAsync(ct);
    }

    // ---------- Туған күн ----------

    public async Task<BirthdaySettingsDto> BirthdayAsync(CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.BonusView, ct);
        var (_, s) = await access.ProgramAsync(c, ct);
        return await BirthdayDtoAsync(c, s, ct);
    }

    private async Task<BirthdaySettingsDto> BirthdayDtoAsync(BonusCtx c, BonusSettings s, CancellationToken ct)
    {
        var excluded = new List<BonusCustomerRefDto>();
        foreach (var id in s.BirthdayExcludedCustomerIds)
            if (await repo.CustomerAsync(c.Store.Id, id, ct) is { } row)
                excluded.Add(new BonusCustomerRefDto(id, BonusAccess.FullName(row.FirstName, row.LastName), BonusAccess.Phone(row.Phone, c.Can.Contacts)));
        return new BirthdaySettingsDto(s.BirthdayEnabled, s.BirthdayAmount, s.BirthdayTiming.ToString(), s.BirthdayTimingDays, s.BirthdayTime.ToString("HH:mm"),
            s.BirthdayValidity.ToString(), s.BirthdayValidityDays, s.BirthdayValidityDate, s.BirthdayStatusIds, s.BirthdayMinPurchaseAmount, s.BirthdayMinPurchaseCount,
            s.BirthdayPurchasePeriodDays, excluded, string.IsNullOrWhiteSpace(s.BirthdayMessage) ? BonusProgram.DefaultBirthdayMessage(Lang) : s.BirthdayMessage, s.UpdatedAt);
    }

    /// <summary>
    /// Ережені сақтау (§5.1, §5.2): қосу тек дұрыс бапталғанда (сома &gt; 0); «До дня рождения» тек туған күннен бұрын есептегенде;
    /// нақты күн болашақта. Бір клиентке жылына бір сыйлық — өңдеу жолымен қорғалған.
    /// </summary>
    public async Task<BirthdaySettingsDto> SaveBirthdayAsync(SaveBirthdaySettingsRequest r, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.BonusManage, ct);
        var (statuses, s) = await access.ProgramAsync(c, ct);
        var timing = Enum.TryParse<BirthdayTiming>(r.Timing, true, out var tm) ? tm : BirthdayTiming.OnDay;
        var validity = Enum.TryParse<BirthdayValidity>(r.Validity, true, out var v) ? v : BirthdayValidity.DaysAfterAccrual;
        if (r.Enabled && r.Amount is < 1 or > 1_000_000) throw new ValidationException(Messages.BonusBirthdayAmountInvalid(Lang), "amount");
        if (r.Amount is < 0 or > 1_000_000) throw new ValidationException(Messages.BonusBirthdayAmountInvalid(Lang), "amount");
        if (timing != BirthdayTiming.OnDay && r.TimingDays is < 1 or > 30) throw new ValidationException(Messages.BonusBirthdayTimingInvalid(Lang), "timingDays");
        if (validity == BirthdayValidity.UntilBirthday && timing != BirthdayTiming.DaysBefore)
            throw new ValidationException(Messages.BonusBirthdayValidityIncompatible(Lang), "validity");
        if (validity is BirthdayValidity.DaysAfterAccrual or BirthdayValidity.DaysAfterBirthday && r.ValidityDays is not (>= 1 and <= 365))
            throw new ValidationException(Messages.BonusBirthdayValidityInvalid(Lang), "validityDays");
        if (validity == BirthdayValidity.FixedDate && (r.ValidityDate is not { } vd || vd <= c.Today))
            throw new ValidationException(Messages.BonusBirthdayValidityInvalid(Lang), "validityDate");
        if (!TimeOnly.TryParse(r.Time ?? "09:00", out var time)) throw new ValidationException(Messages.BonusBirthdayValidityInvalid(Lang), "time");
        if ((r.Message ?? string.Empty).Length > 1000) throw new ValidationException(Messages.BonusBirthdayMessageInvalid(Lang), "message");
        var before = new { s.BirthdayEnabled, s.BirthdayAmount, Timing = s.BirthdayTiming.ToString(), s.BirthdayTimingDays, Validity = s.BirthdayValidity.ToString() };
        s.BirthdayEnabled = r.Enabled;
        s.BirthdayAmount = r.Amount;
        s.BirthdayTiming = timing;
        s.BirthdayTimingDays = timing == BirthdayTiming.OnDay ? 0 : r.TimingDays;
        s.BirthdayTime = new TimeOnly(time.Hour, time.Minute);
        s.BirthdayValidity = validity;
        s.BirthdayValidityDays = validity is BirthdayValidity.DaysAfterAccrual or BirthdayValidity.DaysAfterBirthday ? r.ValidityDays : null;
        s.BirthdayValidityDate = validity == BirthdayValidity.FixedDate ? r.ValidityDate : null;
        s.BirthdayStatusIds = (r.StatusIds ?? []).Where(id => statuses.Any(x => x.Id == id)).Distinct().ToList();
        s.BirthdayMinPurchaseAmount = r.MinPurchaseAmount is > 0 ? Math.Round(r.MinPurchaseAmount.Value, 2) : null;
        s.BirthdayMinPurchaseCount = r.MinPurchaseCount is > 0 ? r.MinPurchaseCount : null;
        s.BirthdayPurchasePeriodDays = Math.Clamp(r.PurchasePeriodDays <= 0 ? 365 : r.PurchasePeriodDays, 1, 3650);
        s.BirthdayExcludedCustomerIds = (r.ExcludedCustomerIds ?? []).Distinct().Take(1000).ToList();
        s.BirthdayMessage = CatalogAccess.Optional(r.Message, 1000) ?? BonusProgram.DefaultBirthdayMessage(Lang);
        s.UpdatedAt = DateTime.UtcNow;
        s.UpdatedBy = access.StaffUserId;
        access.Audit(c, "bonus.birthday.settings", "bonus_settings", c.Store.Id, before,
            new { s.BirthdayEnabled, s.BirthdayAmount, Timing = timing.ToString(), s.BirthdayTimingDays, Validity = validity.ToString(), s.BirthdayStatusIds, s.BirthdayMinPurchaseAmount, s.BirthdayMinPurchaseCount });
        await unitOfWork.SaveChangesAsync(ct);
        program.Forget(c.Store.Id);
        return await BirthdayDtoAsync(c, s, ct);
    }

    /// <summary>Жақын туған күндер (§5.3): күн, клиент, мәртебе, шарттарға сәйкестігі, сома, жоспарланған есептеу күні, күйі.</summary>
    public async Task<BirthdaysPageDto> UpcomingBirthdaysAsync(int days, Guid? statusId, string? state, string? search, int page, int pageSize, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.BonusView, ct);
        var (statuses, s) = await access.ProgramAsync(c, ct);
        var all = await UpcomingAsync(c, statuses, s, Math.Clamp(days <= 0 ? 30 : days, 1, 366), ct);
        var rows = all.Items;
        if (statusId is { } sid) rows = rows.Where(r => r.Status == sid).ToList();
        if (!string.IsNullOrWhiteSpace(state)) rows = rows.Where(r => r.Dto.State == state).ToList();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim();
            var phone = PhoneSearch.Parse(q);
            rows = rows.Where(r => r.Dto.CustomerName.Contains(q, StringComparison.CurrentCultureIgnoreCase) || (phone?.Matches(r.RawPhone) ?? false)).ToList();
        }
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        return new BirthdaysPageDto(rows.Skip((page - 1) * pageSize).Take(pageSize).Select(r => r.Dto).ToList(), rows.Count, page, pageSize,
            BonusBirthdays.Enabled(s), all.WithoutBirthDate);
    }

    public record UpcomingRow(UpcomingBirthdayDto Dto, Guid? Status, string RawPhone);

    public async Task<(List<UpcomingRow> Items, int WithoutBirthDate)> UpcomingAsync(BonusCtx c, IReadOnlyList<LoyaltyStatus> statuses, BonusSettings s, int days, CancellationToken ct)
    {
        var customers = await repo.AllCustomersAsync(c.Store.Id, ct);
        var today = c.Today;
        var withDate = new List<(BonusCustomerRow Row, DateOnly Birthday)>();
        foreach (var r in customers)
        {
            if (r.BirthDate is not { } b) continue;
            var occ = BonusBirthdays.Occurrence(b, today.Year);
            if (occ < today) occ = BonusBirthdays.Occurrence(b, today.Year + 1);
            if (occ.DayNumber - today.DayNumber <= days) withDate.Add((r, occ));
        }
        var now = DateTime.UtcNow;
        var stats = await repo.PurchaseStatsBulkAsync(c.Store.Id, withDate.Select(x => x.Row.Card.CustomerId).ToList(), birthdays.PurchasesFrom(s, now), ct);
        var grants = (await repo.GrantsAsync(c.Store.Id, withDate.Select(x => x.Birthday.Year).Distinct().ToList(), ct))
            .ToDictionary(g => (g.CustomerId, g.Year));
        var enabled = BonusBirthdays.Enabled(s);
        var items = withDate.OrderBy(x => x.Birthday).ThenBy(x => x.Row.FirstName).Select(x =>
        {
            var (name, color) = BonusAccess.StatusOf(x.Row.Card.StatusId, x.Row.Card.Level, statuses, Lang);
            var reason = BonusBirthdays.Check(s, x.Row.Card, stats.GetValueOrDefault(x.Row.Card.CustomerId) ?? PurchaseStats.Empty);
            var grant = grants.GetValueOrDefault((x.Row.Card.CustomerId, x.Birthday.Year));
            var state = grant is { Status: BirthdayGrantStatus.Granted } ? "granted" : grant is { } g ? g.Reason ?? "skipped" : !enabled ? "disabled" : reason ?? "pending";
            var dto = new UpcomingBirthdayDto(x.Birthday, x.Birthday.DayNumber - today.DayNumber, x.Row.Card.CustomerId, BonusAccess.FullName(x.Row.FirstName, x.Row.LastName),
                BonusAccess.Phone(x.Row.Phone, c.Can.Contacts), name, color, enabled && reason is null, enabled && reason is null ? s.BirthdayAmount : 0,
                BonusBirthdays.PlannedDate(s, x.Birthday), state, grant?.TransactionId);
            return new UpcomingRow(dto, x.Row.Card.StatusId, x.Row.Phone);
        }).ToList();
        return (items, customers.Count(r => r.BirthDate is null));
    }

    /// <summary>Алдын ала қарау (§5.1): тест клиентінде сома, күн, мерзім, хабарлама. Ештеңе өзгермейді.</summary>
    public async Task<BirthdayPreviewDto> PreviewAsync(Guid? customerId, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.BonusView, ct);
        var (_, s) = await access.ProgramAsync(c, ct);
        BonusCustomerRow? row = customerId is { } id ? await repo.CustomerAsync(c.Store.Id, id, ct) : (await repo.AllCustomersAsync(c.Store.Id, ct)).FirstOrDefault(r => r.BirthDate is not null);
        var birth = row?.BirthDate ?? c.Today;
        var occ = BonusBirthdays.Occurrence(birth, c.Today.Year);
        if (BonusBirthdays.PlannedDate(s, occ) < c.Today) occ = BonusBirthdays.Occurrence(birth, c.Today.Year + 1);
        var planned = BonusBirthdays.PlannedDate(s, occ);
        var accrual = BonusBirthdays.AccrualUtc(s, planned, c.Zone);
        var expires = BonusBirthdays.ExpiresAt(s, occ, accrual, c.Zone);
        var first = row?.FirstName ?? (Lang == AppLanguage.Ru ? "Айгерим" : "Айгерім");
        var full = row is null ? first : BonusAccess.FullName(row.FirstName, row.LastName);
        var message = BonusTemplates.Render(string.IsNullOrWhiteSpace(s.BirthdayMessage) ? BonusProgram.DefaultBirthdayMessage(Lang) : s.BirthdayMessage, first, full,
            s.BirthdayAmount, expires is { } e ? TimeZoneInfo.ConvertTimeFromUtc(e, c.Zone) : null, birth, c.Store.Name, Lang);
        return new BirthdayPreviewDto(full, message, s.BirthdayAmount, planned, accrual, expires);
    }

    /// <summary>Қолмен есептеу (§5.3): арнайы құқық, себеп, растау; жылына бір сыйлық ережесі сақталады.</summary>
    public async Task<UpcomingBirthdayDto?> GrantManualAsync(ManualBirthdayRequest r, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.BonusAccrue, ct);
        var (statuses, s) = await access.ProgramAsync(c, ct);
        if (s.BirthdayAmount <= 0) throw new ValidationException(Messages.BonusBirthdayDisabled(Lang), "amount");
        var reason = access.Reason(r.Reason);
        var row = await repo.CustomerAsync(c.Store.Id, r.CustomerId, ct) ?? throw new NotFoundException(Messages.BonusCustomerNotFound(Lang));
        if (row.BirthDate is not { } birth) throw new ValidationException(Messages.BonusBirthdayNoBirthDate(Lang), "customerId");
        var occ = BonusBirthdays.Occurrence(birth, c.Today.Year);
        if (occ.DayNumber < c.Today.DayNumber - 31) occ = BonusBirthdays.Occurrence(birth, c.Today.Year + 1);
        var grant = await unitOfWork.InTransactionAsync(async () =>
        {
            var g = await birthdays.ProcessAsync(c.Store, s, r.CustomerId, row.FirstName, BonusAccess.FullName(row.FirstName, row.LastName), birth, occ.Year,
                DateTime.UtcNow, true, reason, access.StaffUserId, ct);
            access.Audit(c, "bonus.birthday.manual", "bonus_birthday", g.Id, null, new { r.CustomerId, Year = occ.Year, s.BirthdayAmount, Reason = reason });
            await unitOfWork.SaveChangesAsync(ct);
            return g;
        }, ct);
        var (items, _) = await UpcomingAsync(c, statuses, s, 366, ct);
        return items.FirstOrDefault(i => i.Dto.CustomerId == r.CustomerId)?.Dto
               ?? new UpcomingBirthdayDto(occ, occ.DayNumber - c.Today.DayNumber, r.CustomerId, BonusAccess.FullName(row.FirstName, row.LastName), null, "", "", true,
                   s.BirthdayAmount, occ, "granted", grant.TransactionId);
    }
}
