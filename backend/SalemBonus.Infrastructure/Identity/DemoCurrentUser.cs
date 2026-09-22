using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Infrastructure.Persistence;

namespace SalemBonus.Infrastructure.Identity;

/// <summary>JWT аутентификациясы қосылғанша демо тұтынушыны қайтарады.</summary>
public class DemoCurrentUser : ICurrentUser
{
    public Guid CustomerId => DbSeeder.DemoCustomerId;
}
