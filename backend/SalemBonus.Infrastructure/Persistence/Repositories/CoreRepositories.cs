using Microsoft.EntityFrameworkCore;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Domain.Core;

namespace SalemBonus.Infrastructure.Persistence.Repositories;

public class StaffRepository(AppDbContext db) : IStaffRepository
{
    private IQueryable<StaffUser> WithAccess() => db.StaffUsers
        .Include(u => u.Organization)
        .Include(u => u.Memberships.Where(m => m.IsActive))
        .ThenInclude(m => m.Store);

    public Task<StaffUser?> GetByPhoneForUpdateAsync(string phone, CancellationToken ct = default) =>
        WithAccess().FirstOrDefaultAsync(u => u.Phone == phone, ct);

    public Task<StaffUser?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default) =>
        WithAccess().FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<StaffUser?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        WithAccess().AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<StoreMembership?> GetMembershipAsync(Guid staffUserId, Guid storeId, CancellationToken ct = default) =>
        db.StoreMemberships
            .AsNoTracking()
            .Include(m => m.Store)
            .FirstOrDefaultAsync(m =>
                m.StaffUserId == staffUserId && m.StoreId == storeId && m.IsActive
                && m.Store!.IsActive && m.StaffUser!.IsActive, ct);
}

public class StaffRefreshTokenRepository(AppDbContext db) : IStaffRefreshTokenRepository
{
    public Task<StaffRefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default) =>
        db.StaffRefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public void Add(StaffRefreshToken token) => db.StaffRefreshTokens.Add(token);

    public Task RevokeAllAsync(Guid staffUserId, CancellationToken ct = default) =>
        db.StaffRefreshTokens
            .Where(t => t.StaffUserId == staffUserId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTime.UtcNow), ct);
}

public class StaffOtpRepository(AppDbContext db) : IStaffOtpRepository
{
    public Task<StaffOtpCode?> GetLatestAsync(Guid staffUserId, string purpose, CancellationToken ct = default) =>
        db.StaffOtpCodes
            .Where(o => o.StaffUserId == staffUserId && o.Purpose == purpose)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public Task<int> CountSinceAsync(Guid staffUserId, string purpose, DateTime since, CancellationToken ct = default) =>
        db.StaffOtpCodes.CountAsync(o => o.StaffUserId == staffUserId && o.Purpose == purpose && o.CreatedAt >= since, ct);

    public Task<StaffOtpCode?> GetByResetTokenHashAsync(string tokenHash, CancellationToken ct = default) =>
        db.StaffOtpCodes.FirstOrDefaultAsync(o => o.ResetTokenHash == tokenHash, ct);

    public void Add(StaffOtpCode code) => db.StaffOtpCodes.Add(code);
}

public class AuditLog(AppDbContext db) : IAuditLog
{
    public void Record(AuditEntry entry) => db.AuditLog.Add(entry);
}
