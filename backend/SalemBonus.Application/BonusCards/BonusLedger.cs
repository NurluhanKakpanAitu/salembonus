using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Application.Notifications;
using SalemBonus.Application.Pos.Bonus;
using SalemBonus.Domain.Entities;
using SalemBonus.Domain.Enums;

namespace SalemBonus.Application.BonusCards;

/// <summary>
/// Бонус шотының операциялары: шегеру (FIFO), есептеу, мәртебе көтерілуі және клиентке хабарлама.
/// Терминал API-ы (<c>PosService</c>), SalemPos кассасы мен «Бонусы» бөлімі бір ережемен жұмыс істеуі үшін бір жерде.
/// Әр операция баланс өзгергеннен кейінгі қалдықты (<see cref="BonusTransaction.BalanceAfter"/>), көзін және авторын сақтайды.
/// Сақтауды шақырушы жасайды — сатылыммен бір транзакцияда.
/// </summary>
public class BonusLedger(
    IBonusCardRepository cards,
    IBonusTransactionRepository transactions,
    INotificationRepository notifications,
    BonusProgram program,
    ICurrentStaff current,
    ICurrentLanguage language)
{
    public record AccrualResult(int Accrued, Guid? TransactionId, bool Upgraded);

    private AppLanguage Lang => language.Value;
    private Guid? Author => current.IsAuthenticated ? current.StaffUserId : null;

    /// <summary>Клиенттің осы дүкендегі картасы; жоқ болса — жасалады (клиентке «жаңа дүкен» хабары, бағдарламаның бастапқы мәртебесі).</summary>
    public async Task<BonusCard> GetOrCreateCardAsync(Customer customer, Store store, DateTime now, CancellationToken ct)
    {
        var card = await cards.GetForUpdateAsync(customer.Id, store.Id, ct);
        if (card is not null) return card;

        card = new BonusCard { Id = Guid.NewGuid(), CustomerId = customer.Id, StoreId = store.Id, Store = store, CreatedAt = now };
        if (BonusProgram.Registration(await program.StatusesAsync(store.Id, ct)) is { } first) BonusProgram.Assign(card, first, now, false, null);
        cards.Add(card);
        notifications.Add(NewNotification(customer.Id, store, NotificationType.StoreAdded,
            "Жаңа дүкен қосылды",
            $"{store.Name} дүкені сіздің карталарыңызға қосылды.",
            "Енді бұл дүкенде де бонус жинай аласыз!", now.AddMilliseconds(-1),
            NotificationTemplates.StoreAdded));
        return card;
    }

    /// <summary>Бонус шегеру: баланс азаяды, ең ескі партиялар бірінші жұмсалады. Карта бұғатталған — қолжетімдіден артық шегерілмейді.</summary>
    public async Task<Guid> RedeemAsync(BonusCard card, Customer customer, Store store, int amount, decimal purchase,
        string? comment, Guid receiptId, DateTime now, CancellationToken ct, string source = "sale")
    {
        if (amount > card.Balance) throw new ValidationException(Messages.BonusInsufficient(Lang, card.Balance), "bonus");
        card.Balance -= amount;
        await ConsumeLotsAsync(card.Id, amount, ct);
        var tx = NewTx(card, BonusTransactionType.Redemption, -amount, purchase, comment, now, receiptId, source);
        transactions.Add(tx);
        notifications.Add(NewNotification(customer.Id, store, NotificationType.BonusRedeemed,
            "Бонус жұмсалды",
            $"{store.Name} — {Fmt(amount)} Б бонус шегерілді.",
            $"Сатып алу сомасы: {Fmt(purchase)} ₸", now,
            NotificationTemplates.BonusRedeemed, amount, purchase, receiptId: receiptId));
        return tx.Id;
    }

    /// <summary>
    /// Төленген сомадан бонус есептеу (§11.1): клиенттің ағымдағы мәртебесінің пайызы, пайыз операцияда сақталады; содан кейін мәртебе
    /// шарттар бойынша көтеріледі (артқа санап бонус есептелмейді). Партияның мерзімі — бағдарламаның жану ережесі бойынша.
    /// </summary>
    public async Task<AccrualResult> AccrueAsync(BonusCard card, Customer customer, Store store, decimal paid, decimal purchase,
        string? comment, Guid receiptId, DateTime now, CancellationToken ct, string source = "sale")
    {
        var percent = await program.PercentForAsync(card, store, ct);
        var accrued = BonusRules.CalculateAccrual(paid, percent);
        Guid? accrualId = null;
        if (accrued > 0)
        {
            card.Balance += accrued;
            var tx = NewTx(card, BonusTransactionType.Accrual, accrued, purchase, comment, now.AddMilliseconds(1), receiptId, source);
            tx.Remaining = accrued;
            tx.Percent = percent;
            tx.ExpiresAt = await program.LotExpiresAtAsync(store, card, BonusTransactionType.Accrual, now, ct);
            transactions.Add(tx);
            accrualId = tx.Id;
            notifications.Add(NewNotification(customer.Id, store, NotificationType.BonusAccrued,
                "Бонус есептелді!",
                $"{store.Name} — Сізге {Fmt(accrued)} Б бонус есептелді.",
                $"Сатып алу сомасы: {Fmt(purchase)} ₸", now.AddMilliseconds(1),
                NotificationTemplates.BonusAccrued, accrued, purchase, receiptId: receiptId));
        }

        card.TotalSpent += paid;
        var statuses = await program.StatusesAsync(store.Id, ct);
        if (statuses.Count > 0)
        {
            var (status, upgraded) = await program.EvaluateAsync(card, store, now, false, paid, 1, ct);
            if (status is not null && upgraded)
                notifications.Add(NewNotification(customer.Id, store, NotificationType.System,
                    Lang == AppLanguage.Ru ? "Новый статус!" : "Жаңа мәртебе!",
                    Lang == AppLanguage.Ru ? $"{store.Name} — поздравляем, ваш статус теперь «{status.Name}»: {status.AccrualPercent:0.##}% бонусами."
                        : $"{store.Name} — құттықтаймыз, сіздің мәртебеңіз енді «{status.Name}»: {status.AccrualPercent:0.##}% бонус.",
                    null, now.AddMilliseconds(2)));
            return new AccrualResult(accrued, accrualId, upgraded && status is not null);
        }

        var ladder = BonusRules.LadderOf(store);
        var newLevel = BonusRules.LevelFor(card.TotalSpent, ladder);
        var legacyUpgraded = newLevel > card.Level;
        if (legacyUpgraded)
        {
            card.Level = newLevel;
            notifications.Add(NewNotification(customer.Id, store, NotificationType.System,
                "Жаңа деңгей!",
                $"{store.Name} — Құттықтаймыз, сіз енді {CustomerLevels.Name(newLevel, language.Value)} деңгейіндесіз.",
                null, now.AddMilliseconds(2),
                NotificationTemplates.LevelUp, levelKey: CustomerLevels.Key(newLevel)));
        }
        return new AccrualResult(accrued, accrualId, legacyUpgraded);
    }

    /// <summary>
    /// Қайтарым: сатылымда шегерілген бонус клиентке қайтарылады (жаңа партия — мерзімі бағдарлама ережесімен)
    /// және есептелген бонус алынады. Клиент есептелгенді жұмсап қойса, баланстан артық алынбайды —
    /// теріс баланс болмайды (§11.3). Мәртебе төмендемейді. Нақты алынғанын қайтарады.
    /// </summary>
    public async Task<(int Restored, int Reversed)> ApplyReturnAsync(BonusCard card, Store store, int restore, int reverse,
        decimal returnedMoney, Guid receiptId, string? comment, DateTime now, CancellationToken ct)
    {
        if (restore > 0)
        {
            card.Balance += restore;
            var tx = NewTx(card, BonusTransactionType.ReturnRestore, restore, returnedMoney, comment, now, receiptId, "return");
            tx.Remaining = restore;
            tx.ExpiresAt = await program.LotExpiresAtAsync(store, card, BonusTransactionType.ReturnRestore, now, ct);
            transactions.Add(tx);
        }

        var take = Math.Min(reverse, Math.Max(0, card.Balance));
        if (take > 0)
        {
            card.Balance -= take;
            await ConsumeLotsAsync(card.Id, take, ct);
            transactions.Add(NewTx(card, BonusTransactionType.ReturnReversal, -take, returnedMoney, comment, now.AddMilliseconds(1), receiptId, "return"));
        }
        card.TotalSpent = Math.Max(0, card.TotalSpent - returnedMoney);
        return (restore, take);
    }

    /// <summary>
    /// Бонус беру (жаңа партия): қолмен есептеу, туған күн сыйлығы, тарату, қайтару, сторно (§8.3). Карта бұғатталып алынған болуы керек.
    /// </summary>
    public BonusTransaction Credit(BonusCard card, BonusTransactionType type, int amount, DateTime? expiresAt, string source, string? comment, DateTime now,
        Guid? sourceId = null, Guid? sourceTxId = null, Guid? key = null, Guid? receiptId = null)
    {
        if (amount <= 0) throw new ValidationException(Messages.BonusAmountInvalid(Lang), "amount");
        card.Balance += amount;
        var tx = NewTx(card, type, amount, null, comment, now, receiptId, source);
        tx.Remaining = amount;
        tx.ExpiresAt = expiresAt;
        tx.SourceId = sourceId;
        tx.SourceTransactionId = sourceTxId;
        tx.IdempotencyKey = key;
        transactions.Add(tx);
        return tx;
    }

    /// <summary>Бонус шегеру (FIFO партиялардан): қолмен шегеру, сторно. Қолжетімдіден артық — тыйым (§15).</summary>
    public async Task<BonusTransaction> DebitAsync(BonusCard card, BonusTransactionType type, int amount, string source, string? comment, DateTime now,
        CancellationToken ct, Guid? sourceTxId = null, Guid? key = null, Guid? receiptId = null)
    {
        if (amount <= 0) throw new ValidationException(Messages.BonusAmountInvalid(Lang), "amount");
        if (amount > card.Balance) throw new ValidationException(Messages.BonusInsufficient(Lang, card.Balance), "amount");
        card.Balance -= amount;
        await ConsumeLotsAsync(card.Id, amount, ct);
        var tx = NewTx(card, type, -amount, null, comment, now, receiptId, source);
        tx.SourceTransactionId = sourceTxId;
        tx.IdempotencyKey = key;
        transactions.Add(tx);
        return tx;
    }

    /// <summary>Нақты партиядан шегеру (оң операцияның сторносы): партияның жұмсалмаған қалдығынан ғана.</summary>
    public BonusTransaction DebitLot(BonusCard card, BonusTransaction lot, BonusTransactionType type, int amount, string source, string? comment, DateTime now)
    {
        if (lot.Remaining < amount || card.Balance < amount) throw new ValidationException(Messages.BonusLotSpent(Lang, Math.Min(lot.Remaining, card.Balance)), "amount");
        lot.Remaining -= amount;
        card.Balance -= amount;
        var tx = NewTx(card, type, -amount, null, comment, now, lot.ReceiptId, source);
        tx.SourceTransactionId = lot.Id;
        transactions.Add(tx);
        return tx;
    }

    /// <summary>Клиентке еркін мәтінді хабарлама (дүкен атынан) — SalemBonus қосымшасында көрінеді.</summary>
    public Notification Notify(Guid customerId, Store store, NotificationType type, string title, string body, string? detail, DateTime now,
        string? templateKey = null, int? amount = null)
    {
        var n = NewNotification(customerId, store, type, title, body, detail, now, templateKey, amount);
        notifications.Add(n);
        return n;
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

    private BonusTransaction NewTx(
        BonusCard card, BonusTransactionType type, int amount, decimal? purchase, string? comment, DateTime at, Guid? receiptId, string source) => new()
    {
        Id = Guid.NewGuid(),
        BonusCardId = card.Id,
        Type = type,
        Amount = amount,
        PurchaseAmount = purchase,
        ReceiptId = receiptId,
        Comment = comment,
        CreatedAt = at,
        BalanceAfter = card.Balance,
        Source = source,
        StaffUserId = Author,
    };

    private static Notification NewNotification(
        Guid customerId, Store s, NotificationType type, string title, string body, string? detail, DateTime at,
        string? templateKey = null, int? amount = null, decimal? purchaseAmount = null, string? levelKey = null,
        Guid? receiptId = null) => new()
    {
        Id = Guid.NewGuid(),
        CustomerId = customerId,
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
