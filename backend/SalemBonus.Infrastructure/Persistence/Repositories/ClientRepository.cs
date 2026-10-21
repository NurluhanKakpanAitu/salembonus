using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Pos.Sales;
using SalemBonus.Domain.Entities;
using SalemBonus.Domain.Enums;
using SalemBonus.Domain.Pos.Clients;
using SalemBonus.Domain.Pos.Finance;
using SalemBonus.Domain.Pos.Sales;

namespace SalemBonus.Infrastructure.Persistence.Repositories;

public class ClientRepository(AppDbContext db) : IClientRepository
{
    // EF позициялық record-қа проекциядан кейін сүзе/сұрыптай алмайды — бәрі осы класта, соңында ClientRow-ға.
    private sealed class ClientQ
    {
        public ClientProfile Profile { get; init; } = null!;
        public string FirstName { get; init; } = "";
        public string LastName { get; init; } = "";
        public DateOnly? BirthDate { get; init; }
        public string Phone { get; init; } = "";
        public string QrCode { get; init; } = "";
        public string? Email { get; init; }
        public bool HasApp { get; init; }
        public Guid? StatusId { get; init; }
        public CustomerLevel? Level { get; init; }
        public bool HasCurrentCard { get; init; }
        public bool OptOut { get; init; }
        public int Purchases { get; init; }
        public decimal Amount { get; init; }
        public DateTime? FirstPurchaseAt { get; init; }
        public DateTime? LastPurchaseAt { get; init; }
        public int Bonus { get; init; }
        public decimal Debt { get; init; }
        public decimal Overdue { get; init; }

        public ClientRow Row() => new(Profile, FirstName, LastName, BirthDate, Phone, QrCode, HasApp, StatusId, Level, HasCurrentCard, OptOut,
            Purchases, Amount, FirstPurchaseAt, LastPurchaseAt, Bonus, Debt, Overdue);
    }

    // ---------- Карточкалар ----------

    /// <summary>Ұйым нөмірлерінің транзакциялық құлпы: бір уақытта жасалған карточкалар бір нөмір алмайды.</summary>
    private Task LockNumbersAsync(Guid orgId, CancellationToken ct)
    {
        var key = BitConverter.ToInt64(orgId.ToByteArray(), 0);
        return db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({key})", ct);
    }

    public async Task EnsureProfilesAsync(Guid orgId, CancellationToken ct = default)
    {
        if (db.Database.CurrentTransaction is not null) { await LockNumbersAsync(orgId, ct); await InsertMissingAsync(orgId, ct); return; }
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockNumbersAsync(orgId, ct);
        await InsertMissingAsync(orgId, ct);
        await tx.CommitAsync(ct);
    }

    private async Task InsertMissingAsync(Guid orgId, CancellationToken ct) =>
        // Картасы бар, бірақ CRM карточкасы жоқ клиенттер: нөмір — бар ең үлкенінен кейін, тіркелу — ұйымдағы бірінші карта күні.
        // Қосымшамен ғана келген (сатылымы жоқ) — «Приложение», қалғаны — «Касса». Жарыста бірегей индекс қайталауды өткізіп жібереді.
        await db.Database.ExecuteSqlAsync($$"""
            INSERT INTO pos.client_profiles ("Id", "OrganizationId", "CustomerId", "Number", "FirstName", "LastName", "BirthDate", "Gender", "KatoCode", "Email",
                "Source", "ConsentApp", "ConsentSms", "ConsentWhatsApp", "ConsentEmail", "CustomFields", "IsArchived", "CreatedAt", "UpdatedAt")
            SELECT gen_random_uuid(), {{orgId}}, x."CustomerId",
                COALESCE((SELECT MAX(p."Number") FROM pos.client_profiles p WHERE p."OrganizationId" = {{orgId}}), 0) + ROW_NUMBER() OVER (ORDER BY x."CreatedAt", x."CustomerId"),
                c."FirstName", c."LastName", c."BirthDate", 'Unknown', c."KatoCode", c."Email",
                CASE WHEN EXISTS (SELECT 1 FROM "RefreshTokens" t WHERE t."CustomerId" = x."CustomerId")
                      AND NOT EXISTS (SELECT 1 FROM pos.sales s WHERE s."CustomerId" = x."CustomerId") THEN 'App' ELSE 'Cashier' END,
                false, false, false, false, '{}'::jsonb, false, x."CreatedAt", now()
            FROM (SELECT b."CustomerId", MIN(b."CreatedAt") AS "CreatedAt"
                  FROM "BonusCards" b JOIN "Stores" st ON st."Id" = b."StoreId"
                  WHERE st."OrganizationId" = {{orgId}}
                  GROUP BY b."CustomerId") x
            JOIN "Customers" c ON c."Id" = x."CustomerId"
            WHERE NOT EXISTS (SELECT 1 FROM pos.client_profiles p WHERE p."OrganizationId" = {{orgId}} AND p."CustomerId" = x."CustomerId")
            ON CONFLICT DO NOTHING
            """, ct);

    public Task<ClientSettings?> SettingsAsync(Guid orgId, CancellationToken ct = default) =>
        db.ClientSettings.FirstOrDefaultAsync(s => s.OrganizationId == orgId, ct);

    public void AddSettings(ClientSettings settings) => db.ClientSettings.Add(settings);

    public Task<ClientProfile?> GetAsync(Guid orgId, Guid id, CancellationToken ct = default) =>
        db.ClientProfiles.FirstOrDefaultAsync(p => p.OrganizationId == orgId && p.Id == id, ct);

    public Task<ClientProfile?> ByCustomerAsync(Guid orgId, Guid customerId, CancellationToken ct = default) =>
        db.ClientProfiles.FirstOrDefaultAsync(p => p.OrganizationId == orgId && p.CustomerId == customerId, ct);

    public Task<ClientProfile?> ByNumberAsync(Guid orgId, long number, CancellationToken ct = default) =>
        db.ClientProfiles.FirstOrDefaultAsync(p => p.OrganizationId == orgId && p.Number == number, ct);

    public async Task<Dictionary<Guid, ClientProfile>> ByCustomersAsync(Guid orgId, IReadOnlyCollection<Guid> customerIds, CancellationToken ct = default) =>
        customerIds.Count == 0 ? [] : await db.ClientProfiles.Where(p => p.OrganizationId == orgId && customerIds.Contains(p.CustomerId)).ToDictionaryAsync(p => p.CustomerId, ct);

    public async Task<long> NextNumberAsync(Guid orgId, CancellationToken ct = default)
    {
        await LockNumbersAsync(orgId, ct);
        var tracked = db.ChangeTracker.Entries<ClientProfile>().Where(e => e.State == EntityState.Added && e.Entity.OrganizationId == orgId)
            .Select(e => e.Entity.Number).DefaultIfEmpty(0).Max();
        var stored = await db.ClientProfiles.Where(p => p.OrganizationId == orgId).MaxAsync(p => (long?)p.Number, ct) ?? 0;
        return Math.Max(tracked, stored) + 1;
    }

    public void Add(ClientProfile profile) => db.ClientProfiles.Add(profile);

    public async Task<IReadOnlyList<Guid>> OrgStoreIdsAsync(Guid orgId, CancellationToken ct = default) =>
        await db.Stores.AsNoTracking().Where(s => s.OrganizationId == orgId).Select(s => s.Id).ToListAsync(ct);

    public async Task<Dictionary<Guid, string>> StoreNamesAsync(Guid orgId, CancellationToken ct = default) =>
        await db.Stores.AsNoTracking().Where(s => s.OrganizationId == orgId).ToDictionaryAsync(s => s.Id, s => s.Name, ct);

    public async Task<Dictionary<Guid, string>> StaffNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
        ids.Count == 0 ? [] : await db.StaffUsers.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => (u.FirstName + " " + u.LastName).Trim(), ct);

    public Task<bool> HasAppAsync(Guid customerId, CancellationToken ct = default) => db.RefreshTokens.AnyAsync(t => t.CustomerId == customerId, ct);

    public Task<bool> HasCardsOutsideAsync(Guid customerId, IReadOnlyCollection<Guid> storeIds, CancellationToken ct = default) =>
        db.BonusCards.AnyAsync(c => c.CustomerId == customerId && !storeIds.Contains(c.StoreId), ct);

    public async Task<Dictionary<string, Customer>> CustomersByPhonesAsync(IReadOnlyCollection<string> phones, CancellationToken ct = default) =>
        phones.Count == 0 ? [] : await db.Customers.Where(c => phones.Contains(c.Phone)).ToDictionaryAsync(c => c.Phone, ct);

    public Task<BonusCard?> CardAsync(Guid customerId, Guid storeId, CancellationToken ct = default) =>
        db.BonusCards.FirstOrDefaultAsync(c => c.CustomerId == customerId && c.StoreId == storeId, ct);

    // ---------- Тізім ----------

    private IQueryable<ClientQ> Base(ClientQuery q, bool requireCard = true)
    {
        var org = q.OrgId;
        var stores = q.StoreIds.ToArray();
        var cur = q.CurrentStoreId;
        var today = q.Today;
        var profiles = db.ClientProfiles.AsNoTracking().Where(p => p.OrganizationId == org);
        if (requireCard) profiles = profiles.Where(p => db.BonusCards.Any(c => c.CustomerId == p.CustomerId && stores.Contains(c.StoreId)));
        return from p in profiles
               join u in db.Customers.AsNoTracking() on p.CustomerId equals u.Id
               select new ClientQ
               {
                   Profile = p,
                   FirstName = p.FirstName != "" ? p.FirstName : u.FirstName,
                   LastName = p.LastName != "" ? p.LastName : u.LastName,
                   BirthDate = p.BirthDate ?? u.BirthDate,
                   Phone = u.Phone,
                   QrCode = u.QrCode,
                   Email = p.Email ?? u.Email,
                   HasApp = db.RefreshTokens.Any(t => t.CustomerId == p.CustomerId),
                   StatusId = db.BonusCards.Where(c => c.CustomerId == p.CustomerId && c.StoreId == cur).Select(c => c.StatusId).FirstOrDefault(),
                   Level = db.BonusCards.Where(c => c.CustomerId == p.CustomerId && c.StoreId == cur).Select(c => (CustomerLevel?)c.Level).FirstOrDefault(),
                   HasCurrentCard = db.BonusCards.Any(c => c.CustomerId == p.CustomerId && c.StoreId == cur),
                   OptOut = db.BonusCards.Any(c => c.CustomerId == p.CustomerId && c.StoreId == cur && c.NotificationsOptOut),
                   Purchases = db.Sales.Count(s => s.CustomerId == p.CustomerId && stores.Contains(s.StoreId) && s.Status != SaleStatus.Returned),
                   Amount = db.Sales.Where(s => s.CustomerId == p.CustomerId && stores.Contains(s.StoreId) && s.Status != SaleStatus.Returned)
                       .Sum(s => (decimal?)(s.Total - s.ReturnedAmount)) ?? 0,
                   FirstPurchaseAt = db.Sales.Where(s => s.CustomerId == p.CustomerId && stores.Contains(s.StoreId) && s.Status != SaleStatus.Returned).Min(s => (DateTime?)s.CreatedAt),
                   LastPurchaseAt = db.Sales.Where(s => s.CustomerId == p.CustomerId && stores.Contains(s.StoreId) && s.Status != SaleStatus.Returned).Max(s => (DateTime?)s.CreatedAt),
                   Bonus = db.BonusCards.Where(c => c.CustomerId == p.CustomerId && stores.Contains(c.StoreId)).Sum(c => (int?)c.Balance) ?? 0,
                   Debt = db.Debts.Where(d => d.CustomerId == p.CustomerId && stores.Contains(d.StoreId) && d.Status == DebtStatus.Open).Sum(d => (decimal?)(d.Amount - d.Paid)) ?? 0,
                   Overdue = db.Debts.Where(d => d.CustomerId == p.CustomerId && stores.Contains(d.StoreId) && d.Status == DebtStatus.Open && d.DueDate < today)
                       .Sum(d => (decimal?)(d.Amount - d.Paid)) ?? 0,
               };
    }

    /// <summary>КАТО: облыс (2 таңба), аудан (4), округ (6) не елді мекен (9) — таңдалған деңгейдің префиксі.</summary>
    private static string KatoPrefix(string code)
    {
        var c = code.Trim();
        if (c.Length != 9) return c;
        if (c[2..].All(ch => ch == '0')) return c[..2];
        if (c[4..].All(ch => ch == '0')) return c[..4];
        if (c[6..].All(ch => ch == '0')) return c[..6];
        return c;
    }

    private static string Title(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..].ToLowerInvariant();

    private static IQueryable<ClientQ> Apply(IQueryable<ClientQ> q, ClientQuery x, bool quick)
    {
        var f = x.Filter;
        if (x.Ids is { } ids)
        {
            var arr = ids.ToArray();
            q = q.Where(r => arr.Contains(r.Profile.Id));
        }
        q = quick && f.Quick == "archived" ? q.Where(r => r.Profile.IsArchived) : q.Where(r => !r.Profile.IsArchived);

        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = f.Search.Trim();
            // Телефон мен ID — тек сан түріндегі сұраныста (әріп аралас мәтіндегі сандар телефонмен салыстырылмайды).
            var numeric = s.TrimStart('#').Replace("ID", "", StringComparison.OrdinalIgnoreCase).All(ch => char.IsDigit(ch) || " +()-:".Contains(ch));
            var digits = numeric ? new string(s.Where(char.IsDigit).ToArray()) : string.Empty;
            if (digits.Length == 11 && digits[0] == '8') digits = "7" + digits[1..];
            var idText = s.TrimStart('#').Replace("ID", "", StringComparison.OrdinalIgnoreCase).Replace(":", "").Trim();
            long? number = idText.Length is > 0 and <= 9 && idText.All(char.IsDigit) ? long.Parse(idText) : null;
            // Базаның локалі кириллицаны кіші әріпке түсірмеуі мүмкін — жазылғандай, кіші әріппен және бас әріппен іздейміз.
            var a = $"%{s}%";
            var b = $"%{s.ToLowerInvariant()}%";
            var c = $"%{Title(s)}%";
            q = q.Where(r =>
                EF.Functions.ILike(r.LastName + " " + r.FirstName + " " + (r.Profile.MiddleName ?? "") + " " + r.FirstName + " " + r.LastName, a)
                || EF.Functions.ILike(r.LastName + " " + r.FirstName + " " + (r.Profile.MiddleName ?? "") + " " + r.FirstName + " " + r.LastName, b)
                || EF.Functions.ILike(r.LastName + " " + r.FirstName + " " + (r.Profile.MiddleName ?? "") + " " + r.FirstName + " " + r.LastName, c)
                || EF.Functions.ILike(r.QrCode, a)
                || (r.Email != null && EF.Functions.ILike(r.Email, b))
                || (digits.Length >= 3 && r.Phone.Contains(digits))
                || (number != null && r.Profile.Number == number));
        }
        if (f.StatusIds is { Count: > 0 })
        {
            var st = f.StatusIds.ToArray();
            q = q.Where(r => r.StatusId != null && st.Contains(r.StatusId.Value));
        }
        if (f.RegisteredFrom is { } rf) { var t = StoreTime.StartOfDayUtc(rf, x.Zone); q = q.Where(r => r.Profile.CreatedAt >= t); }
        if (f.RegisteredTo is { } rt) { var t = StoreTime.StartOfDayUtc(rt.AddDays(1), x.Zone); q = q.Where(r => r.Profile.CreatedAt < t); }
        if (f.LastPurchaseFrom is { } lf) { var t = StoreTime.StartOfDayUtc(lf, x.Zone); q = q.Where(r => r.LastPurchaseAt >= t); }
        if (f.LastPurchaseTo is { } lt) { var t = StoreTime.StartOfDayUtc(lt.AddDays(1), x.Zone); q = q.Where(r => r.LastPurchaseAt < t); }
        if (f.MinPurchases is { } minP) q = q.Where(r => r.Purchases >= minP);
        if (f.MaxPurchases is { } maxP) q = q.Where(r => r.Purchases <= maxP);
        if (f.MinAmount is { } minA) q = q.Where(r => r.Amount >= minA);
        if (f.MaxAmount is { } maxA) q = q.Where(r => r.Amount <= maxA);
        if (f.MinAverage is { } minAv) q = q.Where(r => r.Purchases > 0 && r.Amount / r.Purchases >= minAv);
        if (f.MaxAverage is { } maxAv) q = q.Where(r => r.Purchases == 0 || r.Amount / r.Purchases <= maxAv);
        if (f.MinBonus is { } minB) q = q.Where(r => r.Bonus >= minB);
        if (f.MaxBonus is { } maxB) q = q.Where(r => r.Bonus <= maxB);
        if (f.HasDebt is { } hd) q = hd ? q.Where(r => r.Debt > 0) : q.Where(r => r.Debt == 0);
        if (f.Overdue is { } od) q = od ? q.Where(r => r.Overdue > 0) : q.Where(r => r.Overdue == 0);
        if (!string.IsNullOrWhiteSpace(f.Kato))
        {
            var prefix = KatoPrefix(f.Kato);
            q = q.Where(r => r.Profile.KatoCode != null && r.Profile.KatoCode.StartsWith(prefix));
        }
        if (Enum.TryParse<ClientGender>(f.Gender, true, out var g)) q = q.Where(r => r.Profile.Gender == g);
        if (f.BirthMonth is { } bm) q = q.Where(r => r.BirthDate != null && r.BirthDate.Value.Month == bm);
        if (Enum.TryParse<ClientSource>(f.Source, true, out var src)) q = q.Where(r => r.Profile.Source == src);
        q = f.Consent switch
        {
            "app" => q.Where(r => r.Profile.ConsentApp),
            "sms" => q.Where(r => r.Profile.ConsentSms),
            "whatsapp" => q.Where(r => r.Profile.ConsentWhatsApp),
            "email" => q.Where(r => r.Profile.ConsentEmail),
            _ => q,
        };
        if (!quick) return q;

        var active = x.ActiveSince;
        var passive = x.PassiveBefore;
        var fresh = x.NewSince;
        return f.Quick switch
        {
            "active" => q.Where(r => r.LastPurchaseAt >= active),
            "cooling" => q.Where(r => r.LastPurchaseAt < active && r.LastPurchaseAt >= passive),
            "passive" => q.Where(r => r.LastPurchaseAt < passive),
            "new" => q.Where(r => r.Profile.CreatedAt >= fresh),
            "repeat" => q.Where(r => r.Purchases >= 2),
            "debt" => q.Where(r => r.Debt > 0),
            "overdue" => q.Where(r => r.Overdue > 0),
            "bonus" => q.Where(r => r.Bonus > 0),
            "noPurchases" => q.Where(r => r.Purchases == 0),
            "birthday" => Birthdays(q, x.Today, x.BirthdayDays),
            _ => q,
        };
    }

    /// <summary>Туған күні бүгіннен бастап N күн ішінде (жылға қарамай); 29 ақпан кібісе емес жылы 28-інде саналады.</summary>
    private static IQueryable<ClientQ> Birthdays(IQueryable<ClientQ> q, DateOnly today, int days)
    {
        var keys = Enumerable.Range(0, Math.Clamp(days, 1, 366)).Select(i => today.AddDays(i)).SelectMany(d =>
            d.Month == 2 && d.Day == 28 && !DateTime.IsLeapYear(d.Year) ? new[] { 228, 229 } : [d.Month * 100 + d.Day]).Distinct().ToArray();
        return q.Where(r => r.BirthDate != null && keys.Contains(r.BirthDate.Value.Month * 100 + r.BirthDate.Value.Day));
    }

    private static IOrderedQueryable<ClientQ> By<TKey>(IQueryable<ClientQ> q, Expression<Func<ClientQ, TKey>> key, bool desc) =>
        desc ? q.OrderByDescending(key) : q.OrderBy(key);

    private static IQueryable<ClientQ> Sort(IQueryable<ClientQ> q, string? sort, bool desc) => sort switch
    {
        "name" => By(q, r => r.FirstName, desc).ThenBy(r => r.LastName).ThenBy(r => r.Profile.Number),
        "number" => By(q, r => r.Profile.Number, desc),
        "registered" => By(q, r => r.Profile.CreatedAt, desc).ThenByDescending(r => r.Profile.Number),
        "lastPurchase" => desc
            ? q.OrderBy(r => r.LastPurchaseAt == null).ThenByDescending(r => r.LastPurchaseAt).ThenByDescending(r => r.Profile.Number)
            : q.OrderBy(r => r.LastPurchaseAt == null).ThenBy(r => r.LastPurchaseAt).ThenByDescending(r => r.Profile.Number),
        "purchases" => By(q, r => r.Purchases, desc).ThenByDescending(r => r.Profile.Number),
        "amount" => By(q, r => r.Amount, desc).ThenByDescending(r => r.Profile.Number),
        "average" => By(q, r => r.Purchases == 0 ? 0 : r.Amount / r.Purchases, desc).ThenByDescending(r => r.Profile.Number),
        "bonus" => By(q, r => r.Bonus, desc).ThenByDescending(r => r.Profile.Number),
        "debt" => By(q, r => r.Debt, desc).ThenByDescending(r => r.Profile.Number),
        "birthday" => By(q, r => r.BirthDate == null ? 9999 : r.BirthDate.Value.Month * 100 + r.BirthDate.Value.Day, desc).ThenBy(r => r.FirstName),
        _ => q.OrderByDescending(r => r.Profile.Number),
    };

    public async Task<(IReadOnlyList<ClientRow> Rows, int Total)> ListAsync(ClientQuery q, CancellationToken ct = default)
    {
        var query = Apply(Base(q), q, true);
        var total = await query.CountAsync(ct);
        var rows = await Sort(query, q.Sort, q.Desc).Skip(q.Skip).Take(q.Take).ToListAsync(ct);
        return (rows.Select(r => r.Row()).ToList(), total);
    }

    public async Task<IReadOnlyList<Guid>> IdsAsync(ClientQuery q, CancellationToken ct = default) =>
        await Apply(Base(q), q, true).Select(r => r.Profile.Id).ToListAsync(ct);

    public async Task<ClientKpiCounts> KpisAsync(ClientQuery q, CancellationToken ct = default)
    {
        var b = Apply(Base(q), q, false);
        var active = q.ActiveSince;
        var passive = q.PassiveBefore;
        var fresh = q.NewSince;
        // Бір DbContext-те сұраныстар қатар жүрмейді — бірінен соң бірі.
        var total = await b.CountAsync(ct);
        var act = await b.CountAsync(r => r.LastPurchaseAt >= active, ct);
        var pas = await b.CountAsync(r => r.LastPurchaseAt < passive, ct);
        var nw = await b.CountAsync(r => r.Profile.CreatedAt >= fresh, ct);
        var rep = await b.CountAsync(r => r.Purchases >= 2, ct);
        var debt = await b.CountAsync(r => r.Debt > 0, ct);
        var overdue = await b.CountAsync(r => r.Overdue > 0, ct);
        var bonus = await b.CountAsync(r => r.Bonus > 0, ct);
        return new ClientKpiCounts(total, act, pas, nw, rep, debt, overdue, bonus);
    }

    public async Task<ClientRow?> RowAsync(ClientQuery q, Guid profileId, CancellationToken ct = default) =>
        (await Base(q, requireCard: false).FirstOrDefaultAsync(r => r.Profile.Id == profileId, ct))?.Row();

    public async Task<IReadOnlyList<DateTime>> RegistrationsAsync(ClientQuery q, DateTime sinceUtc, CancellationToken ct = default) =>
        await Apply(Base(q), q, false).Where(r => r.Profile.CreatedAt >= sinceUtc).Select(r => r.Profile.CreatedAt).ToListAsync(ct);

    // ---------- Карточка бөлімдері ----------

    public async Task<(IReadOnlyList<ClientSaleRow> Rows, int Total)> SalesAsync(Guid customerId, IReadOnlyCollection<Guid> storeIds, int skip, int take,
        CancellationToken ct = default)
    {
        var q = db.Sales.AsNoTracking().Where(s => s.CustomerId == customerId && storeIds.Contains(s.StoreId));
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(s => s.CreatedAt).Skip(skip).Take(take)
            .Select(s => new
            {
                s.Id, s.StoreId, s.Number, s.CreatedAt, s.Total, s.ReturnedAmount, s.Status, Items = s.Items.Count, s.BonusAccrued, s.BonusRedeemed, s.DiscountAmount,
                s.StaffUserId, Methods = s.Payments.Select(p => p.Method).ToList(),
            }).ToListAsync(ct);
        return (rows.Select(s => new ClientSaleRow(s.Id, s.StoreId, s.Number, s.CreatedAt, s.Total, s.ReturnedAmount, s.Status, s.Items, s.BonusAccrued,
            s.BonusRedeemed, s.DiscountAmount, s.StaffUserId, s.Methods.Distinct().ToList())).ToList(), total);
    }

    public async Task<IReadOnlyList<(DateTime At, decimal Net)>> SaleTimelineAsync(Guid customerId, IReadOnlyCollection<Guid> storeIds, CancellationToken ct = default) =>
        (await db.Sales.AsNoTracking().Where(s => s.CustomerId == customerId && storeIds.Contains(s.StoreId) && s.Status != SaleStatus.Returned)
            .OrderBy(s => s.CreatedAt).Select(s => new { s.CreatedAt, Net = s.Total - s.ReturnedAmount }).ToListAsync(ct))
        .Select(x => (x.CreatedAt, x.Net)).ToList();

    public async Task<IReadOnlyList<ClientReturnRow>> ReturnsAsync(Guid customerId, IReadOnlyCollection<Guid> storeIds, CancellationToken ct = default) =>
        await (from r in db.SaleReturns.AsNoTracking()
               join s in db.Sales.AsNoTracking() on r.SaleId equals s.Id
               where s.CustomerId == customerId && storeIds.Contains(r.StoreId)
               orderby r.CreatedAt descending
               select new ClientReturnRow(r.Id, r.StoreId, r.SaleId, s.Number, r.CreatedAt, r.Amount, r.Refunded, r.RefundMethod, r.Reason, r.StaffUserId))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ClientProductRow>> TopProductsAsync(Guid customerId, IReadOnlyCollection<Guid> storeIds, int take, CancellationToken ct = default)
    {
        var rows = await (from i in db.SaleItems.AsNoTracking()
                          join s in db.Sales.AsNoTracking() on i.SaleId equals s.Id
                          where s.CustomerId == customerId && storeIds.Contains(s.StoreId) && s.Status != SaleStatus.Returned && i.Quantity > i.ReturnedQuantity
                          group new { i, s } by i.ProductId into g
                          select new
                          {
                              ProductId = g.Key,
                              Name = g.Max(x => x.i.Name),
                              Quantity = g.Sum(x => x.i.Quantity - x.i.ReturnedQuantity),
                              Amount = g.Sum(x => x.i.LineTotal * (x.i.Quantity - x.i.ReturnedQuantity) / x.i.Quantity),
                              Purchases = g.Select(x => x.s.Id).Distinct().Count(),
                          }).OrderByDescending(x => x.Purchases).ThenByDescending(x => x.Amount).Take(take).ToListAsync(ct);
        return rows.Select(x => new ClientProductRow(x.ProductId, x.Name, x.Quantity, Math.Round(x.Amount, 2), x.Purchases)).ToList();
    }

    public async Task<IReadOnlyList<ClientFinanceRow>> FinanceOperationsAsync(Guid orgId, Guid customerId, IReadOnlyCollection<Guid> storeIds, CancellationToken ct = default) =>
        (await db.FinanceOperations.AsNoTracking()
            .Where(o => o.OrganizationId == orgId && o.CounterpartyType == CounterpartyType.Customer && o.CounterpartyId == customerId
                        && storeIds.Contains(o.StoreId) && o.Status != FinanceOperationStatus.Draft)
            .OrderByDescending(o => o.Date).Take(200)
            .Select(o => new { o.Id, o.Number, o.Date, o.Type, o.Status, o.Amount, o.Description, o.StoreId }).ToListAsync(ct))
        .Select(o => new ClientFinanceRow(o.Id, o.Number, o.Date, o.Type.ToString(), o.Status.ToString(), o.Amount, o.Description, o.StoreId)).ToList();

    public async Task<IReadOnlyList<ClientDebtPaymentRow>> DebtPaymentsAsync(Guid customerId, IReadOnlyCollection<Guid> storeIds, CancellationToken ct = default) =>
        await (from p in db.DebtPayments.AsNoTracking()
               join d in db.Debts.AsNoTracking() on p.DebtId equals d.Id
               where d.CustomerId == customerId && storeIds.Contains(d.StoreId)
               orderby p.CreatedAt descending
               select new ClientDebtPaymentRow(p.Id, p.DebtId, d.StoreId, d.SaleNumber, p.CreatedAt, p.Amount, p.Method, p.IsReturn, p.StaffUserId)).ToListAsync(ct);

    // ---------- Жазбалар, хабарлар, тарих ----------

    public async Task<IReadOnlyList<ClientNote>> NotesAsync(Guid profileId, CancellationToken ct = default) =>
        await db.ClientNotes.AsNoTracking().Where(n => n.ProfileId == profileId).OrderByDescending(n => n.CreatedAt).ToListAsync(ct);

    public Task<ClientNote?> NoteAsync(Guid orgId, Guid id, CancellationToken ct = default) =>
        db.ClientNotes.FirstOrDefaultAsync(n => n.OrganizationId == orgId && n.Id == id, ct);

    public void AddNote(ClientNote note) => db.ClientNotes.Add(note);
    public void RemoveNote(ClientNote note) => db.ClientNotes.Remove(note);

    public async Task<IReadOnlyList<ClientCommunication>> CommunicationsAsync(Guid profileId, int take, CancellationToken ct = default) =>
        await db.ClientCommunications.AsNoTracking().Where(c => c.ProfileId == profileId).OrderByDescending(c => c.CreatedAt).Take(take).ToListAsync(ct);

    public void AddCommunication(ClientCommunication communication) => db.ClientCommunications.Add(communication);

    public async Task<IReadOnlyList<ClientAuditRow>> AuditAsync(Guid orgId, Guid profileId, int take, CancellationToken ct = default)
    {
        var id = profileId.ToString();
        return await db.AuditLog.AsNoTracking().Where(a => a.OrganizationId == orgId && a.Entity == "client_profile" && a.EntityId == id)
            .OrderByDescending(a => a.CreatedAt).Take(take)
            .Select(a => new ClientAuditRow(a.Id, a.CreatedAt, a.StaffUserId, a.Action, a.OldValues, a.NewValues)).ToListAsync(ct);
    }

    // ---------- Сегменттер мен импорт ----------

    public async Task<IReadOnlyList<ClientSegment>> SegmentsAsync(Guid orgId, Guid staffUserId, CancellationToken ct = default) =>
        await db.ClientSegments.AsNoTracking().Where(s => s.OrganizationId == orgId && (s.IsShared || s.CreatedBy == staffUserId))
            .OrderBy(s => s.Name).ToListAsync(ct);

    public Task<ClientSegment?> SegmentAsync(Guid orgId, Guid id, CancellationToken ct = default) =>
        db.ClientSegments.FirstOrDefaultAsync(s => s.OrganizationId == orgId && s.Id == id, ct);

    public void AddSegment(ClientSegment segment) => db.ClientSegments.Add(segment);
    public void RemoveSegment(ClientSegment segment) => db.ClientSegments.Remove(segment);

    public Task<ClientImport?> ImportAsync(Guid orgId, Guid id, CancellationToken ct = default) =>
        db.ClientImports.AsNoTracking().FirstOrDefaultAsync(i => i.OrganizationId == orgId && i.Id == id, ct);

    public void AddImport(ClientImport import) => db.ClientImports.Add(import);

    // ---------- Біріктіру ----------

    public async Task<ClientMergeCounts> MergeAsync(Guid orgId, IReadOnlyList<Guid> orgStoreIds, ClientProfile primary, ClientProfile duplicate, CancellationToken ct = default)
    {
        var stores = orgStoreIds.ToArray();
        var p = primary.CustomerId;
        var d = duplicate.CustomerId;
        // Екі клиенттің де карталары бұғатталады: біріктіру кезінде касса балансты өзгерте алмайды.
        await db.Database.ExecuteSqlAsync($"""SELECT 1 FROM "BonusCards" WHERE "CustomerId" IN ({p}, {d}) AND "StoreId" = ANY({stores}) FOR UPDATE""", ct);
        var cards = await db.BonusCards.Where(c => (c.CustomerId == p || c.CustomerId == d) && stores.Contains(c.StoreId)).ToListAsync(ct);
        int moved = 0, merged = 0, bonus = 0;
        foreach (var dc in cards.Where(c => c.CustomerId == d).ToList())
        {
            bonus += dc.Balance;
            var pc = cards.FirstOrDefault(c => c.CustomerId == p && c.StoreId == dc.StoreId);
            if (pc is null)
            {
                dc.CustomerId = p;
                moved++;
                continue;
            }
            // Бір дүкенде екеуінің де картасы бар: операциялар (партиялар) негізгі картаға көшеді, қалдық қосылады, телікті карта жойылады.
            await db.Database.ExecuteSqlAsync($"""UPDATE "BonusTransactions" SET "BonusCardId" = {pc.Id} WHERE "BonusCardId" = {dc.Id}""", ct);
            await db.Database.ExecuteSqlAsync($"""UPDATE pos.sales SET "BonusCardId" = {pc.Id} WHERE "BonusCardId" = {dc.Id}""", ct);
            pc.Balance += dc.Balance;
            pc.TotalSpent += dc.TotalSpent;
            if (dc.CreatedAt < pc.CreatedAt) pc.CreatedAt = dc.CreatedAt;
            db.BonusCards.Remove(dc);
            merged++;
        }
        var sales = await db.Database.ExecuteSqlAsync($"""UPDATE pos.sales SET "CustomerId" = {p} WHERE "CustomerId" = {d} AND "StoreId" = ANY({stores})""", ct);
        var debts = await db.Database.ExecuteSqlAsync($"""UPDATE pos.debts SET "CustomerId" = {p} WHERE "CustomerId" = {d} AND "StoreId" = ANY({stores})""", ct);
        // Туған күн сыйлығы: сол жылы негізгі клиентке берілмеген болса ғана көшеді (екі сыйлық болмайды).
        await db.Database.ExecuteSqlAsync($"""
            UPDATE pos.bonus_birthday_grants g SET "CustomerId" = {p}
            WHERE g."CustomerId" = {d} AND g."StoreId" = ANY({stores})
              AND NOT EXISTS (SELECT 1 FROM pos.bonus_birthday_grants x WHERE x."CustomerId" = {p} AND x."StoreId" = g."StoreId" AND x."Year" = g."Year")
            """, ct);
        var ops = await db.Database.ExecuteSqlAsync($"""
            UPDATE pos.finance_operations SET "CounterpartyId" = {p}
            WHERE "OrganizationId" = {orgId} AND "CounterpartyType" = 'Customer' AND "CounterpartyId" = {d}
            """, ct);
        var notes = await db.Database.ExecuteSqlAsync($"""UPDATE pos.client_notes SET "ProfileId" = {primary.Id} WHERE "ProfileId" = {duplicate.Id}""", ct);
        await db.Database.ExecuteSqlAsync($"""UPDATE pos.client_communications SET "ProfileId" = {primary.Id} WHERE "ProfileId" = {duplicate.Id}""", ct);
        return new ClientMergeCounts(sales, debts, moved, merged, bonus, ops, notes);
    }

    public void AddMergeLog(ClientMergeLog log) => db.ClientMergeLog.Add(log);
}
