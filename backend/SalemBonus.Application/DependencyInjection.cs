using Microsoft.Extensions.DependencyInjection;
using SalemBonus.Application.BonusCards;
using SalemBonus.Application.Customers;
using SalemBonus.Application.Notifications;
using SalemBonus.Application.Pos;

namespace SalemBonus.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IBonusCardService, BonusCardService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IPosService, PosService>();
        return services;
    }
}
