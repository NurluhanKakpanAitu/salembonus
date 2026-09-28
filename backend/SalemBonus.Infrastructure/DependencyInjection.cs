using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SalemBonus.Application.Auth;
using SalemBonus.Application.Core.Staff;
using SalemBonus.Infrastructure.WhatsApp;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Infrastructure.Identity;
using SalemBonus.Infrastructure.Persistence;
using SalemBonus.Infrastructure.Persistence.Repositories;
using SalemBonus.Infrastructure.Sms;
using SalemBonus.Infrastructure.Storage;

namespace SalemBonus.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default орнатылмаған (Postgres, мысалы Neon)");
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.Section));
        services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.Section));
        services.Configure<StaffAuthOptions>(configuration.GetSection(StaffAuthOptions.Section));
        services.Configure<R2Options>(configuration.GetSection(R2Options.Section));
        services.AddSingleton<IFileStorage, R2FileStorage>();
        services.AddSingleton<ISpreadsheet, XlsxSpreadsheet>();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        services.AddScoped<ICurrentLanguage, HttpCurrentLanguage>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<ISmsSender, LogSmsSender>();

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IStoreRepository, StoreRepository>();
        services.AddScoped<IKatoRepository, KatoRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IBonusCardRepository, BonusCardRepository>();
        services.AddScoped<IBonusTransactionRepository, BonusTransactionRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IOtpRepository, OtpRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        // Ортақ ядро: қызметкерлер, дүкен контексі, аудит
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddScoped<ICurrentStaff, HttpCurrentStaff>();
        services.AddScoped<IStoreContext, HttpStoreContext>();
        services.AddScoped<IStaffRepository, StaffRepository>();
        services.AddScoped<IStaffRefreshTokenRepository, StaffRefreshTokenRepository>();
        services.AddScoped<IAuditLog, AuditLog>();
        services.AddScoped<IStaffOtpRepository, StaffOtpRepository>();
        services.AddScoped<IRegisterRepository, RegisterRepository>();
        services.AddScoped<ICatalogRepository, CatalogRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IInventoryRepository, InventoryRepository>();
        // Meta-ның Authentication шаблоны бекітілгенше кодтар логқа жазылады.
        services.AddSingleton<IWhatsAppSender, LogWhatsAppSender>();
        return services;
    }

    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Базаға бірінші қосылу кейде сәтсіз болады: VPS-те база контейнері API-дан кеш көтеріледі,
        // ал Neon-ға кейбір желіде IPv6 бағыты уақытша жоқ. Бірнеше рет қайталаймыз.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await DbSeeder.SeedAsync(db, ct);
                break;
            }
            catch (Npgsql.NpgsqlException ex) when (attempt < 5 && ex.IsTransient)
            {
                scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>()
                    .LogWarning("Базаға қосылу сәтсіз ({Attempt}/5): {Message}", attempt, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(2 * attempt), ct);
            }
        }

        // Демо қызметкерлер тек локал разработкада: белгілі құпиясөзбен аккаунт продакшнда болмауы керек.
        if (scope.ServiceProvider.GetRequiredService<IHostEnvironment>().IsDevelopment())
        {
            await StaffSeeder.SeedAsync(db, ct);
            await CatalogDemoSeeder.SeedAsync(db, ct);
        }
    }
}
