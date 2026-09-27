using SalemBonus.Domain.Core;

namespace SalemBonus.Application.Common.Interfaces;

/// <summary>Кірген қызметкер (salem-staff токені бойынша).</summary>
public interface ICurrentStaff
{
    bool IsAuthenticated { get; }
    Guid StaffUserId { get; }
    string? Ip { get; }
}

/// <summary>
/// Сұраныстың дүкені. Фронт X-Store-Id жібереді, бірақ оған сенбейміз: сервер қызметкердің сол дүкенде
/// белсенді мүшелігі бар-жоғын тексереді. Барлық SalemPos сервисі дүкенді тек осы арқылы алады.
/// </summary>
public interface IStoreContext
{
    Task<StoreMembership> GetAsync(CancellationToken ct = default);
    /// <summary>Рұқсат жоқ болса ForbiddenException.</summary>
    Task<StoreMembership> RequireAsync(string permission, CancellationToken ct = default);
}
