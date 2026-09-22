using SalemBonus.Application.BonusCards;
using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Customers;
using SalemBonus.Application.Pos.Dtos;
using SalemBonus.Domain.Entities;
using SalemBonus.Domain.Enums;

namespace SalemBonus.Application.Pos;

public class PosService(
    IStoreRepository stores,
    ICustomerRepository customers,
    IBonusCardRepository cards,
    IBonusTransactionRepository transactions,
    INotificationRepository notifications,
    IUnitOfWork unitOfWork) : IPosService
{
    public async Task<PosStoreDto> AuthenticateAsync(string? apiKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new UnauthorizedException("X-Store-Api-Key тақырыбы жоқ");
        var store = await stores.GetByApiKeyAsync(apiKey, ct);
        if (store is null || !store.IsActive)
            throw new UnauthorizedException("API кілті жарамсыз");
        return new PosStoreDto(store.Id, store.Name, store.Category, store.CashbackPercent, store.MaxRedeemPercent);
    }

    public async Task<PosCustomerDto> LookupCustomerAsync(Guid storeId, string code, CancellationToken ct = default)
    {
        var store = await GetStoreAsync(storeId, ct);
        var customer = await FindCustomerAsync(code, ct)
            ?? throw new NotFoundException("Тұтынушы табылмады");
        var card = await cards.GetAsync(customer.Id, storeId, ct);
        return ToDto(customer, store, card);
    }

    public async Task<PurchaseResultDto> RegisterPurchaseAsync(Guid storeId, PurchaseRequest request, CancellationToken ct = default)
    {
        if (request.PurchaseAmount <= 0)
            throw new ValidationException("Сатып алу сомасы оң болуы керек");
        if (request.RedeemAmount < 0)
            throw new ValidationException("Шегерілетін бонус теріс бола алмайды");

        var store = await GetStoreAsync(storeId, ct);
        var customer = await FindCustomerAsync(request.CustomerCode, ct) ?? await RegisterByPhoneAsync(request.CustomerCode, ct);

        var card = await cards.GetForUpdateAsync(customer.Id, storeId, ct);
        var isNewCard = card is null;
        if (card is null)
        {
            card = new BonusCard { Id = Guid.NewGuid(), CustomerId = customer.Id, StoreId = storeId, Store = store };
            cards.Add(card);
        }

        var maxRedeem = BonusRules.MaxRedeemable(request.PurchaseAmount, store.MaxRedeemPercent, card.Balance);
        if (request.RedeemAmount > maxRedeem)
            throw new ValidationException($"Ең көп {maxRedeem} Б шегеруге болады (баланс {card.Balance} Б, лимит {store.MaxRedeemPercent}%)");

        var now = DateTime.UtcNow;
        Guid? redemptionId = null;
        if (request.RedeemAmount > 0)
        {
            card.Balance -= request.RedeemAmount;
            var tx = NewTx(card, BonusTransactionType.Redemption, -request.RedeemAmount, request.PurchaseAmount, request.Comment, now);
            transactions.Add(tx);
            redemptionId = tx.Id;
            notifications.Add(NewNotification(customer, store, NotificationType.BonusRedeemed,
                "Бонус жұмсалды",
                $"{store.Name} — {Fmt(request.RedeemAmount)} Б бонус шегерілді.",
                $"Сатып алу сомасы: {Fmt(request.PurchaseAmount)} ₸", now));
        }

        var paid = request.PurchaseAmount - request.RedeemAmount;
        var accrued = BonusRules.CalculateAccrual(paid, store.CashbackPercent);
        Guid? accrualId = null;
        if (accrued > 0)
        {
            card.Balance += accrued;
            var tx = NewTx(card, BonusTransactionType.Accrual, accrued, request.PurchaseAmount, request.Comment, now.AddMilliseconds(1));
            transactions.Add(tx);
            accrualId = tx.Id;
            notifications.Add(NewNotification(customer, store, NotificationType.BonusAccrued,
                "Бонус есептелді!",
                $"{store.Name} — Сізге {Fmt(accrued)} Б бонус есептелді.",
                $"Сатып алу сомасы: {Fmt(request.PurchaseAmount)} ₸", now.AddMilliseconds(1)));
        }

        card.TotalSpent += paid;
        var newLevel = BonusRules.LevelFor(card.TotalSpent);
        var upgraded = newLevel > card.Level;
        if (upgraded)
        {
            card.Level = newLevel;
            notifications.Add(NewNotification(customer, store, NotificationType.System,
                "Жаңа деңгей!",
                $"{store.Name} — Құттықтаймыз, сіз енді {CustomerLevels.Name(newLevel)} деңгейіндесіз.",
                null, now.AddMilliseconds(2)));
        }

        if (isNewCard)
        {
            notifications.Add(NewNotification(customer, store, NotificationType.StoreAdded,
                "Жаңа дүкен қосылды",
                $"{store.Name} дүкені сіздің карталарыңызға қосылды.",
                "Енді бұл дүкенде де бонус жинай аласыз!", now.AddMilliseconds(-1)));
        }

        await unitOfWork.SaveChangesAsync(ct);

        return new PurchaseResultDto(
            customer.Id, card.Id, request.PurchaseAmount, request.RedeemAmount, paid, accrued,
            card.Balance, CustomerLevels.Name(card.Level), upgraded, accrualId, redemptionId);
    }

    private async Task<Store> GetStoreAsync(Guid storeId, CancellationToken ct) =>
        await stores.GetByIdAsync(storeId, ct) ?? throw new NotFoundException("Дүкен табылмады");

    private async Task<Customer?> FindCustomerAsync(string rawCode, CancellationToken ct)
    {
        var code = rawCode.Trim();
        if (string.IsNullOrEmpty(code)) throw new ValidationException("Тұтынушы коды бос");
        if (code.StartsWith(CustomerService.QrPrefix, StringComparison.OrdinalIgnoreCase))
            code = code[CustomerService.QrPrefix.Length..];
        if (BonusRules.LooksLikePhone(code))
            return await customers.GetByPhoneAsync(BonusRules.NormalizePhone(code), ct);
        return await customers.GetByQrCodeAsync(code.ToUpperInvariant(), ct);
    }

    private async Task<Customer> RegisterByPhoneAsync(string rawCode, CancellationToken ct)
    {
        if (!BonusRules.LooksLikePhone(rawCode))
            throw new NotFoundException("QR коды бойынша тұтынушы табылмады");
        var phone = BonusRules.NormalizePhone(rawCode);
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Phone = phone,
            FullName = phone,
            QrCode = BonusRules.GenerateQrCode(),
        };
        customers.Add(customer);
        return customer;
    }

    private static PosCustomerDto ToDto(Customer c, Store store, BonusCard? card) => new(
        c.Id,
        c.FullName,
        MaskPhone(c.Phone),
        card is null,
        card?.Balance ?? 0,
        CustomerLevels.Name(card?.Level ?? CustomerLevel.New),
        store.CashbackPercent,
        store.MaxRedeemPercent);

    private static BonusTransaction NewTx(BonusCard card, BonusTransactionType type, int amount, decimal purchase, string? comment, DateTime at) => new()
    {
        Id = Guid.NewGuid(),
        BonusCardId = card.Id,
        Type = type,
        Amount = amount,
        PurchaseAmount = purchase,
        Comment = comment,
        CreatedAt = at,
    };

    private static Notification NewNotification(Customer c, Store s, NotificationType type, string title, string body, string? detail, DateTime at) => new()
    {
        Id = Guid.NewGuid(),
        CustomerId = c.Id,
        StoreId = s.Id,
        Type = type,
        Title = title,
        Body = body,
        Detail = detail,
        CreatedAt = at,
    };

    private static string MaskPhone(string phone) =>
        phone.Length >= 4 ? $"{phone[..Math.Min(5, phone.Length)]} *** ** {phone[^2..]}" : phone;

    private static string Fmt(decimal n) => string.Format(new System.Globalization.CultureInfo("ru-RU"), "{0:N0}", n).Replace(' ', ' ');
}
