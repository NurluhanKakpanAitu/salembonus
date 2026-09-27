using Microsoft.Extensions.DependencyInjection;
using SalemBonus.Application.Auth;
using SalemBonus.Application.BonusCards;
using SalemBonus.Application.Core.Staff;
using SalemBonus.Application.Customers;
using SalemBonus.Application.Notifications;
using SalemBonus.Application.Pos;
using SalemBonus.Application.Pos.Registers;
using SalemBonus.Application.Kato;
using SalemBonus.Application.Stores;

namespace SalemBonus.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IBonusCardService, BonusCardService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IPosService, PosService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IStoreService, StoreService>();
        services.AddScoped<IKatoService, KatoService>();
        services.AddScoped<IBonusExpiryService, BonusExpiryService>();
        services.AddScoped<IStaffAuthService, StaffAuthService>();
        services.AddScoped<IStaffPasswordResetService, StaffPasswordResetService>();
        services.AddScoped<IStaffCredentialsService, StaffCredentialsService>();
        services.AddScoped<IRegisterService, RegisterService>();
        return services;
    }
}
