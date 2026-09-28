using SalemBonus.Application.BonusCards;
using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Application.Pos.Catalog;
using SalemBonus.Application.Pos.Inventory;
using SalemBonus.Domain.Core;
using SalemBonus.Domain.Pos.Inventory;
using SalemBonus.Domain.Pos.Sales;

namespace SalemBonus.Application.Pos.Sales;

/// <summary>
/// Қайтару (ТЗ «Касса» §13): толық не бөлшек (2 данадан 1-і). Әр позицияның қайтарылғаны сатылғанынан
/// аспайды. Клиентке нақты төлегені қайтарылады (чек жеңілдігі мен бонус үлесі шегерілген баға), алдымен
/// сатылымның ашық қарызы азаяды, қалғаны ақшамен. Тауар қоймаға қайтады, бонус SalemBonus ережесімен түзетіледі.
/// </summary>
public class ReturnService(
    ISalesRepository sales,
    IStoreRepository stores,
    IBonusCardRepository cards,
    IInventoryRepository inventory,
    WarehouseService warehouses,
    CashierService cashier,
    SaleService saleService,
    BonusLedger ledger,
    StoreNotifier notifier,
    CatalogAccess access,
    IUnitOfWork unitOfWork)
{
    private AppLanguage Lang => access.Lang;

    public async Task<ReceiptDto> CreateAsync(Guid saleId, CreateReturnRequest request, string? deviceToken, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.SalesReturn, ct);
        var register = await cashier.RequireRegisterAsync(deviceToken, m, ct);
        if (request.ClientRequestId == Guid.Empty) throw new ValidationException(Messages.SaleRequestIdRequired(Lang));

        var sale = await sales.GetSaleForUpdateAsync(m.StoreId, saleId, ct) ?? throw new NotFoundException(Messages.SaleNotFound(Lang));
        var previous = await sales.ListReturnsAsync(sale.Id, ct);
        // Қайта жіберілсе (екі рет басылды) — сол қайтару, екінші рет ақша берілмейді.
        if (previous.Any(r => r.ClientRequestId == request.ClientRequestId)) return await saleService.ReceiptAsync(sale, ct);

        var lines = (request.Items ?? [])
            .Where(i => i.Quantity > 0)
            .GroupBy(i => i.SaleItemId)
            .Select(g => (ItemId: g.Key, Quantity: Math.Round(g.Sum(x => x.Quantity), 3)))
            .ToList();
        if (lines.Count == 0) throw new ValidationException(Messages.ReturnEmpty(Lang), "items");

        var returnedMoneyByItem = previous.SelectMany(r => r.Items).GroupBy(i => i.SaleItemId).ToDictionary(g => g.Key, g => g.Sum(i => i.Amount));
        var returnItems = new List<(SaleItem Item, decimal Quantity, decimal Amount)>();
        foreach (var (itemId, quantity) in lines)
        {
            var item = sale.Items.FirstOrDefault(i => i.Id == itemId) ?? throw new ValidationException(Messages.ReturnItemNotFound(Lang), "items");
            var available = item.Quantity - item.ReturnedQuantity;
            if (quantity > available) throw new ValidationException(Messages.ReturnTooMuch(Lang, item.Name, available), "items");

            // Позиция түгел қайтса — қалған соманың бәрі (дөңгелектеу қалдығы қалмасын).
            var paidForLine = item.LineTotal - item.Discount;
            var amount = quantity == available
                ? paidForLine - returnedMoneyByItem.GetValueOrDefault(item.Id)
                : Math.Round(paidForLine * quantity / item.Quantity, paidForLine % 1 == 0 ? 0 : 2);
            returnItems.Add((item, quantity, amount));
        }
        var money = returnItems.Sum(x => x.Amount);
        var fullyReturned = sale.Items.All(i =>
            i.Quantity - i.ReturnedQuantity - (returnItems.FirstOrDefault(x => x.Item.Id == i.Id).Quantity) == 0);

        var store = await stores.GetByIdAsync(m.StoreId, ct) ?? throw new NotFoundException(Messages.StoreNotFound(Lang));
        var warehouse = await warehouses.EnsureDefaultAsync(orgId, m.StoreId, ct);

        await unitOfWork.InTransactionAsync(async () =>
        {
            var now = DateTime.UtcNow;

            // Қарызға сатылған болса — алдымен ашық қарыз азаяды (ақшасы әлі төленбеген тауар).
            var debt = await sales.GetDebtBySaleForUpdateAsync(sale.Id, ct);
            var debtReduced = debt is { Status: DebtStatus.Open } ? Math.Min(money, debt.Remaining) : 0;
            var refund = money - debtReduced;
            PaymentMethod? refundMethod = null;
            if (refund > 0)
            {
                refundMethod = Enum.TryParse<PaymentMethod>(request.RefundMethod, ignoreCase: true, out var rm)
                               && rm is PaymentMethod.Cash or PaymentMethod.Card or PaymentMethod.Qr or PaymentMethod.Transfer
                    ? rm : throw new ValidationException(Messages.ReturnRefundMethodRequired(Lang), "refundMethod");
            }

            var saleReturn = new SaleReturn
            {
                Id = Guid.NewGuid(), OrganizationId = orgId, StoreId = m.StoreId, SaleId = sale.Id, RegisterId = register.Id,
                StaffUserId = access.StaffUserId, ClientRequestId = request.ClientRequestId, Amount = money, Refunded = refund,
                RefundMethod = refundMethod, DebtReduced = debtReduced, Reason = CatalogAccess.Optional(request.Reason, 300), CreatedAt = now,
            };
            saleReturn.Items = returnItems.Select(x => new SaleReturnItem
            {
                Id = Guid.NewGuid(), ReturnId = saleReturn.Id, SaleItemId = x.Item.Id, ProductId = x.Item.ProductId,
                Name = x.Item.Name, Quantity = x.Quantity, Amount = x.Amount,
            }).ToList();

            if (debt is not null && debtReduced > 0)
            {
                debt.Paid += debtReduced;
                sales.AddDebtPayment(new DebtPayment
                {
                    Id = Guid.NewGuid(), DebtId = debt.Id, Amount = debtReduced, IsReturn = true, RemainingAfter = debt.Remaining,
                    StaffUserId = access.StaffUserId, RegisterId = register.Id, CreatedAt = now,
                });
                if (debt.Remaining <= 0)
                {
                    debt.Status = DebtStatus.Paid;
                    debt.ClosedAt = now;
                }
            }

            // Тауар қоймаға қайтады (ТЗ §13.7).
            var balances = await sales.BalancesForUpdateAsync(warehouse.Id, returnItems.Select(x => x.Item.ProductId).Distinct().ToList(), ct);
            foreach (var (item, quantity, _) in returnItems)
            {
                item.ReturnedQuantity += quantity;
                if (!balances.TryGetValue(item.ProductId, out var balance))
                {
                    balance = new StockBalance { WarehouseId = warehouse.Id, ProductId = item.ProductId };
                    inventory.AddBalance(balance);
                    balances[item.ProductId] = balance;
                }
                balance.Quantity += quantity;
                balance.UpdatedAt = now;
                inventory.AddMovement(new StockMovement
                {
                    Id = Guid.NewGuid(), OrganizationId = orgId, WarehouseId = warehouse.Id, ProductId = item.ProductId,
                    Type = StockMovementType.Return, Quantity = quantity, StaffUserId = access.StaffUserId,
                    DocumentId = saleReturn.Id, CreatedAt = now,
                });
            }

            // Бонус: шегерілгені қайтарылады, есептелгені алынады — қайтарылған үлеске сай.
            if (sale.BonusCardId is { } cardId && await cards.GetByIdForUpdateAsync(cardId, ct) is { } card)
            {
                var ratio = sale.Total > 0 ? money / sale.Total : 1m;
                var restore = fullyReturned ? sale.BonusRedeemed - sale.BonusRestored : (int)Math.Floor(sale.BonusRedeemed * ratio);
                var reverse = fullyReturned ? sale.BonusAccrued - sale.BonusReversed : (int)Math.Round(sale.BonusAccrued * ratio);
                var (restored, reversed) = await ledger.ApplyReturnAsync(card, store, Math.Max(0, restore), Math.Max(0, reverse),
                    money, sale.Id, $"Возврат по чеку №{sale.Number}", now, ct);
                saleReturn.BonusRestored = restored;
                saleReturn.BonusReversed = reversed;
                sale.BonusRestored += restored;
                sale.BonusReversed += reversed;
            }

            sale.ReturnedAmount += money;
            sale.Status = fullyReturned ? SaleStatus.Returned : SaleStatus.PartiallyReturned;
            sales.AddReturn(saleReturn);
            var staffName = (await sales.StaffNamesAsync([access.StaffUserId], ct)).GetValueOrDefault(access.StaffUserId);
            await notifier.NotifyAsync(m.StoreId, StoreNotificationType.SaleReturn, null, money, sale.Number, staffName, ct);

            access.Audit(orgId, m.StoreId, "sale.return", "sale", sale.Id, null, new
            {
                sale.Number, Amount = money, Refunded = refund, RefundMethod = refundMethod?.ToString(), DebtReduced = debtReduced,
                saleReturn.BonusRestored, saleReturn.BonusReversed, saleReturn.Reason,
                Items = saleReturn.Items.Select(i => new { i.Name, i.Quantity, i.Amount }),
            });
            await unitOfWork.SaveChangesAsync(ct);
            return saleReturn;
        }, ct);

        return await saleService.ReceiptAsync(sale, ct);
    }
}

/// <summary>Қарыздар (ТЗ «Касса» §11.6–11.12): клиенттің қарыздары және бөліп не толық өтеу.</summary>
public class DebtService(ISalesRepository sales, CashierService cashier, StoreNotifier notifier, CatalogAccess access, IUnitOfWork unitOfWork)
{
    private AppLanguage Lang => access.Lang;

    public async Task<IReadOnlyList<DebtDto>> ListByCustomerAsync(Guid customerId, CancellationToken ct = default)
    {
        var (_, m) = await access.RequireAsync(StaffPermissions.SalesCreate, ct);
        var debts = await sales.ListCustomerDebtsAsync(m.StoreId, customerId, ct);
        var names = await sales.StaffNamesAsync(
            debts.Select(d => d.StaffUserId).Concat(debts.SelectMany(d => d.Payments).Select(p => p.StaffUserId)).Distinct().ToList(), ct);
        return debts.Select(d => ToDto(d, names)).ToList();
    }

    /// <summary>Өтеу: бөлшек (100 000 → 40 000 → қалғаны 60 000) не толық; күні мен кассирі автоматты.</summary>
    public async Task<DebtDto> RepayAsync(Guid debtId, RepayDebtRequest request, string? deviceToken, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.SalesCreate, ct);
        var register = await cashier.RequireRegisterAsync(deviceToken, m, ct);
        var debt = await sales.GetDebtForUpdateAsync(m.StoreId, debtId, ct) ?? throw new NotFoundException(Messages.DebtNotFound(Lang));
        if (debt.Status == DebtStatus.Paid) throw new ValidationException(Messages.DebtAlreadyPaid(Lang), "amount");

        var amount = Math.Round(request.Amount, 2);
        if (amount <= 0) throw new ValidationException(Messages.PaymentAmountInvalid(Lang), "amount");
        if (amount > debt.Remaining) throw new ValidationException(Messages.DebtRepayTooMuch(Lang, debt.Remaining), "amount");
        var method = Enum.TryParse<PaymentMethod>(request.Method, ignoreCase: true, out var pm)
                     && pm is PaymentMethod.Cash or PaymentMethod.Card or PaymentMethod.Qr or PaymentMethod.Transfer
            ? pm : throw new ValidationException(Messages.PaymentMethodInvalid(Lang), "method");

        string? recipient = null;
        if (method == PaymentMethod.Transfer)
        {
            var list = await sales.ListRecipientsAsync(m.StoreId, ct);
            recipient = list.FirstOrDefault(r => r.Id == request.TransferRecipientId && r.IsActive)?.Display
                        ?? throw new ValidationException(Messages.TransferRecipientRequired(Lang), "method");
        }

        var now = DateTime.UtcNow;
        debt.Paid += amount;
        sales.AddDebtPayment(new DebtPayment
        {
            Id = Guid.NewGuid(), DebtId = debt.Id, Amount = amount, Method = method, TransferRecipient = recipient,
            RemainingAfter = debt.Remaining, StaffUserId = access.StaffUserId, RegisterId = register.Id, CreatedAt = now,
        });
        if (debt.Remaining <= 0)
        {
            debt.Status = DebtStatus.Paid;
            debt.ClosedAt = now;
        }
        var names = await sales.CustomerNamesAsync([debt.CustomerId], ct);
        var cashierName = (await sales.StaffNamesAsync([access.StaffUserId], ct)).GetValueOrDefault(access.StaffUserId);
        await notifier.NotifyAsync(m.StoreId, StoreNotificationType.DebtRepaid, names.GetValueOrDefault(debt.CustomerId), amount,
            debt.SaleNumber, cashierName, ct);
        access.Audit(orgId, m.StoreId, "debt.repay", "debt", debt.Id, null,
            new { debt.SaleNumber, Amount = amount, Method = method.ToString(), Remaining = debt.Remaining });
        await unitOfWork.SaveChangesAsync(ct);

        var fresh = await sales.GetDebtForUpdateAsync(m.StoreId, debtId, ct);
        var staffNames = await sales.StaffNamesAsync(
            fresh!.Payments.Select(p => p.StaffUserId).Append(fresh.StaffUserId).Distinct().ToList(), ct);
        return ToDto(fresh, staffNames);
    }

    private static DebtDto ToDto(Debt d, IReadOnlyDictionary<Guid, string> names) => new(
        d.Id, d.SaleId, d.SaleNumber, d.Amount, d.Paid, d.Remaining, d.DueDate, d.Comment, d.Status.ToString(), d.CreatedAt,
        names.GetValueOrDefault(d.StaffUserId) ?? string.Empty,
        d.Payments.OrderByDescending(p => p.CreatedAt).Select(p => new DebtPaymentDto(
            p.Amount, p.Method?.ToString(), p.IsReturn, p.TransferRecipient, p.RemainingAfter, p.CreatedAt,
            names.GetValueOrDefault(p.StaffUserId) ?? string.Empty)).ToList());
}
