using Microsoft.EntityFrameworkCore;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Domain.Entities;

namespace SalemBonus.Infrastructure.Persistence.Repositories;

public class OtpRepository(AppDbContext db) : IOtpRepository
{
    public Task<OtpCode?> GetLatestAsync(string phone, CancellationToken ct = default) =>
        db.OtpCodes.Where(o => o.Phone == phone).OrderByDescending(o => o.CreatedAt).FirstOrDefaultAsync(ct);

    public void Add(OtpCode code) => db.OtpCodes.Add(code);
}

public class RefreshTokenRepository(AppDbContext db) : IRefreshTokenRepository
{
    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default) =>
        db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public void Add(RefreshToken token) => db.RefreshTokens.Add(token);

    public Task RevokeAllAsync(Guid customerId, CancellationToken ct = default) =>
        db.RefreshTokens
            .Where(t => t.CustomerId == customerId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTime.UtcNow), ct);
}
