using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Application.Kato;
using SalemBonus.Application.Pos.Finance;
using SalemBonus.Application.Pos.Sales;
using SalemBonus.Domain.Core;
using SalemBonus.Domain.Pos.Clients;
using SalemBonus.Domain.Pos.Finance;
using SalemBonus.Domain.Pos.Sales;

namespace SalemBonus.Application.Pos.Clients;

/// <summary>
/// «Клиенты» оқу жағы (ТЗ «Клиенты» §2–§14): тізім мен KPI (бір сүзгімен — KPI басылса, тізім сол санды көрсетеді), шолу, туған күндер,
/// карточка және оның бөлімдері. Көрсеткіштер таңдалған дүкендер бойынша; сатылым, қарыз, бонус — өз модульдерінен, көшірмесіз.
/// </summary>
public class ClientService(
    ClientAccess access,
    IClientRepository repo,
    IFinanceRepository finance,
    FinanceDebtsService debts,
    IKatoService kato)
{
    private AppLanguage Lang => access.Lang;

    public async Task<ClientMetaDto> MetaAsync(CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.ClientsView, ct, ensure: false);
        return new ClientMetaDto(c.Can, ClientAccess.Settings(c.Settings),
            c.Statuses.Where(s => s.IsActive).Select(s => new ClientStatusOptionDto(s.Id, s.Name, s.Color)).ToList(), c.Store.Id);
    }

    /// <summary>Сүзгі + сегмент: сегмент шарттары бойынша табылғандар мен қолмен қосылғандардың бірігуі, үстінен — ағымдағы сүзгі.</summary>
    public async Task<ClientQuery> QueryAsync(ClientCtx c, ClientFilter? filter, Guid? segmentId, CancellationToken ct)
    {
        var q = c.Query(ClientAccess.Clean(filter));
        if (segmentId is not { } sid) return q;
        var segment = await repo.SegmentAsync(c.OrgId, sid, ct);
        if (segment is null || (!segment.IsShared && segment.CreatedBy != access.StaffUserId)) throw new NotFoundException(Messages.ClientSegmentNotFound(Lang));
        var ids = (await repo.IdsAsync(c.Query(ClientAccess.Clean(segment.Filter)), ct)).Concat(segment.IncludeIds).Distinct().ToList();
        return q with { Ids = ids };
    }

    public async Task<ClientPageDto> SearchAsync(ClientSearchRequest r, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.ClientsView, ct, r.StoreIds);
        var q = await QueryAsync(c, r.Filter, r.SegmentId, ct);
        var size = Math.Clamp(r.PageSize, 1, 200);
        var page = Math.Max(1, r.Page);
        var (rows, total) = await repo.ListAsync(q with
        {
            Sort = r.Sort, Desc = !string.Equals(r.Dir, "asc", StringComparison.OrdinalIgnoreCase), Skip = (page - 1) * size, Take = size,
        }, ct);
        return new ClientPageDto(rows.Select(x => access.Row(c, x)).ToList(), total, page, size);
    }

    public async Task<ClientKpisDto> KpisAsync(ClientSearchRequest r, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.ClientsView, ct, r.StoreIds);
        var q = await QueryAsync(c, r.Filter, r.SegmentId, ct);
        return Kpis(await repo.KpisAsync(q, ct));
    }

    private static ClientKpisDto Kpis(ClientKpiCounts k) =>
        new(k.Total, k.Active, k.Passive, k.New, k.Repeat, k.WithDebt, k.Overdue, k.WithBonus, DateTime.UtcNow);

    public async Task<ClientOverviewDto> OverviewAsync(string? storeIds, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.ClientsView, ct, storeIds);
        var q = c.Query();
        var k = await repo.KpisAsync(q, ct);
        var cooling = (await repo.ListAsync(c.Query(new ClientFilter { Quick = "cooling" }) with { Take = 0 }, ct)).Total;
        var none = (await repo.ListAsync(c.Query(new ClientFilter { Quick = "noPurchases" }) with { Take = 0 }, ct)).Total;
        var activity = new List<ClientSegmentCountDto>
        {
            new("active", k.Active), new("cooling", cooling), new("passive", k.Passive), new("noPurchases", none),
        };

        var firstMonth = new DateOnly(c.Today.Year, c.Today.Month, 1).AddMonths(-11);
        var registrations = await repo.RegistrationsAsync(q, StoreTime.StartOfDayUtc(firstMonth, c.Zone), ct);
        var byMonth = registrations.GroupBy(x => c.LocalDate(x).ToString("yyyy-MM")).ToDictionary(g => g.Key, g => g.Count());
        var months = Enumerable.Range(0, 12).Select(i => firstMonth.AddMonths(i).ToString("yyyy-MM"))
            .Select(m => new ClientMonthDto(m, byMonth.GetValueOrDefault(m), 0)).ToList();

        var (top, _) = await repo.ListAsync(q with { Sort = "amount", Desc = true, Take = 10 }, ct);
        var (risk, _) = await repo.ListAsync(c.Query(new ClientFilter { Quick = "cooling", MinPurchases = 2 }) with { Sort = "amount", Desc = true, Take = 5 }, ct);
        var birthdays = await BirthdayRowsAsync(c, 7, 10, ct);
        var open = (await finance.DebtsAsync(c.StoreIds, true, ct)).ToList();
        return new ClientOverviewDto(Kpis(k), activity, months, top.Where(x => x.Amount > 0).Select(x => access.Row(c, x)).ToList(),
            risk.Select(x => access.Row(c, x)).ToList(), birthdays, open.Sum(d => d.Remaining), open.Where(d => d.DueDate < c.Today).Sum(d => d.Remaining));
    }

    public async Task<IReadOnlyList<ClientBirthdayDto>> BirthdaysAsync(string? storeIds, int days, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.ClientsView, ct, storeIds);
        return await BirthdayRowsAsync(c, Math.Clamp(days, 1, 60), 1000, ct);
    }

    private async Task<IReadOnlyList<ClientBirthdayDto>> BirthdayRowsAsync(ClientCtx c, int days, int take, CancellationToken ct)
    {
        var (rows, _) = await repo.ListAsync(c.Query(new ClientFilter { Quick = "birthday" }) with { BirthdayDays = days, Take = 2000, Sort = "birthday", Desc = false }, ct);
        return rows.Where(r => r.BirthDate is not null).Select(r =>
        {
            var next = NextBirthday(r.BirthDate!.Value, c.Today);
            return new ClientBirthdayDto(access.Row(c, r), next, next.DayNumber - c.Today.DayNumber, next.Year - r.BirthDate.Value.Year);
        }).OrderBy(x => x.DaysLeft).ThenBy(x => x.Client.Name).Take(take).ToList();
    }

    public static DateOnly NextBirthday(DateOnly birth, DateOnly today)
    {
        DateOnly In(int year) => new(year, birth.Month, Math.Min(birth.Day, DateTime.DaysInMonth(year, birth.Month)));
        var d = In(today.Year);
        return d < today ? In(today.Year + 1) : d;
    }

    // ---------- Карточка ----------

    public async Task<ClientCardDto> CardAsync(Guid id, string? storeIds, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.ClientsView, ct, storeIds);
        return await BuildCardAsync(c, id, ct);
    }

    /// <summary>Басқа бөлімнен (қарыз, бонус, сатылым) клиент карточкасын ашу — жаһандық клиент ID-сы бойынша.</summary>
    public async Task<ClientCardDto> ByCustomerAsync(Guid customerId, string? storeIds, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.ClientsView, ct, storeIds);
        var p = await repo.ByCustomerAsync(c.OrgId, customerId, ct) ?? throw new NotFoundException(Messages.ClientNotFound(Lang));
        return await BuildCardAsync(c, p.Id, ct);
    }

    public async Task<ClientCardDto> BuildCardAsync(ClientCtx c, Guid id, CancellationToken ct)
    {
        var row = await repo.RowAsync(c.Query(), id, ct) ?? throw new NotFoundException(Messages.ClientNotFound(Lang));
        var p = row.Profile;
        var path = p.KatoCode is null ? [] : (await kato.GetPathAsync(p.KatoCode, ct)).Select(n => new ClientKatoDto(n.Code, n.Name)).ToList();
        var (sales, _) = await repo.SalesAsync(p.CustomerId, c.StoreIds, 0, 5, ct);
        var top = await repo.TopProductsAsync(p.CustomerId, c.StoreIds, 5, ct);
        var timeline = await repo.SaleTimelineAsync(p.CustomerId, c.StoreIds, ct);
        var notes = (await repo.NotesAsync(p.Id, ct)).Count;
        var merged = p.MergedIntoId is { } mid ? await repo.GetAsync(c.OrgId, mid, ct) : null;
        var orgStores = await repo.OrgStoreIdsAsync(c.OrgId, ct);
        var canEditPhone = c.Can.Edit && !row.HasApp && !await repo.HasCardsOutsideAsync(p.CustomerId, orgStores, ct);
        var card = await repo.CardAsync(p.CustomerId, c.Store.Id, ct);
        var (statusName, statusColor) = access.Status(c, card?.StatusId, card?.Level, card is not null);
        var staffIds = new[] { p.CreatedBy, p.ArchivedBy }.Where(x => x is not null).Select(x => x!.Value).Concat(sales.Select(s => s.StaffUserId)).Distinct().ToList();
        var names = await repo.StaffNamesAsync(staffIds, ct);
        var stores = await repo.StoreNamesAsync(c.OrgId, ct);
        return new ClientCardDto(access.Row(c, row), c.Can.Contacts ? p.Address : null, path, p.Comment, p.ConsentUpdatedAt,
            p.CreatedBy is { } cb ? names.GetValueOrDefault(cb) : null, p.UpdatedAt, p.ArchivedAt, p.ArchivedBy is { } ab ? names.GetValueOrDefault(ab) : null,
            p.ArchiveReason, p.MergedIntoId, merged?.Number, canEditPhone,
            new ClientBonusCardDto(card is not null, card?.Balance ?? 0, card?.StatusId, statusName, statusColor, card?.NotificationsOptOut ?? false),
            Summary(c, row, timeline), top.Select(x => new ClientProductDto(x.ProductId, x.Name, x.Quantity, x.Amount, x.Purchases)).ToList(),
            sales.Select(s => Sale(s, stores, names)).ToList(), notes);
    }

    /// <summary>Белсенділік (§11): сегмент, соңғы сатып алудан бергі күн, орташа аралық, кету қаупі (тек көрсеткіш, автоматты әрекет жоқ).</summary>
    public static ClientActivitySummaryDto Summary(ClientCtx c, ClientRow row, IReadOnlyList<(DateTime At, decimal Net)> timeline)
    {
        var segment = ClientAccess.Activity(c, row);
        int? days = row.LastPurchaseAt is { } last ? c.Today.DayNumber - c.LocalDate(last).DayNumber : null;
        decimal? interval = timeline.Count >= 2 ? Math.Round((decimal)(timeline[^1].At - timeline[0].At).TotalDays / (timeline.Count - 1), 1) : null;
        var risk = days is not { } d ? "none"
            : d > c.Settings.PassiveDays ? "high"
            : interval is not { } avg || avg <= 0 ? (d > c.Settings.ActiveDays ? "medium" : "low")
            : d >= avg * 3 ? "high"
            : d >= avg * 1.5m ? "medium"
            : "low";
        return new ClientActivitySummaryDto(segment, days, interval, risk);
    }

    private static ClientSaleDto Sale(ClientSaleRow s, IReadOnlyDictionary<Guid, string> stores, IReadOnlyDictionary<Guid, string> staff) =>
        new(s.Id, s.Number, s.CreatedAt, s.StoreId, stores.GetValueOrDefault(s.StoreId) ?? "—", s.Total, s.ReturnedAmount, s.Status.ToString(), s.Items,
            s.BonusAccrued, s.BonusRedeemed, s.DiscountAmount, staff.GetValueOrDefault(s.StaffUserId) ?? "", s.Methods.Select(m => m.ToString()).ToList());

    public async Task<ClientSalesPageDto> SalesAsync(Guid id, string? storeIds, int page, int pageSize, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.ClientsView, ct, storeIds, ensure: false);
        var p = await access.ProfileAsync(c, id, ct);
        var size = Math.Clamp(pageSize, 1, 100);
        var (rows, total) = await repo.SalesAsync(p.CustomerId, c.StoreIds, (Math.Max(1, page) - 1) * size, size, ct);
        var names = await repo.StaffNamesAsync(rows.Select(r => r.StaffUserId).Distinct().ToList(), ct);
        var stores = await repo.StoreNamesAsync(c.OrgId, ct);
        return new ClientSalesPageDto(rows.Select(s => Sale(s, stores, names)).ToList(), total);
    }

    /// <summary>«Чеки» (§9): сатылым мен қайтару чектері бір лентада, жаңасы жоғарыда.</summary>
    public async Task<IReadOnlyList<ClientReceiptDto>> ReceiptsAsync(Guid id, string? storeIds, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.ClientsView, ct, storeIds, ensure: false);
        var p = await access.ProfileAsync(c, id, ct);
        var (sales, _) = await repo.SalesAsync(p.CustomerId, c.StoreIds, 0, 300, ct);
        var returns = await repo.ReturnsAsync(p.CustomerId, c.StoreIds, ct);
        var names = await repo.StaffNamesAsync(sales.Select(s => s.StaffUserId).Concat(returns.Select(r => r.StaffUserId)).Distinct().ToList(), ct);
        var stores = await repo.StoreNamesAsync(c.OrgId, ct);
        return sales.Select(s => new ClientReceiptDto("sale", s.Id, s.Id, s.Number, s.CreatedAt, stores.GetValueOrDefault(s.StoreId) ?? "—", s.Total, s.Status.ToString(),
                s.Methods.Select(m => m.ToString()).ToList(), names.GetValueOrDefault(s.StaffUserId) ?? "", null))
            .Concat(returns.Select(r => new ClientReceiptDto("return", r.Id, r.SaleId, r.SaleNumber, r.CreatedAt, stores.GetValueOrDefault(r.StoreId) ?? "—", r.Amount,
                "Returned", r.RefundMethod is { } m ? [m.ToString()] : [], names.GetValueOrDefault(r.StaffUserId) ?? "", r.Reason)))
            .OrderByDescending(x => x.CreatedAt).ToList();
    }

    /// <summary>«Долги и оплаты» (§10): «Финансы» карточкасымен бірдей деректер; құқық — clients.debts не finance.view.</summary>
    public async Task<CustomerDebtCardDto> DebtsAsync(Guid id, string? storeIds, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.ClientsView, ct, storeIds, ensure: false);
        access.Require(c.Can.Debts);
        var p = await access.ProfileAsync(c, id, ct);
        return await debts.BuildCustomerCardAsync(c.StoreIds, c.Today, c.Can.Contacts, p.CustomerId, ct);
    }

    /// <summary>Клиенттер қарызының тізімі («Долги клиентов» бөлімі).</summary>
    public async Task<CustomerDebtsDto> DebtRegistryAsync(string? storeIds, string? segment, string? search, int page, int pageSize, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.ClientsView, ct, storeIds, ensure: false);
        access.Require(c.Can.Debts);
        return await debts.BuildCustomerDebtsAsync(c.StoreIds, c.Today, c.Can.Contacts, segment, search, page, pageSize, ct);
    }

    /// <summary>«Документы» (§12): чектер, қайтарулар, клиентпен байланысты қаржы операциялары (сатылым/қайтару төлемінің қайталауынсыз).</summary>
    public async Task<IReadOnlyList<ClientDocumentDto>> DocumentsAsync(Guid id, string? storeIds, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.ClientsView, ct, storeIds, ensure: false);
        var p = await access.ProfileAsync(c, id, ct);
        var (sales, _) = await repo.SalesAsync(p.CustomerId, c.StoreIds, 0, 300, ct);
        var returns = await repo.ReturnsAsync(p.CustomerId, c.StoreIds, ct);
        var ops = await repo.FinanceOperationsAsync(c.OrgId, p.CustomerId, c.StoreIds, ct);
        var stores = await repo.StoreNamesAsync(c.OrgId, ct);
        string Store(Guid s) => stores.GetValueOrDefault(s) ?? "—";
        return sales.Select(s => new ClientDocumentDto("sale", s.Id, s.Number.ToString(), s.CreatedAt, s.Total, s.Status.ToString(), Store(s.StoreId), s.Id, null, null))
            .Concat(returns.Select(r => new ClientDocumentDto("return", r.Id, r.SaleNumber.ToString(), r.CreatedAt, r.Amount, "Returned", Store(r.StoreId), r.SaleId, null, r.Reason)))
            .Concat(ops.Where(o => o.Type is not (nameof(FinanceOperationType.SalePayment) or nameof(FinanceOperationType.Refund)))
                .Select(o => new ClientDocumentDto("finance", o.Id, o.Number, o.Date, o.Amount, o.Status, Store(o.StoreId), null, o.Id, o.Description)))
            .OrderByDescending(x => x.Date).ToList();
    }

    /// <summary>«Активность» (§11): 30/90/365 күн, айлар бойынша сатып алу, хабарлар тарихы.</summary>
    public async Task<ClientActivityDto> ActivityAsync(Guid id, string? storeIds, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.ClientsView, ct, storeIds, ensure: false);
        var row = await repo.RowAsync(c.Query(), id, ct) ?? throw new NotFoundException(Messages.ClientNotFound(Lang));
        var timeline = await repo.SaleTimelineAsync(row.Profile.CustomerId, c.StoreIds, ct);
        (int Count, decimal Amount) Window(int days)
        {
            var since = c.Now.AddDays(-days);
            var list = timeline.Where(t => t.At >= since).ToList();
            return (list.Count, list.Sum(t => t.Net));
        }
        var w30 = Window(30);
        var w90 = Window(90);
        var w365 = Window(365);
        var firstMonth = new DateOnly(c.Today.Year, c.Today.Month, 1).AddMonths(-11);
        var grouped = timeline.GroupBy(t => c.LocalDate(t.At).ToString("yyyy-MM")).ToDictionary(g => g.Key, g => (g.Count(), g.Sum(x => x.Net)));
        var months = Enumerable.Range(0, 12).Select(i => firstMonth.AddMonths(i).ToString("yyyy-MM"))
            .Select(m => grouped.TryGetValue(m, out var v) ? new ClientMonthDto(m, v.Item1, v.Item2) : new ClientMonthDto(m, 0, 0)).ToList();
        var comms = await repo.CommunicationsAsync(row.Profile.Id, 50, ct);
        var names = await repo.StaffNamesAsync(comms.Select(x => x.StaffUserId).Distinct().ToList(), ct);
        var stores = await repo.StoreNamesAsync(c.OrgId, ct);
        return new ClientActivityDto(Summary(c, row, timeline), w30.Count, w90.Count, w365.Count, w30.Amount, w90.Amount, w365.Amount,
            row.FirstPurchaseAt, row.LastPurchaseAt, months,
            comms.Select(x => new ClientCommunicationDto(x.Id, x.CreatedAt, x.Channel, x.Text, x.Status, names.GetValueOrDefault(x.StaffUserId) ?? "",
                stores.GetValueOrDefault(x.StoreId) ?? "—")).ToList());
    }
}
