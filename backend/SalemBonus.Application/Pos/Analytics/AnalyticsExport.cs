using System.Globalization;
using System.Text;
using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Domain.Core;

namespace SalemBonus.Application.Pos.Analytics;

/// <summary>
/// Экспорт (ТЗ «Статистика» §6, §12): белсенді вкладка ағымдағы сүзгілермен — Excel не CSV. Құқық: statistics.export;
/// телефон тек customers.contacts-пен (жоқ болса, жасырылған күйі шығады), ақшалай бағандар — finance.view-мен (вкладка өзі сүзеді).
/// PDF — браузерде басып шығару терезесі арқылы (фронт).
/// </summary>
public partial class AnalyticsService
{
    public async Task<(byte[] Content, string FileName, string ContentType)> ExportAsync(string tab, string? format, AnalyticsQuery q, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.StatisticsExport, ct);
        var csv = string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase);
        IReadOnlyList<string> headers;
        List<IReadOnlyList<object?>> rows;
        AnalyticsMetaDto meta;
        if (tab == "overview")
        {
            var o = await OverviewAsync(q, ct);
            meta = o.Meta;
            headers = [Label("metric"), Label("value"), Label("amount"), Label("previous"), Label("changePercent")];
            rows = o.Kpis.Where(k => k.Available).Select(k => (IReadOnlyList<object?>)[Label(k.Key), k.Value, k.Amount, k.Previous, k.ChangePercent]).ToList();
        }
        else
        {
            var t = await TabAsync(tab, q, all: true, ct);
            meta = t.Meta;
            var columns = t.Table?.Columns ?? [];
            headers = columns.Select(c => Label(c.Key)).ToList();
            rows = (t.Table?.Rows ?? []).Select(r => (IReadOnlyList<object?>)columns.Select(c => Cell(c, r)).ToList()).ToList();
        }

        var name = $"statistics_{tab}_{meta.From:yyyy-MM-dd}_{meta.To:yyyy-MM-dd}";
        access.Audit(orgId, m.StoreId, "statistics.export", "statistics", m.StoreId, null,
            new { tab, Format = csv ? "csv" : "xlsx", meta.From, meta.To, Stores = meta.Stores.Select(s => s.Id), Rows = rows.Count });
        if (csv) return (Csv(headers, rows), name + ".csv", "text/csv");
        return (spreadsheet.Write(Label("tab." + tab), headers, rows), name + ".xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    private object? Cell(ColumnDto c, Dictionary<string, object?> row)
    {
        var v = row.GetValueOrDefault(c.Key);
        return c.Kind switch
        {
            "period" => row["bucketStart"] is DateOnly s && row["bucketEnd"] is DateOnly e
                ? s == e ? s.ToString("dd.MM.yyyy") : $"{s:dd.MM.yyyy} – {e:dd.MM.yyyy}" : null,
            "datetime" => v is DateTime d ? d.ToString("dd.MM.yyyy HH:mm") + " UTC" : null,
            "enum" => v is string s2 ? Label("enum." + s2) : null,
            "int" => v is int i ? (decimal)i : v,
            _ => v,
        };
    }

    /// <summary>CSV: UTF-8 BOM, «;» бөлгіш (Excel орыс локалінде дұрыс ашылады), бөлшек — нүктемен.</summary>
    internal static byte[] Csv(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<object?>> rows)
    {
        static string Esc(object? v)
        {
            var s = v switch
            {
                null => string.Empty,
                decimal d => d.ToString(CultureInfo.InvariantCulture),
                IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
                _ => v.ToString() ?? string.Empty,
            };
            return s.IndexOfAny([';', '"', '\n', '\r']) >= 0 ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
        }
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(';', headers.Select(Esc)));
        foreach (var r in rows) sb.AppendLine(string.Join(';', r.Select(Esc)));
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(sb.ToString())];
    }

    private string Label(string key) => (Lang == AppLanguage.Ru ? Ru : Kk).TryGetValue(key, out var v) ? v : key;

    private static readonly Dictionary<string, string> Ru = new()
    {
        ["metric"] = "Показатель", ["value"] = "Значение", ["amount"] = "Сумма", ["previous"] = "Прошлый период", ["changePercent"] = "Изменение, %",
        ["revenue"] = "Выручка", ["salesCount"] = "Количество продаж", ["averageCheck"] = "Средний чек", ["returns"] = "Возвраты", ["netSales"] = "Чистые продажи",
        ["customersInBase"] = "Клиенты в базе", ["repeatCustomers"] = "Повторные клиенты", ["bonusRedeemed"] = "Использовано бонусов",
        ["stock"] = "Остаток на складах, ед.", ["customerDebt"] = "Задолженность клиентов", ["discounts"] = "Скидки",
        ["tab.overview"] = "Обзор", ["tab.sales"] = "Продажи", ["tab.products"] = "Товары", ["tab.inventory"] = "Склад", ["tab.suppliers"] = "Поставщики",
        ["tab.finance"] = "Финансы", ["tab.bonus"] = "Бонус", ["tab.customers"] = "Клиенты", ["tab.employees"] = "Сотрудники", ["tab.registers"] = "Касса",
        ["bucketStart"] = "Период", ["receipts"] = "Чеков", ["net"] = "Чистые продажи", ["average"] = "Средний чек",
        ["name"] = "Товар", ["article"] = "Артикул", ["category"] = "Категория", ["brand"] = "Бренд", ["sold"] = "Продано", ["returned"] = "Возвращено",
        ["netQty"] = "Чистое количество", ["discount"] = "Скидки", ["cost"] = "Себестоимость", ["profit"] = "Валовая прибыль",
        ["warehouse"] = "Склад", ["unit"] = "Ед.", ["received"] = "Приход", ["transferIn"] = "Перемещение (приход)", ["transferOut"] = "Перемещение (расход)",
        ["writeOff"] = "Списание", ["returnsIn"] = "Возврат на склад", ["lastMovement"] = "Последнее движение",
        ["supplier"] = "Поставщик", ["documents"] = "Документов", ["purchases"] = "Закупки", ["paid"] = "Оплачено", ["debt"] = "Долг", ["lastReceipt"] = "Последняя приёмка",
        ["type"] = "Тип", ["method"] = "Способ", ["operations"] = "Операций",
        ["store"] = "Магазин", ["points"] = "Бонусы", ["sales"] = "Продаж", ["lastAt"] = "Последняя операция",
        ["customer"] = "Клиент", ["phone"] = "Телефон", ["purchasesTotal"] = "Покупок всего", ["lastPurchase"] = "Последняя покупка",
        ["employee"] = "Сотрудник", ["returnsAmount"] = "Сумма возвратов", ["discountReceipts"] = "Чеков со скидкой",
        ["register"] = "Касса", ["cashiers"] = "Кассиров", ["lastOperation"] = "Последняя операция",
        ["enum.salePayment"] = "Оплата продажи", ["enum.debtRepayment"] = "Погашение долга", ["enum.refund"] = "Возврат денег", ["enum.supplierPayment"] = "Оплата поставщику",
        ["enum.Cash"] = "Наличные", ["enum.Card"] = "Карта", ["enum.Qr"] = "QR", ["enum.Transfer"] = "Перевод", ["enum.Debt"] = "Долг",
        ["enum.Accrual"] = "Начисление", ["enum.Redemption"] = "Списание", ["enum.Birthday"] = "День рождения", ["enum.Promo"] = "Акция",
        ["enum.Expiration"] = "Сгорание", ["enum.ReturnRestore"] = "Возврат списанных", ["enum.ReturnReversal"] = "Отмена начисления",
    };

    private static readonly Dictionary<string, string> Kk = new()
    {
        ["metric"] = "Көрсеткіш", ["value"] = "Мәні", ["amount"] = "Сома", ["previous"] = "Өткен кезең", ["changePercent"] = "Өзгеріс, %",
        ["revenue"] = "Түсім", ["salesCount"] = "Сатылым саны", ["averageCheck"] = "Орташа чек", ["returns"] = "Қайтарымдар", ["netSales"] = "Таза сатылым",
        ["customersInBase"] = "Базадағы клиенттер", ["repeatCustomers"] = "Қайталама клиенттер", ["bonusRedeemed"] = "Жұмсалған бонус",
        ["stock"] = "Қоймадағы қалдық, бірлік", ["customerDebt"] = "Клиент қарызы", ["discounts"] = "Жеңілдіктер",
        ["tab.overview"] = "Шолу", ["tab.sales"] = "Сатылымдар", ["tab.products"] = "Тауарлар", ["tab.inventory"] = "Қойма", ["tab.suppliers"] = "Жеткізушілер",
        ["tab.finance"] = "Қаржы", ["tab.bonus"] = "Бонус", ["tab.customers"] = "Клиенттер", ["tab.employees"] = "Қызметкерлер", ["tab.registers"] = "Касса",
        ["bucketStart"] = "Кезең", ["receipts"] = "Чек", ["net"] = "Таза сатылым", ["average"] = "Орташа чек",
        ["name"] = "Тауар", ["article"] = "Артикул", ["category"] = "Санат", ["brand"] = "Бренд", ["sold"] = "Сатылды", ["returned"] = "Қайтарылды",
        ["netQty"] = "Таза саны", ["discount"] = "Жеңілдік", ["cost"] = "Өзіндік құн", ["profit"] = "Жалпы пайда",
        ["warehouse"] = "Қойма", ["unit"] = "Бірл.", ["received"] = "Кіріс", ["transferIn"] = "Ауыстыру (кіріс)", ["transferOut"] = "Ауыстыру (шығыс)",
        ["writeOff"] = "Есептен шығару", ["returnsIn"] = "Қоймаға қайтарым", ["lastMovement"] = "Соңғы қозғалыс",
        ["supplier"] = "Жеткізуші", ["documents"] = "Құжат", ["purchases"] = "Сатып алу", ["paid"] = "Төленді", ["debt"] = "Қарыз", ["lastReceipt"] = "Соңғы кіріс",
        ["type"] = "Түрі", ["method"] = "Тәсілі", ["operations"] = "Операция",
        ["store"] = "Дүкен", ["points"] = "Бонус", ["sales"] = "Сатылым", ["lastAt"] = "Соңғы операция",
        ["customer"] = "Клиент", ["phone"] = "Телефон", ["purchasesTotal"] = "Барлық сатып алу", ["lastPurchase"] = "Соңғы сатып алу",
        ["employee"] = "Қызметкер", ["returnsAmount"] = "Қайтарым сомасы", ["discountReceipts"] = "Жеңілдікті чек",
        ["register"] = "Касса", ["cashiers"] = "Кассир", ["lastOperation"] = "Соңғы операция",
        ["enum.salePayment"] = "Сатылым төлемі", ["enum.debtRepayment"] = "Қарыз өтеу", ["enum.refund"] = "Ақша қайтару", ["enum.supplierPayment"] = "Жеткізушіге төлем",
        ["enum.Cash"] = "Қолма-қол", ["enum.Card"] = "Карта", ["enum.Qr"] = "QR", ["enum.Transfer"] = "Аударым", ["enum.Debt"] = "Қарыз",
        ["enum.Accrual"] = "Есептеу", ["enum.Redemption"] = "Шегеру", ["enum.Birthday"] = "Туған күн", ["enum.Promo"] = "Акция",
        ["enum.Expiration"] = "Күйіп кету", ["enum.ReturnRestore"] = "Шегерілгенін қайтару", ["enum.ReturnReversal"] = "Есептеуді алу",
    };
}
