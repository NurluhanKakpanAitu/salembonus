using SalemBonus.Application.BonusCards;
using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Application.Customers;
using SalemBonus.Application.Pos.Catalog;
using SalemBonus.Application.Pos.Inventory;
using SalemBonus.Application.Pos.Registers;
using SalemBonus.Domain.Core;
using SalemBonus.Domain.Entities;
using SalemBonus.Domain.Enums;
using SalemBonus.Domain.Pos.Sales;

namespace SalemBonus.Application.Pos.Sales;

/// <summary>
/// Касса экранының деректері (ТЗ «Касса» §2–5): контекст, каталог (баға мен қалдықпен),
/// клиентті іздеу мен тіркеу. Клиенттер SalemBonus-пен ортақ базада.
/// </summary>
public class CashierService(
    ISalesRepository sales,
    ICatalogRepository catalog,
    IStoreRepository stores,
    ICustomerRepository customers,
    IBonusCardRepository cards,
    IRegisterService registers,
    IRegisterRepository registerRepository,
    WarehouseService warehouses,
    BonusLedger ledger,
    CatalogAccess access,
    IUnitOfWork unitOfWork)
{
    private const int MaxPageSize = 100;
    private AppLanguage Lang => access.Lang;

    public async Task<CashierContextDto> GetContextAsync(string? deviceToken, CancellationToken ct = default)
    {
        var (_, m) = await access.RequireAsync(StaffPermissions.SalesCreate, ct);
        var register = await RequireRegisterAsync(deviceToken, m, ct);
        var store = await stores.GetByIdAsync(m.StoreId, ct) ?? throw new NotFoundException(Messages.StoreNotFound(Lang));
        var settings = await sales.GetSettingsAsync(m.StoreId, ct) ?? new StoreCashierSettings { StoreId = m.StoreId };
        var recipients = (await sales.ListRecipientsAsync(m.StoreId, ct)).Where(r => r.IsActive).ToList();
        var approvers = await sales.ListApproversAsync(m.StoreId, StaffPermissions.DiscountApprove, ct);

        // Реквизиті жоқ аударым — таңдауға болмайтын әдіс, сондықтан көрсетілмейді.
        // Қарыз клиент карточкасы мен өтеумен бірге келесі кезеңде қосылады.
        var methods = settings.EnabledMethods
            .Where(x => x != PaymentMethod.Debt)
            .Where(x => x != PaymentMethod.Transfer || recipients.Count > 0)
            .Select(x => x.ToString()).ToList();

        return new CashierContextDto(
            store.Id, store.Name, store.Address, register.Id, register.Name, methods, settings.MixedEnabled,
            recipients.Select(r => new TransferRecipientDto(r.Id, r.BankName, r.Account, r.HolderName)).ToList(),
            m.Has(StaffPermissions.DiscountApprove) ? 100 : m.MaxDiscountPercent,
            m.Has(StaffPermissions.DiscountApprove),
            m.Has(StaffPermissions.SalesNegativeStock),
            store.MaxRedeemPercent,
            approvers.Select(a => new ApproverDto(a.Id, a.Name)).ToList());
    }

    public async Task<CashierCatalogDto> CatalogAsync(string? search, Guid? nodeId, string? sort, int page, int pageSize, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.SalesCreate, ct);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        string? nodePath = null;
        if (nodeId is { } nid)
            nodePath = (await catalog.GetNodeForUpdateAsync(orgId, nid, ct))?.Path ?? throw new NotFoundException(Messages.CatalogNodeNotFound(Lang));
        var order = Enum.TryParse<CashierSort>(sort, ignoreCase: true, out var s) ? s : CashierSort.Popular;
        var warehouse = await warehouses.EnsureDefaultAsync(orgId, m.StoreId, ct);
        var term = search?.Trim();

        var (items, total) = await sales.SearchCatalogAsync(orgId, m.StoreId, warehouse.Id,
            new CashierCatalogFilter(string.IsNullOrEmpty(term) ? null : term, nodePath, order, (page - 1) * pageSize, pageSize), ct);
        var lookup = await LookupAsync(orgId, ct);
        return new CashierCatalogDto(items.Select(r => ToDto(r, lookup)).ToList(), total, page, pageSize);
    }

    /// <summary>Сканер: бір сәйкестік — касса оны бірден себетке қосады (ТЗ §3.8).</summary>
    public async Task<CashierProductDto> ByBarcodeAsync(string barcode, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.SalesCreate, ct);
        var warehouse = await warehouses.EnsureDefaultAsync(orgId, m.StoreId, ct);
        var row = await sales.FindByBarcodeAsync(orgId, m.StoreId, warehouse.Id, barcode.Trim(), ct)
                  ?? throw new NotFoundException(Messages.ProductNotFound(Lang));
        return ToDto(row, await LookupAsync(orgId, ct));
    }

    private sealed record Lookup(Dictionary<Guid, string> Brands, Dictionary<Guid, string> Units);

    private async Task<Lookup> LookupAsync(Guid orgId, CancellationToken ct) => new(
        (await catalog.ListBrandsAsync(orgId, ct)).ToDictionary(b => b.Id, b => b.Name),
        (await catalog.ListUnitsAsync(orgId, ct)).ToDictionary(u => u.Id, u => u.ShortName));

    private static CashierProductDto ToDto(CashierCatalogRow r, Lookup l)
    {
        var p = r.Product;
        return new CashierProductDto(
            p.Id, p.Name, p.Article,
            p.Barcodes.FirstOrDefault(b => b.IsPrimary)?.Barcode ?? p.Barcodes.FirstOrDefault()?.Barcode,
            p.Images.FirstOrDefault(i => i.IsPrimary)?.Url ?? p.Images.OrderBy(i => i.SortOrder).FirstOrDefault()?.Url,
            p.BrandId is { } b ? l.Brands.GetValueOrDefault(b) : null,
            l.Units.GetValueOrDefault(p.UnitId),
            r.Price, r.Stock, p.CatalogNodeId);
    }

    // ---------- Клиент (ТЗ §4) ----------

    /// <summary>
    /// Нөмір не QR бойынша — бүкіл SalemBonus базасынан (клиент өз нөмірін айтады).
    /// Аты-жөні бойынша — тек осы дүкенде картасы барлар: бөтен бизнестің клиенттер тізімі ашылмауы керек.
    /// </summary>
    public async Task<IReadOnlyList<CashierCustomerDto>> SearchCustomersAsync(string? query, CancellationToken ct = default)
    {
        var (_, m) = await access.RequireAsync(StaffPermissions.SalesCreate, ct);
        var q = query?.Trim() ?? string.Empty;
        if (q.Length < 2) return [];
        var store = await stores.GetByIdAsync(m.StoreId, ct) ?? throw new NotFoundException(Messages.StoreNotFound(Lang));

        List<Customer> found;
        if (BonusRules.LooksLikePhone(q))
            found = await customers.GetByPhoneAsync(BonusRules.NormalizePhone(q), ct) is { } c ? [c] : [];
        else if (q.StartsWith(CustomerService.QrPrefix, StringComparison.OrdinalIgnoreCase)
                 || (q.Length >= 6 && q.All(char.IsLetterOrDigit) && q.Any(char.IsDigit) && q.Any(char.IsLetter) && q.ToUpperInvariant() == q))
        {
            var code = q.StartsWith(CustomerService.QrPrefix, StringComparison.OrdinalIgnoreCase) ? q[CustomerService.QrPrefix.Length..] : q;
            found = await customers.GetByQrCodeAsync(code.ToUpperInvariant(), ct) is { } c ? [c] : [];
        }
        else found = (await sales.SearchStoreCustomersAsync(m.StoreId, q, 10, ct)).ToList();

        var result = new List<CashierCustomerDto>();
        foreach (var c in found) result.Add(ToDto(c, store, await cards.GetAsync(c.Id, store.Id, ct)));
        return result;
    }

    public async Task<CashierCustomerDto> GetCustomerAsync(Guid id, CancellationToken ct = default)
    {
        var (_, m) = await access.RequireAsync(StaffPermissions.SalesCreate, ct);
        var store = await stores.GetByIdAsync(m.StoreId, ct) ?? throw new NotFoundException(Messages.StoreNotFound(Lang));
        var customer = await customers.GetByIdAsync(id, ct) ?? throw new NotFoundException(Messages.CustomerNotFound(Lang));
        return ToDto(customer, store, await cards.GetAsync(customer.Id, store.Id, ct));
    }

    /// <summary>
    /// Кассада тіркеу (ТЗ §4.4–4.10): аты, тегі, туған күні міндетті; мекенжайы — дүкеннің тіркелген
    /// жерінен. Клиент ортақ базада жасалады және бірден осы дүкеннің картасы ашылады.
    /// </summary>
    public async Task<CashierCustomerDto> RegisterCustomerAsync(RegisterCustomerRequest request, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.SalesCreate, ct);
        var store = await stores.GetByIdAsync(m.StoreId, ct) ?? throw new NotFoundException(Messages.StoreNotFound(Lang));

        var rawPhone = request.Phone?.Trim() ?? string.Empty;
        if (!BonusRules.LooksLikePhone(rawPhone)) throw new ValidationException(Messages.PhoneInvalid(Lang), "phone");
        var phone = BonusRules.NormalizePhone(rawPhone);
        if (phone.Length != 12 || !phone.StartsWith("+7")) throw new ValidationException(Messages.PhoneNotKz(Lang), "phone");
        if (await customers.GetByPhoneAsync(phone, ct) is not null) throw new ValidationException(Messages.CustomerPhoneExists(Lang), "phone");

        var first = access.Name(request.FirstName, 60, "firstName");
        var last = access.Name(request.LastName, 60, "lastName");
        var birth = request.BirthDate ?? throw new ValidationException(Messages.BirthDateRequired(Lang), "birthDate");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (birth > today || birth < today.AddYears(-120)) throw new ValidationException(Messages.BirthDateInvalid(Lang), "birthDate");

        var now = DateTime.UtcNow;
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Phone = phone,
            FirstName = first,
            LastName = last,
            BirthDate = birth,
            KatoCode = store.KatoCode,
            QrCode = BonusRules.GenerateQrCode(),
            CreatedAt = now,
        };
        customers.Add(customer);
        var card = await ledger.GetOrCreateCardAsync(customer, store, now, ct);
        access.Audit(orgId, m.StoreId, "customer.register", "customer", customer.Id, null, new { customer.FullName, Phone = phone });
        await unitOfWork.SaveChangesAsync(ct);
        return ToDto(customer, store, card);
    }

    private CashierCustomerDto ToDto(Customer c, Store store, BonusCard? card)
    {
        var level = card?.Level ?? CustomerLevel.New;
        return new CashierCustomerDto(
            c.Id, c.FullName, c.Phone, c.BirthDate,
            CustomerLevels.Name(level, Lang), CustomerLevels.Key(level),
            card?.Balance ?? 0,
            BonusRules.PercentFor(level, BonusRules.LadderOf(store)),
            card is not null);
    }

    public async Task<string?> GetRegisterNameAsync(Guid registerId, CancellationToken ct)
    {
        var (_, m) = await access.RequireAsync(StaffPermissions.SalesCreate, ct);
        return (await registerRepository.ListByStoreAsync(m.StoreId, ct)).FirstOrDefault(r => r.Id == registerId)?.Name;
    }

    public async Task<RegisterDto> RequireRegisterAsync(string? deviceToken, StoreMembership m, CancellationToken ct)
    {
        var register = await registers.RequireCurrentAsync(deviceToken, ct);
        // Касса бір дүкенге тіркелген: басқа дүкеннің атынан сату мүмкін емес.
        if (register.StoreId != m.StoreId) throw new ForbiddenException(Messages.RegisterOtherStore(Lang));
        return register;
    }
}
