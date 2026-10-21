using System.Globalization;
using System.Text;
using System.Text.Json;
using SalemBonus.Application.BonusCards;
using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Application.Pos.Bonus;
using SalemBonus.Application.Pos.Catalog;
using SalemBonus.Domain.Core;
using SalemBonus.Domain.Entities;
using SalemBonus.Domain.Pos.Clients;

namespace SalemBonus.Application.Pos.Clients;

/// <summary>
/// Импорт (ТЗ «Клиенты» §19.1): Excel не CSV, бағандарды сәйкестендіру, алдын ала тексеру (жаңа / жаңарту / өткізу / қате — растауға дейін
/// ештеңе сақталмайды), бар клиенттерге стратегия (өткізу не толтыру), кілт бойынша қайталанбау. Қарыз бен бонус импортталмайды.
/// </summary>
public class ClientImportService(
    ClientAccess access,
    IClientRepository repo,
    ICustomerRepository customers,
    BonusLedger ledger,
    ISpreadsheet spreadsheet,
    IUnitOfWork unitOfWork)
{
    public const int MaxRows = 5000;
    public const long MaxFileBytes = 10 * 1024 * 1024;
    private AppLanguage Lang => access.Lang;

    public static readonly string[] Fields = ["phone", "fullName", "lastName", "firstName", "middleName", "birthDate", "gender", "email", "address", "comment"];

    private static readonly Dictionary<string, string[]> Synonyms = new()
    {
        ["phone"] = ["телефон", "phone", "тел", "номер телефона", "мобильный", "телефон нөмірі", "mobile"],
        ["fullName"] = ["фио", "аты-жөні", "full name", "fullname", "клиент", "client"],
        ["lastName"] = ["фамилия", "тегі", "last name", "lastname", "surname"],
        ["firstName"] = ["имя", "аты", "first name", "firstname", "name"],
        ["middleName"] = ["отчество", "әкесінің аты", "middle name", "middlename"],
        ["birthDate"] = ["дата рождения", "туған күні", "birthday", "birth date", "birthdate", "др"],
        ["gender"] = ["пол", "жынысы", "gender", "sex"],
        ["email"] = ["email", "e-mail", "почта", "электронная почта", "эл. почта"],
        ["address"] = ["адрес", "мекенжай", "address"],
        ["comment"] = ["комментарий", "пікір", "comment", "примечание", "заметка"],
    };

    private sealed record Parsed(int Row, string Status, string? Phone, string First, string Last, string? Middle, DateOnly? Birth, ClientGender? Gender,
        string? Email, string? Address, string? Comment, string? Message, Customer? Customer, ClientProfile? Profile);

    public async Task<ClientImportPreviewDto> PreviewAsync(Stream file, string fileName, long length, string? mapping, string? strategy, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.ClientsEdit, ct);
        var data = Read(file, fileName, length);
        var map = Mapping(data.Headers, mapping);
        var rows = await AnalyzeAsync(c, data, map, Update(strategy), ct);
        return new ClientImportPreviewDto(data.Headers, map, rows.Count, rows.Count(r => r.Status == "new"), rows.Count(r => r.Status == "update"),
            rows.Count(r => r.Status == "skip"), rows.Count(r => r.Status == "error"),
            rows.OrderBy(r => r.Status == "error" ? 0 : 1).ThenBy(r => r.Row).Take(500).Select(Dto).ToList());
    }

    public async Task<ClientImportResultDto> CommitAsync(Stream file, string fileName, long length, string? mapping, string? strategy, Guid key, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.ClientsEdit, ct);
        if (await repo.ImportAsync(c.OrgId, key, ct) is { } done) return Result(done);
        var data = Read(file, fileName, length);
        var map = Mapping(data.Headers, mapping);
        var update = Update(strategy);
        var rows = await AnalyzeAsync(c, data, map, update, ct);
        var now = DateTime.UtcNow;
        var name = CatalogAccess.Optional(Path.GetFileName(fileName), 260) ?? "import";

        var import = await unitOfWork.InTransactionAsync(async () =>
        {
            var report = new List<ClientImportRowDto>();
            int created = 0, updated = 0;
            foreach (var r in rows)
            {
                if (r.Status == "new")
                {
                    var customer = r.Customer;
                    if (customer is null)
                    {
                        customer = new Customer
                        {
                            Id = Guid.NewGuid(), Phone = r.Phone!, FirstName = r.First, LastName = r.Last, BirthDate = r.Birth, Email = r.Email,
                            KatoCode = c.Store.KatoCode, QrCode = BonusRules.GenerateQrCode(), CreatedAt = now,
                        };
                        customers.Add(customer);
                    }
                    await ledger.GetOrCreateCardAsync(customer, c.Store, now, ct);
                    var profile = new ClientProfile
                    {
                        Id = Guid.NewGuid(), OrganizationId = c.OrgId, CustomerId = customer.Id, Number = await repo.NextNumberAsync(c.OrgId, ct),
                        FirstName = r.First, LastName = r.Last, MiddleName = r.Middle, BirthDate = r.Birth, Gender = r.Gender ?? ClientGender.Unknown, Email = r.Email,
                        Address = r.Address, Comment = r.Comment, Source = ClientSource.Import, CreatedBy = access.StaffUserId, CreatedAt = now, UpdatedAt = now,
                        UpdatedBy = access.StaffUserId,
                    };
                    repo.Add(profile);
                    created++;
                    report.Add(Dto(r) with { ProfileId = profile.Id });
                }
                else if (r.Status == "update" && r.Profile is { } p)
                {
                    // Тек файлда толтырылған өрістер жазылады — бос ұяшық бар мәнді өшірмейді.
                    var tracked = await repo.GetAsync(c.OrgId, p.Id, ct) ?? p;
                    if (r.First.Length > 0) tracked.FirstName = r.First;
                    if (r.Last.Length > 0) tracked.LastName = r.Last;
                    tracked.MiddleName = r.Middle ?? tracked.MiddleName;
                    tracked.BirthDate = r.Birth ?? tracked.BirthDate;
                    if (r.Gender is { } g && g != ClientGender.Unknown) tracked.Gender = g;
                    tracked.Email = r.Email ?? tracked.Email;
                    tracked.Address = r.Address ?? tracked.Address;
                    tracked.Comment = r.Comment ?? tracked.Comment;
                    tracked.UpdatedAt = now;
                    tracked.UpdatedBy = access.StaffUserId;
                    updated++;
                    report.Add(Dto(r));
                }
                else report.Add(Dto(r));
            }
            var errors = rows.Count(r => r.Status == "error");
            var entity = new ClientImport
            {
                Id = key, OrganizationId = c.OrgId, FileName = name, Rows = rows.Count, Created = created, Updated = updated,
                Skipped = rows.Count - created - updated - errors, Errors = errors, StaffUserId = access.StaffUserId, CreatedAt = now,
                Report = JsonSerializer.Serialize(report.OrderBy(x => x.Status == "error" ? 0 : 1).ThenBy(x => x.Row).Take(2000), Json),
            };
            repo.AddImport(entity);
            access.Catalog.Audit(c.OrgId, c.Store.Id, "clients.import", "client_import", key, null, new { File = name, entity.Rows, Created = created, Updated = updated, entity.Skipped, Errors = errors });
            await unitOfWork.SaveChangesAsync(ct);
            return entity;
        }, ct);
        return Result(import);
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static ClientImportResultDto Result(ClientImport i) =>
        new(i.Id, i.FileName, i.Rows, i.Created, i.Updated, i.Skipped, i.Errors,
            JsonSerializer.Deserialize<List<ClientImportRowDto>>(i.Report, Json) ?? [], i.CreatedAt);

    private static ClientImportRowDto Dto(Parsed r) =>
        new(r.Row, r.Status, r.Phone, BonusAccess.FullName(r.First, r.Last) is var n && n != "—" ? n : null, r.Message, r.Profile?.Id);

    private static bool Update(string? strategy) => string.Equals(strategy, "update", StringComparison.OrdinalIgnoreCase);

    public byte[] Template() =>
        spreadsheet.Write(Lang == AppLanguage.Ru ? "Клиенты" : "Клиенттер",
            Lang == AppLanguage.Ru
                ? ["Телефон", "Фамилия", "Имя", "Отчество", "Дата рождения", "Пол", "Email", "Адрес", "Комментарий"]
                : ["Телефон", "Тегі", "Аты", "Әкесінің аты", "Туған күні", "Жынысы", "Email", "Мекенжай", "Пікір"],
            [["+77001234567", "Иванова", "Айгерим", "", "1990-05-14", "Ж", "aigerim@example.kz", "", ""]]);

    // ---------- Файлды оқу ----------

    private SpreadsheetData Read(Stream file, string fileName, long length)
    {
        if (length <= 0) throw new ValidationException(Messages.ClientImportEmpty(Lang), "file");
        if (length > MaxFileBytes) throw new ValidationException(Messages.ClientImportFormat(Lang), "file");
        using var buffer = new MemoryStream();
        file.CopyTo(buffer);
        var bytes = buffer.ToArray();
        SpreadsheetData data;
        var zip = bytes.Length > 4 && bytes[0] == 'P' && bytes[1] == 'K';
        if (zip && !fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            try { data = spreadsheet.Read(new MemoryStream(bytes)); }
            catch (InvalidDataException) { throw new ValidationException(Messages.ClientImportFormat(Lang), "file"); }
        }
        else data = Csv(bytes);
        var rows = data.Rows.Where(r => r.Any(v => !string.IsNullOrWhiteSpace(v))).ToList();
        if (data.Headers.Count == 0 || rows.Count == 0) throw new ValidationException(Messages.ClientImportEmpty(Lang), "file");
        if (rows.Count > MaxRows) throw new ValidationException(Messages.ClientImportTooLarge(Lang, MaxRows), "file");
        return new SpreadsheetData(data.Headers, rows);
    }

    /// <summary>CSV: UTF-8 (BOM-мен не онсыз), бөлгіш — бірінші жолдан («;», «,» не таб), тырнақшадағы мән мен «""» қолдауы.</summary>
    public static SpreadsheetData Csv(byte[] bytes)
    {
        var text = new UTF8Encoding(false).GetString(bytes).TrimStart('﻿');
        var firstLine = text.Split('\n', 2)[0];
        var delimiter = new[] { ';', ',', '\t' }.OrderByDescending(d => firstLine.Count(ch => ch == d)).First();
        var rows = new List<IReadOnlyList<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (quoted)
            {
                if (ch == '"' && i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                else if (ch == '"') quoted = false;
                else cell.Append(ch);
                continue;
            }
            if (ch == '"' && cell.Length == 0) quoted = true;
            else if (ch == delimiter) { row.Add(cell.ToString().Trim()); cell.Clear(); }
            else if (ch == '\n')
            {
                row.Add(cell.ToString().Trim()); cell.Clear();
                rows.Add(row); row = [];
            }
            else if (ch != '\r') cell.Append(ch);
        }
        if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString().Trim()); rows.Add(row); }
        if (rows.Count == 0) return new SpreadsheetData([], []);
        return new SpreadsheetData(rows[0], rows.Skip(1).ToList());
    }

    /// <summary>Сәйкестендіру: пайдаланушы бергені (өріс → баған нөмірі), әйтпесе тақырып атауы бойынша автоматты.</summary>
    private Dictionary<string, int> Mapping(IReadOnlyList<string> headers, string? mapping)
    {
        var map = new Dictionary<string, int>();
        if (!string.IsNullOrWhiteSpace(mapping))
        {
            try
            {
                var given = JsonSerializer.Deserialize<Dictionary<string, int>>(mapping) ?? [];
                foreach (var (field, index) in given)
                    if (Fields.Contains(field) && index >= 0 && index < headers.Count) map[field] = index;
            }
            catch (JsonException) { }
        }
        else
        {
            for (var i = 0; i < headers.Count; i++)
            {
                var h = headers[i].Trim().ToLowerInvariant();
                var field = Synonyms.FirstOrDefault(s => s.Value.Contains(h)).Key;
                if (field is not null && !map.ContainsKey(field)) map[field] = i;
            }
        }
        if (!map.ContainsKey("phone")) throw new ValidationException(Messages.ClientImportNoPhone(Lang), "mapping");
        return map;
    }

    private static readonly string[] DateFormats = ["yyyy-MM-dd", "dd.MM.yyyy", "d.M.yyyy", "dd/MM/yyyy", "d/M/yyyy", "dd.MM.yy", "yyyy.MM.dd"];

    private async Task<List<Parsed>> AnalyzeAsync(ClientCtx c, SpreadsheetData data, IReadOnlyDictionary<string, int> map, bool update, CancellationToken ct)
    {
        string? Get(IReadOnlyList<string> row, string field) =>
            map.TryGetValue(field, out var i) && i < row.Count ? CatalogAccess.Optional(row[i], 300) : null;

        var phones = new List<string>();
        foreach (var row in data.Rows) if (ClientAccess.TryPhone(Get(row, "phone"), out var p)) phones.Add(p);
        var known = await repo.CustomersByPhonesAsync(phones.Distinct().ToList(), ct);
        var profiles = await repo.ByCustomersAsync(c.OrgId, known.Values.Select(x => x.Id).ToList(), ct);
        var seen = new Dictionary<string, int>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var result = new List<Parsed>();

        for (var i = 0; i < data.Rows.Count; i++)
        {
            var row = data.Rows[i];
            var number = i + 2;
            Parsed Error(string message, string? phone = null) => new(number, "error", phone, "", "", null, null, null, null, null, null, message, null, null);

            if (!ClientAccess.TryPhone(Get(row, "phone"), out var phone)) { result.Add(Error(Messages.ClientImportPhoneInvalid(Lang), Get(row, "phone"))); continue; }
            if (seen.TryGetValue(phone, out var firstRow)) { result.Add(Error(Messages.ClientImportDuplicateInFile(Lang, firstRow), phone)); continue; }
            seen[phone] = number;

            var first = Get(row, "firstName");
            var last = Get(row, "lastName");
            var middle = Get(row, "middleName");
            if (Get(row, "fullName") is { } full)
            {
                var parts = full.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 1) first ??= parts[0];
                else
                {
                    last ??= parts[0];
                    first ??= parts[1];
                    if (parts.Length > 2) middle ??= string.Join(' ', parts[2..]);
                }
            }
            DateOnly? birth = null;
            if (Get(row, "birthDate") is { } rawBirth)
            {
                if (!DateOnly.TryParseExact(rawBirth.Split(' ')[0], DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var b)
                    || b > today || b < today.AddYears(-120)) { result.Add(Error(Messages.ClientImportBirthDate(Lang), phone)); continue; }
                birth = b;
            }
            var email = Get(row, "email");
            if (email is not null && !ClientAccess.IsEmail(email)) { result.Add(Error(Messages.ClientEmailInvalid(Lang), phone)); continue; }
            ClientGender? gender = Get(row, "gender") is { } g ? ClientAccess.Gender(g) : null;

            known.TryGetValue(phone, out var customer);
            var profile = customer is not null ? profiles.GetValueOrDefault(customer.Id) : null;
            string status;
            string? message = null;
            if (profile is not null)
            {
                status = update && !profile.IsArchived ? "update" : "skip";
                message = Messages.ClientPhoneExists(Lang, BonusAccess.FullName(profile.FirstName, profile.LastName), profile.Number);
            }
            else
            {
                if (first is null) { result.Add(Error(Messages.ClientFirstNameRequired(Lang), phone)); continue; }
                if (last is null && c.Settings.RequireLastName) { result.Add(Error(Messages.ClientLastNameRequired(Lang), phone)); continue; }
                if (birth is null && c.Settings.RequireBirthDate) { result.Add(Error(Messages.BirthDateRequired(Lang), phone)); continue; }
                status = "new";
            }
            result.Add(new Parsed(number, status, phone, (first ?? string.Empty)[..Math.Min((first ?? string.Empty).Length, 100)],
                (last ?? string.Empty)[..Math.Min((last ?? string.Empty).Length, 100)], middle, birth, gender, email?.ToLowerInvariant(), Get(row, "address"),
                Get(row, "comment"), message, customer, profile));
        }
        return result;
    }
}
