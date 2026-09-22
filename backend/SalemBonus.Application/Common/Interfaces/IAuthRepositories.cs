using SalemBonus.Domain.Entities;

namespace SalemBonus.Application.Common.Interfaces;

public interface IOtpRepository
{
    Task<OtpCode?> GetLatestAsync(string phone, CancellationToken ct = default);
    void Add(OtpCode code);
}

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default);
    void Add(RefreshToken token);
    Task RevokeAllAsync(Guid customerId, CancellationToken ct = default);
}
