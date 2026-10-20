using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Application.Pos.Analytics;
using SalemBonus.Domain.Core;

namespace SalemBonus.Application.Pos.Bonus;

/// <summary>
/// Экспорт (ТЗ «Бонусы» §4.3, §7.4, §8.2): ағымдағы сүзгі бойынша Excel (нақты сандар) не CSV; телефон — құқыққа қарай жасырын (§16).
/// Құқық: bonus.view + statistics.export; аудитке жазылады.
/// </summary>
public class BonusExport(
    BonusAccess access,
    BonusOperationsService operations,
    BonusSettingsService settings,
    BonusStatusService statuses,
    BonusReportsService reports,
    BonusCampaignService campaigns,
    IBonusRepository repo,
    ISpreadsheet spreadsheet)
{
    public const int MaxRows = 20_000;
    private AppLanguage Lang => access.Lang;

    public async Task<(byte[] Content, string FileName, string ContentType)> ExportAsync(string report, string? format, BonusListQuery q, int days, Guid? id,
        Guid? statusId, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.BonusView, ct, q.StoreIds, q.From, q.To);
        access.Require(c.Can.Export);
        var csv = string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase);
        IReadOnlyList<string> headers;
        List<IReadOnlyList<object?>> rows;
        switch (report)
        {
            case "operations":
            {
                var (ctx, f) = await operations.FilterAsync(q, 0, MaxRows, ct);
                var page = await repo.OperationsAsync(f, ct);
                var mapper = new BonusMapper(repo, access);
                var list = await mapper.OperationsAsync(page.Rows, ctx.Can.Contacts, ct);
                headers = H("date", "customer", "phone", "type", "amount", "balanceAfter", "basis", "source", "author", "status", "comment", "store");
                rows = list.Select(o => (IReadOnlyList<object?>)[Dt(o.CreatedAt, c), o.CustomerName, o.Phone, L("type." + o.Type), (decimal)o.Amount,
                    o.BalanceAfter is { } b ? (decimal)b : null, o.Basis, L("source." + o.Source), o.Author, L("status." + o.Status), o.Comment, o.StoreName]).ToList();
                break;
            }
            case "expiring":
            {
                var page = await settings.ExpiringAsync(days, q.From, q.To, q.Search, statusId, q.StoreIds, 1, 100, ct);
                var all = await settings.ExpiringAsync(days, q.From, q.To, q.Search, statusId, q.StoreIds, 1, Math.Max(1, Math.Min(page.Total, MaxRows)), ct);
                headers = H("expiresAt", "customer", "phone", "statusName", "amount", "original", "accruedAt", "notification");
                rows = all.Items.Select(x => (IReadOnlyList<object?>)[Dt(x.ExpiresAt, c), x.CustomerName, x.Phone, x.StatusName, (decimal)x.Amount, (decimal)x.Original,
                    Dt(x.AccruedAt, c), L("notification." + x.Notification)]).ToList();
                break;
            }
            case "birthdays":
            {
                var page = await settings.UpcomingBirthdaysAsync(days, statusId, null, q.Search, 1, 100, ct);
                headers = H("birthday", "customer", "phone", "statusName", "amount", "plannedDate", "state");
                var all = await settings.UpcomingBirthdaysAsync(days, statusId, null, q.Search, 1, Math.Max(1, Math.Min(page.Total, MaxRows)), ct);
                rows = all.Items.Select(x => (IReadOnlyList<object?>)[x.Birthday.ToString("dd.MM.yyyy"), x.CustomerName, x.Phone, x.StatusName, (decimal)x.Amount,
                    x.PlannedDate.ToString("dd.MM.yyyy"), L("birthday." + x.State)]).ToList();
                break;
            }
            case "customers":
            {
                var page = await statuses.CustomersAsync(q.Search, statusId, false, null, null, 1, 100, ct);
                var list = new List<BonusCustomerDto>(page.Items);
                for (var p = 2; list.Count < Math.Min(page.Total, MaxRows); p++) list.AddRange((await statuses.CustomersAsync(q.Search, statusId, false, null, null, p, 100, ct)).Items);
                headers = H("customer", "phone", "statusName", "balance", "totalSpent", "registeredAt", "lastOperation");
                rows = list.Select(x => (IReadOnlyList<object?>)[x.Name, x.Phone, x.StatusName, (decimal)x.Balance, x.TotalSpent, Dt(x.RegisteredAt, c),
                    x.LastOperationAt is { } lo ? Dt(lo, c) : null]).ToList();
                break;
            }
            case "statistics":
            {
                var s = await reports.StatisticsAsync(q.StoreIds, q.From, q.To, statusId, q.Type, ct);
                headers = H("date", "accrued", "used", "returned", "expired");
                rows = s.Days.Select(d => (IReadOnlyList<object?>)[d.Date.ToString("dd.MM.yyyy"), (decimal)d.Accrued, (decimal)d.Used, (decimal)d.Returned, (decimal)d.Expired]).ToList();
                rows.Add([L("total"), (decimal)s.Days.Sum(d => d.Accrued), (decimal)s.Days.Sum(d => d.Used), (decimal)s.Days.Sum(d => d.Returned), (decimal)s.Days.Sum(d => d.Expired)]);
                break;
            }
            case "recipients":
            {
                if (id is not { } cid) throw new ValidationException(Messages.BonusCampaignNotFound(Lang));
                var page = await campaigns.RecipientsAsync(cid, null, 1, 100, ct);
                var list = new List<CampaignRecipientDto>(page.Items);
                for (var p = 2; list.Count < Math.Min(page.Total, MaxRows); p++) list.AddRange((await campaigns.RecipientsAsync(cid, null, p, 100, ct)).Items);
                headers = H("customer", "phone", "delivery", "reason", "sentAt", "read");
                rows = list.Select(x => (IReadOnlyList<object?>)[x.CustomerName, x.Contact, L("recipient." + x.Status), x.Reason is null ? null : L("reason." + x.Reason),
                    x.SentAt is { } s ? Dt(s, c) : null, x.Read ? L("yes") : L("no")]).ToList();
                break;
            }
            default: throw new NotFoundException(Messages.AnalyticsTabUnknown(Lang));
        }
        var name = $"bonus_{report}_{DateTime.UtcNow:yyyy-MM-dd}";
        access.Audit(c, "bonus.export", "bonus", c.Store.Id, null, new { report, Format = csv ? "csv" : "xlsx", Rows = rows.Count });
        await Task.CompletedTask;
        if (csv) return (AnalyticsService.Csv(headers, rows), name + ".csv", "text/csv");
        return (spreadsheet.Write(L("report." + report), headers, rows), name + ".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    private static string Dt(DateTime utc, BonusCtx c) => TimeZoneInfo.ConvertTimeFromUtc(utc, c.Zone).ToString("dd.MM.yyyy HH:mm");
    private IReadOnlyList<string> H(params string[] keys) => keys.Select(L).ToList();
    private string L(string key) => (Lang == AppLanguage.Ru ? Ru : Kk).TryGetValue(key, out var v) ? v : key;

    private static readonly Dictionary<string, string> Ru = new()
    {
        ["date"] = "Дата", ["customer"] = "Клиент", ["phone"] = "Телефон", ["type"] = "Тип операции", ["amount"] = "Сумма бонусов", ["balanceAfter"] = "Баланс после",
        ["basis"] = "Основание", ["source"] = "Источник", ["author"] = "Автор", ["status"] = "Статус", ["comment"] = "Комментарий", ["store"] = "Магазин",
        ["expiresAt"] = "Дата сгорания", ["statusName"] = "Статус клиента", ["original"] = "Начислено в лоте", ["accruedAt"] = "Дата начисления", ["notification"] = "Уведомление",
        ["birthday"] = "День рождения", ["plannedDate"] = "Дата начисления", ["state"] = "Состояние", ["balance"] = "Баланс", ["totalSpent"] = "Сумма покупок",
        ["registeredAt"] = "Регистрация", ["lastOperation"] = "Последняя операция", ["accrued"] = "Начислено", ["used"] = "Использовано", ["returned"] = "Возвращено",
        ["expired"] = "Сгорело", ["total"] = "Итого", ["delivery"] = "Статус доставки", ["reason"] = "Причина", ["sentAt"] = "Отправлено", ["read"] = "Прочитано",
        ["yes"] = "Да", ["no"] = "Нет",
        ["type.Accrual"] = "Начисление", ["type.Redemption"] = "Использование", ["type.Birthday"] = "Начисление (день рождения)", ["type.Promo"] = "Начисление (рассылка)",
        ["type.Expiration"] = "Сгорание", ["type.ReturnRestore"] = "Возврат бонусов", ["type.ReturnReversal"] = "Отмена начисления (возврат)", ["type.Adjustment"] = "Корректировка",
        ["type.Reversal"] = "Сторно",
        ["source.sale"] = "Касса", ["source.return"] = "Возврат", ["source.pos"] = "Терминал", ["source.birthday"] = "День рождения", ["source.campaign"] = "Рассылка",
        ["source.manual"] = "Вручную", ["source.expiry"] = "Фоновая задача", ["source.reversal"] = "Сторно", ["source.legacy"] = "Система",
        ["status.posted"] = "Проведена", ["status.reversed"] = "Сторнирована",
        ["notification.none"] = "Не требуется", ["notification.scheduled"] = "Запланировано", ["notification.sent"] = "Отправлено",
        ["birthday.pending"] = "Ожидает", ["birthday.granted"] = "Начислено", ["birthday.status"] = "Не подходит по статусу", ["birthday.purchases"] = "Условие покупок не выполнено",
        ["birthday.excluded"] = "Исключён", ["birthday.disabled"] = "Правило выключено", ["birthday.noBirthDate"] = "Нет даты рождения", ["birthday.manual"] = "Начислено вручную",
        ["recipient.Pending"] = "В очереди", ["recipient.Sent"] = "Доставлено", ["recipient.Failed"] = "Ошибка", ["recipient.Excluded"] = "Исключён", ["recipient.Cancelled"] = "Отменён",
        ["reason.optOut"] = "Отказ от рассылок", ["reason.noContact"] = "Нет контакта", ["reason.noCard"] = "Нет бонусной карты",
        ["report.operations"] = "История операций", ["report.expiring"] = "Скоро сгорят", ["report.birthdays"] = "Дни рождения", ["report.customers"] = "Клиенты",
        ["report.statistics"] = "Статистика", ["report.recipients"] = "Получатели",
    };

    private static readonly Dictionary<string, string> Kk = new()
    {
        ["date"] = "Күні", ["customer"] = "Клиент", ["phone"] = "Телефон", ["type"] = "Операция түрі", ["amount"] = "Бонус", ["balanceAfter"] = "Кейінгі баланс",
        ["basis"] = "Негізі", ["source"] = "Көзі", ["author"] = "Автор", ["status"] = "Мәртебе", ["comment"] = "Пікір", ["store"] = "Дүкен",
        ["expiresAt"] = "Жану күні", ["statusName"] = "Клиент мәртебесі", ["original"] = "Партиядағы бонус", ["accruedAt"] = "Есептелген күні", ["notification"] = "Ескерту",
        ["birthday"] = "Туған күні", ["plannedDate"] = "Есептеу күні", ["state"] = "Күйі", ["balance"] = "Баланс", ["totalSpent"] = "Сатып алу сомасы",
        ["registeredAt"] = "Тіркелген", ["lastOperation"] = "Соңғы операция", ["accrued"] = "Есептелді", ["used"] = "Жұмсалды", ["returned"] = "Қайтарылды",
        ["expired"] = "Жанды", ["total"] = "Барлығы", ["delivery"] = "Жеткізу", ["reason"] = "Себебі", ["sentAt"] = "Жіберілді", ["read"] = "Оқылды",
        ["yes"] = "Иә", ["no"] = "Жоқ",
        ["type.Accrual"] = "Есептеу", ["type.Redemption"] = "Жұмсау", ["type.Birthday"] = "Есептеу (туған күн)", ["type.Promo"] = "Есептеу (тарату)",
        ["type.Expiration"] = "Жану", ["type.ReturnRestore"] = "Бонус қайтару", ["type.ReturnReversal"] = "Есептеуді алу (қайтарым)", ["type.Adjustment"] = "Түзету",
        ["type.Reversal"] = "Сторно",
        ["source.sale"] = "Касса", ["source.return"] = "Қайтарым", ["source.pos"] = "Терминал", ["source.birthday"] = "Туған күн", ["source.campaign"] = "Тарату",
        ["source.manual"] = "Қолмен", ["source.expiry"] = "Фондық тапсырма", ["source.reversal"] = "Сторно", ["source.legacy"] = "Жүйе",
        ["status.posted"] = "Өткізілді", ["status.reversed"] = "Сторноланды",
        ["notification.none"] = "Қажет емес", ["notification.scheduled"] = "Жоспарланған", ["notification.sent"] = "Жіберілді",
        ["birthday.pending"] = "Күтуде", ["birthday.granted"] = "Есептелді", ["birthday.status"] = "Мәртебе сәйкес емес", ["birthday.purchases"] = "Сатып алу шарты орындалмаған",
        ["birthday.excluded"] = "Алынып тасталған", ["birthday.disabled"] = "Ереже өшірулі", ["birthday.noBirthDate"] = "Туған күні жоқ", ["birthday.manual"] = "Қолмен есептелді",
        ["recipient.Pending"] = "Кезекте", ["recipient.Sent"] = "Жеткізілді", ["recipient.Failed"] = "Қате", ["recipient.Excluded"] = "Алынып тасталды", ["recipient.Cancelled"] = "Тоқтатылды",
        ["reason.optOut"] = "Таратудан бас тартқан", ["reason.noContact"] = "Байланыс жоқ", ["reason.noCard"] = "Бонус картасы жоқ",
        ["report.operations"] = "Операциялар тарихы", ["report.expiring"] = "Жанатын бонустар", ["report.birthdays"] = "Туған күндер", ["report.customers"] = "Клиенттер",
        ["report.statistics"] = "Статистика", ["report.recipients"] = "Алушылар",
    };
}
