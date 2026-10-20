using SalemBonus.Application.BonusCards;
using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Domain.Core;
using SalemBonus.Domain.Entities;
using SalemBonus.Domain.Enums;
using SalemBonus.Domain.Pos.Bonus;

namespace SalemBonus.Application.Pos.Bonus;

/// <summary>
/// «Рассылка бонусов» (ТЗ «Бонусы» §6): черновик → (тест) → жоспарлау не жіберу → есеп. Арна — SalemBonus қосымшасының хабарламалары
/// (басқа интеграциялар қосылмаған — таңдалмайды). Аудитория: барлық сәйкес клиенттер не қолмен таңдалғандар; таратудан бас тартқандар
/// алынып тасталады, себебі жазылады. Жіберу бір рет: тарату жолы бұғатталады, алушы бірегей, бонус алушы кілтімен қайталанбайды.
/// </summary>
public class BonusCampaignService(
    IBonusRepository repo,
    IBonusTransactionRepository transactions,
    BonusAccess access,
    BonusLedger ledger,
    IStoreRepository stores,
    IUnitOfWork unitOfWork)
{
    private const int MaxPageSize = 100;
    private AppLanguage Lang => access.Lang;
    private bool Ru => Lang == AppLanguage.Ru;

    public async Task<CampaignsPageDto> ListAsync(string? status, string? search, int page, int pageSize, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.BonusView, ct);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var (items, total) = await repo.CampaignsAsync(c.Store.Id, status, search, (page - 1) * pageSize, pageSize, ct);
        var authors = await repo.StaffNamesAsync(items.Select(i => i.CreatedBy).Distinct().ToList(), ct);
        var list = new List<CampaignDto>();
        foreach (var i in items) list.Add(ToDto(i, authors, await repo.ReadCountAsync(i.Id, ct)));
        return new CampaignsPageDto(list, total, page, pageSize);
    }

    public async Task<CampaignDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.BonusView, ct);
        var campaign = await repo.CampaignAsync(c.Store.Id, id, ct) ?? throw new NotFoundException(Messages.BonusCampaignNotFound(Lang));
        return ToDto(campaign, await repo.StaffNamesAsync([campaign.CreatedBy], ct), await repo.ReadCountAsync(id, ct));
    }

    public async Task<CampaignDto> CreateAsync(SaveCampaignRequest r, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.BonusCampaigns, ct);
        await access.ProgramAsync(c, ct);
        var campaign = new BonusCampaign { Id = Guid.NewGuid(), StoreId = c.Store.Id, CreatedBy = access.StaffUserId };
        Fill(campaign, r);
        repo.AddCampaign(campaign);
        access.Audit(c, "bonus.campaign.create", "bonus_campaign", campaign.Id, null, new { campaign.Name, Type = campaign.Type.ToString() });
        await unitOfWork.SaveChangesAsync(ct);
        return await GetAsync(campaign.Id, ct);
    }

    public async Task<CampaignDto> UpdateAsync(Guid id, SaveCampaignRequest r, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.BonusCampaigns, ct);
        var campaign = await repo.CampaignAsync(c.Store.Id, id, ct) ?? throw new NotFoundException(Messages.BonusCampaignNotFound(Lang));
        if (campaign.Status is not (CampaignStatus.Draft or CampaignStatus.Scheduled)) throw new ValidationException(Messages.BonusCampaignNotEditable(Lang));
        Fill(campaign, r);
        if (campaign.Status == CampaignStatus.Scheduled && (campaign.ScheduledAt is not { } at || at <= DateTime.UtcNow.AddMinutes(1)))
            throw new ValidationException(Messages.BonusCampaignPastDate(Lang), "scheduledAt");
        campaign.UpdatedAt = DateTime.UtcNow;
        access.Audit(c, "bonus.campaign.update", "bonus_campaign", campaign.Id, null, new { campaign.Name });
        await unitOfWork.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<CampaignDto> DuplicateAsync(Guid id, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.BonusCampaigns, ct);
        var src = await repo.CampaignAsync(c.Store.Id, id, ct) ?? throw new NotFoundException(Messages.BonusCampaignNotFound(Lang));
        var suffix = Ru ? " (копия)" : " (көшірме)";
        var copy = new BonusCampaign
        {
            Id = Guid.NewGuid(), StoreId = c.Store.Id, CreatedBy = access.StaffUserId, Name = (src.Name.Length + suffix.Length > 120 ? src.Name[..(120 - suffix.Length)] : src.Name) + suffix,
            Type = src.Type, Channel = src.Channel, AudienceMode = src.AudienceMode, Filter = src.Filter, CustomerIds = [.. src.CustomerIds], Message = src.Message,
            BonusAmount = src.BonusAmount, BonusValidityDays = src.BonusValidityDays,
        };
        repo.AddCampaign(copy);
        access.Audit(c, "bonus.campaign.duplicate", "bonus_campaign", copy.Id, new { From = src.Id }, new { copy.Name });
        await unitOfWork.SaveChangesAsync(ct);
        return await GetAsync(copy.Id, ct);
    }

    private void Fill(BonusCampaign campaign, SaveCampaignRequest r)
    {
        var name = (r.Name ?? string.Empty).Trim();
        if (name.Length is < 2 or > 120) throw new ValidationException(Messages.BonusCampaignNameRequired(Lang), "name");
        var message = (r.Message ?? string.Empty).Trim();
        if (message.Length is < 1 or > 1000) throw new ValidationException(Messages.BonusCampaignMessageRequired(Lang), "message");
        if (!string.IsNullOrEmpty(r.Channel) && r.Channel != "app") throw new ValidationException(Messages.BonusChannelUnavailable(Lang), "channel");
        if (r.BonusAmount is { } amount && (amount is < 1 or > 100_000 || r.BonusValidityDays is { } d && d is < 1 or > 365))
            throw new ValidationException(Messages.BonusCampaignBonusInvalid(Lang), "bonusAmount");
        campaign.Name = name;
        campaign.Message = message;
        campaign.Type = Enum.TryParse<CampaignType>(r.Type, true, out var t) ? t : CampaignType.Custom;
        campaign.Channel = "app";
        campaign.AudienceMode = Enum.TryParse<AudienceMode>(r.AudienceMode, true, out var m) ? m : AudienceMode.All;
        campaign.Filter = r.Filter ?? new AudienceFilter();
        campaign.CustomerIds = (r.CustomerIds ?? []).Distinct().Take(10_000).ToList();
        campaign.BonusAmount = r.BonusAmount is > 0 ? r.BonusAmount : null;
        campaign.BonusValidityDays = campaign.BonusAmount is null ? null : r.BonusValidityDays;
        if (r.ScheduledAt is { } at) campaign.ScheduledAt = DateTime.SpecifyKind(at.ToUniversalTime(), DateTimeKind.Utc);
    }

    // ---------- Аудитория (§6.3) ----------

    private record Candidate(BonusCustomerRow Row, string? Exclusion);

    private async Task<List<Candidate>> AudienceAsync(Guid storeId, AudienceMode mode, AudienceFilter f, IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        var rows = await repo.AllCustomersAsync(storeId, ct);
        IEnumerable<BonusCustomerRow> q = rows;
        var now = DateTime.UtcNow;
        if (mode == AudienceMode.Manual) q = q.Where(r => ids.Contains(r.Card.CustomerId));
        else
        {
            var statuses = await repo.StatusesAsync(storeId, ct);
            if (f.StatusIds.Count > 0) q = q.Where(r => BonusProgram.Current(r.Card, statuses) is { } s && f.StatusIds.Contains(s.Id));
            if (f.PositiveBalance) q = q.Where(r => r.Card.Balance > 0);
            if (f.BirthdayMonth is { } month) q = q.Where(r => r.BirthDate?.Month == month);
            if (f.RegisteredFrom is { } from) q = q.Where(r => DateOnly.FromDateTime(r.RegisteredAt) >= from);
            if (f.RegisteredTo is { } to) q = q.Where(r => DateOnly.FromDateTime(r.RegisteredAt) <= to);
            if (f.ExpiringWithinDays is { } days)
            {
                var (lots, _, _) = await repo.ExpiringLotsAsync([storeId], now, now.AddDays(days), null, null, 0, 100_000, ct);
                var expiring = lots.Select(l => l.CustomerId).ToHashSet();
                q = q.Where(r => expiring.Contains(r.Card.CustomerId));
            }
            if (f.MinPurchaseAmount is not null || f.MinPurchaseCount is not null || f.InactiveDays is not null)
            {
                var list = q.ToList();
                var stats = await repo.PurchaseStatsBulkAsync(storeId, list.Select(r => r.Card.CustomerId).ToList(), null, ct);
                PurchaseStats S(BonusCustomerRow r) => stats.GetValueOrDefault(r.Card.CustomerId) ?? PurchaseStats.Empty;
                if (f.MinPurchaseAmount is { } min) list = list.Where(r => Math.Max(S(r).Amount(true), r.Card.TotalSpent) >= min).ToList();
                if (f.MinPurchaseCount is { } cnt) list = list.Where(r => S(r).Purchases(true) >= cnt).ToList();
                if (f.InactiveDays is { } inactive) list = list.Where(r => S(r).LastAt is not { } last || last <= now.AddDays(-inactive)).ToList();
                q = list;
            }
        }
        return q.Select(r => new Candidate(r, r.Card.NotificationsOptOut ? "optOut" : string.IsNullOrWhiteSpace(r.Phone) ? "noContact" : null)).ToList();
    }

    public async Task<AudiencePreviewDto> PreviewAsync(SaveCampaignRequest r, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.BonusCampaigns, ct);
        await access.ProgramAsync(c, ct);
        var mode = Enum.TryParse<AudienceMode>(r.AudienceMode, true, out var m) ? m : AudienceMode.All;
        var list = await AudienceAsync(c.Store.Id, mode, r.Filter ?? new AudienceFilter(), (r.CustomerIds ?? []).ToList(), ct);
        var eligible = list.Where(x => x.Exclusion is null).ToList();
        return new AudiencePreviewDto(list.Count, eligible.Count, list.Count - eligible.Count,
            list.Where(x => x.Exclusion is not null).GroupBy(x => x.Exclusion!).Select(g => new ExclusionDto(g.Key, g.Count())).ToList(),
            eligible.Take(5).Select(x => new BonusCustomerRefDto(x.Row.Card.CustomerId, BonusAccess.FullName(x.Row.FirstName, x.Row.LastName), BonusAccess.Phone(x.Row.Phone, c.Can.Contacts))).ToList(),
            (r.BonusAmount ?? 0) * eligible.Count);
    }

    // ---------- Тест, жоспарлау, жіберу, тоқтату ----------

    /// <summary>«Отправить тест» (§6.4): таңдалған клиентке «[Тест]» белгісімен хабарлама; бонус балансы өзгермейді.</summary>
    public async Task<TestSendResultDto> TestAsync(Guid id, TestSendRequest r, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.BonusCampaigns, ct);
        var campaign = await repo.CampaignAsync(c.Store.Id, id, ct) ?? throw new NotFoundException(Messages.BonusCampaignNotFound(Lang));
        BonusCustomerRow? row = null;
        if (r.CustomerId is { } cid) row = await repo.CustomerAsync(c.Store.Id, cid, ct);
        else if (!string.IsNullOrWhiteSpace(r.Phone))
        {
            var digits = new string(r.Phone.Where(char.IsDigit).ToArray());
            row = (await repo.AllCustomersAsync(c.Store.Id, ct)).FirstOrDefault(x => digits.Length >= 10 && new string(x.Phone.Where(char.IsDigit).ToArray()).EndsWith(digits[^10..]));
        }
        if (row is null) throw new ValidationException(Messages.BonusTestCustomerNotFound(Lang), "phone");
        var text = Render(r.Message ?? campaign.Message, row, campaign.BonusAmount ?? row.Card.Balance, null, c.Store.Name, c.Zone);
        ledger.Notify(row.Card.CustomerId, c.Store, NotificationType.Promo, $"[Тест] {campaign.Name}", text, null, DateTime.UtcNow);
        access.Audit(c, "bonus.campaign.test", "bonus_campaign", campaign.Id, null, new { Customer = row.Card.CustomerId });
        await unitOfWork.SaveChangesAsync(ct);
        return new TestSendResultDto(BonusAccess.FullName(row.FirstName, row.LastName), text, true);
    }

    /// <summary>Жоспарлау (§6.5): уақыт болашақта (өткен уақыт — тыйым, тест 9), дүкеннің белдеуімен көрсетіледі.</summary>
    public async Task<CampaignDto> ScheduleAsync(Guid id, ScheduleCampaignRequest r, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.BonusCampaigns, ct);
        var campaign = await repo.CampaignAsync(c.Store.Id, id, ct) ?? throw new NotFoundException(Messages.BonusCampaignNotFound(Lang));
        if (campaign.Status is not (CampaignStatus.Draft or CampaignStatus.Scheduled)) throw new ValidationException(Messages.BonusCampaignNotEditable(Lang));
        if (r.ScheduledAt is not { } at || at.ToUniversalTime() <= DateTime.UtcNow.AddMinutes(1)) throw new ValidationException(Messages.BonusCampaignPastDate(Lang), "scheduledAt");
        campaign.ScheduledAt = DateTime.SpecifyKind(at.ToUniversalTime(), DateTimeKind.Utc);
        campaign.Status = CampaignStatus.Scheduled;
        campaign.UpdatedAt = DateTime.UtcNow;
        access.Audit(c, "bonus.campaign.schedule", "bonus_campaign", campaign.Id, null, new { campaign.ScheduledAt });
        await unitOfWork.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<CampaignDto> CancelAsync(Guid id, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.BonusCampaigns, ct);
        var campaign = await repo.CampaignAsync(c.Store.Id, id, ct) ?? throw new NotFoundException(Messages.BonusCampaignNotFound(Lang));
        if (campaign.Status is not (CampaignStatus.Draft or CampaignStatus.Scheduled or CampaignStatus.Queued)) throw new ValidationException(Messages.BonusCampaignNotCancellable(Lang));
        campaign.Status = CampaignStatus.Cancelled;
        campaign.CancelledAt = DateTime.UtcNow;
        campaign.CancelledBy = access.StaffUserId;
        access.Audit(c, "bonus.campaign.cancel", "bonus_campaign", campaign.Id, null, new { campaign.Name });
        await unitOfWork.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <summary>«Подтвердить и отправить» (§6.6): құқық тексеріледі, қайта басу екінші тарату не бонус жасамайды.</summary>
    public async Task<CampaignDto> SendAsync(Guid id, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.BonusCampaigns, ct);
        var campaign = await repo.CampaignAsync(c.Store.Id, id, ct) ?? throw new NotFoundException(Messages.BonusCampaignNotFound(Lang));
        access.Audit(c, "bonus.campaign.send", "bonus_campaign", campaign.Id, null, new { campaign.Name });
        await unitOfWork.SaveChangesAsync(ct);
        await DispatchAsync(id, false, ct);
        return await GetAsync(id, ct);
    }

    /// <summary>Қатемен аяқталғанда — тек «ошибка» күйіндегі алушыларға қайта (§6.1).</summary>
    public async Task<CampaignDto> RetryAsync(Guid id, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.BonusCampaigns, ct);
        _ = await repo.CampaignAsync(c.Store.Id, id, ct) ?? throw new NotFoundException(Messages.BonusCampaignNotFound(Lang));
        access.Audit(c, "bonus.campaign.retry", "bonus_campaign", id, null, null);
        await unitOfWork.SaveChangesAsync(ct);
        await DispatchAsync(id, true, ct);
        return await GetAsync(id, ct);
    }

    /// <summary>
    /// Тарату (сұраныстан не фондық тапсырмадан): жол FOR UPDATE-пен бұғатталады, күйі тексеріледі — қайталанған іске қосу ештеңе жасамайды.
    /// Әр алушыға: хабарлама (қосымшада «жеткізілді»), бонус болса — партия (алушы кілтімен бірегей). Бәрі бір транзакцияда.
    /// </summary>
    public async Task DispatchAsync(Guid id, bool retryFailed, CancellationToken ct)
    {
        await unitOfWork.InTransactionAsync(async () =>
        {
            var campaign = await repo.CampaignForUpdateAsync(id, ct) ?? throw new NotFoundException(Messages.BonusCampaignNotFound(Lang));
            var store = await stores.GetByIdAsync(campaign.StoreId, ct) ?? throw new NotFoundException(Messages.StoreNotFound(Lang));
            var zone = Sales.StoreTime.Zone(store);
            var now = DateTime.UtcNow;
            List<CampaignRecipient> work;
            if (retryFailed)
            {
                if (campaign.Status != CampaignStatus.CompletedWithErrors) return 0;
                work = (await repo.RecipientsForUpdateAsync(campaign.Id, RecipientStatus.Failed, ct)).ToList();
            }
            else
            {
                if (campaign.Status is not (CampaignStatus.Draft or CampaignStatus.Scheduled or CampaignStatus.Queued))
                {
                    if (campaign.Status == CampaignStatus.Cancelled) throw new ValidationException(Messages.BonusCampaignNotCancellable(Lang));
                    return 0;
                }
                if (await repo.HasRecipientsAsync(campaign.Id, ct)) return 0;
                var audience = await AudienceAsync(campaign.StoreId, campaign.AudienceMode, campaign.Filter, campaign.CustomerIds, ct);
                if (audience.All(a => a.Exclusion is not null)) throw new ValidationException(Messages.BonusCampaignNoRecipients(Lang));
                work = [];
                foreach (var a in audience)
                {
                    var rec = new CampaignRecipient
                    {
                        Id = Guid.NewGuid(), CampaignId = campaign.Id, CustomerId = a.Row.Card.CustomerId, BonusCardId = a.Row.Card.Id, Contact = a.Row.Phone,
                        Status = a.Exclusion is null ? RecipientStatus.Pending : RecipientStatus.Excluded, Reason = a.Exclusion,
                    };
                    repo.AddRecipient(rec);
                    if (a.Exclusion is null) work.Add(rec);
                }
                campaign.ExcludedCount = audience.Count(a => a.Exclusion is not null);
                campaign.RecipientsCount = work.Count;
            }
            campaign.Status = CampaignStatus.Sending;
            campaign.StartedAt ??= now;

            var customers = (await repo.AllCustomersAsync(campaign.StoreId, ct)).ToDictionary(r => r.Card.CustomerId);
            var next = (await transactions.GetNextExpiringAsync(work.Select(w => w.BonusCardId).ToList(), now, ct)).ToDictionary(t => t.BonusCardId);
            foreach (var rec in work)
            {
                if (!customers.TryGetValue(rec.CustomerId, out var row))
                {
                    rec.Status = RecipientStatus.Failed;
                    rec.Reason = "noCard";
                    continue;
                }
                DateTime? expires = null;
                if (campaign.BonusAmount is { } amount)
                {
                    var card = await repo.CardByIdForUpdateAsync(rec.BonusCardId, ct);
                    if (card is null) { rec.Status = RecipientStatus.Failed; rec.Reason = "noCard"; continue; }
                    expires = campaign.BonusValidityDays is { } d ? now.AddDays(d) : null;
                    if (rec.BonusTransactionId is null)
                    {
                        var lot = ledger.Credit(card, BonusTransactionType.Promo, amount, expires, "campaign", campaign.Name, now, sourceId: campaign.Id, key: rec.Id);
                        rec.BonusTransactionId = lot.Id;
                    }
                }
                var nearest = expires ?? (next.TryGetValue(rec.BonusCardId, out var lotNext) ? lotNext.ExpiresAt : null);
                var text = Render(campaign.Message, row, campaign.BonusAmount ?? row.Card.Balance, nearest, store.Name, zone);
                var n = ledger.Notify(rec.CustomerId, store, campaign.BonusAmount is not null ? NotificationType.Promo : NotificationType.Promo, campaign.Name, text,
                    campaign.BonusAmount is { } a ? (Ru ? $"+{a:N0} бонусов" : $"+{a:N0} бонус") : null, now, null, campaign.BonusAmount);
                rec.NotificationId = n.Id;
                rec.Status = RecipientStatus.Sent;
                rec.Reason = null;
                rec.SentAt = now;
            }
            var all = retryFailed ? (await repo.RecipientsAsync(campaign.Id, null, 0, 100_000, ct)).Items.Where(r => !work.Any(w => w.Id == r.Id)).Concat(work).ToList() : work;
            campaign.SentCount = all.Count(r => r.Status == RecipientStatus.Sent);
            campaign.FailedCount = all.Count(r => r.Status == RecipientStatus.Failed);
            campaign.Status = campaign.FailedCount > 0 ? CampaignStatus.CompletedWithErrors : CampaignStatus.Completed;
            campaign.CompletedAt = now;
            campaign.UpdatedAt = now;
            await unitOfWork.SaveChangesAsync(ct);
            return work.Count;
        }, ct);
    }

    private string Render(string template, BonusCustomerRow row, int bonuses, DateTime? expiresUtc, string storeName, TimeZoneInfo zone) =>
        BonusTemplates.Render(template, row.FirstName, BonusAccess.FullName(row.FirstName, row.LastName), bonuses,
            expiresUtc is { } e ? TimeZoneInfo.ConvertTimeFromUtc(e, zone) : null, row.BirthDate, storeName, Lang);

    public async Task<CampaignRecipientsPageDto> RecipientsAsync(Guid id, string? status, int page, int pageSize, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.BonusView, ct);
        _ = await repo.CampaignAsync(c.Store.Id, id, ct) ?? throw new NotFoundException(Messages.BonusCampaignNotFound(Lang));
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var (items, total) = await repo.RecipientsAsync(id, status, (page - 1) * pageSize, pageSize, ct);
        var customers = (await repo.AllCustomersAsync(c.Store.Id, ct)).ToDictionary(r => r.Card.CustomerId);
        var read = await repo.ReadNotificationsAsync(items.Where(i => i.NotificationId != null).Select(i => i.NotificationId!.Value).ToList(), ct);
        return new CampaignRecipientsPageDto(items.Select(i => new CampaignRecipientDto(i.Id, i.CustomerId,
            customers.TryGetValue(i.CustomerId, out var row) ? BonusAccess.FullName(row.FirstName, row.LastName) : "—", BonusAccess.Phone(i.Contact, c.Can.Contacts),
            i.Status.ToString(), i.Reason, i.SentAt, i.NotificationId is { } nid && read.Contains(nid), i.BonusTransactionId)).ToList(), total, page, pageSize);
    }

    /// <summary>Мерзімі келген жоспарланған таратулар (фондық тапсырма).</summary>
    public async Task<int> ProcessDueAsync(CancellationToken ct)
    {
        var done = 0;
        foreach (var id in await repo.DueCampaignIdsAsync(DateTime.UtcNow, ct))
        {
            await DispatchAsync(id, false, ct);
            done++;
        }
        return done;
    }

    private static CampaignDto ToDto(BonusCampaign i, IReadOnlyDictionary<Guid, string> authors, int read) => new(
        i.Id, i.Name, i.Type.ToString(), i.Status.ToString(), i.Channel, i.AudienceMode.ToString(), i.Filter, i.CustomerIds, i.Message, i.BonusAmount,
        i.BonusValidityDays, i.ScheduledAt, i.RecipientsCount, i.SentCount, i.SentCount, read, i.FailedCount, i.ExcludedCount,
        authors.GetValueOrDefault(i.CreatedBy, ""), i.CreatedAt, i.StartedAt, i.CompletedAt, i.CancelledAt,
        i.Status is CampaignStatus.Draft or CampaignStatus.Scheduled, i.Status is CampaignStatus.Draft or CampaignStatus.Scheduled or CampaignStatus.Queued,
        i.Status is CampaignStatus.Draft or CampaignStatus.Scheduled, i.Status == CampaignStatus.CompletedWithErrors);
}
