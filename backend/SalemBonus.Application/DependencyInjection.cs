using Microsoft.Extensions.DependencyInjection;
using SalemBonus.Application.BonusCards;

namespace SalemBonus.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IBonusCardService, BonusCardService>();
        return services;
    }
}
