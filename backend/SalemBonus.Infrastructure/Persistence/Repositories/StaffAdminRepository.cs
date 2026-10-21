using Microsoft.EntityFrameworkCore;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Domain.Core;
using SalemBonus.Domain.Pos.Sales;

namespace SalemBonus.Infrastructure.Persistence.Repositories;

public class StaffAdminRepository(AppDbContext db) : IStaffAdminRepository
{
    private IQueryable<StaffUser> WithMemberships() => db.StaffUsers.Include(u => u.Memberships).ThenInclude(m => m.Store);

    public async Task<IReadOnlyList<StaffUser>> ListAsync(Guid orgId, CancellationToken ct = default) =>
        await WithMemberships().AsNoTracking().Where(u => u.OrganizationId == orgId).OrderBy(u => u.CreatedAt).ToListAsync(ct);

    public Task<StaffUser?> GetForUpdateAsync(Guid orgId, Guid id, CancellationToken ct = default) =>
        WithMemberships().FirstOrDefaultAsync(u => u.OrganizationId == orgId && u.Id == id, ct);

    public Task<bool> PhoneExistsAsync(string phone, CancellationToken ct = default) => db.StaffUsers.AnyAsync(u => u.Phone == phone, ct);

    public void Add(StaffUser user) => db.StaffUsers.Add(user);

    public void AddMembership(StoreMembership membership) => db.StoreMemberships.Add(membership);

    public Task<int> OtherActiveOwnersAsync(Guid orgId, Guid exceptStaffId, CancellationToken ct = default) =>
        db.StaffUsers.AsNoTracking()
            .Where(u => u.OrganizationId == orgId && u.IsActive && u.Id != exceptStaffId
                && u.Memberships.Any(m => m.IsActive && m.Role == StaffRole.Owner && m.Store!.IsActive))
            .CountAsync(ct);

    public async Task<IReadOnlyList<StaffRefreshToken>> SessionsAsync(Guid staffUserId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return await db.StaffRefreshTokens.AsNoTracking()
            .Where(t => t.StaffUserId == staffUserId && t.RevokedAt == null && t.ExpiresAt > now)
            .OrderByDescending(t => t.CreatedAt).ToListAsync(ct);
    }

    public async Task<bool> RevokeSessionAsync(Guid staffUserId, Guid tokenId, CancellationToken ct = default) =>
        await db.StaffRefreshTokens.Where(t => t.Id == tokenId && t.StaffUserId == staffUserId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTime.UtcNow), ct) > 0;

    public Task RevokeSessionsAsync(Guid staffUserId, string? exceptHash, CancellationToken ct = default) =>
        db.StaffRefreshTokens
            .Where(t => t.StaffUserId == staffUserId && t.RevokedAt == null && (exceptHash == null || t.TokenHash != exceptHash))
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTime.UtcNow), ct);

    private IQueryable<AuditEntry> OrgAudit(Guid orgId)
    {
        var stores = db.Stores.Where(s => s.OrganizationId == orgId).Select(s => s.Id);
        var staff = db.StaffUsers.Where(u => u.OrganizationId == orgId).Select(u => u.Id);
        return db.AuditLog.AsNoTracking().Where(a =>
            a.OrganizationId == orgId
            || (a.StoreId != null && stores.Contains(a.StoreId.Value))
            || (a.StaffUserId != null && staff.Contains(a.StaffUserId.Value)));
    }

    public async Task<(IReadOnlyList<AuditEntry> Rows, int Total)> AuditAsync(Guid orgId, AuditQuery q, CancellationToken ct = default)
    {
        var query = OrgAudit(orgId);
        if (q.StaffUserId is { } staffId) query = query.Where(a => a.StaffUserId == staffId);
        if (q.StoreId is { } storeId) query = query.Where(a => a.StoreId == storeId);
        if (!string.IsNullOrEmpty(q.Action)) query = query.Where(a => a.Action.StartsWith(q.Action));
        if (q.From is { } from) query = query.Where(a => a.CreatedAt >= from);
        if (q.To is { } to) query = query.Where(a => a.CreatedAt < to);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(a => a.CreatedAt).Skip(q.Skip).Take(q.Take).ToListAsync(ct);
        return (rows, total);
    }

    public async Task<IReadOnlyList<string>> AuditActionsAsync(Guid orgId, CancellationToken ct = default) =>
        await OrgAudit(orgId).Select(a => a.Action).Distinct().OrderBy(a => a).ToListAsync(ct);

    public async Task<Dictionary<Guid, StaffSalesStats>> SalesStatsAsync(Guid orgId, DateTime sinceUtc, CancellationToken ct = default)
    {
        var recent = await db.Sales.AsNoTracking()
            .Where(s => s.OrganizationId == orgId && s.CreatedAt >= sinceUtc && s.Status != SaleStatus.Returned)
            .GroupBy(s => s.StaffUserId).Select(g => new { g.Key, Count = g.Count(), Sum = g.Sum(s => s.Total - s.ReturnedAmount) })
            .ToDictionaryAsync(x => x.Key, ct);
        var last = await db.Sales.AsNoTracking().Where(s => s.OrganizationId == orgId)
            .GroupBy(s => s.StaffUserId).Select(g => new { g.Key, Last = g.Max(s => s.CreatedAt) }).ToDictionaryAsync(x => x.Key, x => x.Last, ct);
        return recent.Keys.Union(last.Keys).ToDictionary(id => id, id =>
        {
            var r = recent.GetValueOrDefault(id);
            return new StaffSalesStats(r?.Count ?? 0, r?.Sum ?? 0, last.TryGetValue(id, out var l) ? l : null);
        });
    }

    public async Task<Dictionary<Guid, string>> RegisterNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
        ids.Count == 0 ? [] : await db.Registers.AsNoTracking().Where(r => ids.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.Name, ct);
}
