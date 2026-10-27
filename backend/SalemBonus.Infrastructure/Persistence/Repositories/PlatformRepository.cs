using Microsoft.EntityFrameworkCore;
using SalemBonus.Application.Common;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Domain.Core;
using SalemBonus.Domain.Entities;
using SalemBonus.Domain.Platform;
using SalemBonus.Domain.Pos;
using SalemBonus.Domain.Pos.Sales;

namespace SalemBonus.Infrastructure.Persistence.Repositories;

public class PlatformRepository(AppDbContext db) : IPlatformRepository
{
    // ---------- Әкімшілер ----------

    public Task<PlatformAdmin?> AdminByEmailAsync(string email, CancellationToken ct = default) =>
        db.PlatformAdmins.FirstOrDefaultAsync(a => a.Email == email, ct);

    public Task<PlatformAdmin?> AdminAsync(Guid id, CancellationToken ct = default) =>
        db.PlatformAdmins.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<PlatformAdmin>> AdminsAsync(CancellationToken ct = default) =>
        await db.PlatformAdmins.AsNoTracking().OrderBy(a => a.Name).ToListAsync(ct);

    public void AddAdmin(PlatformAdmin admin) => db.PlatformAdmins.Add(admin);

    // ---------- Тарифтер ----------

    public async Task<IReadOnlyList<PlatformPlan>> PlansAsync(CancellationToken ct = default) =>
        await db.PlatformPlans.OrderBy(p => p.SortOrder).ThenBy(p => p.MonthlyPrice).ToListAsync(ct);

    public Task<PlatformPlan?> PlanAsync(Guid id, CancellationToken ct = default) => db.PlatformPlans.FirstOrDefaultAsync(p => p.Id == id, ct);

    public void AddPlan(PlatformPlan plan) => db.PlatformPlans.Add(plan);

    // ---------- Бизнестер ----------

    public async Task<IReadOnlyList<Organization>> OrganizationsAsync(CancellationToken ct = default) =>
        await db.Organizations.AsNoTracking().OrderByDescending(o => o.CreatedAt).ToListAsync(ct);

    public Task<Organization?> OrganizationAsync(Guid id, CancellationToken ct = default) => db.Organizations.FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task<Dictionary<Guid, OrgStats>> StatsAsync(IReadOnlyCollection<Guid> orgIds, DateTime sinceUtc, CancellationToken ct = default)
    {
        if (orgIds.Count == 0) return [];
        var ids = orgIds.ToArray();
        var stores = await db.Stores.AsNoTracking().Where(s => s.OrganizationId != null && ids.Contains(s.OrganizationId.Value))
            .GroupBy(s => s.OrganizationId!.Value).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var staff = await db.StaffUsers.AsNoTracking().Where(u => ids.Contains(u.OrganizationId) && u.IsActive)
            .GroupBy(u => u.OrganizationId).Select(g => new { g.Key, Count = g.Count(), Last = g.Max(u => u.LastLoginAt) })
            .ToDictionaryAsync(x => x.Key, ct);
        var recent = await db.Sales.AsNoTracking().Where(s => ids.Contains(s.OrganizationId) && s.CreatedAt >= sinceUtc && s.Status != SaleStatus.Returned)
            .GroupBy(s => s.OrganizationId).Select(g => new { g.Key, Count = g.Count(), Sum = g.Sum(s => s.Total - s.ReturnedAmount) })
            .ToDictionaryAsync(x => x.Key, ct);
        var last = await db.Sales.AsNoTracking().Where(s => ids.Contains(s.OrganizationId))
            .GroupBy(s => s.OrganizationId).Select(g => new { g.Key, Last = g.Max(s => s.CreatedAt) }).ToDictionaryAsync(x => x.Key, x => x.Last, ct);
        var clients = await (from c in db.BonusCards.AsNoTracking()
                             join s in db.Stores.AsNoTracking() on c.StoreId equals s.Id
                             where s.OrganizationId != null && ids.Contains(s.OrganizationId.Value)
                             group c by s.OrganizationId!.Value into g
                             select new { g.Key, Count = g.Select(x => x.CustomerId).Distinct().Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var owners = await (from m in db.StoreMemberships.AsNoTracking()
                            join u in db.StaffUsers.AsNoTracking() on m.StaffUserId equals u.Id
                            where m.Role == StaffRole.Owner && ids.Contains(u.OrganizationId)
                            orderby u.CreatedAt
                            select new { u.OrganizationId, u.FirstName, u.LastName, u.Phone }).ToListAsync(ct);
        return ids.ToDictionary(id => id, id =>
        {
            var owner = owners.FirstOrDefault(o => o.OrganizationId == id);
            var st = staff.GetValueOrDefault(id);
            var r = recent.GetValueOrDefault(id);
            return new OrgStats(stores.GetValueOrDefault(id), st?.Count ?? 0, r?.Count ?? 0, r?.Sum ?? 0, last.TryGetValue(id, out var l) ? l : null,
                clients.GetValueOrDefault(id), owner is null ? null : $"{owner.FirstName} {owner.LastName}".Trim(), owner?.Phone, st?.Last);
        });
    }

    public void AddOrganization(Organization organization) => db.Organizations.Add(organization);

    // ---------- Дүкендер ----------

    public async Task<IReadOnlyList<Store>> StoresAsync(Guid orgId, CancellationToken ct = default) =>
        await db.Stores.AsNoTracking().Where(s => s.OrganizationId == orgId).OrderBy(s => s.CreatedAt).ToListAsync(ct);

    public Task<Store?> StoreAsync(Guid orgId, Guid storeId, CancellationToken ct = default) =>
        db.Stores.FirstOrDefaultAsync(s => s.OrganizationId == orgId && s.Id == storeId, ct);

    public async Task<Dictionary<Guid, int>> RegisterCountsAsync(IReadOnlyCollection<Guid> storeIds, CancellationToken ct = default) =>
        storeIds.Count == 0 ? [] : await db.Registers.AsNoTracking().Where(r => storeIds.Contains(r.StoreId))
            .GroupBy(r => r.StoreId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);

    public Task<bool> JoinCodeExistsAsync(string code, CancellationToken ct = default) => db.Stores.AnyAsync(s => s.JoinCode == code, ct);

    public void AddStore(Store store) => db.Stores.Add(store);

    public async Task EnsureStoreDefaultsAsync(Guid orgId, Guid storeId, CancellationToken ct = default)
    {
        await CatalogDefaults.EnsureUnitsAsync(db, orgId, ct);
        var hasRegister = db.ChangeTracker.Entries<Register>().Any(e => e.Entity.StoreId == storeId) || await db.Registers.AnyAsync(r => r.StoreId == storeId, ct);
        if (!hasRegister) db.Registers.Add(new Register { Id = Guid.NewGuid(), StoreId = storeId, Name = "Касса №1" });
    }

    // ---------- Қызметкерлер ----------

    public async Task<IReadOnlyList<StaffUser>> StaffAsync(Guid orgId, CancellationToken ct = default) =>
        await db.StaffUsers.AsNoTracking().Include(u => u.Memberships).ThenInclude(m => m.Store)
            .Where(u => u.OrganizationId == orgId).OrderBy(u => u.CreatedAt).ToListAsync(ct);

    public Task<StaffUser?> StaffUserAsync(Guid orgId, Guid id, CancellationToken ct = default) =>
        db.StaffUsers.Include(u => u.Memberships).ThenInclude(m => m.Store).FirstOrDefaultAsync(u => u.OrganizationId == orgId && u.Id == id, ct);

    public Task<bool> StaffPhoneExistsAsync(string phone, CancellationToken ct = default) => db.StaffUsers.AnyAsync(u => u.Phone == phone, ct);

    public void AddStaff(StaffUser user) => db.StaffUsers.Add(user);

    public void AddMembership(StoreMembership membership) => db.StoreMemberships.Add(membership);

    public async Task RevokeStaffSessionsAsync(Guid staffUserId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        await db.StaffRefreshTokens.Where(t => t.StaffUserId == staffUserId && t.RevokedAt == null)
            .ExecuteUpdateAsync(x => x.SetProperty(t => t.RevokedAt, now), ct);
    }

    // ---------- Төлемдер ----------

    public async Task<(IReadOnlyList<PlatformPayment> Rows, int Total, decimal Sum)> PaymentsAsync(PlatformPaymentQuery q, CancellationToken ct = default)
    {
        var query = db.PlatformPayments.AsNoTracking().AsQueryable();
        if (q.OrganizationId is { } org) query = query.Where(p => p.OrganizationId == org);
        if (q.From is { } from) query = query.Where(p => p.PaidOn >= from);
        if (q.To is { } to) query = query.Where(p => p.PaidOn <= to);
        if (!string.IsNullOrWhiteSpace(q.Method)) query = query.Where(p => p.Method == q.Method);
        if (!q.IncludeVoided) query = query.Where(p => p.VoidedAt == null);
        var total = await query.CountAsync(ct);
        var sum = await query.Where(p => p.VoidedAt == null).SumAsync(p => (decimal?)p.Amount, ct) ?? 0;
        var rows = await query.OrderByDescending(p => p.PaidOn).ThenByDescending(p => p.CreatedAt).Skip(q.Skip).Take(q.Take).ToListAsync(ct);
        return (rows, total, sum);
    }

    public Task<PlatformPayment?> PaymentAsync(Guid id, CancellationToken ct = default) => db.PlatformPayments.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<DateOnly?> PaidUntilAsync(Guid orgId, CancellationToken ct = default)
    {
        // Сақталмаған (жаңа не жойылған) төлемдер де ескеріледі — қайта есептеу бір транзакция ішінде.
        var tracked = db.ChangeTracker.Entries<PlatformPayment>().Where(e => e.Entity.OrganizationId == orgId).Select(e => e.Entity).ToList();
        var trackedIds = tracked.Select(p => p.Id).ToArray();
        var stored = await db.PlatformPayments.AsNoTracking().Where(p => p.OrganizationId == orgId && p.VoidedAt == null && !trackedIds.Contains(p.Id))
            .MaxAsync(p => (DateOnly?)p.PeriodTo, ct);
        var fromTracked = tracked.Where(p => p.VoidedAt == null).Select(p => (DateOnly?)p.PeriodTo).DefaultIfEmpty(null).Max();
        return stored is null ? fromTracked : fromTracked is null ? stored : (stored > fromTracked ? stored : fromTracked);
    }

    public async Task<IReadOnlyList<(DateOnly PaidOn, decimal Amount)>> PaymentsSinceAsync(DateOnly from, CancellationToken ct = default) =>
        (await db.PlatformPayments.AsNoTracking().Where(p => p.VoidedAt == null && p.PaidOn >= from).Select(p => new { p.PaidOn, p.Amount }).ToListAsync(ct))
        .Select(x => (x.PaidOn, x.Amount)).ToList();

    public void AddPayment(PlatformPayment payment) => db.PlatformPayments.Add(payment);

    // ---------- Лендинг өтінімдері ----------

    public async Task<(IReadOnlyList<PlatformLead> Rows, int Total)> LeadsAsync(string? status, string? search, int skip, int take, CancellationToken ct = default)
    {
        var q = db.PlatformLeads.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(l => l.Status == status);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = $"%{search.Trim()}%";
            var phone = PhoneSearch.Parse(search, 4);
            var (phoneDigits, phonePrefix) = (phone?.Digits, phone?.Prefix);
            q = q.Where(l => EF.Functions.ILike(l.Name, s) || (l.BusinessName != null && EF.Functions.ILike(l.BusinessName, s))
                             || (l.City != null && EF.Functions.ILike(l.City, s))
                             || (phoneDigits != null && (l.Phone.Contains(phoneDigits) || (phonePrefix != null && l.Phone.StartsWith(phonePrefix)))));
        }
        var total = await q.CountAsync(ct);
        return (await q.OrderByDescending(l => l.CreatedAt).Skip(skip).Take(take).ToListAsync(ct), total);
    }

    public async Task<Dictionary<string, int>> LeadCountsAsync(CancellationToken ct = default) =>
        await db.PlatformLeads.AsNoTracking().GroupBy(l => l.Status).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);

    public Task<PlatformLead?> LeadAsync(Guid id, CancellationToken ct = default) => db.PlatformLeads.FirstOrDefaultAsync(l => l.Id == id, ct);

    public Task<bool> RecentLeadExistsAsync(string phone, DateTime sinceUtc, CancellationToken ct = default) =>
        db.PlatformLeads.AnyAsync(l => l.Phone == phone && l.CreatedAt >= sinceUtc, ct);

    public void AddLead(PlatformLead lead) => db.PlatformLeads.Add(lead);

    // ---------- Журнал ----------

    public async Task<Dictionary<Guid, string>> AdminNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
        ids.Count == 0 ? [] : await db.PlatformAdmins.AsNoTracking().Where(a => ids.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => a.Name, ct);

    public void AddAudit(PlatformAuditEntry entry) => db.PlatformAudit.Add(entry);

    public async Task<IReadOnlyList<PlatformAuditEntry>> AuditAsync(Guid? orgId, int take, CancellationToken ct = default)
    {
        var q = db.PlatformAudit.AsNoTracking();
        if (orgId is { } id) q = q.Where(a => a.OrganizationId == id);
        return await q.OrderByDescending(a => a.CreatedAt).Take(take).ToListAsync(ct);
    }
}
