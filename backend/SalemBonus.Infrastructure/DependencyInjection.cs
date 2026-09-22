using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SalemBonus.Application.Auth;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Infrastructure.Identity;
using SalemBonus.Infrastructure.Persistence;
using SalemBonus.Infrastructure.Persistence.Repositories;
using SalemBonus.Infrastructure.Sms;

namespace SalemBonus.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default") ?? "Data Source=salembonus.db";
        services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.Section));
        services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.Section));

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<ISmsSender, LogSmsSender>();

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IStoreRepository, StoreRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IBonusCardRepository, BonusCardRepository>();
        services.AddScoped<IBonusTransactionRepository, BonusTransactionRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IOtpRepository, OtpRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        return services;
    }

    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await DbSeeder.SeedAsync(db, ct);
    }
}
