using System.Text.Json;
using System.Text.RegularExpressions;
using SalemBonus.Application.BonusCards;
using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Application.Pos.Catalog;
using SalemBonus.Domain.Core;
using SalemBonus.Domain.Enums;
using SalemBonus.Domain.Pos.Clients;

namespace SalemBonus.Application.Pos.Clients;

/// <summary>
/// CRM (ТЗ «Клиенты» §13, §15, §21, §24): қызметкер жазбалары (автор мен күн автоматты, өзгерту — автор не әкімші), өзгерістер тарихы,
/// клиентке хабар (тек SalemBonus қосымшасы арқылы — келісіммен; басқа арналар қосылмаған, жіберу имитацияланбайды), бөлім баптаулары.
/// </summary>
public partial class ClientCrmService(
    ClientAccess access,
    IClientRepository repo,
    BonusLedger ledger,
    IUnitOfWork unitOfWork)
{
    private AppLanguage Lang => access.Lang;
    private static bool IsAdmin(ClientCtx c) => c.Current.Role is StaffRole.Owner or StaffRole.Admin;

    // ---------- Жазбалар ----------

    public async Task<IReadOnlyList<ClientNoteDto>> NotesAsync(Guid id, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.ClientsView, ct, ensure: false);
        var p = await access.ProfileAsync(c, id, ct);
        return await NoteDtosAsync(c, p.Id, ct);
    }

    private async Task<IReadOnlyList<ClientNoteDto>> NoteDtosAsync(ClientCtx c, Guid profileId, CancellationToken ct)
    {
        var notes = await repo.NotesAsync(profileId, ct);
        var names = await repo.StaffNamesAsync(notes.Select(n => n.AuthorId).Concat(notes.Where(n => n.UpdatedBy != null).Select(n => n.UpdatedBy!.Value)).Distinct().ToList(), ct);
        return notes.Select(n => new ClientNoteDto(n.Id, n.Text, n.Category.ToString(), n.AuthorId, names.GetValueOrDefault(n.AuthorId) ?? "", n.CreatedAt, n.UpdatedAt,
            n.UpdatedBy is { } u ? names.GetValueOrDefault(u) : null, c.Can.Edit && (n.AuthorId == access.StaffUserId || IsAdmin(c)))).ToList();
    }

    private (string Text, NoteCategory Category) Note(SaveClientNoteRequest r)
    {
        var text = CatalogAccess.Optional(r.Text, 2000) ?? throw new ValidationException(Messages.ClientNoteRequired(Lang), "text");
        var category = Enum.TryParse<NoteCategory>(r.Category, true, out var cat) ? cat : NoteCategory.General;
        return (text, category);
    }

    public async Task<IReadOnlyList<ClientNoteDto>> AddNoteAsync(Guid id, SaveClientNoteRequest r, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.ClientsEdit, ct, ensure: false);
        var p = await access.ProfileAsync(c, id, ct);
        var (text, category) = Note(r);
        var note = new ClientNote
        {
            Id = Guid.NewGuid(), OrganizationId = c.OrgId, ProfileId = p.Id, Text = text, Category = category, AuthorId = access.StaffUserId, CreatedAt = DateTime.UtcNow,
        };
        repo.AddNote(note);
        access.Audit(c, "client.note.add", p.Id, null, new { Note = text, Category = category.ToString() });
        await unitOfWork.SaveChangesAsync(ct);
        return await NoteDtosAsync(c, p.Id, ct);
    }

    private async Task<ClientNote> EditableNoteAsync(ClientCtx c, Guid noteId, CancellationToken ct)
    {
        var note = await repo.NoteAsync(c.OrgId, noteId, ct) ?? throw new NotFoundException(Messages.ClientNoteNotFound(Lang));
        if (note.AuthorId != access.StaffUserId && !IsAdmin(c)) throw new ForbiddenException(Messages.ClientNoteForbidden(Lang));
        return note;
    }

    public async Task<IReadOnlyList<ClientNoteDto>> UpdateNoteAsync(Guid noteId, SaveClientNoteRequest r, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.ClientsEdit, ct, ensure: false);
        var note = await EditableNoteAsync(c, noteId, ct);
        var (text, category) = Note(r);
        access.Audit(c, "client.note.edit", note.ProfileId, new { Note = note.Text, Category = note.Category.ToString() }, new { Note = text, Category = category.ToString() });
        note.Text = text;
        note.Category = category;
        note.UpdatedAt = DateTime.UtcNow;
        note.UpdatedBy = access.StaffUserId;
        await unitOfWork.SaveChangesAsync(ct);
        return await NoteDtosAsync(c, note.ProfileId, ct);
    }

    public async Task DeleteNoteAsync(Guid noteId, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.ClientsEdit, ct, ensure: false);
        var note = await EditableNoteAsync(c, noteId, ct);
        access.Audit(c, "client.note.delete", note.ProfileId, new { Note = note.Text }, null);
        repo.RemoveNote(note);
        await unitOfWork.SaveChangesAsync(ct);
    }

    // ---------- Тарих ----------

    public async Task<IReadOnlyList<ClientHistoryDto>> HistoryAsync(Guid id, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.ClientsView, ct, ensure: false);
        var p = await access.ProfileAsync(c, id, ct);
        var rows = await repo.AuditAsync(c.OrgId, p.Id, 300, ct);
        var names = await repo.StaffNamesAsync(rows.Where(r => r.StaffUserId != null).Select(r => r.StaffUserId!.Value).Distinct().ToList(), ct);
        return rows.Select(r =>
        {
            var before = Parse(r.OldValues);
            var after = Parse(r.NewValues);
            var changes = before.Keys.Union(after.Keys)
                .Select(k => new ClientChangeDto(k, Mask(c, k, before.GetValueOrDefault(k)), Mask(c, k, after.GetValueOrDefault(k)))).ToList();
            return new ClientHistoryDto(r.Id, r.CreatedAt, r.StaffUserId is { } s ? names.GetValueOrDefault(s) ?? "" : "", r.Action, changes);
        }).ToList();
    }

    private static string? Mask(ClientCtx c, string field, string? value) =>
        c.Can.Contacts ? value : field switch
        {
            "phone" => Bonus.BonusAccess.Phone(value, false),
            "email" or "address" => value is null ? null : "***",
            _ => value,
        };

    private static Dictionary<string, string?> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return [];
            return doc.RootElement.EnumerateObject().ToDictionary(x => char.ToLowerInvariant(x.Name[0]) + x.Name[1..], x => x.Value.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.String => x.Value.GetString(),
                _ => x.Value.GetRawText(),
            });
        }
        catch (JsonException) { return []; }
    }

    // ---------- Хабар ----------

    public async Task<ClientCommunicationDto> MessageAsync(Guid id, ClientMessageRequest r, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.ClientsEdit, ct, ensure: false);
        var p = await access.ProfileAsync(c, id, ct);
        if (p.IsArchived) throw new ValidationException(Messages.ClientArchived(Lang));
        var text = CatalogAccess.Optional(r.Text, 1000) ?? throw new ValidationException(Messages.ClientMessageRequired(Lang), "text");
        var channel = (r.Channel ?? "app").Trim().ToLowerInvariant();
        if (channel != "app")
            throw new ValidationException(Messages.ClientChannelNotConnected(Lang, channel switch { "sms" => "SMS", "whatsapp" => "WhatsApp", "email" => "E-mail", _ => channel }), "channel");
        if (!p.ConsentApp) throw new ValidationException(Messages.ClientNoConsent(Lang), "channel");
        if (!await repo.HasAppAsync(p.CustomerId, ct)) throw new ValidationException(Messages.ClientNoApp(Lang), "channel");
        var card = await repo.CardAsync(p.CustomerId, c.Store.Id, ct) ?? throw new ValidationException(Messages.ClientNotInStore(Lang), "channel");
        if (card.NotificationsOptOut) throw new ValidationException(Messages.ClientOptedOut(Lang), "channel");

        var now = DateTime.UtcNow;
        var n = ledger.Notify(p.CustomerId, c.Store, NotificationType.Promo, c.Store.Name, text, null, now);
        var message = new ClientCommunication
        {
            Id = Guid.NewGuid(), OrganizationId = c.OrgId, ProfileId = p.Id, StoreId = c.Store.Id, Channel = channel, Text = text, Status = "delivered",
            NotificationId = n.Id, StaffUserId = access.StaffUserId, CreatedAt = now,
        };
        repo.AddCommunication(message);
        access.Audit(c, "client.message", p.Id, null, new { Channel = channel, Text = text });
        await unitOfWork.SaveChangesAsync(ct);
        var names = await repo.StaffNamesAsync([access.StaffUserId], ct);
        return new ClientCommunicationDto(message.Id, now, channel, text, message.Status, names.GetValueOrDefault(access.StaffUserId) ?? "", c.Store.Name);
    }

    // ---------- Баптаулар ----------

    public async Task<ClientSettingsDto> SettingsAsync(CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.ClientsView, ct, ensure: false);
        return ClientAccess.Settings(c.Settings);
    }

    [GeneratedRegex("^[a-z][a-z0-9_]{0,31}$")]
    private static partial Regex KeyPattern();

    private static readonly string[] FieldTypes = ["text", "number", "date", "select"];

    public async Task<ClientSettingsDto> SaveSettingsAsync(SaveClientSettingsRequest r, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.SettingsManage, ct, ensure: false);
        var s = await repo.SettingsAsync(c.OrgId, ct);
        if (s is null)
        {
            s = new ClientSettings { OrganizationId = c.OrgId };
            repo.AddSettings(s);
        }
        var before = ClientAccess.Settings(s);
        var active = r.ActiveDays ?? s.ActiveDays;
        var passive = r.PassiveDays ?? s.PassiveDays;
        var fresh = r.NewDays ?? s.NewDays;
        var dueSoon = r.DueSoonDays ?? s.DueSoonDays;
        if (active is < 1 or > 365 || passive <= active || passive > 730 || fresh is < 1 or > 365 || dueSoon is < 0 or > 60)
            throw new ValidationException(Messages.ClientSettingsInvalid(Lang));
        s.ActiveDays = active;
        s.PassiveDays = passive;
        s.NewDays = fresh;
        s.DueSoonDays = dueSoon;
        s.RequireLastName = r.RequireLastName ?? s.RequireLastName;
        s.RequireBirthDate = r.RequireBirthDate ?? s.RequireBirthDate;
        if (r.DefaultColumns is not null)
            s.DefaultColumns = r.DefaultColumns.Select(x => CatalogAccess.Optional(x, 40)).Where(x => x is not null).Select(x => x!).Distinct().Take(40).ToList();
        if (r.CustomFields is not null)
        {
            var list = new List<CustomFieldDefinition>();
            foreach (var f in r.CustomFields.Take(20))
            {
                var label = CatalogAccess.Optional(f.Label, 60) ?? throw new ValidationException(Messages.ClientCustomFieldInvalid(Lang, "?"), "customFields");
                var key = CatalogAccess.Optional(f.Key, 32)?.ToLowerInvariant() ?? $"field_{list.Count + 1}";
                if (!KeyPattern().IsMatch(key) || list.Any(x => x.Key == key)) throw new ValidationException(Messages.ClientCustomFieldInvalid(Lang, label), "customFields");
                var type = FieldTypes.Contains(f.Type) ? f.Type : "text";
                var options = type == "select"
                    ? (f.Options ?? []).Select(o => CatalogAccess.Optional(o, 60)).Where(o => o is not null).Select(o => o!).Distinct().Take(50).ToList()
                    : [];
                if (type == "select" && options.Count == 0) throw new ValidationException(Messages.ClientCustomFieldInvalid(Lang, label), "customFields");
                list.Add(new CustomFieldDefinition { Key = key, Label = label, Type = type, Options = options });
            }
            s.CustomFields = list;
        }
        s.UpdatedAt = DateTime.UtcNow;
        s.UpdatedBy = access.StaffUserId;
        access.Catalog.Audit(c.OrgId, c.Store.Id, "clients.settings", "client_settings", c.OrgId, before, ClientAccess.Settings(s));
        await unitOfWork.SaveChangesAsync(ct);
        return ClientAccess.Settings(s);
    }
}
