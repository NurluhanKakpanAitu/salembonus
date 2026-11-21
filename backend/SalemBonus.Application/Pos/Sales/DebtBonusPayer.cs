using SalemBonus.Application.BonusCards;
using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Application.Pos.Catalog;
using SalemBonus.Domain.Pos.Sales;

namespace SalemBonus.Application.Pos.Sales;

/// <summary>
/// Қарызды клиенттің бонустарымен өтеу (1 Б = 1 ₸). Ереже сатылымдағыдай: қарыздың <c>MaxRedeemPercent</c>-інен (әдепкі 30%)
/// және қарыз сол дүкендегі бонус картасының балансынан артық емес; бұрын бонуспен төленгені осы шектен шегеріледі.
/// Бонус FIFO-мен шегеріледі (Redemption, көзі «debt»), клиентке хабарлама барады. Ақша шоттарына, кассаға тимейді.
/// Шақырушы транзакция ішінде, қарызды бұғаттап алғаннан кейін шақырады және сақтайды.
/// </summary>
public class DebtBonusPayer(IBonusCardRepository cards, ICustomerRepository customers, IStoreRepository stores, BonusLedger ledger, CatalogAccess access)
{
    private AppLanguage Lang => access.Lang;

    /// <summary>Осы қарызға бонуспен ең көбі қанша төлеуге болады (баланс ескерілмейді).</summary>
    public static int Limit(Debt debt, decimal maxPercent)
    {
        var paidByBonus = debt.Payments.Where(p => p.IsBonus).Sum(p => p.Amount);
        var byPercent = (int)Math.Floor(debt.Amount * maxPercent / 100m - paidByBonus);
        return Math.Max(0, Math.Min(byPercent, (int)Math.Floor(debt.Remaining)));
    }

    /// <summary>Клиенттің осы дүкендегі балансы мен дүкеннің шегі (UI-ға көрсету үшін).</summary>
    public async Task<(int Balance, decimal MaxPercent)> InfoAsync(Guid customerId, Guid storeId, CancellationToken ct)
    {
        var card = await cards.GetAsync(customerId, storeId, ct);
        var store = await stores.GetByIdAsync(storeId, ct);
        return (card?.Balance ?? 0, store?.MaxRedeemPercent ?? 0);
    }

    public async Task<DebtPayment> PayAsync(Debt debt, decimal requested, Guid? registerId, string? comment, DateTime now, CancellationToken ct, Guid? key = null)
    {
        if (requested != Math.Floor(requested)) throw new ValidationException(Messages.DebtBonusWhole(Lang), "amount");
        var amount = (int)requested;
        var store = await stores.GetByIdAsync(debt.StoreId, ct) ?? throw new NotFoundException(Messages.StoreNotFound(Lang));
        var customer = await customers.GetByIdAsync(debt.CustomerId, ct) ?? throw new ValidationException(Messages.CustomerNotFound(Lang), "customer");
        var card = await cards.GetForUpdateAsync(customer.Id, store.Id, ct);
        var max = Math.Min(Limit(debt, store.MaxRedeemPercent), card?.Balance ?? 0);
        if (amount <= 0 || amount > max) throw new ValidationException(Messages.DebtBonusTooMuch(Lang, max, store.MaxRedeemPercent), "amount");

        var label = debt.SaleNumber > 0 ? $"Погашение долга · чек №{debt.SaleNumber}" : "Погашение долга";
        await ledger.RedeemAsync(card!, customer, store, amount, amount, label, null, now, ct, source: "debt", key: key);
        debt.Paid += amount;
        var payment = new DebtPayment
        {
            Id = Guid.NewGuid(), DebtId = debt.Id, Amount = amount, Method = null, IsBonus = true, Comment = comment,
            RemainingAfter = debt.Remaining, StaffUserId = access.StaffUserId, RegisterId = registerId, CreatedAt = now,
        };
        if (debt.Remaining <= 0)
        {
            debt.Status = DebtStatus.Paid;
            debt.ClosedAt = now;
        }
        return payment;
    }
}
