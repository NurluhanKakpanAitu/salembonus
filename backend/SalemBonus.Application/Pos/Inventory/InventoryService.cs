using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Application.Pos.Catalog;
using SalemBonus.Domain.Core;
using SalemBonus.Domain.Pos.Catalog;
using SalemBonus.Domain.Pos.Inventory;

namespace SalemBonus.Application.Pos.Inventory;

/// <summary>
/// «Склад»: қоймалар (ТЗ §3, §6), қалдықтар мен KPI (§4–5), қозғалыс журналы (§14).
/// Ақшалай көрсеткіштер мұнда жоқ — олар «Финансы»-да (§4).
/// </summary>
public class InventoryService(
    IInventoryRepository repo,
    IStoreRepository stores,
    ICatalogRepository catalog,
    IStaffRepository staff,
    WarehouseService warehouses,
    CatalogAccess access,
    IUnitOfWork unitOfWork)
{
    private const int MaxPageSize = 100;
    private AppLanguage Lang => access.Lang;

    // ---------- Қоймалар ----------

    public async Task<IReadOnlyList<WarehouseListDto>> ListWarehousesAsync(bool allStores, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.InventoryView, ct);
        if ((await repo.ListWarehousesAsync(m.StoreId, ct)).Count == 0) // әдепкі қойма жоқ болса — жасалады (кассирге де: тек inventory.view)
        {
            await warehouses.EnsureDefaultAsync(orgId, m.StoreId, ct);
            await unitOfWork.SaveChangesAsync(ct);
        }
        var list = allStores ? await repo.ListOrgWarehousesAsync(orgId, ct) : await repo.ListWarehousesAsync(m.StoreId, ct);
        var totals = await repo.WarehouseTotalsAsync(orgId, ct);
        var stores = await repo.StoresAsync(list.Select(w => w.StoreId).Distinct().ToList(), ct);
        return list.Select(w =>
        {
            var t = totals.GetValueOrDefault(w.Id) ?? new WarehouseTotals(0, 0);
            return new WarehouseListDto(w.Id, w.Name, w.StoreId, stores.GetValueOrDefault(w.StoreId)?.Name ?? "", w.Type, w.Comment,
                w.IsDefault, w.IsActive, t.ProductKinds, t.TotalUnits, w.CreatedAt);
        }).ToList();
    }

    public async Task<WarehouseListDto> CreateWarehouseAsync(SaveWarehouseRequest request, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.InventoryWarehouses, ct);
        var name = access.Name(request.Name, 100);
        if (await repo.WarehouseNameExistsAsync(m.StoreId, name, null, ct))
            throw new ValidationException(Messages.CatalogDuplicate(Lang), "name");

        var all = await repo.ListWarehousesForUpdateAsync(m.StoreId, ct);
        var makeDefault = request.MakeDefault || all.All(w => !w.IsDefault || !w.IsActive);
        var warehouse = new Warehouse
        {
            Id = Guid.NewGuid(), OrganizationId = orgId, StoreId = m.StoreId, Name = name,
            Type = CatalogAccess.Optional(request.Type, 50), Comment = CatalogAccess.Optional(request.Comment, 500), IsDefault = makeDefault,
        };
        if (makeDefault) foreach (var w in all) w.IsDefault = false;
        repo.AddWarehouse(warehouse);
        access.Audit(orgId, m.StoreId, "inventory.warehouse.create", "warehouse", warehouse.Id, null, new { warehouse.Name, warehouse.Type, warehouse.IsDefault });
        await unitOfWork.SaveChangesAsync(ct);
        return (await ListWarehousesAsync(false, ct)).First(w => w.Id == warehouse.Id);
    }

    public async Task<WarehouseListDto> UpdateWarehouseAsync(Guid id, SaveWarehouseRequest request, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.InventoryWarehouses, ct);
        var warehouse = await repo.GetWarehouseForUpdateAsync(orgId, id, ct) ?? throw new NotFoundException(Messages.WarehouseNotFound(Lang));
        var name = access.Name(request.Name, 100);
        if (await repo.WarehouseNameExistsAsync(warehouse.StoreId, name, warehouse.Id, ct))
            throw new ValidationException(Messages.CatalogDuplicate(Lang), "name");
        var before = new { warehouse.Name, warehouse.Type, warehouse.Comment, warehouse.IsDefault };

        warehouse.Name = name;
        warehouse.Type = CatalogAccess.Optional(request.Type, 50);
        warehouse.Comment = CatalogAccess.Optional(request.Comment, 500);
        if (request.MakeDefault && !warehouse.IsDefault) await MakeDefaultCoreAsync(warehouse, ct);

        access.Audit(orgId, m.StoreId, "inventory.warehouse.update", "warehouse", warehouse.Id, before,
            new { warehouse.Name, warehouse.Type, warehouse.Comment, warehouse.IsDefault });
        await unitOfWork.SaveChangesAsync(ct);
        return (await ListWarehousesAsync(true, ct)).First(w => w.Id == warehouse.Id);
    }

    /// <summary>ТЗ §3: жаңа негізгі қойма тағайындалса, бұрынғысынан белгі алынады.</summary>
    public async Task MakeDefaultAsync(Guid id, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.InventoryWarehouses, ct);
        var warehouse = await repo.GetWarehouseForUpdateAsync(orgId, id, ct) ?? throw new NotFoundException(Messages.WarehouseNotFound(Lang));
        if (!warehouse.IsActive) throw new ValidationException(Messages.WarehouseArchived(Lang));
        if (warehouse.IsDefault) return;
        await MakeDefaultCoreAsync(warehouse, ct);
        access.Audit(orgId, m.StoreId, "inventory.warehouse.default", "warehouse", warehouse.Id, null, new { warehouse.Name });
        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task MakeDefaultCoreAsync(Warehouse warehouse, CancellationToken ct)
    {
        foreach (var w in await repo.ListWarehousesForUpdateAsync(warehouse.StoreId, ct)) w.IsDefault = w.Id == warehouse.Id;
        warehouse.IsDefault = true;
    }

    /// <summary>ТЗ §6: қалдығы, черновигі не жолдағы перемещениесі бар қойма және негізгі қойма архивтелмейді.</summary>
    public async Task ArchiveWarehouseAsync(Guid id, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.InventoryWarehouses, ct);
        var warehouse = await repo.GetWarehouseForUpdateAsync(orgId, id, ct) ?? throw new NotFoundException(Messages.WarehouseNotFound(Lang));
        if (!warehouse.IsActive) return;
        if (warehouse.IsDefault) throw new ValidationException(Messages.WarehouseDefaultCannotArchive(Lang));
        var totals = (await repo.WarehouseTotalsAsync(orgId, ct)).GetValueOrDefault(warehouse.Id);
        if (totals is { TotalUnits: > 0 }) throw new ValidationException(Messages.WarehouseHasStock(Lang, totals.TotalUnits));
        if (await repo.WarehouseHasOpenDocumentsAsync(warehouse.Id, ct)) throw new ValidationException(Messages.WarehouseHasOpenDocuments(Lang));

        warehouse.IsActive = false;
        access.Audit(orgId, m.StoreId, "inventory.warehouse.archive", "warehouse", warehouse.Id, null, new { warehouse.Name });
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task RestoreWarehouseAsync(Guid id, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.InventoryWarehouses, ct);
        var warehouse = await repo.GetWarehouseForUpdateAsync(orgId, id, ct) ?? throw new NotFoundException(Messages.WarehouseNotFound(Lang));
        warehouse.IsActive = true;
        access.Audit(orgId, m.StoreId, "inventory.warehouse.restore", "warehouse", warehouse.Id, null, new { warehouse.Name });
        await unitOfWork.SaveChangesAsync(ct);
    }

    // ---------- Қалдықтар ----------

    /// <summary>Контекст: бір қойма немесе дүкеннің барлық белсенді қоймасы (ТЗ §5).</summary>
    private async Task<IReadOnlyList<Warehouse>> ContextWarehousesAsync(Guid orgId, Guid storeId, Guid? warehouseId, CancellationToken ct)
    {
        var list = await repo.ListWarehousesAsync(storeId, ct);
        if (list.Count == 0)
        {
            await warehouses.EnsureDefaultAsync(orgId, storeId, ct);
            await unitOfWork.SaveChangesAsync(ct);
            list = await repo.ListWarehousesAsync(storeId, ct);
        }
        if (warehouseId is { } wid)
        {
            var one = list.FirstOrDefault(w => w.Id == wid) ?? throw new NotFoundException(Messages.WarehouseNotFound(Lang));
            return [one];
        }
        return list.Where(w => w.IsActive).ToList();
    }

    public async Task<StockKpiDto> KpiAsync(Guid? warehouseId, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.InventoryView, ct);
        var ws = await ContextWarehousesAsync(orgId, m.StoreId, warehouseId, ct);
        var (kinds, units, with, without, below) = await repo.StockKpiAsync(orgId, ws.Select(w => w.Id).ToList(), 0, ct);
        return new StockKpiDto(kinds, units, with, without, below);
    }

    public async Task<StockPageDto> StockAsync(Guid? warehouseId, string? search, Guid? nodeId, Guid? brandId, string? availability,
        string? status, string? sort, int page, int pageSize, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.InventoryView, ct);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var ws = await ContextWarehousesAsync(orgId, m.StoreId, warehouseId, ct);
        var nodes = await catalog.ListNodesAsync(orgId, ct);
        string? nodePath = null;
        if (nodeId is { } nid)
            nodePath = nodes.FirstOrDefault(n => n.Id == nid)?.Path ?? throw new NotFoundException(Messages.CatalogNodeNotFound(Lang));
        CatalogStatus? statusFilter = string.IsNullOrWhiteSpace(status) ? CatalogStatus.Active
            : status == "all" ? null : access.Status(status, CatalogStatus.Active);

        var (rows, total) = await repo.SearchStockAsync(orgId,
            new StockFilter(ws.Select(w => w.Id).ToList(), search?.Trim(), nodePath, brandId, availability, statusFilter, sort,
                (page - 1) * pageSize, pageSize), ct);

        var names = nodes.ToDictionary(n => n.Id.ToString(), n => n.Name);
        var brands = (await catalog.ListBrandsAsync(orgId, ct)).ToDictionary(b => b.Id, b => b.Name);
        var units = (await catalog.ListUnitsAsync(orgId, ct)).ToDictionary(u => u.Id, u => u.ShortName);
        var wNames = ws.ToDictionary(w => w.Id, w => w.Name);

        var items = rows.Select(r =>
        {
            var p = r.Product;
            var path = p.CatalogNodeId is { } cid && nodes.FirstOrDefault(n => n.Id == cid) is { } node
                ? node.Path.Split('/').Select(id => names.GetValueOrDefault(id) ?? "?").ToList() : [];
            var byWarehouse = ws.Select(w => new StockWarehouseQtyDto(w.Id, w.Name,
                r.Balances.FirstOrDefault(b => b.WarehouseId == w.Id).Quantity)).ToList();
            return new StockRowDto(p.Id, p.Name, p.Article,
                p.Barcodes.FirstOrDefault(b => b.IsPrimary)?.Barcode ?? p.Barcodes.FirstOrDefault()?.Barcode,
                p.Images.FirstOrDefault(i => i.IsPrimary)?.Url ?? p.Images.OrderBy(i => i.SortOrder).FirstOrDefault()?.Url,
                path, p.BrandId is { } b ? brands.GetValueOrDefault(b) : null, units.GetValueOrDefault(p.UnitId), p.Status.ToString(),
                byWarehouse.Sum(x => x.Quantity), p.MinStock, byWarehouse, r.LastMovementAt);
        }).ToList();
        _ = wNames;
        return new StockPageDto(items, total, page, pageSize);
    }

    // ---------- Журнал ----------

    public async Task<MovementPageDto> MovementsAsync(Guid? warehouseId, Guid? productId, string? type, Guid? staffUserId, string? document,
        DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.InventoryView, ct);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var ws = await ContextWarehousesAsync(orgId, m.StoreId, warehouseId, ct);
        var all = warehouseId is null ? await repo.ListWarehousesAsync(m.StoreId, ct) : ws; // архивтегі қойманың тарихы да көрінеді
        StockMovementType? typeFilter = Enum.TryParse<StockMovementType>(type, true, out var tp) ? tp : null;
        var (fromUtc, toUtc) = await InventoryPeriod.ResolveAsync(stores, m.StoreId, from, to, ct);
        var (rows, total) = await repo.SearchMovementsAsync(orgId,
            new MovementFilter(all.Select(w => w.Id).ToList(), productId, typeFilter, staffUserId, document?.Trim(),
                fromUtc, toUtc, (page - 1) * pageSize, pageSize), ct);

        var products = (await repo.ProductsAsync(orgId, rows.Select(r => r.ProductId).Distinct().ToList(), ct)).ToDictionary(p => p.Id);
        var staffNames = await repo.StaffNamesAsync(rows.Where(r => r.StaffUserId is not null).Select(r => r.StaffUserId!.Value).Distinct().ToList(), ct);
        var wNames = all.ToDictionary(w => w.Id, w => w.Name);
        return new MovementPageDto(rows.Select(r => new MovementDto(
            r.Id, r.CreatedAt, r.Type.ToString(), r.DocumentId, r.DocumentNumber, r.ProductId,
            products.GetValueOrDefault(r.ProductId)?.Name ?? "?", products.GetValueOrDefault(r.ProductId)?.Article,
            r.WarehouseId, wNames.GetValueOrDefault(r.WarehouseId) ?? "?", r.Quantity, r.BalanceAfter,
            r.StaffUserId is { } sid ? staffNames.GetValueOrDefault(sid) : null, r.Comment)).ToList(), total, page, pageSize);
    }

    // ---------- Перемещение үшін мақсаттар ----------

    /// <summary>Қызметкер мүше болған дүкендер мен олардың белсенді қоймалары (ТЗ §3: екі дүкенге де рұқсат керек).</summary>
    public async Task<IReadOnlyList<TransferTargetDto>> TransferTargetsAsync(CancellationToken ct = default)
    {
        var (orgId, _) = await access.RequireAsync(StaffPermissions.InventoryView, ct);
        var user = await staff.GetByIdAsync(access.StaffUserId, ct) ?? throw new NotFoundException(Messages.StaffNotFound(Lang));
        var storeIds = user.Memberships.Where(x => x.IsActive && x.Store is { IsActive: true } && x.Store.OrganizationId == orgId)
            .Select(x => x.StoreId).ToList();
        var all = (await repo.ListOrgWarehousesAsync(orgId, ct)).Where(w => w.IsActive && storeIds.Contains(w.StoreId)).ToList();
        var stores = await repo.StoresAsync(storeIds, ct);
        return storeIds.Select(sid => new TransferTargetDto(sid, stores.GetValueOrDefault(sid)?.Name ?? "",
                all.Where(w => w.StoreId == sid).OrderByDescending(w => w.IsDefault).ThenBy(w => w.Name)
                    .Select(w => new WarehouseOptionDto(w.Id, w.Name, w.IsDefault)).ToList()))
            .Where(t => t.Warehouses.Count > 0).OrderBy(t => t.StoreName).ToList();
    }
}
