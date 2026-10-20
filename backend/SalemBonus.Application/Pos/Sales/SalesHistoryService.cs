using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Application.Pos.Catalog;
using SalemBonus.Domain.Core;
using SalemBonus.Domain.Pos.Sales;

namespace SalemBonus.Application.Pos.Sales;

/// <summary>
/// «Продажи» бөлімі (ТЗ «Продажи» v1.0): сатылым тарихы, KPI, чек тарихы, экспорт. Жаңа сатылым мұнда
/// жасалмайды — тек «Касса»-да; қайтару — <see cref="ReturnService"/> арқылы, касса мен кабинетке ортақ.
/// Чектің тарихи сомасы ешқашан қайта жазылмайды; қайтарым бөлек операция (§1).
/// </summary>
public class SalesHistoryService(
    ISalesHistoryRepository history,
    ISalesRepository sales,
    IRegisterRepository registers,
    IStoreRepository stores,
    SaleService saleService,
    ISpreadsheet spreadsheet,
    CatalogAccess access)
{
    private const int MaxPageSize = 100;
    /// <summary>Экспорт синхронды: одан көп жол — кезеңді тарылту керек (ТЗ §12: үлкен выгрузка — кейін асинхронды).</summary>
    public const int MaxExportRows = 10_000;
    private AppLanguage Lang => access.Lang;

    private static readonly string[] Sorts = ["number", "date", "total", "returned", "net", "status"];

    // ---------- Тізім (§7) ----------

    public async Task<SalesHistoryPageDto> ListAsync(SalesHistoryQuery q, CancellationToken ct = default)
    {
        var (_, m) = await access.RequireAsync(StaffPermissions.SalesView, ct);
        var page = Math.Max(1, q.Page);
        var pageSize = Math.Clamp(q.PageSize, 1, MaxPageSize);
        var filter = await FilterAsync(m.StoreId, q, (page - 1) * pageSize, pageSize, ct);
        var (items, total) = await history.SearchAsync(filter, ct);
        return new SalesHistoryPageDto(await ToItemsAsync(items, ct), total, page, pageSize);
    }

    private async Task<IReadOnlyList<SalesHistoryItemDto>> ToItemsAsync(IReadOnlyList<Sale> items, CancellationToken ct)
    {
        if (items.Count == 0) return [];
        var ids = items.Select(s => s.Id).ToList();
        var customers = await sales.CustomerNamesAsync(items.Where(s => s.CustomerId != null).Select(s => s.CustomerId!.Value).Distinct().ToList(), ct);
        var cashiers = await sales.StaffNamesAsync(items.Select(s => s.StaffUserId).Distinct().ToList(), ct);
        var registerNames = (await registers.ListByStoreAsync(items[0].StoreId, ct)).ToDictionary(r => r.Id, r => r.Name);
        var units = await history.UnitsBySaleAsync(ids, ct);
        var debts = await history.DebtsBySaleAsync(ids, ct);
        return items.Select(s =>
        {
            var debt = debts.GetValueOrDefault(s.Id);
            var openDebt = debt is { Status: DebtStatus.Open };
            return new SalesHistoryItemDto(
                s.Id, s.Number, s.CreatedAt, s.CustomerId, s.CustomerId is { } c ? customers.GetValueOrDefault(c) : null,
                cashiers.GetValueOrDefault(s.StaffUserId) ?? string.Empty, registerNames.GetValueOrDefault(s.RegisterId) ?? string.Empty,
                s.Items.Count, units.GetValueOrDefault(s.Id), s.Total, s.ReturnedAmount, s.Total - s.ReturnedAmount,
                s.Status == SaleStatus.Completed && openDebt ? "Debt" : s.Status.ToString(),
                s.Payments.Select(p => p.Method.ToString()).Distinct().ToList(),
                openDebt, openDebt ? debt!.Remaining : 0);
        }).ToList();
    }

    // ---------- KPI (§5, §11) ----------

    public async Task<SalesSummaryDto> SummaryAsync(SalesHistoryQuery q, CancellationToken ct = default)
    {
        var (_, m) = await access.RequireAsync(StaffPermissions.SalesView, ct);
        var filter = await FilterAsync(m.StoreId, q, 0, 1, ct);
        var s = await history.StatsAsync(filter, ct);
        var returns = await history.ReturnsStatsAsync(m.StoreId, filter.FromUtc, filter.ToUtc, filter.RegisterId, filter.StaffUserId, ct);

        // Алдыңғы кезеңмен салыстыру: сол ұзақтықтағы тікелей алдыңғы аралық (тек кезең берілгенде).
        decimal? change = null;
        if (filter.FromUtc is { } from && filter.ToUtc is { } to)
        {
            var length = to - from;
            var prev = await history.StatsAsync(filter with { FromUtc = from - length, ToUtc = from }, ct);
            if (prev.Total > 0) change = Math.Round((s.Total - prev.Total) / prev.Total * 100, 1);
        }

        static decimal Share(decimal part, decimal whole) => whole > 0 ? Math.Round(part / whole * 100, 1) : 0;
        return new SalesSummaryDto(
            s.Count, s.Total, change,
            returns.Count, returns.Amount, Share(returns.Amount, s.Total),
            s.DebtCount, s.DebtAmount, s.DebtOpen,
            s.Count > 0 ? Math.Round(s.Total / s.Count, 2) : 0,
            s.Units,
            s.Discounts, s.DiscountCount, Share(s.Discounts, s.Total + s.Discounts),
            s.BonusRedeemed, s.BonusCount, Share(s.BonusRedeemed, s.Total + s.BonusRedeemed),
            s.WithReturn, Share(s.WithReturn, s.Count));
    }

    public async Task<SalesFiltersDto> FiltersAsync(CancellationToken ct = default)
    {
        var (_, m) = await access.RequireAsync(StaffPermissions.SalesView, ct);
        var regs = await registers.ListByStoreAsync(m.StoreId, ct);
        var people = await history.StoreStaffAsync(m.StoreId, ct);
        return new SalesFiltersDto(
            regs.OrderBy(r => r.Name).Select(r => new SalesFilterOptionDto(r.Id, r.Name)).ToList(),
            people.Select(p => new SalesFilterOptionDto(p.Id, p.Name)).ToList());
    }

    // ---------- Чек карточкасы (§8) ----------

    public async Task<ReceiptDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var (_, m) = await access.RequireAsync(StaffPermissions.SalesView, ct);
        var sale = await sales.GetSaleAsync(m.StoreId, id, ct) ?? throw new NotFoundException(Messages.SaleNotFound(Lang));
        return await saleService.ReceiptAsync(sale, ct);
    }

    /// <summary>Хронология (§8.4): сатылым, төлемдер, жеңілдік, бонус, қарыз өтеулері, қайтарымдар — бәрі дерек бойынша.</summary>
    public async Task<IReadOnlyList<SaleEventDto>> HistoryAsync(Guid id, CancellationToken ct = default)
    {
        var (_, m) = await access.RequireAnyAsync(ct, StaffPermissions.SalesView, StaffPermissions.SalesCreate);
        var sale = await sales.GetSaleAsync(m.StoreId, id, ct) ?? throw new NotFoundException(Messages.SaleNotFound(Lang));
        var returns = await sales.ListReturnsAsync(sale.Id, ct);
        var repayments = await history.DebtPaymentsBySaleAsync(sale.Id, ct);
        var staffIds = new[] { sale.StaffUserId }.Concat(returns.Select(r => r.StaffUserId)).Concat(repayments.Select(p => p.StaffUserId));
        if (sale.DiscountApprovedBy is { } approver) staffIds = staffIds.Append(approver);
        var names = await sales.StaffNamesAsync(staffIds.Distinct().ToList(), ct);
        string? Name(Guid staffId) => names.GetValueOrDefault(staffId);
        var cashier = Name(sale.StaffUserId);

        var events = new List<SaleEventDto> { new(sale.CreatedAt, "sale", cashier, sale.Total, null, $"№{sale.Number}", null) };
        foreach (var p in sale.Payments)
            events.Add(new SaleEventDto(sale.CreatedAt, p.Method == PaymentMethod.Debt ? "debt" : "payment", cashier, p.Amount, p.Method.ToString(), p.TransferRecipient, null));
        if (sale.DiscountAmount > 0)
            events.Add(new SaleEventDto(sale.CreatedAt, "discount", cashier, sale.DiscountAmount, null,
                sale.DiscountApprovedBy is { } a ? Name(a) : null, null));
        if (sale.BonusRedeemed > 0) events.Add(new SaleEventDto(sale.CreatedAt, "bonus.redeemed", cashier, sale.BonusRedeemed, null, null, null));
        if (sale.BonusAccrued > 0) events.Add(new SaleEventDto(sale.CreatedAt, "bonus.accrued", cashier, sale.BonusAccrued, null, null, null));
        foreach (var p in repayments.Where(p => !p.IsReturn))
            events.Add(new SaleEventDto(p.CreatedAt, "debt.repaid", Name(p.StaffUserId), p.Amount, p.Method?.ToString(), p.TransferRecipient, null));
        foreach (var r in returns)
        {
            var who = Name(r.StaffUserId);
            var note = string.Join(" · ", new[] { r.ReasonCode?.ToString(), r.Reason, r.StockAction == ReturnStockAction.WriteOff ? "WriteOff" : null }
                .Where(x => !string.IsNullOrWhiteSpace(x)));
            events.Add(new SaleEventDto(r.CreatedAt, "return", who, r.Amount, r.RefundMethod?.ToString(), note, r.Id));
            if (r.DebtReduced > 0) events.Add(new SaleEventDto(r.CreatedAt, "debt.reduced", who, r.DebtReduced, null, null, r.Id));
            if (r.BonusRestored > 0) events.Add(new SaleEventDto(r.CreatedAt, "bonus.restored", who, r.BonusRestored, null, null, r.Id));
            if (r.BonusReversed > 0) events.Add(new SaleEventDto(r.CreatedAt, "bonus.reversed", who, r.BonusReversed, null, null, r.Id));
        }
        return events.OrderBy(e => e.At).ToList();
    }

    // ---------- Экспорт (§12) ----------

    public async Task<(byte[] Content, string FileName)> ExportAsync(SalesHistoryQuery q, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.SalesExport, ct);
        var filter = await FilterAsync(m.StoreId, q, 0, MaxExportRows + 1, ct);
        var (items, total) = await history.SearchAsync(filter, ct);
        if (total > MaxExportRows) throw new ValidationException(Messages.SalesExportTooLarge(Lang, MaxExportRows));
        var rows = await ToItemsAsync(items, ct);
        var store = await stores.GetByIdAsync(m.StoreId, ct);
        var zone = store is null ? TimeZoneInfo.Utc : StoreTime.Zone(store);
        var ru = Lang == AppLanguage.Ru;
        string[] headers = ru
            ? ["Чек №", "Дата и время", "Магазин", "Касса", "Кассир", "Клиент", "Товаров", "Сумма продажи", "Возврат", "Итог", "Способ оплаты", "Статус"]
            : ["Чек №", "Күні мен уақыты", "Дүкен", "Касса", "Кассир", "Клиент", "Тауар", "Сату сомасы", "Қайтарым", "Қорытынды", "Төлем түрі", "Мәртебе"];
        var content = spreadsheet.Write(ru ? "Продажи" : "Сатылымдар", headers, rows.Select(r => (IReadOnlyList<object?>)
        [
            r.Number, TimeZoneInfo.ConvertTimeFromUtc(r.CreatedAt, zone).ToString("dd.MM.yyyy HH:mm"), store?.Name, r.RegisterName, r.CashierName,
            r.CustomerName, r.Units, r.Total, r.Returned, r.Net, string.Join(" + ", r.Methods.Select(x => MethodName(x, ru))), StatusName(r.Status, ru),
        ]));
        var name = $"sales_{(q.From?.ToString("yyyy-MM-dd") ?? "all")}_{(q.To?.ToString("yyyy-MM-dd") ?? "all")}.xlsx";
        access.Audit(orgId, m.StoreId, "sales.export", "sales", m.StoreId, null, new { q.From, q.To, Rows = rows.Count });
        return (content, name);
    }

    private static string MethodName(string method, bool ru) => method switch
    {
        "Cash" => ru ? "Наличные" : "Қолма-қол",
        "Card" => ru ? "Карта" : "Карта",
        "Qr" => "QR",
        "Transfer" => ru ? "Перевод" : "Аударым",
        "Debt" => ru ? "Долг" : "Қарыз",
        _ => method,
    };

    private static string StatusName(string status, bool ru) => status switch
    {
        "Completed" => ru ? "Оплачен" : "Төленді",
        "PartiallyReturned" => ru ? "Частичный возврат" : "Ішінара қайтарым",
        "Returned" => ru ? "Полный возврат" : "Толық қайтарым",
        "Debt" => ru ? "Долг" : "Қарыз",
        _ => status,
    };

    // ---------- Сүзгіні түрлендіру ----------

    private async Task<SalesHistoryFilter> FilterAsync(Guid storeId, SalesHistoryQuery q, int skip, int take, CancellationToken ct)
    {
        var store = await stores.GetByIdAsync(storeId, ct) ?? throw new NotFoundException(Messages.StoreNotFound(Lang));
        var zone = StoreTime.Zone(store);
        var (from, to) = (q.From, q.To);
        if (from is { } f && to is { } t && t < f) (from, to) = (t, f);
        PaymentMethod? method = Enum.TryParse<PaymentMethod>(q.Method, true, out var pm) ? pm : null;
        var status = q.Status is "Completed" or "PartiallyReturned" or "Returned" or "Debt" ? q.Status : null;
        bool? hasReturn = q.HasReturn?.ToLowerInvariant() switch { "yes" or "true" or "1" => true, "no" or "false" or "0" => false, _ => null };
        var sort = Sorts.Contains(q.Sort ?? "") ? q.Sort : "date";
        var desc = !string.Equals(q.Dir, "asc", StringComparison.OrdinalIgnoreCase);
        return new SalesHistoryFilter(storeId,
            from is { } a ? StoreTime.StartOfDayUtc(a, zone) : null,
            to is { } b ? StoreTime.StartOfDayUtc(b.AddDays(1), zone) : null,
            CatalogAccess.Optional(q.Search, 100), q.RegisterId, q.StaffUserId, method, status, hasReturn, q.CustomerId, q.NoCustomer,
            sort, desc, skip, take);
    }
}
