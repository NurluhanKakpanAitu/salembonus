using SalemBonus.Application.BonusCards;
using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Customers;
using SalemBonus.Application.Notifications;
using SalemBonus.Application.Pos.Dtos;
using SalemBonus.Domain.Entities;
using SalemBonus.Domain.Enums;

namespace SalemBonus.Application.Pos;

public class PosService(
    IStoreRepository stores,
    ICustomerRepository customers,
    IBonusCardRepository cards,
    BonusLedger ledger,
    IUnitOfWork unitOfWork,
    ICurrentLanguage language) : IPosService
{
    private AppLanguage Lang => language.Value;

    public async Task<PosStoreDto> AuthenticateAsync(string? apiKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new UnauthorizedException(Messages.StoreApiKeyMissing(Lang));
        var store = await stores.GetByApiKeyAsync(apiKey, ct);
        if (store is null || !store.IsActive)
            throw new UnauthorizedException(Messages.StoreApiKeyInvalid(Lang));
        return new PosStoreDto(store.Id, store.Name, store.Category, store.CashbackPercent, store.MaxRedeemPercent);
    }

    public async Task<PosCustomerDto> LookupCustomerAsync(Guid storeId, string code, CancellationToken ct = default)
    {
        var store = await GetStoreAsync(storeId, ct);
        var customer = await FindCustomerAsync(code, ct)
            ?? throw new NotFoundException(Messages.CustomerNotFound(Lang));
        var card = await cards.GetAsync(customer.Id, storeId, ct);
        return ToDto(customer, store, card);
    }

    public async Task<PurchaseResultDto> RegisterPurchaseAsync(Guid storeId, PurchaseRequest request, CancellationToken ct = default)
    {
        if (request.PurchaseAmount <= 0)
            throw new ValidationException(Messages.PurchaseAmountPositive(Lang));
        if (request.RedeemAmount < 0)
            throw new ValidationException(Messages.RedeemNegative(Lang));

        var store = await GetStoreAsync(storeId, ct);
        var customer = await FindCustomerAsync(request.CustomerCode, ct) ?? await RegisterByPhoneAsync(request.CustomerCode, ct);

        var now = DateTime.UtcNow;
        var card = await ledger.GetOrCreateCardAsync(customer, store, now, ct);

        var maxRedeem = BonusRules.MaxRedeemable(request.PurchaseAmount, store.MaxRedeemPercent, card.Balance);
        if (request.RedeemAmount > maxRedeem)
            throw new ValidationException(Messages.RedeemTooMuch(Lang, maxRedeem, card.Balance, store.MaxRedeemPercent));

        // Бір сатып алудың барлық операциясы бір чекке жатады.
        var receiptId = Guid.NewGuid();
        Guid? redemptionId = request.RedeemAmount > 0
            ? await ledger.RedeemAsync(card, customer, store, request.RedeemAmount, request.PurchaseAmount, request.Comment, receiptId, now, ct)
            : null;

        var paid = request.PurchaseAmount - request.RedeemAmount;
        // Пайыз клиенттің сол дүкендегі ағымдағы мәртебесі бойынша алынады.
        var (accrued, accrualId, upgraded) = ledger.Accrue(card, customer, store, paid, request.PurchaseAmount, request.Comment, receiptId, now);

        await unitOfWork.SaveChangesAsync(ct);

        return new PurchaseResultDto(
            customer.Id, card.Id, request.PurchaseAmount, request.RedeemAmount, paid, accrued,
            card.Balance, CustomerLevels.Name(card.Level, Lang), upgraded, accrualId, redemptionId);
    }

    private async Task<Store> GetStoreAsync(Guid storeId, CancellationToken ct) =>
        await stores.GetByIdAsync(storeId, ct) ?? throw new NotFoundException(Messages.StoreNotFound(Lang));

    private async Task<Customer?> FindCustomerAsync(string rawCode, CancellationToken ct)
    {
        var code = rawCode.Trim();
        if (string.IsNullOrEmpty(code)) throw new ValidationException(Messages.CustomerCodeEmpty(Lang));
        if (code.StartsWith(CustomerService.QrPrefix, StringComparison.OrdinalIgnoreCase))
            code = code[CustomerService.QrPrefix.Length..];
        if (BonusRules.LooksLikePhone(code))
            return await customers.GetByPhoneAsync(BonusRules.NormalizePhone(code), ct);
        return await customers.GetByQrCodeAsync(code.ToUpperInvariant(), ct);
    }

    private async Task<Customer> RegisterByPhoneAsync(string rawCode, CancellationToken ct)
    {
        if (!BonusRules.LooksLikePhone(rawCode))
            throw new NotFoundException(Messages.CustomerByQrNotFound(Lang));
        var phone = BonusRules.NormalizePhone(rawCode);
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Phone = phone,
            QrCode = BonusRules.GenerateQrCode(),
        };
        customers.Add(customer);
        return customer;
    }

    private PosCustomerDto ToDto(Customer c, Store store, BonusCard? card) => new(
        c.Id,
        c.FullName,
        MaskPhone(c.Phone),
        card is null,
        card?.Balance ?? 0,
        CustomerLevels.Name(card?.Level ?? CustomerLevel.New, Lang),
        BonusRules.PercentFor(card?.Level ?? CustomerLevel.New, BonusRules.LadderOf(store)),
        store.MaxRedeemPercent);

    private static string MaskPhone(string phone) =>
        phone.Length >= 4 ? $"{phone[..Math.Min(5, phone.Length)]} *** ** {phone[^2..]}" : phone;

}
