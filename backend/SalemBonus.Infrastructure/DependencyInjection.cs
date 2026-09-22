using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Infrastructure.Identity;
using SalemBonus.Infrastructure.Persistence;
using SalemBonus.Infrastructure.Persistence.Repositories;

namespace SalemBonus.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default") ?? "Data Source=salembonus.db";
        services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IStoreRepository, StoreRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IBonusCardRepository, BonusCardRepository>();
        services.AddScoped<IBonusTransactionRepository, BonusTransactionRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<ICurrentUser, DemoCurrentUser>();
        return services;
    }

    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await DbSeeder.SeedAsync(db, ct);
    }
}
