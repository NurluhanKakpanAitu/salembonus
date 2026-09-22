namespace SalemBonus.Application.Common.Interfaces;

/// <summary>Ағымдағы аутентификацияланған тұтынушы. Auth қосылғанша Infrastructure мок береді.</summary>
public interface ICurrentUser
{
    Guid CustomerId { get; }
}
