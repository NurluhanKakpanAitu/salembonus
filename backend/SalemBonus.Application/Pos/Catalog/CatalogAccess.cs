using System.Text.Json;
using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Domain.Core;
using SalemBonus.Domain.Pos.Catalog;

namespace SalemBonus.Application.Pos.Catalog;

/// <summary>
/// Каталог сервистерінің ортақ бөлігі: рұқсат пен бизнесті анықтау, аудит, мәтін тазалау.
/// Каталог бизнес деңгейінде, сондықтан дүкен контексінен оның бизнесі алынады.
/// </summary>
public class CatalogAccess(IStoreContext storeContext, ICurrentStaff current, ICurrentLanguage language, IAuditLog audit)
{
    public AppLanguage Lang => language.Value;

    public async Task<(Guid OrgId, StoreMembership Membership)> RequireAsync(string permission, CancellationToken ct)
    {
        var membership = await storeContext.RequireAsync(permission, ct);
        var orgId = membership.Store?.OrganizationId ?? throw new ForbiddenException(Messages.CatalogNoOrganization(Lang));
        return (orgId, membership);
    }

    /// <summary>Бірнеше рұқсаттың кем дегенде біреуі жеткілікті (мысалы, фото жүктеу: жасау не өзгерту).</summary>
    public async Task<(Guid OrgId, StoreMembership Membership)> RequireAnyAsync(CancellationToken ct, params string[] permissions)
    {
        var membership = await storeContext.GetAsync(ct);
        if (!permissions.Any(membership.Has)) throw new ForbiddenException(Messages.StaffPermissionDenied(Lang));
        var orgId = membership.Store?.OrganizationId ?? throw new ForbiddenException(Messages.CatalogNoOrganization(Lang));
        return (orgId, membership);
    }

    public Guid StaffUserId => current.StaffUserId;

    public void Audit(Guid orgId, Guid storeId, string action, string entity, Guid entityId, object? oldValues, object? newValues) =>
        audit.Record(new AuditEntry
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            StoreId = storeId,
            StaffUserId = current.StaffUserId,
            Action = action,
            Entity = entity,
            EntityId = entityId.ToString(),
            OldValues = oldValues is null ? null : JsonSerializer.Serialize(oldValues),
            NewValues = newValues is null ? null : JsonSerializer.Serialize(newValues),
            Ip = current.Ip,
        });

    /// <summary>Атау: бос емес, шеттері тазаланған, ұзындығы шектеулі.</summary>
    public string Name(string? raw, int max, string field = "name")
    {
        var name = raw?.Trim() ?? string.Empty;
        if (name.Length == 0) throw new ValidationException(Messages.CatalogNameRequired(Lang), field);
        return name.Length > max ? name[..max] : name;
    }

    public static string? Optional(string? raw, int max)
    {
        var value = raw?.Trim();
        if (string.IsNullOrEmpty(value)) return null;
        return value.Length > max ? value[..max] : value;
    }

    public CatalogStatus Status(string? raw, CatalogStatus fallback) =>
        raw is null ? fallback
        : Enum.TryParse<CatalogStatus>(raw, ignoreCase: true, out var s) ? s
        : throw new ValidationException(Messages.CatalogStatusInvalid(Lang), "status");
}
