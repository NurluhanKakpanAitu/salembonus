using Microsoft.Extensions.DependencyInjection;
using SalemBonus.Application.Auth;
using SalemBonus.Application.BonusCards;
using SalemBonus.Application.Core.Staff;
using SalemBonus.Application.Customers;
using SalemBonus.Application.Notifications;
using SalemBonus.Application.Pos;
using SalemBonus.Application.Pos.Catalog;
using SalemBonus.Application.Pos.Inventory;
using SalemBonus.Application.Pos.Media;
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
        services.AddScoped<CatalogAccess>();
        services.AddScoped<ICatalogNodeService, CatalogNodeService>();
        services.AddScoped<ICatalogDictionaryService, CatalogDictionaryService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<SalemBonus.Application.BonusCards.BonusLedger>();
        services.AddScoped<ProductExchangeService>();
        services.AddScoped<SalemBonus.Application.Pos.Sales.CashierService>();
        services.AddScoped<SalemBonus.Application.Pos.Sales.SaleService>();
        services.AddScoped<SalemBonus.Application.Pos.Sales.ReturnService>();
        services.AddScoped<SalemBonus.Application.Pos.Sales.DebtService>();
        services.AddScoped<WarehouseService>();
        services.AddScoped<MediaService>();
        return services;
    }
}
