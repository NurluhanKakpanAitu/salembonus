using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Application.Pos.Analytics;
using SalemBonus.Application.Pos.Catalog;
using SalemBonus.Domain.Core;

namespace SalemBonus.Application.Pos.Finance;

/// <summary>
/// Экспорт (ТЗ «Финансы» §13.5): Excel — нақты ұяшықтар мен сандар, CSV — машина оқитын мәндер; сүзгінің барлық жолы.
/// Құқық: finance.view + statistics.export; аудитке жазылады (§18).
/// </summary>
public class FinanceExport(FinanceOperationsService operations, FinanceReportsService reports, FinanceDebtsService debts, ISpreadsheet spreadsheet, CatalogAccess access)
{
    public const int MaxRows = 20_000;
    private AppLanguage Lang => access.Lang;

    public async Task<(byte[] Content, string FileName, string ContentType)> ExportAsync(string report, string? format, FinanceListQuery q, Guid? id, bool compare,
        CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAnyAsync(ct, StaffPermissions.StatisticsExport, StaffPermissions.FinanceAccounts);
        if (!m.Has(StaffPermissions.FinanceView)) throw new ForbiddenException(Messages.StaffPermissionDenied(Lang));
        var csv = string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase);
        IReadOnlyList<string> headers;
        List<IReadOnlyList<object?>> rows;
        DateOnly? from = q.From, to = q.To;
        switch (report)
        {
            case "operations":
            {
                var (ctx, list) = await operations.AllAsync(q, MaxRows, ct);
                (from, to) = (ctx.Start, ctx.End);
                headers = H("number", "date", "type", "status", "description", "counterparty", "fromAccount", "toAccount", "amount", "category", "document", "source", "author");
                rows = list.Select(o => (IReadOnlyList<object?>)[o.Number, Dt(o.Date), L("type." + o.Type), L("status." + o.Status), o.Description, o.CounterpartyName,
                    o.FromAccountName, o.ToAccountName, o.Amount, o.CategoryName, o.DocumentNumber, o.SourceModule, o.CreatedByName]).ToList();
                break;
            }
            case "statement":
            {
                var s = await operations.StatementAsync(id ?? throw new ValidationException(Messages.FinanceAccountNotFound(Lang)), q.From, q.To, ct);
                (from, to) = (s.From, s.To);
                headers = H("date", "number", "type", "description", "counterparty", "amount", "balance", "document");
                rows = [[L("opening"), null, null, null, null, null, s.Opening, null]];
                rows.AddRange(s.Rows.Select(r => (IReadOnlyList<object?>)[Dt(r.Date), r.Number, L("type." + r.Type), r.Description, r.CounterpartyName, r.Amount, r.BalanceAfter, r.DocumentNumber]));
                rows.Add([L("closing"), null, null, null, null, null, s.Closing, null]);
                break;
            }
            case "pnl":
            {
                var p = await reports.PnlAsync(q.StoreIds, q.From, q.To, compare, ct);
                (from, to) = (p.From, p.To);
                headers = H("metric", "amount", "percentOfRevenue", "previous", "change", "changePercent");
                rows = p.Rows.Select(r => (IReadOnlyList<object?>)[(r.Level > 0 ? "   " : "") + (r.Label ?? L("pnl." + r.Key)), r.Amount, r.PercentOfRevenue, r.Previous, r.Change, r.ChangePercent]).ToList();
                break;
            }
            case "cashflow":
            {
                var cf = await reports.CashFlowAsync(q.StoreIds, q.From, q.To, q.AccountId, ct);
                (from, to) = (cf.From, cf.To);
                headers = H("date", "inflow", "outflow", "balance");
                rows = [[L("opening"), null, null, cf.Opening]];
                rows.AddRange(cf.Days.Select(d => (IReadOnlyList<object?>)[d.Date.ToString("dd.MM.yyyy"), d.Inflow, d.Outflow, d.Balance]));
                rows.Add([L("closing"), cf.Inflow, cf.Outflow, cf.Closing]);
                break;
            }
            case "product-profit":
            {
                var pp = await reports.ProductProfitAsync(q.StoreIds, q.From, q.To, q.Search, null, null, q.Sort, q.Dir, 1, MaxRows, ct);
                headers = H("product", "article", "category", "brand", "sold", "returned", "netQty", "netRevenue", "cost", "profit", "margin", "averagePrice");
                rows = pp.Rows.Select(r => (IReadOnlyList<object?>)[r.Name, r.Article, r.Category, r.Brand, r.Sold, r.Returned, r.NetQty, r.NetRevenue,
                    r.CostComplete ? r.Cost : L("notCalculated"), r.CostComplete ? r.Profit : L("notCalculated"), r.Margin, r.AveragePrice]).ToList();
                break;
            }
            case "customer-debts":
            {
                var d = await debts.CustomerDebtsAsync(q.StoreIds, q.Status, q.Search, 1, 100, ct);
                var all = await debts.CustomerDebtsAsync(q.StoreIds, q.Status, q.Search, 1, Math.Max(1, d.RowsTotal), ct);
                headers = H("customer", "phone", "debt", "overdue", "nextDue", "documents", "status");
                rows = all.Rows.Select(r => (IReadOnlyList<object?>)[r.Name, r.Phone, r.Total, r.Overdue, r.NextDue?.ToString("dd.MM.yyyy"), (decimal)r.Documents, L("debt." + r.Status)]).ToList();
                break;
            }
            case "supplier-debts":
            {
                var d = await debts.SupplierDebtsAsync(q.StoreIds, q.From, q.To, q.Status, q.Search, 1, 100, ct);
                var all = await debts.SupplierDebtsAsync(q.StoreIds, q.From, q.To, q.Status, q.Search, 1, Math.Max(1, d.RowsTotal), ct);
                headers = H("supplier", "phone", "lastReceipt", "purchases", "paid", "debt", "overdue", "nextDue", "documents", "status");
                rows = all.Rows.Select(r => (IReadOnlyList<object?>)[r.Name, r.Phone, r.LastReceipt is { } lr ? Dt(lr) : null, r.Purchases, r.Paid, r.Debt, r.Overdue,
                    r.NextDue?.ToString("dd.MM.yyyy"), (decimal)r.Documents, L("debt." + r.Status)]).ToList();
                break;
            }
            case "reconciliation":
            {
                var rc = await debts.SupplierReconciliationAsync(id ?? throw new ValidationException(Messages.SupplierNotFound(Lang)), q.StoreIds, q.From, q.To, ct);
                (from, to) = (rc.From, rc.To);
                headers = H("date", "kind", "number", "description", "debit", "credit", "balance");
                rows = [[L("opening"), null, null, null, null, null, rc.Opening]];
                rows.AddRange(rc.Rows.Select(r => (IReadOnlyList<object?>)[Dt(r.Date), L("kind." + r.Kind), r.Number, r.Description, r.Debit, r.Credit, r.Balance]));
                rows.Add([L("closing"), null, null, null, rc.Debit, rc.Credit, rc.Closing]);
                break;
            }
            default: throw new NotFoundException(Messages.AnalyticsTabUnknown(Lang));
        }
        var name = $"finance_{report}_{from?.ToString("yyyy-MM-dd") ?? "all"}_{to?.ToString("yyyy-MM-dd") ?? "all"}";
        access.Audit(orgId, m.StoreId, "finance.export", "finance", m.StoreId, null, new { report, Format = csv ? "csv" : "xlsx", from, to, Rows = rows.Count });
        if (csv) return (AnalyticsService.Csv(headers, rows), name + ".csv", "text/csv");
        return (spreadsheet.Write(L("report." + report), headers, rows), name + ".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    private static string Dt(DateTime utc) => utc.ToString("dd.MM.yyyy HH:mm") + " UTC";
    private IReadOnlyList<string> H(params string[] keys) => keys.Select(L).ToList();
    private string L(string key) => (Lang == AppLanguage.Ru ? Ru : Kk).TryGetValue(key, out var v) ? v : key;

    private static readonly Dictionary<string, string> Ru = new()
    {
        ["number"] = "Номер", ["date"] = "Дата", ["type"] = "Тип", ["status"] = "Статус", ["description"] = "Описание", ["counterparty"] = "Контрагент",
        ["fromAccount"] = "Счёт откуда", ["toAccount"] = "Счёт куда", ["amount"] = "Сумма", ["category"] = "Категория", ["document"] = "Документ", ["source"] = "Источник",
        ["author"] = "Ответственный", ["balance"] = "Остаток", ["opening"] = "Остаток на начало", ["closing"] = "Остаток на конец", ["metric"] = "Показатель",
        ["percentOfRevenue"] = "% от выручки", ["previous"] = "Прошлый период", ["change"] = "Изменение", ["changePercent"] = "Изменение, %", ["inflow"] = "Поступления",
        ["outflow"] = "Выплаты", ["product"] = "Товар", ["article"] = "Артикул", ["brand"] = "Бренд", ["sold"] = "Продано", ["returned"] = "Возвращено",
        ["netQty"] = "Чистое кол-во", ["netRevenue"] = "Чистая выручка", ["cost"] = "Себестоимость", ["profit"] = "Прибыль", ["margin"] = "Маржа, %",
        ["averagePrice"] = "Средняя цена", ["notCalculated"] = "Не рассчитана", ["customer"] = "Клиент", ["phone"] = "Телефон", ["debt"] = "Долг",
        ["overdue"] = "Просрочено", ["nextDue"] = "Ближайший платёж", ["documents"] = "Документов", ["supplier"] = "Поставщик", ["lastReceipt"] = "Последняя поставка",
        ["purchases"] = "Закупки", ["paid"] = "Оплачено", ["kind"] = "Вид", ["debit"] = "Поставки", ["credit"] = "Оплаты",
        ["type.Income"] = "Поступление", ["type.Expense"] = "Расход", ["type.Transfer"] = "Перемещение", ["type.Collection"] = "Инкассация", ["type.Adjustment"] = "Корректировка",
        ["type.SalePayment"] = "Оплата чека", ["type.Refund"] = "Возврат клиенту", ["type.CustomerPayment"] = "Оплата от клиента", ["type.SupplierPayment"] = "Оплата поставщику",
        ["status.Draft"] = "Черновик", ["status.Posted"] = "Проведён", ["status.Reversed"] = "Сторнирован",
        ["debt.Open"] = "В срок", ["debt.Overdue"] = "Просрочка", ["debt.Partial"] = "Частично оплачено", ["debt.Paid"] = "Погашено",
        ["kind.receipt"] = "Поставка", ["kind.payment"] = "Оплата",
        ["pnl.revenue"] = "Выручка от продаж", ["pnl.discounts"] = "Скидки", ["pnl.bonusRedeemed"] = "Оплачено бонусами", ["pnl.returns"] = "Возвраты",
        ["pnl.netRevenue"] = "Чистая выручка", ["pnl.cogs"] = "Себестоимость проданных товаров", ["pnl.grossProfit"] = "Валовая прибыль",
        ["pnl.operatingExpenses"] = "Операционные расходы", ["pnl.otherIncome"] = "Прочие доходы", ["pnl.otherExpenses"] = "Прочие расходы",
        ["pnl.profitBeforeTax"] = "Прибыль до налогов", ["pnl.taxes"] = "Налоги", ["pnl.netProfit"] = "Чистая прибыль",
        ["report.operations"] = "Операции", ["report.statement"] = "Выписка", ["report.pnl"] = "P&L", ["report.cashflow"] = "Cash Flow", ["report.product-profit"] = "Прибыль по товарам",
        ["report.customer-debts"] = "Долги клиентов", ["report.supplier-debts"] = "Долги поставщикам", ["report.reconciliation"] = "Акт сверки",
    };

    private static readonly Dictionary<string, string> Kk = new()
    {
        ["number"] = "Нөмір", ["date"] = "Күні", ["type"] = "Түрі", ["status"] = "Мәртебе", ["description"] = "Сипаттама", ["counterparty"] = "Контрагент",
        ["fromAccount"] = "Қай шоттан", ["toAccount"] = "Қай шотқа", ["amount"] = "Сома", ["category"] = "Санат", ["document"] = "Құжат", ["source"] = "Көзі",
        ["author"] = "Жауапты", ["balance"] = "Қалдық", ["opening"] = "Басындағы қалдық", ["closing"] = "Соңындағы қалдық", ["metric"] = "Көрсеткіш",
        ["percentOfRevenue"] = "Түсімнен %", ["previous"] = "Өткен кезең", ["change"] = "Өзгеріс", ["changePercent"] = "Өзгеріс, %", ["inflow"] = "Түсімдер",
        ["outflow"] = "Төлемдер", ["product"] = "Тауар", ["article"] = "Артикул", ["brand"] = "Бренд", ["sold"] = "Сатылды", ["returned"] = "Қайтарылды",
        ["netQty"] = "Таза саны", ["netRevenue"] = "Таза түсім", ["cost"] = "Өзіндік құн", ["profit"] = "Пайда", ["margin"] = "Маржа, %",
        ["averagePrice"] = "Орташа баға", ["notCalculated"] = "Есептелмеген", ["customer"] = "Клиент", ["phone"] = "Телефон", ["debt"] = "Қарыз",
        ["overdue"] = "Мерзімі өткен", ["nextDue"] = "Жақын төлем", ["documents"] = "Құжат", ["supplier"] = "Жеткізуші", ["lastReceipt"] = "Соңғы жеткізу",
        ["purchases"] = "Сатып алу", ["paid"] = "Төленді", ["kind"] = "Түрі", ["debit"] = "Жеткізу", ["credit"] = "Төлем",
        ["type.Income"] = "Түсім", ["type.Expense"] = "Шығыс", ["type.Transfer"] = "Аударым", ["type.Collection"] = "Инкассация", ["type.Adjustment"] = "Түзету",
        ["type.SalePayment"] = "Чек төлемі", ["type.Refund"] = "Клиентке қайтарым", ["type.CustomerPayment"] = "Клиенттен төлем", ["type.SupplierPayment"] = "Жеткізушіге төлем",
        ["status.Draft"] = "Черновик", ["status.Posted"] = "Проведён", ["status.Reversed"] = "Сторно",
        ["debt.Open"] = "Мерзімінде", ["debt.Overdue"] = "Мерзімі өткен", ["debt.Partial"] = "Ішінара төленді", ["debt.Paid"] = "Өтелді",
        ["kind.receipt"] = "Жеткізу", ["kind.payment"] = "Төлем",
        ["pnl.revenue"] = "Сатылым түсімі", ["pnl.discounts"] = "Жеңілдіктер", ["pnl.bonusRedeemed"] = "Бонуспен төленді", ["pnl.returns"] = "Қайтарымдар",
        ["pnl.netRevenue"] = "Таза түсім", ["pnl.cogs"] = "Сатылған тауардың өзіндік құны", ["pnl.grossProfit"] = "Жалпы пайда",
        ["pnl.operatingExpenses"] = "Операциялық шығын", ["pnl.otherIncome"] = "Басқа кіріс", ["pnl.otherExpenses"] = "Басқа шығын",
        ["pnl.profitBeforeTax"] = "Салыққа дейінгі пайда", ["pnl.taxes"] = "Салықтар", ["pnl.netProfit"] = "Таза пайда",
        ["report.operations"] = "Операциялар", ["report.statement"] = "Выписка", ["report.pnl"] = "P&L", ["report.cashflow"] = "Cash Flow", ["report.product-profit"] = "Тауар пайдасы",
        ["report.customer-debts"] = "Клиент қарызы", ["report.supplier-debts"] = "Жеткізуші қарызы", ["report.reconciliation"] = "Салыстыру актісі",
    };
}
