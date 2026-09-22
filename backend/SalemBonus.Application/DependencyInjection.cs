using Microsoft.Extensions.DependencyInjection;
using SalemBonus.Application.BonusCards;
using SalemBonus.Application.Customers;

namespace SalemBonus.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IBonusCardService, BonusCardService>();
        services.AddScoped<ICustomerService, CustomerService>();
        return services;
    }
}
