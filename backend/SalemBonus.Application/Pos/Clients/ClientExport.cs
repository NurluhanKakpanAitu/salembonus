using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Application.Pos.Analytics;
using SalemBonus.Domain.Core;

namespace SalemBonus.Application.Pos.Clients;

/// <summary>
/// Экспорт (ТЗ «Клиенты» §19.2): ағымдағы сүзгі, сегмент не таңдалғандар бойынша; бағандарды пайдаланушы таңдайды.
/// Жеке деректер (телефон, e-mail, мекенжай, туған күн) — тек customers.contacts құқығымен; құқық — clients.export; аудитке жазылады.
/// </summary>
public class ClientExport(ClientAccess access, ClientService reader, IClientRepository repo, ISpreadsheet spreadsheet)
{
    public const int MaxRows = 50_000;
    private AppLanguage Lang => access.Lang;

    public static readonly string[] DefaultFields =
        ["number", "name", "phone", "status", "purchases", "amount", "average", "lastPurchase", "bonus", "debt", "registered"];

    private static readonly string[] AllFields =
    [
        "number", "lastName", "firstName", "middleName", "name", "phone", "email", "birthDate", "gender", "status", "purchases", "amount", "average",
        "lastPurchase", "bonus", "debt", "overdue", "registered", "source", "activity", "consentApp", "consentSms", "consentWhatsApp", "consentEmail",
        "katoCode", "address", "comment",
    ];

    private static readonly HashSet<string> Personal = ["phone", "email", "birthDate", "address"];

    public async Task<(byte[] Content, string FileName, string ContentType)> ExportAsync(ClientExportRequest r, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.ClientsExport, ct, r.StoreIds);
        var q = await reader.QueryAsync(c, r.Filter, r.SegmentId, ct);
        if (r.Ids is { Count: > 0 } ids) q = q with { Ids = q.Ids is null ? ids : q.Ids.Intersect(ids).ToList() };
        var (rows, _) = await repo.ListAsync(q with { Sort = "number", Desc = false, Take = MaxRows }, ct);

        var custom = c.Settings.CustomFields;
        var requested = (r.Fields is { Count: > 0 } f ? f : DefaultFields).Distinct().ToList();
        var fields = requested.Where(x => AllFields.Contains(x) || (x.StartsWith("custom.") && custom.Any(d => "custom." + d.Key == x)))
            .Where(x => c.Can.Contacts || !Personal.Contains(x) || x == "phone").ToList();
        if (fields.Count == 0) fields = DefaultFields.ToList();

        var headers = fields.Select(x => x.StartsWith("custom.") ? custom.First(d => "custom." + d.Key == x).Label : Header(x)).ToList();
        var data = rows.Select(row =>
        {
            var d = access.Row(c, row);
            return (IReadOnlyList<object?>)fields.Select(x => Value(c, row.Profile, d, x)).ToList();
        }).ToList();
        var csv = string.Equals(r.Format, "csv", StringComparison.OrdinalIgnoreCase);
        access.Catalog.Audit(c.OrgId, c.Store.Id, "clients.export", "clients", c.Store.Id, null,
            new { Format = csv ? "csv" : "xlsx", Rows = data.Count, Fields = fields, r.SegmentId, Selected = r.Ids?.Count ?? 0 });
        var name = $"clients-{c.Today:yyyyMMdd}";
        if (csv) return (AnalyticsService.Csv(headers, data), name + ".csv", "text/csv");
        return (spreadsheet.Write(Lang == AppLanguage.Ru ? "Клиенты" : "Клиенттер", headers, data), name + ".xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    private object? Value(ClientCtx c, Domain.Pos.Clients.ClientProfile p, ClientRowDto d, string field) => field switch
    {
        "number" => p.Number.ToString("000000"),
        "lastName" => d.LastName,
        "firstName" => d.FirstName,
        "middleName" => d.MiddleName,
        "name" => d.Name,
        "phone" => d.Phone,
        "email" => d.Email,
        "birthDate" => d.BirthDate?.ToString("dd.MM.yyyy"),
        "gender" => L("gender." + d.Gender),
        "status" => d.StatusName,
        "purchases" => (decimal)d.Purchases,
        "amount" => d.Amount,
        "average" => d.Average,
        "lastPurchase" => d.LastPurchaseAt is { } l ? TimeZoneInfo.ConvertTimeFromUtc(l, c.Zone).ToString("dd.MM.yyyy") : null,
        "bonus" => (decimal)d.Bonus,
        "debt" => d.Debt,
        "overdue" => d.Overdue,
        "registered" => TimeZoneInfo.ConvertTimeFromUtc(d.RegisteredAt, c.Zone).ToString("dd.MM.yyyy"),
        "source" => L("source." + d.Source),
        "activity" => L("activity." + d.Activity),
        "consentApp" => YesNo(d.ConsentApp),
        "consentSms" => YesNo(d.ConsentSms),
        "consentWhatsApp" => YesNo(d.ConsentWhatsApp),
        "consentEmail" => YesNo(d.ConsentEmail),
        "katoCode" => d.KatoCode,
        "address" => c.Can.Contacts ? p.Address : null,
        "comment" => p.Comment,
        _ when field.StartsWith("custom.") => p.CustomFields.GetValueOrDefault(field[7..]),
        _ => null,
    };

    private string YesNo(bool v) => Lang == AppLanguage.Ru ? (v ? "Да" : "Нет") : (v ? "Иә" : "Жоқ");

    private string Header(string key) => L("h." + key);

    private string L(string key) => (Lang == AppLanguage.Ru ? Ru : Kk).GetValueOrDefault(key) ?? key.Split('.').Last();

    private static readonly Dictionary<string, string> Ru = new()
    {
        ["h.number"] = "ID", ["h.lastName"] = "Фамилия", ["h.firstName"] = "Имя", ["h.middleName"] = "Отчество", ["h.name"] = "Клиент", ["h.phone"] = "Телефон",
        ["h.email"] = "E-mail", ["h.birthDate"] = "Дата рождения", ["h.gender"] = "Пол", ["h.status"] = "Статус", ["h.purchases"] = "Покупок",
        ["h.amount"] = "Сумма покупок", ["h.average"] = "Средний чек", ["h.lastPurchase"] = "Последняя покупка", ["h.bonus"] = "Бонусы", ["h.debt"] = "Долг",
        ["h.overdue"] = "Просрочено", ["h.registered"] = "Дата регистрации", ["h.source"] = "Источник", ["h.activity"] = "Активность",
        ["h.consentApp"] = "Согласие: приложение", ["h.consentSms"] = "Согласие: SMS", ["h.consentWhatsApp"] = "Согласие: WhatsApp", ["h.consentEmail"] = "Согласие: e-mail",
        ["h.katoCode"] = "КАТО", ["h.address"] = "Адрес", ["h.comment"] = "Комментарий",
        ["gender.Male"] = "Мужской", ["gender.Female"] = "Женский", ["gender.Unknown"] = "—",
        ["source.Manual"] = "Вручную", ["source.Cashier"] = "Касса", ["source.Import"] = "Импорт", ["source.App"] = "Приложение", ["source.Integration"] = "Интеграция",
        ["activity.active"] = "Активный", ["activity.cooling"] = "Остывает", ["activity.passive"] = "Пассивный", ["activity.noPurchases"] = "Без покупок",
    };

    private static readonly Dictionary<string, string> Kk = new()
    {
        ["h.number"] = "ID", ["h.lastName"] = "Тегі", ["h.firstName"] = "Аты", ["h.middleName"] = "Әкесінің аты", ["h.name"] = "Клиент", ["h.phone"] = "Телефон",
        ["h.email"] = "E-mail", ["h.birthDate"] = "Туған күні", ["h.gender"] = "Жынысы", ["h.status"] = "Мәртебе", ["h.purchases"] = "Сатып алу",
        ["h.amount"] = "Сатып алу сомасы", ["h.average"] = "Орташа чек", ["h.lastPurchase"] = "Соңғы сатып алу", ["h.bonus"] = "Бонус", ["h.debt"] = "Қарыз",
        ["h.overdue"] = "Мерзімі өткен", ["h.registered"] = "Тіркелген күні", ["h.source"] = "Көзі", ["h.activity"] = "Белсенділік",
        ["h.consentApp"] = "Келісім: қосымша", ["h.consentSms"] = "Келісім: SMS", ["h.consentWhatsApp"] = "Келісім: WhatsApp", ["h.consentEmail"] = "Келісім: e-mail",
        ["h.katoCode"] = "КАТО", ["h.address"] = "Мекенжай", ["h.comment"] = "Пікір",
        ["gender.Male"] = "Ер", ["gender.Female"] = "Әйел", ["gender.Unknown"] = "—",
        ["source.Manual"] = "Қолмен", ["source.Cashier"] = "Касса", ["source.Import"] = "Импорт", ["source.App"] = "Қосымша", ["source.Integration"] = "Интеграция",
        ["activity.active"] = "Белсенді", ["activity.cooling"] = "Суып барады", ["activity.passive"] = "Пассив", ["activity.noPurchases"] = "Сатып алусыз",
    };
}
