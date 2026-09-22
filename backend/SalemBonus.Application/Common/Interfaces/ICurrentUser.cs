namespace SalemBonus.Application.Common.Interfaces;

/// <summary>Ағымдағы аутентификацияланған тұтынушы (JWT-тен).</summary>
public interface ICurrentUser
{
    /// <summary>Аутентификация жоқ болса UnauthorizedException лақтырады.</summary>
    Guid CustomerId { get; }
    bool IsAuthenticated { get; }
}
