using SalemBonus.Application.BonusCards;
using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Application.Pos.Catalog;
using SalemBonus.Domain.Platform;

namespace SalemBonus.Application.Platform;

/// <summary>Лендингтегі тариф — әкімшіліктегі белсенді тарифтен тікелей (баға, модульдер, «Популярный» белгісі).</summary>
public record PublicPlanDto(Guid Id, string Name, decimal MonthlyPrice, IReadOnlyList<string> Modules, string? Description, bool IsFeatured);

public record LeadRequest(string? Name, string? Phone, string? BusinessName, string? City, string? BusinessType, string? Interest, string? Comment,
    string? Language, string? Website);

public record LeadDto(Guid Id, string Name, string Phone, string? BusinessName, string? City, string? BusinessType, string Interest, string? Comment,
    string Language, string Status, string? AdminComment, Guid? OrganizationId, DateTime CreatedAt, DateTime? UpdatedAt, string? UpdatedBy);

public record LeadsPageDto(IReadOnlyList<LeadDto> Rows, int Total, IReadOnlyDictionary<string, int> Counts);

public record UpdateLeadRequest(string? Status, string? AdminComment, Guid? OrganizationId);

/// <summary>
/// Лендинг (salemtech.kz): белсенді тарифтер (баға әкімшілікте басқарылады) және өтінім формасы. Өтінім — кіру қажет емес:
/// IP бойынша жиілік шегі, бот-тұзақ өрісі, бір телефоннан 10 минут ішінде қайталанса — жаңа жазба жасалмайды.
/// Әкімшілікте: тізім, күйі (new, contacted, won, lost), пікір, байланған бизнес.
/// </summary>
public class PlatformLeadsService(PlatformAccess access, IPlatformRepository repo, IUnitOfWork unitOfWork)
{
    public static readonly string[] Statuses = ["new", "contacted", "won", "lost"];
    private AppLanguage Lang => access.Lang;

    public async Task<IReadOnlyList<PublicPlanDto>> PublicPlansAsync(CancellationToken ct = default) =>
        (await repo.PlansAsync(ct)).Where(p => p.IsActive)
            .Select(p => new PublicPlanDto(p.Id, p.Name, p.MonthlyPrice, p.Modules, p.Description, p.IsFeatured)).ToList();

    public async Task SubmitAsync(LeadRequest r, CancellationToken ct = default)
    {
        // Бот-тұзақ: адамға көрінбейтін өріс толтырылса — сәтті деп жауап береміз, бірақ сақтамаймыз.
        if (!string.IsNullOrWhiteSpace(r.Website)) return;
        var name = CatalogAccess.Optional(r.Name, 120);
        if (name is null || name.Length < 2) throw new ValidationException(Messages.LeadNameRequired(Lang), "name");
        var raw = r.Phone?.Trim() ?? string.Empty;
        if (!BonusRules.LooksLikePhone(raw)) throw new ValidationException(Messages.PhoneInvalid(Lang), "phone");
        var phone = BonusRules.NormalizePhone(raw);
        if (phone.Length != 12 || !phone.StartsWith("+7")) throw new ValidationException(Messages.PhoneNotKz(Lang), "phone");
        var now = DateTime.UtcNow;
        if (await repo.RecentLeadExistsAsync(phone, now.AddMinutes(-10), ct)) return;
        var interest = r.Interest?.Trim().ToLowerInvariant() is "pos" or "bonus" ? r.Interest.Trim().ToLowerInvariant() : "both";
        repo.AddLead(new PlatformLead
        {
            Id = Guid.NewGuid(), Name = name, Phone = phone, BusinessName = CatalogAccess.Optional(r.BusinessName, 200), City = CatalogAccess.Optional(r.City, 100),
            BusinessType = CatalogAccess.Optional(r.BusinessType, 100), Interest = interest, Comment = CatalogAccess.Optional(r.Comment, 1000),
            Language = r.Language == "kk" ? "kk" : "ru", Ip = access.Ip, CreatedAt = now,
        });
        await unitOfWork.SaveChangesAsync(ct);
    }

    // ---------- Әкімшілік ----------

    private static LeadDto Dto(PlatformLead l, IReadOnlyDictionary<Guid, string> admins) =>
        new(l.Id, l.Name, l.Phone, l.BusinessName, l.City, l.BusinessType, l.Interest, l.Comment, l.Language, l.Status, l.AdminComment, l.OrganizationId,
            l.CreatedAt, l.UpdatedAt, l.UpdatedBy is { } u ? admins.GetValueOrDefault(u) : null);

    public async Task<LeadsPageDto> ListAsync(string? status, string? search, int page, int pageSize, CancellationToken ct = default)
    {
        await access.RequireAsync(ct);
        var size = Math.Clamp(pageSize, 1, 200);
        var (rows, total) = await repo.LeadsAsync(status is "all" ? null : status, search, (Math.Max(1, page) - 1) * size, size, ct);
        var admins = await repo.AdminNamesAsync(rows.Where(l => l.UpdatedBy != null).Select(l => l.UpdatedBy!.Value).Distinct().ToList(), ct);
        var counts = await repo.LeadCountsAsync(ct);
        counts["all"] = counts.Values.Sum();
        return new LeadsPageDto(rows.Select(l => Dto(l, admins)).ToList(), total, counts);
    }

    public async Task<LeadDto> UpdateAsync(Guid id, UpdateLeadRequest r, CancellationToken ct = default)
    {
        var admin = await access.RequireAsync(ct);
        var lead = await repo.LeadAsync(id, ct) ?? throw new NotFoundException(Messages.LeadNotFound(Lang));
        var status = r.Status?.Trim().ToLowerInvariant() ?? lead.Status;
        if (!Statuses.Contains(status)) throw new ValidationException(Messages.LeadStatusInvalid(Lang), "status");
        if (r.OrganizationId is { } orgId && await repo.OrganizationAsync(orgId, ct) is null) throw new ValidationException(Messages.BusinessNotFound(Lang), "organizationId");
        var before = new { lead.Status, lead.AdminComment, lead.OrganizationId };
        lead.Status = status;
        lead.AdminComment = r.AdminComment is null ? lead.AdminComment : CatalogAccess.Optional(r.AdminComment, 1000);
        lead.OrganizationId = r.OrganizationId ?? lead.OrganizationId;
        lead.UpdatedAt = DateTime.UtcNow;
        lead.UpdatedBy = admin.Id;
        access.Audit(admin, "lead.update", lead.OrganizationId, "lead", lead.Id, new { Before = before, After = new { lead.Status, lead.AdminComment, lead.OrganizationId }, lead.Phone });
        await unitOfWork.SaveChangesAsync(ct);
        return Dto(lead, new Dictionary<Guid, string> { [admin.Id] = admin.Name });
    }

    public async Task<int> NewCountAsync(CancellationToken ct) => (await repo.LeadCountsAsync(ct)).GetValueOrDefault("new");
}
