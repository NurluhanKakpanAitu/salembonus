using SalemBonus.Application.BonusCards;
using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Application.Core.Staff;
using SalemBonus.Application.Pos.Catalog;
using SalemBonus.Application.Pos.Inventory;
using SalemBonus.Domain.Core;
using SalemBonus.Domain.Entities;
using SalemBonus.Domain.Pos.Inventory;
using SalemBonus.Domain.Pos.Sales;

namespace SalemBonus.Application.Pos.Sales;

/// <summary>
/// Сатылым (ТЗ «Касса» §7–10, §20–21). Сервер кассаға сенбейді: бағаны, қалдықты, жеңілдік шегін,
/// бонус шегін және төлем қосындысын өзі қайта есептеп тексереді. Чек, сатылым, төлем, бонус,
/// қалдықтың азаюы мен аудит бір транзакцияда — не бәрі, не ештеңе.
/// </summary>
public class SaleService(
    ISalesRepository sales,
    ICatalogRepository catalog,
    IStoreRepository stores,
    ICustomerRepository customers,
    IBonusCardRepository cards,
    IInventoryRepository inventory,
    IStaffRepository staff,
    IStaffAuthService staffAuth,
    WarehouseService warehouses,
    CashierService cashier,
    BonusLedger ledger,
    CatalogAccess access,
    IUnitOfWork unitOfWork)
{
    public const int MaxLines = 200;
    private AppLanguage Lang => access.Lang;

    private sealed record Line(Guid ProductId, string Name, string? Article, string? Unit, decimal Quantity, decimal Price, decimal LineTotal);

    public async Task<ReceiptDto> CreateAsync(CreateSaleRequest request, string? deviceToken, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.SalesCreate, ct);
        var register = await cashier.RequireRegisterAsync(deviceToken, m, ct);
        if (request.ClientRequestId == Guid.Empty) throw new ValidationException(Messages.SaleRequestIdRequired(Lang));

        // «Оплатить» екі рет басылса не желі үзіліп қайта жіберілсе — сол чек қайтарылады.
        if (await sales.FindByClientRequestAsync(m.StoreId, request.ClientRequestId, ct) is { } done)
            return await ToReceiptAsync(done, ct);

        var store = await stores.GetByIdAsync(m.StoreId, ct) ?? throw new NotFoundException(Messages.StoreNotFound(Lang));

        // ---------- Тауарлар мен бағалар ----------
        var requested = (request.Items ?? [])
            .GroupBy(i => i.ProductId)
            .Select(g => (ProductId: g.Key, Quantity: Math.Round(g.Sum(x => x.Quantity), 3)))
            .ToList();
        if (requested.Count == 0) throw new ValidationException(Messages.SaleEmpty(Lang), "items");
        if (requested.Count > MaxLines) throw new ValidationException(Messages.SaleTooManyLines(Lang, MaxLines), "items");
        if (requested.Any(r => r.Quantity <= 0)) throw new ValidationException(Messages.SaleQuantityInvalid(Lang), "items");

        var ids = requested.Select(r => r.ProductId).ToList();
        var products = (await sales.ListProductsAsync(orgId, ids, ct)).ToDictionary(p => p.Id);
        var prices = await sales.PricesAsync(m.StoreId, ids, ct);
        var units = (await catalog.ListUnitsAsync(orgId, ct)).ToDictionary(u => u.Id, u => u.ShortName);

        var lines = new List<Line>();
        foreach (var (productId, quantity) in requested)
        {
            if (!products.TryGetValue(productId, out var p) || p.Status != Domain.Pos.Catalog.CatalogStatus.Active || p.HideInCashier)
                throw new ValidationException(Messages.SaleProductUnavailable(Lang), "items");
            if (!prices.TryGetValue(productId, out var price) || price <= 0)
                throw new ValidationException(Messages.SaleNoPrice(Lang, p.Name), "items");
            lines.Add(new Line(productId, p.Name, p.Article, units.GetValueOrDefault(p.UnitId), quantity, price, Math.Round(price * quantity, 2)));
        }
        var subtotal = lines.Sum(l => l.LineTotal);

        // ---------- Жеңілдік (ТЗ §6.5–6.6) ----------
        DiscountKind? discountKind = null;
        decimal? discountValue = null;
        decimal discount = 0;
        Guid? approvedBy = null;
        if (request.Discount is { Value: > 0 } d)
        {
            discountKind = Enum.TryParse<DiscountKind>(d.Kind, ignoreCase: true, out var k) ? k
                : throw new ValidationException(Messages.DiscountKindInvalid(Lang), "discount");
            discountValue = Math.Round(d.Value, 2);
            discount = k switch
            {
                DiscountKind.Percent when d.Value <= 100 => Math.Round(subtotal * d.Value / 100m, 0, MidpointRounding.AwayFromZero),
                DiscountKind.Amount when d.Value <= subtotal => Math.Round(d.Value, 2),
                _ => throw new ValidationException(Messages.DiscountTooLarge(Lang), "discount"),
            };

            // Кассир өз шегінен асырса — құқығы бар қызметкер PIN-імен растайды.
            var percent = subtotal == 0 ? 0 : discount / subtotal * 100m;
            if (!m.Has(StaffPermissions.DiscountApprove) && percent > m.MaxDiscountPercent)
            {
                if (request.Approval is not { } approval)
                    throw new ValidationException(Messages.DiscountNeedsApproval(Lang, m.MaxDiscountPercent), "approval");
                await staffAuth.ApproveWithPinAsync(approval.StaffUserId, approval.Pin, m.StoreId, register.Id,
                    StaffPermissions.DiscountApprove, ct);
                approvedBy = approval.StaffUserId;
            }
        }
        var afterDiscount = subtotal - discount;

        // ---------- Клиент мен бонус (ТЗ §6.1–6.4) ----------
        Customer? customer = null;
        if (request.CustomerId is { } customerId)
            customer = await customers.GetByIdAsync(customerId, ct) ?? throw new ValidationException(Messages.CustomerNotFound(Lang), "customer");
        if (request.BonusRedeem < 0) throw new ValidationException(Messages.RedeemNegative(Lang), "bonus");
        if (request.BonusRedeem > 0)
        {
            if (customer is null) throw new ValidationException(Messages.SaleBonusNeedsCustomer(Lang), "bonus");
            var card = await cards.GetAsync(customer.Id, m.StoreId, ct);
            var max = BonusRules.MaxRedeemable(afterDiscount, store.MaxRedeemPercent, card?.Balance ?? 0);
            if (request.BonusRedeem > max)
                throw new ValidationException(Messages.RedeemTooMuch(Lang, max, card?.Balance ?? 0, store.MaxRedeemPercent), "bonus");
        }
        var total = afterDiscount - request.BonusRedeem;

        // ---------- Төлем (ТЗ §7–10, §21.2–21.3) ----------
        var (payments, debtRequest) = await ValidatePaymentsAsync(request.Payments ?? [], total, m, customer, ct);

        // Жеңілдік пен бонус жолдарға үлес бойынша бөлінеді — қайтарғанда клиент нақты төлегенін алады.
        var reduction = discount + request.BonusRedeem;
        var shares = new decimal[lines.Count];
        if (reduction > 0 && subtotal > 0)
        {
            // Бүтін теңгемен: қайтарғанда клиентке тиынсыз сома беріледі (қалдығы соңғы жолға).
            var digits = reduction % 1 == 0 ? 0 : 2;
            for (var i = 0; i < lines.Count - 1; i++) shares[i] = Math.Round(reduction * lines[i].LineTotal / subtotal, digits);
            shares[^1] = reduction - shares[..^1].Sum();
        }

        var warehouse = await warehouses.EnsureDefaultAsync(orgId, m.StoreId, ct);
        var sale = await unitOfWork.InTransactionAsync(async () =>
        {
            // Қалдық транзакция ішінде бұғатталып тексеріледі: екі касса соңғы данаға бір уақытта таласса,
            // екіншісі күтеді де, нақты қалдықты көреді.
            var balances = await sales.BalancesForUpdateAsync(warehouse.Id, ids, ct);
            if (!m.Has(StaffPermissions.SalesNegativeStock))
                foreach (var l in lines)
                {
                    var available = balances.TryGetValue(l.ProductId, out var b) ? b.Quantity : 0;
                    if (l.Quantity > available)
                        throw new ValidationException(Messages.SaleStockInsufficient(Lang, l.Name, available), "items");
                }

            var now = DateTime.UtcNow;
            var sale = new Sale
            {
                Id = Guid.NewGuid(),
                OrganizationId = orgId,
                StoreId = m.StoreId,
                RegisterId = register.Id,
                StaffUserId = access.StaffUserId,
                Number = await sales.NextReceiptNumberAsync(m.StoreId, ct),
                ClientRequestId = request.ClientRequestId,
                CustomerId = customer?.Id,
                Subtotal = subtotal,
                DiscountKind = discountKind,
                DiscountValue = discountValue,
                DiscountAmount = discount,
                DiscountApprovedBy = approvedBy,
                BonusRedeemed = request.BonusRedeem,
                Total = total,
                CreatedAt = now,
            };
            sale.Items = lines.Select((l, i) => new SaleItem
            {
                Id = Guid.NewGuid(), SaleId = sale.Id, ProductId = l.ProductId, Name = l.Name, Article = l.Article,
                UnitShortName = l.Unit, Quantity = l.Quantity, Price = l.Price, LineTotal = l.LineTotal, Discount = shares[i], SortOrder = i + 1,
            }).ToList();
            sale.Payments = payments.Select(p => { p.SaleId = sale.Id; return p; }).ToList();

            foreach (var l in lines)
            {
                if (!balances.TryGetValue(l.ProductId, out var balance))
                {
                    balance = new StockBalance { WarehouseId = warehouse.Id, ProductId = l.ProductId };
                    inventory.AddBalance(balance);
                }
                balance.Quantity -= l.Quantity;
                balance.UpdatedAt = now;
                inventory.AddMovement(new StockMovement
                {
                    Id = Guid.NewGuid(), OrganizationId = orgId, WarehouseId = warehouse.Id, ProductId = l.ProductId,
                    Type = StockMovementType.Sale, Quantity = -l.Quantity, StaffUserId = access.StaffUserId,
                    DocumentId = sale.Id, CreatedAt = now,
                });
            }

            // Бонус: шегеру мен есептеу SalemBonus ережесімен, чекке байланады (клиент қосымшасында «ЧЕК»).
            if (customer is not null)
            {
                var card = await ledger.GetOrCreateCardAsync(customer, store, now, ct);
                sale.BonusCardId = card.Id;
                if (request.BonusRedeem > 0)
                    await ledger.RedeemAsync(card, customer, store, request.BonusRedeem, afterDiscount, $"Чек №{sale.Number}", sale.Id, now, ct);
                // Бонус тек нақты төленген бөліктен: қарызға берілгені есептелмейді.
                var paidNow = total - sale.DebtAmount;
                sale.BonusAccrued = ledger.Accrue(card, customer, store, paidNow, afterDiscount, $"Чек №{sale.Number}", sale.Id, now).Accrued;
            }

            if (debtRequest is { } dr)
            {
                var debt = new Debt
                {
                    Id = Guid.NewGuid(), OrganizationId = orgId, StoreId = m.StoreId, CustomerId = customer!.Id,
                    SaleId = sale.Id, SaleNumber = sale.Number, Amount = sale.DebtAmount, DueDate = dr.DueDate,
                    Comment = dr.Comment, StaffUserId = access.StaffUserId, CreatedAt = now,
                };
                sales.AddDebt(debt);
                access.Audit(orgId, m.StoreId, "debt.create", "debt", debt.Id, null,
                    new { SaleNumber = sale.Number, debt.Amount, DueDate = debt.DueDate.ToString("yyyy-MM-dd"), CustomerId = customer.Id });
            }

            sales.AddSale(sale);
            access.Audit(orgId, m.StoreId, "sale.create", "sale", sale.Id, null, new
            {
                sale.Number, sale.Total, sale.Subtotal, sale.DiscountAmount, sale.BonusRedeemed, sale.BonusAccrued,
                RegisterId = register.Id, CustomerId = customer?.Id,
                Payments = payments.Select(p => new { Method = p.Method.ToString(), p.Amount }),
            });
            if (discount > 0)
                access.Audit(orgId, m.StoreId, "sale.discount", "sale", sale.Id, null,
                    new { sale.Number, Kind = discountKind.ToString(), Value = discountValue, Amount = discount, ApprovedBy = approvedBy });

            await unitOfWork.SaveChangesAsync(ct);
            return sale;
        }, ct);

        return await ToReceiptAsync(sale, ct);
    }

    private sealed record DebtRequest(DateOnly DueDate, string? Comment);

    private async Task<(List<SalePayment> Payments, DebtRequest? Debt)> ValidatePaymentsAsync(IReadOnlyList<SalePaymentRequest> input,
        decimal total, StoreMembership m, Customer? customer, CancellationToken ct)
    {
        var storeId = m.StoreId;
        var settings = await sales.GetSettingsAsync(storeId, ct) ?? new StoreCashierSettings { StoreId = storeId };
        if (total == 0 && input.Count == 0) return ([], null);
        if (input.Count == 0) throw new ValidationException(Messages.PaymentRequired(Lang), "payments");
        if (input.Count > 1 && !settings.MixedEnabled) throw new ValidationException(Messages.PaymentMethodDisabled(Lang), "payments");

        var recipients = (await sales.ListRecipientsAsync(storeId, ct)).Where(r => r.IsActive).ToDictionary(r => r.Id);
        var result = new List<SalePayment>();
        DebtRequest? debt = null;
        foreach (var p in input)
        {
            if (!Enum.TryParse<PaymentMethod>(p.Method, ignoreCase: true, out var method) || !Enum.IsDefined(method))
                throw new ValidationException(Messages.PaymentMethodInvalid(Lang), "payments");
            if (!settings.EnabledMethods.Contains(method))
                throw new ValidationException(Messages.PaymentMethodDisabled(Lang), "payments");
            if (result.Any(x => x.Method == method)) throw new ValidationException(Messages.PaymentMethodRepeated(Lang), "payments");
            var amount = Math.Round(p.Amount, 2);
            if (amount <= 0) throw new ValidationException(Messages.PaymentAmountInvalid(Lang), "payments");

            var payment = new SalePayment { Id = Guid.NewGuid(), Method = method, Amount = amount };
            // Қарыз (ТЗ §11, §21.6): құқық, анықталған клиент және қайтару күні міндетті.
            if (method == PaymentMethod.Debt)
            {
                if (!m.Has(StaffPermissions.SalesDebt)) throw new ForbiddenException(Messages.StaffPermissionDenied(Lang));
                if (customer is null) throw new ValidationException(Messages.DebtNeedsCustomer(Lang), "payments");
                var due = p.DueDate ?? throw new ValidationException(Messages.DebtDueDateRequired(Lang), "dueDate");
                if (due < DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1)) throw new ValidationException(Messages.DebtDueDateInvalid(Lang), "dueDate");
                debt = new DebtRequest(due, CatalogAccess.Optional(p.Comment, 300));
            }
            if (method == PaymentMethod.Transfer)
            {
                var recipient = p.TransferRecipientId is { } rid && recipients.TryGetValue(rid, out var r) ? r
                    : throw new ValidationException(Messages.TransferRecipientRequired(Lang), "payments");
                payment.TransferRecipientId = recipient.Id;
                payment.TransferRecipient = recipient.Display;
            }
            result.Add(payment);
        }

        // Төлемдердің қосындысы итогқа дәл тең болуы керек: кем де, артық та қабылданбайды (ТЗ §10.4–10.6).
        var sum = result.Sum(x => x.Amount);
        if (sum != total) throw new ValidationException(Messages.PaymentMismatch(Lang, total, sum), "payments");

        // Қолма-қол: клиент берген сома мен қайтарым (ТЗ §8). Аралас төлемде сомасы нақты енгізіледі.
        var cash = result.FirstOrDefault(x => x.Method == PaymentMethod.Cash);
        if (cash is not null)
        {
            var received = result.Count == 1 ? Math.Round(input.First(x => string.Equals(x.Method, "Cash", StringComparison.OrdinalIgnoreCase)).Received ?? cash.Amount, 2) : cash.Amount;
            if (received < cash.Amount) throw new ValidationException(Messages.CashNotEnough(Lang, cash.Amount - received), "payments");
            cash.Received = received;
            cash.Change = received - cash.Amount;
        }
        return (result, debt);
    }

    public async Task<ReceiptDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var (_, m) = await access.RequireAsync(StaffPermissions.SalesCreate, ct);
        var sale = await sales.GetSaleAsync(m.StoreId, id, ct) ?? throw new NotFoundException(Messages.SaleNotFound(Lang));
        return await ToReceiptAsync(sale, ct);
    }

    private async Task<ReceiptDto> ToReceiptAsync(Sale sale, CancellationToken ct)
    {
        var store = await stores.GetByIdAsync(sale.StoreId, ct);
        var cashierUser = await staff.GetByIdAsync(sale.StaffUserId, ct);
        var registerName = (await cashier.GetRegisterNameAsync(sale.RegisterId, ct)) ?? string.Empty;
        ReceiptCustomerDto? customerDto = null;
        if (sale.CustomerId is { } cid && await customers.GetByIdAsync(cid, ct) is { } c)
        {
            var card = await cards.GetAsync(c.Id, sale.StoreId, ct);
            customerDto = new ReceiptCustomerDto(c.Id, c.FullName, c.Phone, card?.Balance);
        }

        return new ReceiptDto(
            sale.Id, sale.Number, sale.CreatedAt, store?.Name ?? string.Empty, registerName,
            cashierUser is null ? string.Empty : $"{cashierUser.FirstName} {cashierUser.LastName}".Trim(),
            customerDto,
            sale.Items.OrderBy(i => i.SortOrder).Select(i => new ReceiptItemDto(
                i.Id, i.ProductId, i.Name, i.Article, i.UnitShortName, i.Quantity, i.Price, i.LineTotal, i.Discount, i.ReturnedQuantity)).ToList(),
            sale.Subtotal, sale.DiscountKind?.ToString(), sale.DiscountValue, sale.DiscountAmount,
            sale.BonusRedeemed, sale.BonusAccrued, sale.Total,
            sale.Payments.Select(p => new ReceiptPaymentDto(p.Method.ToString(), p.Amount, p.Received, p.Change, p.TransferRecipient)).ToList(),
            sale.Status.ToString(), sale.ReturnedAmount,
            await sales.GetDebtBySaleAsync(sale.Id, ct) is { } d
                ? new ReceiptDebtDto(d.Id, d.Amount, d.Paid, d.Remaining, d.DueDate, d.Status.ToString(), d.Comment) : null,
            await ReturnsAsync(sale.Id, ct));
    }

    private async Task<IReadOnlyList<ReceiptReturnDto>> ReturnsAsync(Guid saleId, CancellationToken ct)
    {
        var list = await sales.ListReturnsAsync(saleId, ct);
        if (list.Count == 0) return [];
        var names = await sales.StaffNamesAsync(list.Select(r => r.StaffUserId).Distinct().ToList(), ct);
        return list.Select(r => new ReceiptReturnDto(r.Id, r.CreatedAt, names.GetValueOrDefault(r.StaffUserId) ?? string.Empty,
            r.Amount, r.Refunded, r.RefundMethod?.ToString(), r.DebtReduced, r.BonusRestored, r.BonusReversed, r.Reason,
            r.Items.Select(i => new ReceiptReturnItemDto(i.Name, i.Quantity, i.Amount)).ToList())).ToList();
    }

    /// <summary>Чектер тарихы (ТЗ §12): күн кезеңі дүкеннің уақыт белдеуімен, соңғылары жоғарыда.</summary>
    public async Task<SalePageDto> ListAsync(DateOnly? from, DateOnly? to, string? search, int page, int pageSize, CancellationToken ct = default)
    {
        var (_, m) = await access.RequireAsync(StaffPermissions.SalesCreate, ct);
        var store = await stores.GetByIdAsync(m.StoreId, ct) ?? throw new NotFoundException(Messages.StoreNotFound(Lang));
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var zone = StoreTime.Zone(store);
        var (items, total) = await sales.SearchSalesAsync(m.StoreId,
            from is { } f ? StoreTime.StartOfDayUtc(f, zone) : null,
            to is { } t ? StoreTime.StartOfDayUtc(t.AddDays(1), zone) : null,
            search, (page - 1) * pageSize, pageSize, ct);
        var names = await sales.CustomerNamesAsync(items.Where(s => s.CustomerId != null).Select(s => s.CustomerId!.Value).Distinct().ToList(), ct);
        return new SalePageDto(items.Select(s => new SaleListItemDto(
            s.Id, s.Number, s.CreatedAt, s.CustomerId is { } c ? names.GetValueOrDefault(c) : null, s.Total,
            s.Payments.Select(p => p.Method.ToString()).ToList(), s.Status.ToString())).ToList(), total, page, pageSize);
    }

    public Task<ReceiptDto> ReceiptAsync(Sale sale, CancellationToken ct) => ToReceiptAsync(sale, ct);
}
