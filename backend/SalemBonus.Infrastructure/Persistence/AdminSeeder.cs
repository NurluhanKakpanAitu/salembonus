using Microsoft.EntityFrameworkCore;
using SalemBonus.Domain.Core;
using SalemBonus.Domain.Platform;
using SalemBonus.Infrastructure.Identity;

namespace SalemBonus.Infrastructure.Persistence;

/// <summary>
/// SalemTech әкімшілігінің демо деректері — ТЕК Development пен тесттерде: белгілі құпиясөзді әкімші және үш тариф.
/// Продакшнда бірінші әкімші CLI-мен жасалады: <c>pos create-admin --email .. --name ..</c>.
/// </summary>
public static class AdminSeeder
{
    public const string AdminEmail = "admin@salemtech.kz";
    public const string AdminPassword = "Demo-Admin-2026";

    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (!await db.PlatformAdmins.AnyAsync(ct))
            db.PlatformAdmins.Add(new PlatformAdmin
            {
                Id = Guid.NewGuid(), Email = AdminEmail, Name = "SalemTech Admin", PasswordHash = new Pbkdf2PasswordHasher().Hash(AdminPassword),
            });
        if (!await db.PlatformPlans.AnyAsync(ct))
            db.PlatformPlans.AddRange(
                Plan("Старт", 9_900, 1, PlatformModules.Cashier, PlatformModules.Products, PlatformModules.Sales),
                Plan("Бизнес", 19_900, 2, PlatformModules.Cashier, PlatformModules.Products, PlatformModules.Warehouse, PlatformModules.Sales,
                    PlatformModules.Statistics, PlatformModules.Clients),
                Plan("Про", 34_900, 3, [.. PlatformModules.All]));
        await db.SaveChangesAsync(ct);
    }

    private static PlatformPlan Plan(string name, decimal price, int order, params string[] modules) =>
        new() { Id = Guid.NewGuid(), Name = name, MonthlyPrice = price, SortOrder = order, Modules = [.. modules], IsFeatured = order == 2 };
}
