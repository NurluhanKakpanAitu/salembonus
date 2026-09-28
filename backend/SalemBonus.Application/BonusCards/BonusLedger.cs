using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Application.Notifications;
using SalemBonus.Domain.Entities;
using SalemBonus.Domain.Enums;

namespace SalemBonus.Application.BonusCards;

/// <summary>
/// Бонус шотының операциялары: шегеру (FIFO), есептеу, мәртебе көтерілуі және клиентке хабарлама.
/// Терминал API-ы (<c>PosService</c>) мен SalemPos кассасы бір ережемен жұмыс істеуі үшін бір жерде.
/// Сақтауды шақырушы жасайды — сатылыммен бір транзакцияда.
/// </summary>
public class BonusLedger(
    IBonusCardRepository cards,
    IBonusTransactionRepository transactions,
    INotificationRepository notifications,
    ICurrentLanguage language)
{
    public record AccrualResult(int Accrued, Guid? TransactionId, bool Upgraded);

    /// <summary>Клиенттің осы дүкендегі картасы; жоқ болса — жасалады (клиентке «жаңа дүкен» хабары).</summary>
    public async Task<BonusCard> GetOrCreateCardAsync(Customer customer, Store store, DateTime now, CancellationToken ct)
    {
        var card = await cards.GetForUpdateAsync(customer.Id, store.Id, ct);
        if (card is not null) return card;

        card = new BonusCard { Id = Guid.NewGuid(), CustomerId = customer.Id, StoreId = store.Id, Store = store };
        cards.Add(card);
        notifications.Add(NewNotification(customer, store, NotificationType.StoreAdded,
            "Жаңа дүкен қосылды",
            $"{store.Name} дүкені сіздің карталарыңызға қосылды.",
            "Енді бұл дүкенде де бонус жинай аласыз!", now.AddMilliseconds(-1),
            NotificationTemplates.StoreAdded));
        return card;
    }

    /// <summary>Бонус шегеру: баланс азаяды, ең ескі партиялар бірінші жұмсалады.</summary>
    public async Task<Guid> RedeemAsync(BonusCard card, Customer customer, Store store, int amount, decimal purchase,
        string? comment, Guid receiptId, DateTime now, CancellationToken ct)
    {
        card.Balance -= amount;
        await ConsumeLotsAsync(card.Id, amount, ct);
        var tx = NewTx(card, BonusTransactionType.Redemption, -amount, purchase, comment, now, receiptId);
        transactions.Add(tx);
        notifications.Add(NewNotification(customer, store, NotificationType.BonusRedeemed,
            "Бонус жұмсалды",
            $"{store.Name} — {Fmt(amount)} Б бонус шегерілді.",
            $"Сатып алу сомасы: {Fmt(purchase)} ₸", now,
            NotificationTemplates.BonusRedeemed, amount, purchase, receiptId: receiptId));
        return tx.Id;
    }

    /// <summary>Төленген сомадан бонус есептеу (клиенттің ағымдағы мәртебесі бойынша) және мәртебені көтеру.</summary>
    public AccrualResult Accrue(BonusCard card, Customer customer, Store store, decimal paid, decimal purchase,
        string? comment, Guid receiptId, DateTime now)
    {
        var ladder = BonusRules.LadderOf(store);
        var accrued = BonusRules.CalculateAccrual(paid, BonusRules.PercentFor(card.Level, ladder));
        Guid? accrualId = null;
        if (accrued > 0)
        {
            card.Balance += accrued;
            var tx = NewTx(card, BonusTransactionType.Accrual, accrued, purchase, comment, now.AddMilliseconds(1), receiptId);
            tx.Remaining = accrued;
            tx.ExpiresAt = store.BonusLifetimeDays is { } days ? now.AddDays(days) : null;
            transactions.Add(tx);
            accrualId = tx.Id;
            notifications.Add(NewNotification(customer, store, NotificationType.BonusAccrued,
                "Бонус есептелді!",
                $"{store.Name} — Сізге {Fmt(accrued)} Б бонус есептелді.",
                $"Сатып алу сомасы: {Fmt(purchase)} ₸", now.AddMilliseconds(1),
                NotificationTemplates.BonusAccrued, accrued, purchase, receiptId: receiptId));
        }

        card.TotalSpent += paid;
        var newLevel = BonusRules.LevelFor(card.TotalSpent, ladder);
        var upgraded = newLevel > card.Level;
        if (upgraded)
        {
            card.Level = newLevel;
            notifications.Add(NewNotification(customer, store, NotificationType.System,
                "Жаңа деңгей!",
                $"{store.Name} — Құттықтаймыз, сіз енді {CustomerLevels.Name(newLevel, language.Value)} деңгейіндесіз.",
                null, now.AddMilliseconds(2),
                NotificationTemplates.LevelUp, levelKey: CustomerLevels.Key(newLevel)));
        }
        return new AccrualResult(accrued, accrualId, upgraded);
    }

    /// <summary>Есептелетін бонус (сатылымға дейін көрсету үшін, ештеңе өзгертпейді).</summary>
    public static int PreviewAccrual(BonusCard? card, Store store, decimal paid) =>
        BonusRules.CalculateAccrual(paid, BonusRules.PercentFor(card?.Level ?? CustomerLevel.New, BonusRules.LadderOf(store)));

    /// <summary>Шегерілген бонус ең ескі партиядан бастап жұмсалады (FIFO).</summary>
    private async Task ConsumeLotsAsync(Guid cardId, int amount, CancellationToken ct)
    {
        var left = amount;
        foreach (var lot in await transactions.GetOpenLotsAsync(cardId, ct))
        {
            if (left <= 0) break;
            var take = Math.Min(lot.Remaining, left);
            lot.Remaining -= take;
            left -= take;
        }
        // Партиясы жоқ ескі баланстан шегерілсе, left > 0 болуы мүмкін — бұл қалыпты жағдай.
    }

    private static BonusTransaction NewTx(
        BonusCard card, BonusTransactionType type, int amount, decimal purchase, string? comment, DateTime at, Guid receiptId) => new()
    {
        Id = Guid.NewGuid(),
        BonusCardId = card.Id,
        Type = type,
        Amount = amount,
        PurchaseAmount = purchase,
        ReceiptId = receiptId,
        Comment = comment,
        CreatedAt = at,
    };

    private static Notification NewNotification(
        Customer c, Store s, NotificationType type, string title, string body, string? detail, DateTime at,
        string? templateKey = null, int? amount = null, decimal? purchaseAmount = null, string? levelKey = null,
        Guid? receiptId = null) => new()
    {
        Id = Guid.NewGuid(),
        CustomerId = c.Id,
        StoreId = s.Id,
        Type = type,
        Title = title,
        Body = body,
        Detail = detail,
        TemplateKey = templateKey,
        Amount = amount,
        PurchaseAmount = purchaseAmount,
        LevelKey = levelKey,
        ReceiptId = receiptId,
        CreatedAt = at,
    };

    private static string Fmt(decimal n) => string.Format(new System.Globalization.CultureInfo("ru-RU"), "{0:N0}", n).Replace(' ', ' ');
}
