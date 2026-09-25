using Microsoft.EntityFrameworkCore;
using SalemBonus.Application.BonusCards;
using SalemBonus.Application.Notifications;
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
        await SyncDemoCardLevelsAsync(db, ct);

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
            N(NotificationType.BonusAccrued, stores[0], "Бонус есептелді!", "MKM AUTO — Сізге 1 250 Б бонус есептелді.", "Сатып алу сомасы: 25 000 ₸", now.AddHours(-2), false, NotificationTemplates.BonusAccrued, 1250, 25_000),
            N(NotificationType.BonusRedeemed, stores[1], "Бонус жұмсалды", "Coffee House — 5 000 Б бонус шегерілді.", "Сатып алу сомасы: 12 000 ₸", now.AddHours(-5), false, NotificationTemplates.BonusRedeemed, 5000, 12_000),
            N(NotificationType.Birthday, stores[2], "Туған күн бонусы!", "Beauty Shop — Туған күніңізге 3 000 Б бонус берілді!", "Бонус 3 күн ішінде жарамды.", now.AddHours(-7), false, NotificationTemplates.Birthday, 3000),
            N(NotificationType.Promo, stores[3], "Акция басталды!", "SportLife — Барлық кроссовкаларға 2x бонус!", "Акция 30 қыркүйекке дейін.", now.AddHours(-8), true),
            N(NotificationType.StoreAdded, stores[1], "Жаңа дүкен қосылды", "Coffee House дүкені сіздің карталарыңызға қосылды.", "Енді бұл дүкенде де бонус жинай аласыз!", now.AddDays(-1).AddHours(-3), true, NotificationTemplates.StoreAdded),
            N(NotificationType.ProfileUpdated, null, "Профиль жаңартылды", "Жеке деректеріңіз сәтті жаңартылды.", null, now.AddDays(-2).AddHours(-6), true, NotificationTemplates.ProfileUpdated),
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
        new()
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Name = "MKM AUTO", Category = "Автобөлшектер",
            Description = "Көлікке арналған қосалқы бөлшектер, майлар және аксессуарлар.",
            CashbackPercent = 2, ThemeColor = "#111113", Icon = "car", MaxRedeemPercent = 30,
            ApiKey = "sk_test_mkm_auto_11111111", JoinCode = "MKMAUTO",
            Address = "Алматы, Сейфуллин даңғ. 502",
            Phone = "+7 727 350 40 40",
            Levels = Ladder((0, 2), (50_000, 4), (150_000, 6), (400_000, 8)),
        },
        new()
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Name = "Coffee House", Category = "Кофехана",
            Description = "Кофе, десерт және таңғы ас. Қалада бірнеше нүкте.",
            CashbackPercent = 1, ThemeColor = "#3B2A22", Icon = "coffee", MaxRedeemPercent = 50,
            ApiKey = "sk_test_coffee_house_2222", JoinCode = "COFFEE",
            Address = "Алматы, Абай даңғ. 44",
            Phone = "+7 727 311 22 33",
            Levels = Ladder((0, 1), (15_000, 3), (40_000, 4), (100_000, 5)),
        },
        new()
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"), Name = "Beauty Shop", Category = "Косметика",
            Description = "Косметика, парфюмерия және күтім құралдары.",
            CashbackPercent = 1, ThemeColor = "#C2185B", Icon = "flower", MaxRedeemPercent = 30,
            ApiKey = "sk_test_beauty_shop_33333", JoinCode = "BEAUTY",
            Address = "Алматы, Розыбакиев көш. 247",
            Phone = "+7 727 390 15 15",
            Levels = Ladder((0, 1), (20_000, 2), (60_000, 4), (150_000, 6)),
        },
        new()
        {
            Id = Guid.Parse("44444444-4444-4444-4444-444444444444"), Name = "SportLife", Category = "Спорт тауарлары",
            Description = "Спорт киімі, аяқкиім және жаттығу жабдықтары.",
            CashbackPercent = 1, ThemeColor = "#1B8A4C", Icon = "dumbbell", MaxRedeemPercent = 20,
            ApiKey = "sk_test_sportlife_444444", JoinCode = "SPORT",
            Address = "Алматы, Достык даңғ. 111",
            Phone = "+7 727 264 78 90",
            Levels = Ladder((0, 1), (25_000, 2), (75_000, 3), (200_000, 5)),
        },
        new()
        {
            Id = Guid.Parse("55555555-5555-5555-5555-555555555555"), Name = "Дәрі-Дәрмек", Category = "Дәріхана",
            Description = "Дәрілер, витаминдер және медициналық тауарлар.",
            CashbackPercent = 1, ThemeColor = "#0E7C66", Icon = "store", MaxRedeemPercent = 30,
            ApiKey = "sk_test_pharmacy_555555", JoinCode = "DARIHANA",
            Address = "Алматы, Толе би көш. 285",
            Phone = "+7 727 233 44 55",
            Levels = Ladder((0, 1), (10_000, 3), (30_000, 4), (80_000, 6)),
        },
        new()
        {
            Id = Guid.Parse("66666666-6666-6666-6666-666666666666"), Name = "Нан Үйі", Category = "Наубайхана",
            Description = "Жаңа піскен нан, тоқаш және торттар.",
            CashbackPercent = 2, ThemeColor = "#8A5A2B", Icon = "shopping-bag", MaxRedeemPercent = 40,
            ApiKey = "sk_test_bakery_666666", JoinCode = "NANUI",
            Address = "Алматы, Жандосов көш. 58",
            Phone = "+7 727 276 09 09",
            Levels = Ladder((0, 2), (10_000, 4), (25_000, 5), (60_000, 7)),
        },
        new()
        {
            Id = Guid.Parse("77777777-7777-7777-7777-777777777777"), Name = "Достар Кафе", Category = "Мейрамхана",
            Description = "Отбасылық кафе: ұлттық және еуропалық асхана, бизнес-ланч.",
            CashbackPercent = 2, ThemeColor = "#B3261E", Icon = "utensils", MaxRedeemPercent = 40,
            ApiKey = "sk_test_dostar_cafe_7777", JoinCode = "DOSTAR",
            Address = "Алматы, Гоголь көш. 123",
            Phone = "+7 727 279 88 11",
            Levels = Ladder((0, 2), (20_000, 4), (50_000, 6), (120_000, 8)),
        },
    };

    /// <summary>Мәртебелер баспалдағы: (сома, пайыз) жұптары Жаңа → Тұрақты → Сүйікті → VIP ретімен.</summary>
    private static List<StoreLevel> Ladder(
        (decimal From, decimal Percent) newClient,
        (decimal From, decimal Percent) regular,
        (decimal From, decimal Percent) favorite,
        (decimal From, decimal Percent) vip) =>
    [
        Lvl(CustomerLevel.New, newClient), Lvl(CustomerLevel.Regular, regular),
        Lvl(CustomerLevel.Favorite, favorite), Lvl(CustomerLevel.Vip, vip),
    ];

    private static StoreLevel Lvl(CustomerLevel level, (decimal From, decimal Percent) v) => new()
    {
        Id = Guid.NewGuid(),
        Level = level,
        FromAmount = v.From,
        CashbackPercent = v.Percent,
    };

    /// <summary>Каталогтағы жаңа дүкендерді қосады, жетіспейтін деректерін толтырады.</summary>
    private static async Task SyncStoresAsync(AppDbContext db, List<Store> catalog, CancellationToken ct)
    {
        var existing = await db.Stores.Include(x => x.Levels).ToDictionaryAsync(x => x.Id, ct);
        foreach (var store in catalog)
        {
            if (!existing.TryGetValue(store.Id, out var current))
            {
                db.Stores.Add(store);
                continue;
            }
            if (string.IsNullOrWhiteSpace(current.JoinCode)) current.JoinCode = store.JoinCode;
            if (string.IsNullOrWhiteSpace(current.Address)) current.Address = store.Address;
            if (string.IsNullOrWhiteSpace(current.Phone)) current.Phone = store.Phone;
            // Баспалдағы бапталмаған дүкенге каталогтағы баспалдақты береміз.
            // Каталогтағы нысандарды тікелей байламаймыз — олар бөлек Store-ға тиесілі.
            if (current.Levels.Count == 0)
            {
                foreach (var level in store.Levels)
                {
                    db.StoreLevels.Add(new StoreLevel
                    {
                        Id = Guid.NewGuid(),
                        StoreId = current.Id,
                        Level = level.Level,
                        FromAmount = level.FromAmount,
                        CashbackPercent = level.CashbackPercent,
                    });
                }
                current.CashbackPercent = store.CashbackPercent;
            }
        }
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Демо карталардың мәртебесі қолмен қойылған. Дүкеннің баспалдағы өзгергенде
    /// олар сәйкес келмей қалмауы үшін мәртебені жұмсалған сома бойынша қайта есептейміз.
    /// </summary>
    private static async Task SyncDemoCardLevelsAsync(AppDbContext db, CancellationToken ct)
    {
        var cards = await db.BonusCards
            .Include(c => c.Store)
            .ThenInclude(s => s!.Levels)
            .Where(c => c.CustomerId == DemoCustomerId)
            .ToListAsync(ct);

        var changed = false;
        foreach (var card in cards)
        {
            var level = BonusRules.LevelFor(card.TotalSpent, BonusRules.LadderOf(card.Store));
            if (card.Level == level) continue;
            card.Level = level;
            changed = true;
        }
        if (changed) await db.SaveChangesAsync(ct);
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

    private static Notification N(
        NotificationType type, Store? store, string title, string body, string? detail, DateTime at, bool read,
        string? templateKey = null, int? amount = null, decimal? purchaseAmount = null) => new()
    {
        Id = Guid.NewGuid(),
        CustomerId = DemoCustomerId,
        StoreId = store?.Id,
        Type = type,
        Title = title,
        Body = body,
        Detail = detail,
        TemplateKey = templateKey,
        Amount = amount,
        PurchaseAmount = purchaseAmount,
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
