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
        services.AddScoped<SalemBonus.Application.Pos.Bonus.BonusProgram>();
        services.AddScoped<SalemBonus.Application.Pos.Bonus.BonusAccess>();
        services.AddScoped<SalemBonus.Application.Pos.Bonus.BonusMapper>();
        services.AddScoped<SalemBonus.Application.Pos.Bonus.BonusBirthdays>();
        services.AddScoped<SalemBonus.Application.Pos.Bonus.BonusStatusService>();
        services.AddScoped<SalemBonus.Application.Pos.Bonus.BonusSettingsService>();
        services.AddScoped<SalemBonus.Application.Pos.Bonus.BonusOperationsService>();
        services.AddScoped<SalemBonus.Application.Pos.Bonus.BonusCampaignService>();
        services.AddScoped<SalemBonus.Application.Pos.Bonus.BonusReportsService>();
        services.AddScoped<SalemBonus.Application.Pos.Bonus.BonusTermsService>();
        services.AddScoped<SalemBonus.Application.Pos.Bonus.BonusJobs>();
        services.AddScoped<SalemBonus.Application.Pos.Bonus.BonusExport>();
        services.AddScoped<SalemBonus.Application.Platform.PlatformAccess>();
        services.AddScoped<SalemBonus.Application.Platform.PlatformAuthService>();
        services.AddScoped<SalemBonus.Application.Platform.PlatformBusinessService>();
        services.AddScoped<SalemBonus.Application.Platform.PlatformBillingService>();
        services.AddScoped<SalemBonus.Application.Pos.Clients.ClientAccess>();
        services.AddScoped<SalemBonus.Application.Pos.Clients.ClientService>();
        services.AddScoped<SalemBonus.Application.Pos.Clients.ClientEditService>();
        services.AddScoped<SalemBonus.Application.Pos.Clients.ClientCrmService>();
        services.AddScoped<SalemBonus.Application.Pos.Clients.ClientSegmentsService>();
        services.AddScoped<SalemBonus.Application.Pos.Clients.ClientImportService>();
        services.AddScoped<SalemBonus.Application.Pos.Clients.ClientExport>();
        services.AddScoped<SalemBonus.Application.BonusCards.BonusLedger>();
        services.AddScoped<ProductExchangeService>();
        services.AddScoped<SalemBonus.Application.Pos.Sales.CashierService>();
        services.AddScoped<SalemBonus.Application.Pos.Sales.SaleService>();
        services.AddScoped<SalemBonus.Application.Pos.Sales.ReturnService>();
        services.AddScoped<SalemBonus.Application.Pos.Sales.DebtService>();
        services.AddScoped<SalemBonus.Application.Pos.Sales.StoreNotifier>();
        services.AddScoped<SalemBonus.Application.Pos.Sales.FinanceService>();
        services.AddScoped<SalemBonus.Application.Pos.Sales.SalesHistoryService>();
        services.AddScoped<SalemBonus.Application.Pos.Sales.CashierSettingsService>();
        services.AddScoped<SalemBonus.Application.Pos.Analytics.AnalyticsService>();
        services.AddScoped<SalemBonus.Application.Pos.Finance.FinanceLedger>();
        services.AddScoped<SalemBonus.Application.Pos.Finance.FinanceOperationsService>();
        services.AddScoped<SalemBonus.Application.Pos.Finance.FinanceReportsService>();
        services.AddScoped<SalemBonus.Application.Pos.Finance.FinanceDebtsService>();
        services.AddScoped<SalemBonus.Application.Pos.Finance.FinanceExport>();
        services.AddScoped<WarehouseService>();
        services.AddScoped<SalemBonus.Application.Pos.Inventory.StockPosting>();
        services.AddScoped<SalemBonus.Application.Pos.Inventory.InventoryService>();
        services.AddScoped<SalemBonus.Application.Pos.Inventory.StockDocumentService>();
        services.AddScoped<SalemBonus.Application.Pos.Inventory.SupplierService>();
        services.AddScoped<SalemBonus.Application.Pos.Inventory.TransferService>();
        services.AddScoped<MediaService>();
        services.AddScoped<SalemBonus.Application.Pos.Inventory.InvoiceRecognitionService>();
        return services;
    }
}
