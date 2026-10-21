using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Application.Pos.Catalog;
using SalemBonus.Domain.Core;
using SalemBonus.Domain.Platform;

namespace SalemBonus.Application.Platform;

/// <summary>
/// Абонент төлемдері мен тарифтер (admin.salemtech.kz): төлем тіркеу (кезең әдепкіде — төленген күннен кейін жалғасады), жою (себебімен),
/// «төленген күні» әр өзгерістен кейін жойылмаған төлемдерден қайта есептеледі. Шолу: күйлер, MRR, айлар бойынша түсім, мерзімі өткендер.
/// </summary>
public class PlatformBillingService(PlatformAccess access, IPlatformRepository repo, PlatformBusinessService businesses, IUnitOfWork unitOfWork)
{
    private AppLanguage Lang => access.Lang;
    public static readonly string[] Methods = ["kaspi", "transfer", "card", "cash", "other"];

    public static PaymentDto Payment(PlatformPayment p, string orgName, IReadOnlyDictionary<Guid, string> admins) =>
        new(p.Id, p.OrganizationId, orgName, p.Amount, p.PaidOn, p.PeriodFrom, p.PeriodTo, p.Method, p.Reference, p.Comment,
            admins.GetValueOrDefault(p.CreatedBy) ?? "—", p.CreatedAt, p.VoidedAt is not null, p.VoidedAt, p.VoidedBy is { } v ? admins.GetValueOrDefault(v) : null, p.VoidReason);

    private async Task<IReadOnlyList<PaymentDto>> DtosAsync(IReadOnlyList<PlatformPayment> rows, CancellationToken ct)
    {
        var orgs = (await repo.OrganizationsAsync(ct)).ToDictionary(o => o.Id, o => o.Name);
        var admins = await repo.AdminNamesAsync(rows.Select(p => p.CreatedBy).Concat(rows.Where(p => p.VoidedBy != null).Select(p => p.VoidedBy!.Value)).Distinct().ToList(), ct);
        return rows.Select(p => Payment(p, orgs.GetValueOrDefault(p.OrganizationId) ?? "—", admins)).ToList();
    }

    // ---------- Шолу ----------

    public async Task<PlatformOverviewDto> OverviewAsync(CancellationToken ct = default)
    {
        await access.RequireAsync(ct);
        var today = access.Today;
        var rows = await businesses.RowsAsync(ct);
        var byStatus = rows.GroupBy(r => r.Status).ToDictionary(g => g.Key, g => g.Count());
        // MRR — төлеп жүрген бизнестердің айлық төлемі (сынақтағы мен тоқтатылғандар кірмейді).
        var mrr = rows.Where(r => r.Status is "active" or "dueSoon" or "overdue").Sum(r => r.Fee);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var firstMonth = monthStart.AddMonths(-11);
        var payments = await repo.PaymentsSinceAsync(firstMonth, ct);
        var revenue = Enumerable.Range(0, 12).Select(i => firstMonth.AddMonths(i)).Select(m =>
        {
            var list = payments.Where(p => p.PaidOn.Year == m.Year && p.PaidOn.Month == m.Month).ToList();
            return new MonthAmountDto(m.ToString("yyyy-MM"), list.Sum(p => p.Amount), list.Count);
        }).ToList();
        var thisMonth = payments.Where(p => p.PaidOn >= monthStart).ToList();
        var lastMonth = payments.Where(p => p.PaidOn >= monthStart.AddMonths(-1) && p.PaidOn < monthStart).ToList();
        var (recent, _, _) = await repo.PaymentsAsync(new PlatformPaymentQuery(null, null, null, null, false, 0, 10), ct);
        var monthStartUtc = Pos.Sales.StoreTime.StartOfDayUtc(monthStart, PlatformAccess.Zone);
        return new PlatformOverviewDto(rows.Count, byStatus, mrr, thisMonth.Sum(p => p.Amount), thisMonth.Count, lastMonth.Sum(p => p.Amount),
            rows.Count(r => r.CreatedAt >= monthStartUtc), revenue,
            rows.Where(r => r.Status is "overdue" or "unpaid").OrderByDescending(r => r.DaysOverdue ?? 0).Take(10).ToList(),
            rows.Where(r => r.Status is "dueSoon" || (r.Status == "trial" && r.TrialUntil is { } t && t <= today.AddDays(7)))
                .OrderBy(r => r.PaidUntil ?? r.TrialUntil).Take(10).ToList(),
            await DtosAsync(recent, ct),
            PlatformModules.All.Select(m => new ModuleUsageDto(m, rows.Count(r => r.Status != "suspended" && r.Modules.Contains(m)))).ToList(),
            (await repo.LeadCountsAsync(ct)).GetValueOrDefault("new"));
    }

    // ---------- Төлемдер ----------

    public async Task<PaymentsPageDto> PaymentsAsync(Guid? organizationId, DateOnly? from, DateOnly? to, string? method, bool includeVoided, int page, int pageSize,
        CancellationToken ct = default)
    {
        await access.RequireAsync(ct);
        var size = Math.Clamp(pageSize, 1, 200);
        var (rows, total, sum) = await repo.PaymentsAsync(new PlatformPaymentQuery(organizationId, from, to, method, includeVoided, (Math.Max(1, page) - 1) * size, size), ct);
        return new PaymentsPageDto(await DtosAsync(rows, ct), total, sum);
    }

    /// <summary>Келесі кезең: төленген күннен кейін; ешқашан төлемесе — сынақ біткен соң не бүгіннен.</summary>
    private static DateOnly NextFrom(Organization o, DateOnly today) =>
        o.PaidUntil is { } paid ? paid.AddDays(1) : o.TrialUntil is { } trial && trial >= today ? trial.AddDays(1) : today;

    public async Task<PaymentSuggestionDto> SuggestAsync(Guid organizationId, int months, CancellationToken ct = default)
    {
        await access.RequireAsync(ct);
        var org = await repo.OrganizationAsync(organizationId, ct) ?? throw new NotFoundException(Messages.BusinessNotFound(Lang));
        var plan = org.PlanId is { } pid ? await repo.PlanAsync(pid, ct) : null;
        var m = Math.Clamp(months, 1, 36);
        var from = NextFrom(org, access.Today);
        var fee = PlatformAccess.Fee(org, plan);
        return new PaymentSuggestionDto(from, from.AddMonths(m).AddDays(-1), fee * m, fee);
    }

    public async Task<PaymentDto> CreatePaymentAsync(CreatePaymentRequest r, CancellationToken ct = default)
    {
        var admin = await access.RequireAsync(ct);
        var org = await repo.OrganizationAsync(r.OrganizationId, ct) ?? throw new NotFoundException(Messages.BusinessNotFound(Lang));
        if (r.Amount <= 0 || r.Amount > 100_000_000) throw new ValidationException(Messages.PlatformPaymentAmountInvalid(Lang), "amount");
        var method = (r.Method ?? "transfer").Trim().ToLowerInvariant();
        if (!Methods.Contains(method)) throw new ValidationException(Messages.PlatformPaymentMethodInvalid(Lang), "method");
        var today = access.Today;
        var from = r.PeriodFrom ?? NextFrom(org, today);
        DateOnly to;
        if (r.PeriodTo is { } explicitTo) to = explicitTo;
        else
        {
            var months = r.Months ?? 1;
            if (months is < 1 or > 36) throw new ValidationException(Messages.PlatformPaymentPeriodInvalid(Lang), "months");
            to = from.AddMonths(months).AddDays(-1);
        }
        if (to < from || to > from.AddMonths(36)) throw new ValidationException(Messages.PlatformPaymentPeriodInvalid(Lang), "periodTo");
        var payment = new PlatformPayment
        {
            Id = Guid.NewGuid(), OrganizationId = org.Id, Amount = Math.Round(r.Amount, 2), PaidOn = r.PaidOn ?? today, PeriodFrom = from, PeriodTo = to, Method = method,
            Reference = CatalogAccess.Optional(r.Reference, 120), Comment = CatalogAccess.Optional(r.Comment, 500), CreatedBy = admin.Id, CreatedAt = DateTime.UtcNow,
        };
        repo.AddPayment(payment);
        var before = org.PaidUntil;
        org.PaidUntil = await repo.PaidUntilAsync(org.Id, ct);
        access.Audit(admin, "payment.create", org.Id, "payment", payment.Id,
            new { payment.Amount, payment.PaidOn, payment.PeriodFrom, payment.PeriodTo, payment.Method, payment.Reference, PaidUntilBefore = before, PaidUntil = org.PaidUntil });
        await unitOfWork.SaveChangesAsync(ct);
        return Payment(payment, org.Name, new Dictionary<Guid, string> { [admin.Id] = admin.Name });
    }

    /// <summary>Төлемді жою (қате енгізілсе): жазба қалады, «төленген күні» қайта есептеледі.</summary>
    public async Task<PaymentDto> VoidPaymentAsync(Guid id, ReasonRequest r, CancellationToken ct = default)
    {
        var admin = await access.RequireAsync(ct);
        var reason = CatalogAccess.Optional(r.Reason, 500);
        if (reason is null || reason.Length < 3) throw new ValidationException(Messages.PlatformReasonRequired(Lang), "reason");
        var payment = await repo.PaymentAsync(id, ct) ?? throw new NotFoundException(Messages.PlatformPaymentNotFound(Lang));
        if (payment.VoidedAt is not null) throw new ValidationException(Messages.PlatformPaymentAlreadyVoided(Lang));
        var org = await repo.OrganizationAsync(payment.OrganizationId, ct) ?? throw new NotFoundException(Messages.BusinessNotFound(Lang));
        payment.VoidedAt = DateTime.UtcNow;
        payment.VoidedBy = admin.Id;
        payment.VoidReason = reason;
        var before = org.PaidUntil;
        org.PaidUntil = await repo.PaidUntilAsync(org.Id, ct);
        access.Audit(admin, "payment.void", org.Id, "payment", payment.Id, new { payment.Amount, payment.PeriodFrom, payment.PeriodTo, Reason = reason, PaidUntilBefore = before, PaidUntil = org.PaidUntil });
        await unitOfWork.SaveChangesAsync(ct);
        var admins = await repo.AdminNamesAsync([payment.CreatedBy, admin.Id], ct);
        return Payment(payment, org.Name, admins);
    }

    // ---------- Тарифтер ----------

    public async Task<IReadOnlyList<PlanDto>> PlansAsync(CancellationToken ct = default)
    {
        await access.RequireAsync(ct);
        var orgs = await repo.OrganizationsAsync(ct);
        return (await repo.PlansAsync(ct)).Select(p => Plan(p, orgs.Count(o => o.PlanId == p.Id))).ToList();
    }

    private static PlanDto Plan(PlatformPlan p, int businesses) =>
        new(p.Id, p.Name, p.MonthlyPrice, p.Modules, p.Description, p.IsActive, p.SortOrder, businesses);

    private void Apply(PlatformPlan p, SavePlanRequest r)
    {
        var name = CatalogAccess.Optional(r.Name, 120);
        if (name is null || name.Length < 2) throw new ValidationException(Messages.PlanNameRequired(Lang), "name");
        var price = r.MonthlyPrice ?? p.MonthlyPrice;
        if (price < 0 || price > 10_000_000) throw new ValidationException(Messages.BusinessFeeInvalid(Lang), "monthlyPrice");
        p.Name = name;
        p.MonthlyPrice = price;
        if (r.Modules is not null) p.Modules = access.Modules(r.Modules);
        p.Description = CatalogAccess.Optional(r.Description, 500);
        p.IsActive = r.IsActive ?? p.IsActive;
        p.SortOrder = r.SortOrder ?? p.SortOrder;
        p.UpdatedAt = DateTime.UtcNow;
    }

    public async Task<PlanDto> CreatePlanAsync(SavePlanRequest r, CancellationToken ct = default)
    {
        var admin = await access.RequireAsync(ct);
        var plan = new PlatformPlan { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow, Modules = [.. PlatformModules.All] };
        Apply(plan, r);
        repo.AddPlan(plan);
        access.Audit(admin, "plan.create", null, "plan", plan.Id, new { plan.Name, plan.MonthlyPrice, plan.Modules });
        await unitOfWork.SaveChangesAsync(ct);
        return Plan(plan, 0);
    }

    /// <summary>Тарифті өзгерту бизнестердің модульдерін өзгертпейді (олар бизнес карточкасында бөлек басқарылады); жеке бағасы жоқтардың төлемі жаңа бағамен есептеледі.</summary>
    public async Task<PlanDto> UpdatePlanAsync(Guid id, SavePlanRequest r, CancellationToken ct = default)
    {
        var admin = await access.RequireAsync(ct);
        var plan = await repo.PlanAsync(id, ct) ?? throw new NotFoundException(Messages.PlanNotFound(Lang));
        var before = new { plan.Name, plan.MonthlyPrice, Modules = plan.Modules.ToList(), plan.IsActive };
        Apply(plan, r);
        access.Audit(admin, "plan.update", null, "plan", plan.Id, new { Before = before, After = new { plan.Name, plan.MonthlyPrice, plan.Modules, plan.IsActive } });
        await unitOfWork.SaveChangesAsync(ct);
        var orgs = await repo.OrganizationsAsync(ct);
        return Plan(plan, orgs.Count(o => o.PlanId == plan.Id));
    }
}
