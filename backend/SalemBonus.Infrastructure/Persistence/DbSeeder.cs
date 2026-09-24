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

        var stores = Catalog();
        await SyncStoresAsync(db, stores, ct);

        // Демо тұтынушы бір рет қана құрылады.
        if (await db.Customers.AnyAsync(ct)) return;

        var customer = new Customer
        {
            Id = DemoCustomerId,
            Phone = "+77011234567",
            FullName = "Мақсадбек Абдужаббаров",
            Email = "maksadbek97@gmail.com",
            BirthDate = new DateOnly(1997, 9, 15),
            QrCode = "SALEM2025X",
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

        var notifications = new List<Notification>
        {
            N(NotificationType.BonusAccrued, stores[0], "Бонус есептелді!", "MKM AUTO — Сізге 1 250 Б бонус есептелді.", "Сатып алу сомасы: 25 000 ₸", now.AddHours(-2), false),
            N(NotificationType.BonusRedeemed, stores[1], "Бонус жұмсалды", "Coffee House — 5 000 Б бонус шегерілді.", "Сатып алу сомасы: 12 000 ₸", now.AddHours(-5), false),
            N(NotificationType.Birthday, stores[2], "Туған күн бонусы!", "Beauty Shop — Туған күніңізге 3 000 Б бонус берілді!", "Бонус 3 күн ішінде жарамды.", now.AddHours(-7), false),
            N(NotificationType.Promo, stores[3], "Акция басталды!", "SportLife — Барлық кроссовкаларға 2x бонус!", "Акция 30 қыркүйекке дейін.", now.AddHours(-8), true),
            N(NotificationType.StoreAdded, stores[1], "Жаңа дүкен қосылды", "Coffee House дүкені сіздің карталарыңызға қосылды.", "Енді бұл дүкенде де бонус жинай аласыз!", now.AddDays(-1).AddHours(-3), true),
            N(NotificationType.ProfileUpdated, null, "Профиль жаңартылды", "Жеке деректеріңіз сәтті жаңартылды.", null, now.AddDays(-2).AddHours(-6), true),
        };

        db.Customers.Add(customer);
        db.BonusCards.AddRange(cards);
        db.BonusTransactions.AddRange(transactions);
        db.Notifications.AddRange(notifications);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Серіктес дүкендер каталогы. Жаңа дүкен қосылса, келесі іске қосылғанда базаға түседі.</summary>
    private static List<Store> Catalog() => new()
    {
            new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Name = "MKM AUTO", Category = "Автобөлшектер", Description = "Көлікке арналған қосалқы бөлшектер, майлар және аксессуарлар.", CashbackPercent = 5, ThemeColor = "#111113", Icon = "car", MaxRedeemPercent = 30, ApiKey = "sk_test_mkm_auto_11111111", JoinCode = "MKMAUTO" },
            new() { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Name = "Coffee House", Category = "Кофехана", Description = "Кофе, десерт және таңғы ас. Қалада бірнеше нүкте.", CashbackPercent = 3, ThemeColor = "#3B2A22", Icon = "coffee", MaxRedeemPercent = 50, ApiKey = "sk_test_coffee_house_2222", JoinCode = "COFFEE" },
            new() { Id = Guid.Parse("33333333-3333-3333-3333-333333333333"), Name = "Beauty Shop", Category = "Косметика", Description = "Косметика, парфюмерия және күтім құралдары.", CashbackPercent = 2, ThemeColor = "#C2185B", Icon = "flower", MaxRedeemPercent = 30, ApiKey = "sk_test_beauty_shop_33333", JoinCode = "BEAUTY" },
            new() { Id = Guid.Parse("44444444-4444-4444-4444-444444444444"), Name = "SportLife", Category = "Спорт тауарлары", Description = "Спорт киімі, аяқкиім және жаттығу жабдықтары.", CashbackPercent = 2, ThemeColor = "#1B8A4C", Icon = "dumbbell", MaxRedeemPercent = 20, ApiKey = "sk_test_sportlife_444444", JoinCode = "SPORT" },
            new() { Id = Guid.Parse("55555555-5555-5555-5555-555555555555"), Name = "Дәрі-Дәрмек", Category = "Дәріхана", Description = "Дәрілер, витаминдер және медициналық тауарлар.", CashbackPercent = 3, ThemeColor = "#0E7C66", Icon = "store", MaxRedeemPercent = 30, ApiKey = "sk_test_pharmacy_555555", JoinCode = "DARIHANA" },
            new() { Id = Guid.Parse("66666666-6666-6666-6666-666666666666"), Name = "Нан Үйі", Category = "Наубайхана", Description = "Жаңа піскен нан, тоқаш және торттар.", CashbackPercent = 4, ThemeColor = "#8A5A2B", Icon = "shopping-bag", MaxRedeemPercent = 40, ApiKey = "sk_test_bakery_666666", JoinCode = "NANUI" },
    };

    /// <summary>Каталогтағы жаңа дүкендерді қосады, бос қосылу кодын толтырады.</summary>
    private static async Task SyncStoresAsync(AppDbContext db, List<Store> catalog, CancellationToken ct)
    {
        var existing = await db.Stores.ToDictionaryAsync(x => x.Id, ct);
        foreach (var store in catalog)
        {
            if (!existing.TryGetValue(store.Id, out var current))
            {
                db.Stores.Add(store);
                continue;
            }
            if (string.IsNullOrWhiteSpace(current.JoinCode)) current.JoinCode = store.JoinCode;
        }
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

    private static Notification N(NotificationType type, Store? store, string title, string body, string? detail, DateTime at, bool read) => new()
    {
        Id = Guid.NewGuid(),
        CustomerId = DemoCustomerId,
        StoreId = store?.Id,
        Type = type,
        Title = title,
        Body = body,
        Detail = detail,
        IsRead = read,
        CreatedAt = at,
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
