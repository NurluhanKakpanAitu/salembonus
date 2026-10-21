using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Application.Pos.Catalog;
using SalemBonus.Domain.Core;
using SalemBonus.Domain.Pos.Clients;

namespace SalemBonus.Application.Pos.Clients;

/// <summary>
/// Сегменттер (ТЗ «Клиенты» §16): шарттар сақталады, құрамы әр ашылғанда қайта есептеледі (динамикалық) + қолмен қосылғандар.
/// Жеке сегментті тек авторы көреді; ортақты бәрі көреді, өзгерту — авторы не әкімші.
/// </summary>
public class ClientSegmentsService(ClientAccess access, ClientService reader, IClientRepository repo, IUnitOfWork unitOfWork)
{
    private AppLanguage Lang => access.Lang;

    public async Task<IReadOnlyList<ClientSegmentDto>> ListAsync(string? storeIds, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.ClientsView, ct, storeIds);
        var segments = await repo.SegmentsAsync(c.OrgId, access.StaffUserId, ct);
        var names = await repo.StaffNamesAsync(segments.Select(s => s.CreatedBy).Distinct().ToList(), ct);
        var list = new List<ClientSegmentDto>();
        foreach (var s in segments) list.Add(await DtoAsync(c, s, names, ct));
        return list;
    }

    private async Task<ClientSegmentDto> DtoAsync(ClientCtx c, ClientSegment s, IReadOnlyDictionary<Guid, string> names, CancellationToken ct)
    {
        var q = await reader.QueryAsync(c, null, s.Id, ct);
        var count = (await repo.ListAsync(q with { Take = 0 }, ct)).Total;
        return new ClientSegmentDto(s.Id, s.Name, s.Description, s.Filter, s.IncludeIds.Count, s.IsShared, s.CreatedBy == access.StaffUserId, count, s.CreatedAt,
            s.UpdatedAt, names.GetValueOrDefault(s.CreatedBy) ?? "");
    }

    private async Task<ClientSegment> EditableAsync(ClientCtx c, Guid id, CancellationToken ct)
    {
        var s = await repo.SegmentAsync(c.OrgId, id, ct);
        if (s is null || (!s.IsShared && s.CreatedBy != access.StaffUserId)) throw new NotFoundException(Messages.ClientSegmentNotFound(Lang));
        if (s.CreatedBy != access.StaffUserId && c.Current.Role is not (StaffRole.Owner or StaffRole.Admin)) throw new ForbiddenException(Messages.StaffPermissionDenied(Lang));
        return s;
    }

    private async Task<List<Guid>> IncludesAsync(ClientCtx c, IReadOnlyList<Guid>? ids, CancellationToken ct)
    {
        var list = new List<Guid>();
        foreach (var id in (ids ?? []).Distinct().Take(10_000))
            if (await repo.GetAsync(c.OrgId, id, ct) is not null) list.Add(id);
        return list;
    }

    private string Name(string? raw)
    {
        var name = CatalogAccess.Optional(raw, 120);
        if (name is null || name.Length < 2) throw new ValidationException(Messages.ClientSegmentNameRequired(Lang), "name");
        return name;
    }

    public async Task<ClientSegmentDto> CreateAsync(SaveClientSegmentRequest r, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.ClientsEdit, ct, ensure: false);
        var now = DateTime.UtcNow;
        var s = new ClientSegment
        {
            Id = Guid.NewGuid(), OrganizationId = c.OrgId, Name = Name(r.Name), Description = CatalogAccess.Optional(r.Description, 500),
            Filter = ClientAccess.Clean(r.Filter), IncludeIds = await IncludesAsync(c, r.IncludeIds, ct), IsShared = r.IsShared ?? false,
            CreatedBy = access.StaffUserId, CreatedAt = now, UpdatedAt = now,
        };
        repo.AddSegment(s);
        access.Catalog.Audit(c.OrgId, c.Store.Id, "client.segment.create", "client_segment", s.Id, null, new { s.Name, s.IsShared });
        await unitOfWork.SaveChangesAsync(ct);
        return await DtoAsync(c, s, await repo.StaffNamesAsync([s.CreatedBy], ct), ct);
    }

    public async Task<ClientSegmentDto> UpdateAsync(Guid id, SaveClientSegmentRequest r, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.ClientsEdit, ct, ensure: false);
        var s = await EditableAsync(c, id, ct);
        var before = new { s.Name, s.IsShared, s.Filter, Included = s.IncludeIds.Count };
        s.Name = Name(r.Name ?? s.Name);
        s.Description = r.Description is null ? s.Description : CatalogAccess.Optional(r.Description, 500);
        if (r.Filter is not null) s.Filter = ClientAccess.Clean(r.Filter);
        if (r.IncludeIds is not null) s.IncludeIds = await IncludesAsync(c, r.IncludeIds, ct);
        s.IsShared = r.IsShared ?? s.IsShared;
        s.UpdatedAt = DateTime.UtcNow;
        access.Catalog.Audit(c.OrgId, c.Store.Id, "client.segment.update", "client_segment", s.Id, before, new { s.Name, s.IsShared, s.Filter, Included = s.IncludeIds.Count });
        await unitOfWork.SaveChangesAsync(ct);
        return await DtoAsync(c, s, await repo.StaffNamesAsync([s.CreatedBy], ct), ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.ClientsEdit, ct, ensure: false);
        var s = await EditableAsync(c, id, ct);
        access.Catalog.Audit(c.OrgId, c.Store.Id, "client.segment.delete", "client_segment", s.Id, new { s.Name }, null);
        repo.RemoveSegment(s);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
