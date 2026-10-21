using System.Text.Json;
using SalemBonus.Application.BonusCards;
using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Application.Kato;
using SalemBonus.Application.Pos.Bonus;
using SalemBonus.Application.Pos.Catalog;
using SalemBonus.Domain.Core;
using SalemBonus.Domain.Entities;
using SalemBonus.Domain.Pos.Clients;

namespace SalemBonus.Application.Pos.Clients;

/// <summary>
/// Карточканы жасау мен өзгерту (ТЗ «Клиенты» §5, §6, §18, §20): телефон бірегей — бар клиент «телік» ретінде көрсетіледі;
/// басқа бизнестің клиенті болса, оның деректері ашылмай, тек байланыстырылады. Өзгерістер өріс бойынша аудитке жазылады.
/// Мұрағат — физикалық жоюсыз; біріктіру — қарыз бен бонус екі еселенбейді, бәрі бір транзакцияда.
/// </summary>
public class ClientEditService(
    ClientAccess access,
    ClientService reader,
    IClientRepository repo,
    ICustomerRepository customers,
    BonusLedger ledger,
    BonusStatusService statuses,
    IKatoService kato,
    IUnitOfWork unitOfWork)
{
    private AppLanguage Lang => access.Lang;

    public async Task<ClientLookupDto> LookupAsync(string? phone, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.ClientsView, ct);
        if (!ClientAccess.TryPhone(phone, out var normalized)) return new ClientLookupDto(false, false, null, null, null, false);
        var customer = await customers.GetByPhoneAsync(normalized, ct);
        if (customer is null) return new ClientLookupDto(false, false, null, null, null, false);
        var profile = await repo.ByCustomerAsync(c.OrgId, customer.Id, ct);
        // Басқа бизнестің клиенті: тек «SalemBonus-та бар» белгісі, аты-жөні ашылмайды (SALEMPOS.md 4.6).
        if (profile is null) return new ClientLookupDto(false, true, null, null, null, false);
        return new ClientLookupDto(true, true, profile.Id, profile.Number, Name(profile, customer), profile.IsArchived);
    }

    private static string Name(ClientProfile p, Customer c) =>
        BonusAccess.FullName(p.FirstName.Length > 0 ? p.FirstName : c.FirstName, p.LastName.Length > 0 ? p.LastName : c.LastName);

    private sealed record Fields(string First, string Last, string? Middle, DateOnly? Birth, ClientGender Gender, string? Kato, string? Address, string? Email,
        string? Comment, Dictionary<string, string> Custom);

    private async Task<Fields> ValidateAsync(ClientCtx c, ClientSaveRequest r, ClientProfile? existing, CancellationToken ct)
    {
        var first = CatalogAccess.Optional(r.FirstName, 100) ?? throw new ValidationException(Messages.ClientFirstNameRequired(Lang), "firstName");
        var last = CatalogAccess.Optional(r.LastName, 100);
        if (last is null && c.Settings.RequireLastName) throw new ValidationException(Messages.ClientLastNameRequired(Lang), "lastName");
        var birth = access.BirthDate(r.BirthDate, c.Settings.RequireBirthDate);
        var katoCode = CatalogAccess.Optional(r.KatoCode, 9);
        if (katoCode is not null && (await kato.GetPathAsync(katoCode, ct)).Count == 0) throw new ValidationException(Messages.ClientKatoInvalid(Lang), "katoCode");
        return new Fields(first, last ?? string.Empty, CatalogAccess.Optional(r.MiddleName, 100), birth, ClientAccess.Gender(r.Gender), katoCode,
            CatalogAccess.Optional(r.Address, 300), access.Email(r.Email), CatalogAccess.Optional(r.Comment, 1000),
            access.CustomFields(c.Settings, r.CustomFields, existing?.CustomFields));
    }

    /// <summary>«Добавить клиента» (§5): жаңа адам — жаһандық аккаунт пен ағымдағы дүкен картасы; бар адам — тек осы ұйымның карточкасы.</summary>
    public async Task<ClientCardDto> CreateAsync(ClientSaveRequest r, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.ClientsEdit, ct);
        var phone = access.Phone(r.Phone);
        var f = await ValidateAsync(c, r, null, ct);
        var existing = await customers.GetByPhoneAsync(phone, ct);
        if (existing is not null && await repo.ByCustomerAsync(c.OrgId, existing.Id, ct) is { } dup)
            throw new ValidationException(Messages.ClientPhoneExists(Lang, Name(dup, existing), dup.Number), "phone");

        var now = DateTime.UtcNow;
        var id = await unitOfWork.InTransactionAsync(async () =>
        {
            var customer = existing;
            if (customer is null)
            {
                customer = new Customer
                {
                    Id = Guid.NewGuid(), Phone = phone, FirstName = f.First, LastName = f.Last, BirthDate = f.Birth, Email = f.Email,
                    KatoCode = f.Kato ?? c.Store.KatoCode, QrCode = BonusRules.GenerateQrCode(), CreatedAt = now,
                };
                customers.Add(customer);
            }
            await ledger.GetOrCreateCardAsync(customer, c.Store, now, ct);
            var consent = r.ConsentApp == true || r.ConsentSms == true || r.ConsentWhatsApp == true || r.ConsentEmail == true;
            var profile = new ClientProfile
            {
                Id = Guid.NewGuid(), OrganizationId = c.OrgId, CustomerId = customer.Id, Number = await repo.NextNumberAsync(c.OrgId, ct),
                FirstName = f.First, LastName = f.Last, MiddleName = f.Middle, BirthDate = f.Birth, Gender = f.Gender, KatoCode = f.Kato, Address = f.Address,
                Email = f.Email, Comment = f.Comment, Source = ClientSource.Manual, ConsentApp = r.ConsentApp == true, ConsentSms = r.ConsentSms == true,
                ConsentWhatsApp = r.ConsentWhatsApp == true, ConsentEmail = r.ConsentEmail == true, ConsentUpdatedAt = consent ? now : null,
                CustomFields = f.Custom, CreatedBy = access.StaffUserId, CreatedAt = now, UpdatedAt = now, UpdatedBy = access.StaffUserId,
            };
            repo.Add(profile);
            access.Audit(c, "client.create", profile.Id, null, Snapshot(profile, phone));
            await unitOfWork.SaveChangesAsync(ct);
            return profile.Id;
        }, ct);
        return await reader.BuildCardAsync(c, id, ct);
    }

    /// <summary>
    /// Өзгерту (§6): телефон — тек басқа клиентте болмаса және адам SalemBonus қосымшасын қолданбаса, басқа бизнесте картасы жоқ болса
    /// (әйтпесе ол өз нөмірін өзі басқарады). Аты-жөні сондай жағдайда жаһандық аккаунтқа да жазылады.
    /// </summary>
    public async Task<ClientCardDto> UpdateAsync(Guid id, ClientSaveRequest r, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.ClientsEdit, ct, ensure: false);
        await unitOfWork.InTransactionAsync(async () =>
        {
            var profile = await access.ProfileAsync(c, id, ct);
            if (profile.IsArchived) throw new ValidationException(Messages.ClientArchived(Lang));
            var customer = await customers.GetForUpdateAsync(profile.CustomerId, ct) ?? throw new NotFoundException(Messages.ClientNotFound(Lang));
            var f = await ValidateAsync(c, r, profile, ct);
            var before = Snapshot(profile, customer.Phone);
            var orgStores = await repo.OrgStoreIdsAsync(c.OrgId, ct);
            var owned = !await repo.HasAppAsync(customer.Id, ct) && !await repo.HasCardsOutsideAsync(customer.Id, orgStores, ct);

            if (r.Phone is not null)
            {
                var phone = access.Phone(r.Phone);
                if (phone != customer.Phone)
                {
                    if (!owned) throw new ValidationException(Messages.ClientPhoneLocked(Lang), "phone");
                    if (await customers.GetByPhoneAsync(phone, ct) is { } other && other.Id != customer.Id)
                        throw new ValidationException(Messages.ClientPhoneTaken(Lang), "phone");
                    customer.Phone = phone;
                }
            }
            var now = DateTime.UtcNow;
            var consentChanged = (r.ConsentApp is { } a && a != profile.ConsentApp) || (r.ConsentSms is { } s && s != profile.ConsentSms)
                || (r.ConsentWhatsApp is { } w && w != profile.ConsentWhatsApp) || (r.ConsentEmail is { } e && e != profile.ConsentEmail);
            profile.FirstName = f.First;
            profile.LastName = f.Last;
            profile.MiddleName = f.Middle;
            profile.BirthDate = f.Birth;
            profile.Gender = f.Gender;
            profile.KatoCode = f.Kato;
            profile.Address = f.Address;
            profile.Email = f.Email;
            profile.Comment = f.Comment;
            profile.CustomFields = f.Custom;
            profile.ConsentApp = r.ConsentApp ?? profile.ConsentApp;
            profile.ConsentSms = r.ConsentSms ?? profile.ConsentSms;
            profile.ConsentWhatsApp = r.ConsentWhatsApp ?? profile.ConsentWhatsApp;
            profile.ConsentEmail = r.ConsentEmail ?? profile.ConsentEmail;
            if (consentChanged) profile.ConsentUpdatedAt = now;
            profile.UpdatedAt = now;
            profile.UpdatedBy = access.StaffUserId;
            if (owned)
            {
                customer.FirstName = f.First;
                customer.LastName = f.Last;
                customer.BirthDate = f.Birth;
                customer.Email = f.Email;
                if (f.Kato is not null) customer.KatoCode = f.Kato;
            }
            var after = Snapshot(profile, customer.Phone);
            var changed = after.Keys.Where(k => before.GetValueOrDefault(k) != after.GetValueOrDefault(k)).ToList();
            if (changed.Count > 0)
                access.Audit(c, "client.update", profile.Id, changed.ToDictionary(k => k, k => before.GetValueOrDefault(k)), changed.ToDictionary(k => k, k => after.GetValueOrDefault(k)));
            await unitOfWork.SaveChangesAsync(ct);
            return true;
        }, ct);
        return await reader.BuildCardAsync(c, id, ct);
    }

    /// <summary>Аудитке жазылатын өрістер (тарихта «было → стало»).</summary>
    public static Dictionary<string, string?> Snapshot(ClientProfile p, string phone) => new()
    {
        ["phone"] = phone,
        ["firstName"] = p.FirstName,
        ["lastName"] = p.LastName,
        ["middleName"] = p.MiddleName,
        ["birthDate"] = p.BirthDate?.ToString("yyyy-MM-dd"),
        ["gender"] = p.Gender.ToString(),
        ["katoCode"] = p.KatoCode,
        ["address"] = p.Address,
        ["email"] = p.Email,
        ["comment"] = p.Comment,
        ["consentApp"] = p.ConsentApp.ToString(),
        ["consentSms"] = p.ConsentSms.ToString(),
        ["consentWhatsApp"] = p.ConsentWhatsApp.ToString(),
        ["consentEmail"] = p.ConsentEmail.ToString(),
        ["customFields"] = p.CustomFields.Count == 0 ? null : JsonSerializer.Serialize(p.CustomFields.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value)),
    };

    /// <summary>Мұрағат (§18): ашық қарыз болса — болмайды; бонус қалдығы болса — растаумен. Сатылым мен тарих сақталады.</summary>
    public async Task<ClientCardDto> ArchiveAsync(Guid id, ClientArchiveRequest r, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.ClientsArchive, ct, ensure: false);
        var reason = access.Reason(r.Reason);
        var orgStores = await repo.OrgStoreIdsAsync(c.OrgId, ct);
        var all = c with { StoreIds = orgStores };
        await unitOfWork.InTransactionAsync(async () =>
        {
            var profile = await access.ProfileAsync(c, id, ct);
            if (profile.IsArchived) return true;
            var row = await repo.RowAsync(all.Query(), id, ct) ?? throw new NotFoundException(Messages.ClientNotFound(Lang));
            if (row.Debt > 0) throw new ValidationException(Messages.ClientArchiveHasDebt(Lang, row.Debt), "debt");
            if (row.Bonus > 0 && !r.Force) throw new ValidationException(Messages.ClientArchiveHasBonus(Lang, row.Bonus), "bonus");
            profile.IsArchived = true;
            profile.ArchivedAt = DateTime.UtcNow;
            profile.ArchivedBy = access.StaffUserId;
            profile.ArchiveReason = reason;
            access.Audit(c, "client.archive", profile.Id, null, new { Reason = reason, row.Bonus });
            await unitOfWork.SaveChangesAsync(ct);
            return true;
        }, ct);
        return await reader.BuildCardAsync(c, id, ct);
    }

    public async Task<ClientCardDto> RestoreAsync(Guid id, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.ClientsArchive, ct, ensure: false);
        var profile = await access.ProfileAsync(c, id, ct);
        if (profile.MergedIntoId is not null) throw new ValidationException(Messages.ClientMergedCannotRestore(Lang));
        if (profile.IsArchived)
        {
            access.Audit(c, "client.restore", profile.Id, new { profile.ArchiveReason }, null);
            profile.IsArchived = false;
            profile.ArchivedAt = null;
            profile.ArchivedBy = null;
            profile.ArchiveReason = null;
            profile.UpdatedAt = DateTime.UtcNow;
            profile.UpdatedBy = access.StaffUserId;
            await unitOfWork.SaveChangesAsync(ct);
        }
        return await reader.BuildCardAsync(c, id, ct);
    }

    /// <summary>
    /// Біріктіру (§20): телікті карточканың ұйым дүкендеріндегі сатылымдары, қарыздары, бонус карталары (бір дүкенде екеуі болса —
    /// партиялар мен қалдық қосылады), қаржы операциялары, жазбалары негізгіге көшеді; бос өрістер толтырылады; телік мұрағатқа түседі.
    /// Адамның жаһандық аккаунты (SalemBonus қосымшасы) өзгермейді.
    /// </summary>
    public async Task<ClientCardDto> MergeAsync(ClientMergeRequest r, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.ClientsMerge, ct, ensure: false);
        if (r.PrimaryId == r.DuplicateId) throw new ValidationException(Messages.ClientMergeSame(Lang));
        var orgStores = await repo.OrgStoreIdsAsync(c.OrgId, ct);
        await unitOfWork.InTransactionAsync(async () =>
        {
            var primary = await access.ProfileAsync(c, r.PrimaryId, ct);
            var duplicate = await access.ProfileAsync(c, r.DuplicateId, ct);
            if (primary.IsArchived || duplicate.IsArchived) throw new ValidationException(Messages.ClientMergeArchived(Lang));
            var counts = await repo.MergeAsync(c.OrgId, orgStores, primary, duplicate, ct);
            var filled = new List<string>();
            void Fill(string name, string? current, string? value, Action<string> set)
            {
                if (string.IsNullOrEmpty(current) && !string.IsNullOrEmpty(value)) { set(value); filled.Add(name); }
            }
            Fill("middleName", primary.MiddleName, duplicate.MiddleName, v => primary.MiddleName = v);
            Fill("lastName", primary.LastName, duplicate.LastName, v => primary.LastName = v);
            if (primary.BirthDate is null && duplicate.BirthDate is not null) { primary.BirthDate = duplicate.BirthDate; filled.Add("birthDate"); }
            Fill("katoCode", primary.KatoCode, duplicate.KatoCode, v => primary.KatoCode = v);
            Fill("address", primary.Address, duplicate.Address, v => primary.Address = v);
            Fill("email", primary.Email, duplicate.Email, v => primary.Email = v);
            Fill("comment", primary.Comment, duplicate.Comment, v => primary.Comment = v);
            if (primary.Gender == ClientGender.Unknown && duplicate.Gender != ClientGender.Unknown) { primary.Gender = duplicate.Gender; filled.Add("gender"); }
            foreach (var (key, value) in duplicate.CustomFields.Where(x => !primary.CustomFields.ContainsKey(x.Key)))
            {
                primary.CustomFields = new Dictionary<string, string>(primary.CustomFields) { [key] = value };
                filled.Add($"customFields.{key}");
            }
            var now = DateTime.UtcNow;
            primary.UpdatedAt = now;
            primary.UpdatedBy = access.StaffUserId;
            duplicate.IsArchived = true;
            duplicate.ArchivedAt = now;
            duplicate.ArchivedBy = access.StaffUserId;
            duplicate.MergedIntoId = primary.Id;
            duplicate.ArchiveReason = (Lang == AppLanguage.Ru ? "Объединён с ID " : "Біріктірілді: ID ") + primary.Number.ToString("000000");
            var summary = new
            {
                counts.Sales, counts.Debts, counts.CardsMoved, counts.CardsMerged, counts.BonusMoved, counts.Operations, counts.Notes, Filled = filled,
                DuplicateNumber = duplicate.Number, PrimaryNumber = primary.Number,
            };
            repo.AddMergeLog(new ClientMergeLog
            {
                Id = Guid.NewGuid(), OrganizationId = c.OrgId, PrimaryProfileId = primary.Id, DuplicateProfileId = duplicate.Id, PrimaryCustomerId = primary.CustomerId,
                DuplicateCustomerId = duplicate.CustomerId, Summary = JsonSerializer.Serialize(summary), StaffUserId = access.StaffUserId, CreatedAt = now,
            });
            access.Audit(c, "client.merge", primary.Id, null, summary);
            access.Audit(c, "client.merged", duplicate.Id, null, new { PrimaryNumber = primary.Number });
            await unitOfWork.SaveChangesAsync(ct);
            return true;
        }, ct);
        return await reader.BuildCardAsync(c, r.PrimaryId, ct);
    }

    /// <summary>Ықтимал телік (§20): аты-жөні бірдей, не e-mail бірдей, не аты мен туған күні бірдей карточкалар.</summary>
    public async Task<IReadOnlyList<ClientDuplicateDto>> DuplicatesAsync(Guid id, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.ClientsView, ct, ensure: false);
        var orgStores = await repo.OrgStoreIdsAsync(c.OrgId, ct);
        var all = c with { StoreIds = orgStores };
        var self = await repo.RowAsync(all.Query(), id, ct) ?? throw new NotFoundException(Messages.ClientNotFound(Lang));
        var (rows, _) = await repo.ListAsync(all.Query() with { Take = 20_000 }, ct);
        static string Norm(string? s) => (s ?? string.Empty).Trim().ToLowerInvariant();
        var name = Norm(self.FirstName) + " " + Norm(self.LastName);
        var list = new List<ClientDuplicateDto>();
        foreach (var r in rows.Where(r => r.Profile.Id != id))
        {
            string? reason = null;
            if (Norm(self.LastName).Length > 0 && Norm(r.FirstName) + " " + Norm(r.LastName) == name) reason = "name";
            else if (self.Profile.Email is { Length: > 0 } e && string.Equals(e, r.Profile.Email, StringComparison.OrdinalIgnoreCase)) reason = "email";
            else if (self.BirthDate is { } b && r.BirthDate == b && Norm(r.FirstName) == Norm(self.FirstName)) reason = "birthDate";
            if (reason is null) continue;
            list.Add(new ClientDuplicateDto(r.Profile.Id, r.Profile.Number, BonusAccess.FullName(r.FirstName, r.LastName), BonusAccess.Phone(r.Phone, c.Can.Contacts),
                r.BirthDate, reason));
        }
        return list.Take(20).ToList();
    }

    // ---------- Топтық әрекеттер ----------

    public async Task<ClientBulkResultDto> AddToSegmentAsync(ClientBulkSegmentRequest r, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.ClientsEdit, ct, ensure: false);
        var ids = (r.Ids ?? []).Distinct().ToList();
        if (ids.Count == 0) throw new ValidationException(Messages.ClientSelectionRequired(Lang), "ids");
        var segment = await repo.SegmentAsync(c.OrgId, r.SegmentId, ct);
        if (segment is null || (!segment.IsShared && segment.CreatedBy != access.StaffUserId)) throw new NotFoundException(Messages.ClientSegmentNotFound(Lang));
        var valid = new List<Guid>();
        foreach (var id in ids) if (await repo.GetAsync(c.OrgId, id, ct) is not null) valid.Add(id);
        var before = segment.IncludeIds.Count;
        segment.IncludeIds = segment.IncludeIds.Concat(valid).Distinct().ToList();
        segment.UpdatedAt = DateTime.UtcNow;
        access.Catalog.Audit(c.OrgId, c.Store.Id, "client.segment.add", "client_segment", segment.Id, null, new { Added = segment.IncludeIds.Count - before });
        await unitOfWork.SaveChangesAsync(ct);
        return new ClientBulkResultDto(valid.Count, ids.Count - valid.Count);
    }

    /// <summary>Топтық мәртебе (§17): «Бонусы» бөлімінің қолмен мәртебе беру ережесімен (bonus.manage, себебімен), ағымдағы дүкенде.</summary>
    public async Task<ClientBulkResultDto> AssignStatusAsync(ClientBulkStatusRequest r, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.ClientsEdit, ct, ensure: false);
        access.Require(c.Can.BonusManage);
        var ids = (r.Ids ?? []).Distinct().ToList();
        if (ids.Count == 0) throw new ValidationException(Messages.ClientSelectionRequired(Lang), "ids");
        var reason = access.Reason(r.Reason);
        int done = 0, skipped = 0;
        foreach (var id in ids)
        {
            var profile = await repo.GetAsync(c.OrgId, id, ct);
            if (profile is null || profile.IsArchived || await repo.CardAsync(profile.CustomerId, c.Store.Id, ct) is null) { skipped++; continue; }
            await statuses.ChangeStatusAsync(profile.CustomerId, new ChangeStatusRequest("manual", r.StatusId, reason), ct);
            done++;
        }
        return new ClientBulkResultDto(done, skipped);
    }
}
