using SalemBonus.Domain.Core;

namespace SalemBonus.Application.Common.Interfaces;

public interface IStaffRepository
{
    /// <summary>Өзгерту үшін (tracked): белсенді дүкендері мен бизнесі бірге.</summary>
    Task<StaffUser?> GetByPhoneForUpdateAsync(string phone, CancellationToken ct = default);
    Task<StaffUser?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default);
    /// <summary>Оқу үшін: белсенді дүкендері мен бизнесі бірге.</summary>
    Task<StaffUser?> GetByIdAsync(Guid id, CancellationToken ct = default);
    /// <summary>Қызметкердің осы дүкендегі белсенді мүшелігі. Жоқ болса — бос.</summary>
    Task<StoreMembership?> GetMembershipAsync(Guid staffUserId, Guid storeId, CancellationToken ct = default);
}

public interface IStaffRefreshTokenRepository
{
    Task<StaffRefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default);
    void Add(StaffRefreshToken token);
}

/// <summary>Әрекет журналы. Жазба келесі SaveChanges-пен бірге сақталады.</summary>
public interface IAuditLog
{
    void Record(AuditEntry entry);
}
