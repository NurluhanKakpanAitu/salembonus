using Microsoft.EntityFrameworkCore;
using SalemBonus.Domain.Entities;
using SalemBonus.Domain.Enums;

namespace SalemBonus.Infrastructure.Persistence;

/// <summary>MVP демо деректері. Auth қосылғанда демо тұтынушы нақты тіркеумен алмасады.</summary>
public static class DbSeeder
{
    public static readonly Guid DemoCustomerId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        await db.Database.MigrateAsync(ct);
        if (await db.Stores.AnyAsync(ct)) return;

        var stores = new List<Store>
        {
            new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Name = "MKM AUTO", Category = "Автобөлшектер", CashbackPercent = 5 },
            new() { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Name = "Coffee House", Category = "Кофехана", CashbackPercent = 3 },
            new() { Id = Guid.Parse("33333333-3333-3333-3333-333333333333"), Name = "Beauty Shop", Category = "Косметика", CashbackPercent = 2 },
            new() { Id = Guid.Parse("44444444-4444-4444-4444-444444444444"), Name = "SportLife", Category = "Спорт тауарлары", CashbackPercent = 2 },
        };

        var customer = new Customer
        {
            Id = DemoCustomerId,
            Phone = "+77011234567",
            FullName = "Мақсадбек Абдужаббаров",
            Email = "maksadbek97@gmail.com",
            BirthDate = new DateOnly(1997, 9, 15),
        };

        var cards = new List<BonusCard>
        {
            Card(stores[0], 12450, 380_000, CustomerLevel.Vip),
            Card(stores[1], 2350, 0, CustomerLevel.Regular),
            Card(stores[2], 4200, 55_000, CustomerLevel.Favorite),
            Card(stores[3], 850, 850, CustomerLevel.New),
        };

        var now = DateTime.UtcNow;
        var transactions = new List<BonusTransaction>
        {
            Tx(cards[0], BonusTransactionType.Accrual, 1250, 25_000, now.AddHours(-2)),
            Tx(cards[0], BonusTransactionType.Redemption, -5000, 12_000, now.AddHours(-5)),
            Tx(cards[0], BonusTransactionType.Accrual, 850, 17_000, now.AddDays(-4)),
            Tx(cards[0], BonusTransactionType.Birthday, 3000, null, now.AddDays(-5)),
        };

        db.Stores.AddRange(stores);
        db.Customers.Add(customer);
        db.BonusCards.AddRange(cards);
        db.BonusTransactions.AddRange(transactions);
        await db.SaveChangesAsync(ct);
    }

    private static BonusCard Card(Store store, int balance, decimal spent, CustomerLevel level) => new()
    {
        Id = Guid.NewGuid(),
        CustomerId = DemoCustomerId,
        StoreId = store.Id,
        Balance = balance,
        TotalSpent = spent,
        Level = level,
    };

    private static BonusTransaction Tx(BonusCard card, BonusTransactionType type, int amount, decimal? purchase, DateTime at) => new()
    {
        Id = Guid.NewGuid(),
        BonusCardId = card.Id,
        Type = type,
        Amount = amount,
        PurchaseAmount = purchase,
        CreatedAt = at,
    };
}
