using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Domain.Core;
using SalemBonus.Domain.Pos.Catalog;

namespace SalemBonus.Application.Pos.Catalog;

public class CatalogNodeService(ICatalogRepository repo, CatalogAccess access, IUnitOfWork unitOfWork) : ICatalogNodeService
{
    private AppLanguage Lang => access.Lang;

    public async Task<IReadOnlyList<CatalogNodeDto>> ListAsync(CancellationToken ct = default)
    {
        var (orgId, _) = await access.RequireAsync(StaffPermissions.ProductsView, ct);
        return await BuildAsync(orgId, ct);
    }

    public async Task<CatalogNodeDto> CreateAsync(SaveCatalogNodeRequest request, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.CatalogStructure, ct);
        var name = access.Name(request.Name, 150);
        var status = access.Status(request.Status, CatalogStatus.Active);

        CatalogNode? parent = null;
        if (request.ParentId is { } parentId)
        {
            parent = await repo.GetNodeForUpdateAsync(orgId, parentId, ct)
                ?? throw new ValidationException(Messages.CatalogNodeNotFound(Lang), "parentId");
            if (parent.Status == CatalogStatus.Archived && status == CatalogStatus.Active)
                throw new ValidationException(Messages.CatalogParentArchived(Lang), "parentId");
            if (parent.Depth + 1 >= CatalogNode.MaxDepth)
                throw new ValidationException(Messages.CatalogTooDeep(Lang, CatalogNode.MaxDepth), "parentId");
        }

        var icon = CatalogAccess.Optional(request.Icon, 50);
        if (parent is null && icon is null)
            throw new ValidationException(Messages.CatalogIconRequired(Lang), "icon");
        if (await repo.NodeNameExistsAsync(orgId, parent?.Id, name, null, ct))
            throw new ValidationException(Messages.CatalogDuplicateName(Lang), "name");

        var id = Guid.NewGuid();
        var node = new CatalogNode
        {
            Id = id,
            OrganizationId = orgId,
            ParentId = parent?.Id,
            Name = name,
            Icon = icon,
            ImageUrl = CatalogAccess.Optional(request.ImageUrl, 500),
            Description = CatalogAccess.Optional(request.Description, 500),
            Status = status,
            SortOrder = request.SortOrder ?? await repo.MaxNodeSortOrderAsync(orgId, parent?.Id, ct) + 1,
            Path = parent is null ? id.ToString() : $"{parent.Path}/{id}",
            Depth = parent is null ? 0 : parent.Depth + 1,
        };
        repo.AddNode(node);
        access.Audit(orgId, m.StoreId, "catalog.node.create", "catalog_node", id, null, Snapshot(node));
        await unitOfWork.SaveChangesAsync(ct);
        return await SingleAsync(orgId, id, ct);
    }

    public async Task<CatalogNodeDto> UpdateAsync(Guid id, SaveCatalogNodeRequest request, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.CatalogStructure, ct);
        var node = await RequireNodeAsync(orgId, id, ct);
        var before = Snapshot(node);
        var oldParentId = node.ParentId;
        var now = DateTime.UtcNow;

        var name = access.Name(request.Name, 150);
        var icon = CatalogAccess.Optional(request.Icon, 50);

        // Ата-ана өзгерсе — тасымалдау: цикл жоқ па, барлық ұрпақтың жолын жаңарту (ТЗ §7.2, §14–15).
        if (request.ParentId != node.ParentId)
            await MoveAsync(orgId, node, request.ParentId, now, ct);

        if (node.ParentId is null && icon is null)
            throw new ValidationException(Messages.CatalogIconRequired(Lang), "icon");
        if (await repo.NodeNameExistsAsync(orgId, node.ParentId, name, node.Id, ct))
            throw new ValidationException(Messages.CatalogDuplicateName(Lang), "name");

        node.Name = name;
        node.Icon = icon;
        node.ImageUrl = CatalogAccess.Optional(request.ImageUrl, 500);
        node.Description = CatalogAccess.Optional(request.Description, 500);
        if (request.SortOrder is { } order) node.SortOrder = order;

        var status = access.Status(request.Status, node.Status);
        if (status != node.Status) await SetStatusAsync(orgId, node, status, now, ct);

        node.UpdatedAt = now;
        access.Audit(orgId, m.StoreId, oldParentId == node.ParentId ? "catalog.node.update" : "catalog.node.move",
            "catalog_node", node.Id, before, Snapshot(node));
        await unitOfWork.SaveChangesAsync(ct);
        return await SingleAsync(orgId, node.Id, ct);
    }

    public async Task ReorderAsync(Guid id, string direction, CancellationToken ct = default)
    {
        var (orgId, _) = await access.RequireAsync(StaffPermissions.CatalogStructure, ct);
        var node = await RequireNodeAsync(orgId, id, ct);
        var siblings = (await repo.ListSiblingsForUpdateAsync(orgId, node.ParentId, ct))
            .OrderBy(n => n.SortOrder).ThenBy(n => n.Name).ToList();

        var index = siblings.FindIndex(n => n.Id == node.Id);
        var target = direction.Equals("up", StringComparison.OrdinalIgnoreCase) ? index - 1 : index + 1;
        if (index < 0 || target < 0 || target >= siblings.Count) return;

        (siblings[index], siblings[target]) = (siblings[target], siblings[index]);
        // Қатарды 1, 2, 3… етіп қайта нөмірлейміз — бірдей реттер араласып кетпесін.
        for (var i = 0; i < siblings.Count; i++) siblings[i].SortOrder = i + 1;
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task ArchiveAsync(Guid id, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.CatalogLifecycle, ct);
        var node = await RequireNodeAsync(orgId, id, ct);
        await SetStatusAsync(orgId, node, CatalogStatus.Archived, DateTime.UtcNow, ct);
        access.Audit(orgId, m.StoreId, "catalog.node.archive", "catalog_node", node.Id, null, null);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task RestoreAsync(Guid id, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.CatalogLifecycle, ct);
        var node = await RequireNodeAsync(orgId, id, ct);
        await SetStatusAsync(orgId, node, CatalogStatus.Active, DateTime.UtcNow, ct);
        access.Audit(orgId, m.StoreId, "catalog.node.restore", "catalog_node", node.Id, null, null);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.CatalogLifecycle, ct);
        var node = await RequireNodeAsync(orgId, id, ct);

        // ТЗ §7.3, §16: ішінде түйін не тауар болса, физикалық өшіру жоқ — тасымалдау не архив.
        var descendants = await repo.ListDescendantsForUpdateAsync(orgId, node.Path, ct);
        var counts = await repo.ProductCountsByNodeAsync(orgId, ct);
        var products = counts.GetValueOrDefault(node.Id) + descendants.Sum(d => counts.GetValueOrDefault(d.Id));
        if (descendants.Count > 0 || products > 0)
            throw new ValidationException(Messages.CatalogDeleteBlocked(Lang, descendants.Count, products));

        access.Audit(orgId, m.StoreId, "catalog.node.delete", "catalog_node", node.Id, Snapshot(node), null);
        repo.RemoveNode(node);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task MoveContentAsync(Guid id, Guid targetId, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.CatalogStructure, ct);
        var node = await RequireNodeAsync(orgId, id, ct);
        var target = await repo.GetNodeForUpdateAsync(orgId, targetId, ct)
            ?? throw new ValidationException(Messages.CatalogNodeNotFound(Lang), "targetId");
        if (target.IsSelfOrDescendantOf(node))
            throw new ValidationException(Messages.CatalogMoveTargetInvalid(Lang), "targetId");
        if (target.Status == CatalogStatus.Archived)
            throw new ValidationException(Messages.CatalogParentArchived(Lang), "targetId");

        var now = DateTime.UtcNow;
        var children = (await repo.ListSiblingsForUpdateAsync(orgId, node.Id, ct)).OrderBy(c => c.SortOrder).ToList();
        // Реттер базадан бір рет алынады: сақталмаған өзгерістерді база әлі көрмейді.
        var nextOrder = await repo.MaxNodeSortOrderAsync(orgId, target.Id, ct) + 1;
        foreach (var child in children)
        {
            await MoveAsync(orgId, child, target.Id, now, ct);
            child.SortOrder = nextOrder++;
        }
        await repo.MoveProductsAsync(node.Id, target.Id, ct);

        access.Audit(orgId, m.StoreId, "catalog.node.move_content", "catalog_node", node.Id,
            new { node.Id }, new { TargetId = target.Id, Children = children.Count });
        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Түйінді жаңа ата-анаға ауыстыру. Түйін қайта жасалмайды, тек parent_id мен жолы өзгереді —
    /// балалары мен тауарлары байланысын сақтап, жаңа тармақта көрінеді (ТЗ §8.2, §15).
    /// </summary>
    private async Task MoveAsync(Guid orgId, CatalogNode node, Guid? newParentId, DateTime now, CancellationToken ct)
    {
        CatalogNode? parent = null;
        if (newParentId is { } pid)
        {
            parent = await repo.GetNodeForUpdateAsync(orgId, pid, ct)
                ?? throw new ValidationException(Messages.CatalogNodeNotFound(Lang), "parentId");
            if (parent.IsSelfOrDescendantOf(node))
                throw new ValidationException(Messages.CatalogCycle(Lang), "parentId");
            if (parent.Status == CatalogStatus.Archived && node.Status == CatalogStatus.Active)
                throw new ValidationException(Messages.CatalogParentArchived(Lang), "parentId");
        }

        var descendants = await repo.ListDescendantsForUpdateAsync(orgId, node.Path, ct);
        var newDepth = parent is null ? 0 : parent.Depth + 1;
        var delta = newDepth - node.Depth;
        var deepest = descendants.Count == 0 ? node.Depth : descendants.Max(d => d.Depth);
        if (deepest + delta >= CatalogNode.MaxDepth)
            throw new ValidationException(Messages.CatalogTooDeep(Lang, CatalogNode.MaxDepth), "parentId");

        var oldPath = node.Path;
        var newPath = parent is null ? node.Id.ToString() : $"{parent.Path}/{node.Id}";
        foreach (var d in descendants)
        {
            d.Path = newPath + d.Path[oldPath.Length..];
            d.Depth += delta;
            d.UpdatedAt = now;
        }

        node.ParentId = parent?.Id;
        node.Path = newPath;
        node.Depth = newDepth;
        node.SortOrder = await repo.MaxNodeSortOrderAsync(orgId, parent?.Id, ct) + 1;
        node.UpdatedAt = now;
    }

    /// <summary>
    /// Архивтеу бүкіл тармаққа әсер етеді: архивтегі санаттың топтары жаңа тауарға ұсынылмауы керек.
    /// Қалпына келтіру — ата-анасы белсенді болса ғана (ТЗ §16).
    /// </summary>
    private async Task SetStatusAsync(Guid orgId, CatalogNode node, CatalogStatus status, DateTime now, CancellationToken ct)
    {
        if (status == CatalogStatus.Active && node.ParentId is { } pid)
        {
            var parent = await repo.GetNodeForUpdateAsync(orgId, pid, ct);
            if (parent is { Status: CatalogStatus.Archived })
                throw new ValidationException(Messages.CatalogParentArchived(Lang), "status");
        }

        node.Status = status;
        node.UpdatedAt = now;
        foreach (var d in await repo.ListDescendantsForUpdateAsync(orgId, node.Path, ct))
        {
            d.Status = status;
            d.UpdatedAt = now;
        }
    }

    private async Task<CatalogNode> RequireNodeAsync(Guid orgId, Guid id, CancellationToken ct) =>
        await repo.GetNodeForUpdateAsync(orgId, id, ct) ?? throw new NotFoundException(Messages.CatalogNodeNotFound(Lang));

    private async Task<CatalogNodeDto> SingleAsync(Guid orgId, Guid id, CancellationToken ct) =>
        (await BuildAsync(orgId, ct)).First(n => n.Id == id);

    /// <summary>
    /// Барлық түйін бір сұраныспен, санақтар жадта: әр түйін өз жолындағы ата-бабаларына
    /// «ұрпақ» пен «тауар» санын қосады. Бір бизнестің каталогына (мыңдаған түйін) бұл жеткілікті жылдам.
    /// </summary>
    private async Task<IReadOnlyList<CatalogNodeDto>> BuildAsync(Guid orgId, CancellationToken ct)
    {
        var nodes = await repo.ListNodesAsync(orgId, ct);
        var direct = await repo.ProductCountsByNodeAsync(orgId, ct);
        var byId = nodes.ToDictionary(n => n.Id);

        var children = new Dictionary<Guid, int>();
        var descendants = new Dictionary<Guid, int>();
        var products = nodes.ToDictionary(n => n.Id, n => direct.GetValueOrDefault(n.Id));

        foreach (var node in nodes)
        {
            if (node.ParentId is { } pid) children[pid] = children.GetValueOrDefault(pid) + 1;
            foreach (var ancestor in Ancestors(node))
            {
                descendants[ancestor] = descendants.GetValueOrDefault(ancestor) + 1;
                products[ancestor] = products.GetValueOrDefault(ancestor) + direct.GetValueOrDefault(node.Id);
            }
        }

        return nodes
            .OrderBy(n => n.Depth).ThenBy(n => n.SortOrder).ThenBy(n => n.Name)
            .Select(n => new CatalogNodeDto(
                n.Id, n.ParentId, n.Name, n.Icon, n.ImageUrl, n.Description, n.Status.ToString(), n.SortOrder, n.Depth,
                n.Path.Split('/').Select(p => Guid.TryParse(p, out var g) && byId.TryGetValue(g, out var x) ? x.Name : null)
                    .OfType<string>().ToList(),
                children.GetValueOrDefault(n.Id),
                descendants.GetValueOrDefault(n.Id),
                products.GetValueOrDefault(n.Id),
                n.UpdatedAt))
            .ToList();

        static IEnumerable<Guid> Ancestors(CatalogNode node) =>
            node.Path.Split('/').SkipLast(1).Select(p => Guid.TryParse(p, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty);
    }

    private static object Snapshot(CatalogNode n) => new
    {
        n.Name, n.ParentId, n.Icon, n.ImageUrl, n.Description, Status = n.Status.ToString(), n.SortOrder,
    };
}
