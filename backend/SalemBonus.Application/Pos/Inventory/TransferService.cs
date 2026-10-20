using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Application.Pos.Catalog;
using SalemBonus.Domain.Core;
using SalemBonus.Domain.Pos.Catalog;
using SalemBonus.Domain.Pos.Inventory;

namespace SalemBonus.Application.Pos.Inventory;

/// <summary>
/// Перемещение (ТЗ «Склад» §10, §15): черновик → жіберу (жіберуші қоймадан шығыс, «В пути») → қабылдау
/// (алушы қоймаға нақты қабылданған сан, айырмашылық жолда). Екі қадам да идемпотентті. Жіберу — жіберуші
/// дүкеннің контекстінде, қабылдау — алушы дүкеннің контекстінде (X-Store-Id), екеуінде де құқық керек.
/// </summary>
public class TransferService(
    IInventoryRepository repo,
    IStoreRepository stores,
    ICatalogRepository catalog,
    IStaffRepository staff,
    StockPosting posting,
    CatalogAccess access,
    IUnitOfWork unitOfWork)
{
    private const int MaxPageSize = 100;
    private const int MaxLines = 500;
    private AppLanguage Lang => access.Lang;

    public async Task<TransferPageDto> ListAsync(string? status, bool incoming, string? search, DateOnly? from, DateOnly? to, int page, int pageSize,
        CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.InventoryView, ct);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        TransferStatus? st = Enum.TryParse<TransferStatus>(status, true, out var s) ? s : null;
        var (fromUtc, toUtc) = await InventoryPeriod.ResolveAsync(stores, m.StoreId, from, to, ct);
        var filter = new TransferFilter(m.StoreId, st, null, null, incoming, search?.Trim(), fromUtc, toUtc, (page - 1) * pageSize, pageSize);
        var (items, total) = await repo.SearchTransfersAsync(orgId, filter, ct);
        var (all, drafts, inTransit, completed) = await repo.TransferStatsAsync(orgId, filter, ct);
        var l = await LookupAsync(orgId, items, ct);
        return new TransferPageDto(items.Select(t => new TransferListItemDto(t.Id, t.Number, t.Status.ToString(),
                l.StoreName(t.SourceStoreId), l.WarehouseName(t.SourceWarehouseId), l.StoreName(t.DestinationStoreId), l.WarehouseName(t.DestinationWarehouseId),
                t.DestinationStoreId, t.Lines.Count, t.Lines.Sum(x => x.QuantitySent), t.ReceivedAt is null ? null : t.Lines.Sum(x => x.QuantityReceived ?? 0),
                l.Staff.GetValueOrDefault(t.CreatedBy) ?? "", t.CreatedAt, t.SentAt, t.ReceivedAt)).ToList(),
            total, page, pageSize, new TransferStatsDto(all, drafts, inTransit, completed));
    }

    public async Task<TransferDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.InventoryView, ct);
        var t = await repo.GetTransferAsync(orgId, id, ct) ?? throw new NotFoundException(Messages.TransferNotFound(Lang));
        if (t.SourceStoreId != m.StoreId && t.DestinationStoreId != m.StoreId) throw new ForbiddenException(Messages.StaffStoreForbidden(Lang));
        return await ToDtoAsync(orgId, m, t, ct);
    }

    public async Task<TransferDto> CreateAsync(SaveTransferRequest r, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.InventoryTransfer, ct);
        var t = new StockTransfer { Id = Guid.NewGuid(), OrganizationId = orgId, SourceStoreId = m.StoreId, CreatedBy = access.StaffUserId };
        await ApplyAsync(orgId, m, t, r, ct);
        var seq = await repo.NextDocumentNumberAsync(orgId, "transfer", ct);
        t.Seq = seq;
        t.Number = StockPosting.FormatNumber("ПМ", seq);
        repo.AddTransfer(t);
        access.Audit(orgId, m.StoreId, "inventory.transfer.create", "stock_transfer", t.Id, null, Snapshot(t));
        await unitOfWork.SaveChangesAsync(ct);
        return await GetAsync(t.Id, ct);
    }

    public async Task<TransferDto> UpdateAsync(Guid id, SaveTransferRequest r, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.InventoryTransfer, ct);
        var t = await repo.GetTransferForUpdateAsync(orgId, id, ct) ?? throw new NotFoundException(Messages.TransferNotFound(Lang));
        if (t.SourceStoreId != m.StoreId) throw new ForbiddenException(Messages.StaffStoreForbidden(Lang));
        if (t.Status != TransferStatus.Draft) throw new ValidationException(Messages.TransferNotDraft(Lang));
        var before = Snapshot(t);
        await ApplyAsync(orgId, m, t, r, ct);
        t.UpdatedAt = DateTime.UtcNow;
        access.Audit(orgId, m.StoreId, "inventory.transfer.update", "stock_transfer", t.Id, before, Snapshot(t));
        await unitOfWork.SaveChangesAsync(ct);
        return await GetAsync(t.Id, ct);
    }

    private async Task ApplyAsync(Guid orgId, StoreMembership m, StockTransfer t, SaveTransferRequest r, CancellationToken ct)
    {
        var source = r.SourceWarehouseId is { } sw
            ? await repo.GetWarehouseAsync(m.StoreId, sw, ct) ?? throw new ValidationException(Messages.WarehouseNotFound(Lang), "sourceWarehouseId")
            : (await repo.ListWarehousesAsync(m.StoreId, ct)).FirstOrDefault(w => w.IsDefault && w.IsActive)
              ?? throw new ValidationException(Messages.WarehouseNotFound(Lang), "sourceWarehouseId");
        var destStoreId = r.DestinationStoreId ?? m.StoreId;
        // ТЗ §3: алушы дүкенге де мүшелік керек; басқа бизнестің қоймасы көрінбейді.
        var destMembership = destStoreId == m.StoreId ? m : await staff.GetMembershipAsync(access.StaffUserId, destStoreId, ct);
        if (destMembership is not { IsActive: true } || destMembership.Store?.OrganizationId != orgId)
            throw new ValidationException(Messages.TransferStoreForbidden(Lang), "destinationStoreId");
        var dest = r.DestinationWarehouseId is { } dw
            ? await repo.GetWarehouseAsync(destStoreId, dw, ct) ?? throw new ValidationException(Messages.WarehouseNotFound(Lang), "destinationWarehouseId")
            : throw new ValidationException(Messages.WarehouseNotFound(Lang), "destinationWarehouseId");
        if (dest.Id == source.Id) throw new ValidationException(Messages.TransferSameWarehouse(Lang), "destinationWarehouseId");

        t.SourceWarehouseId = source.Id;
        t.DestinationStoreId = destStoreId;
        t.DestinationWarehouseId = dest.Id;
        t.Comment = CatalogAccess.Optional(r.Comment, 1000);

        var requested = (r.Lines ?? []).Where(l => l.ProductId != Guid.Empty).GroupBy(l => l.ProductId)
            .Select(g => new SaveTransferLineRequest(g.Key, g.Sum(x => x.Quantity))).ToList();
        if (requested.Count > MaxLines) throw new ValidationException(Messages.DocumentTooManyLines(Lang, MaxLines), "lines");
        var catalogProducts = (await repo.ProductsAsync(orgId, requested.Select(l => l.ProductId).ToList(), ct)).ToDictionary(p => p.Id);
        foreach (var existing in t.Lines.ToList())
            if (requested.All(l => l.ProductId != existing.ProductId)) { t.Lines.Remove(existing); repo.RemoveTransferLine(existing); }
        var order = 0;
        foreach (var l in requested)
        {
            var p = catalogProducts.GetValueOrDefault(l.ProductId) ?? throw new ValidationException(Messages.ProductNotFound(Lang), "lines");
            if (l.Quantity <= 0 || l.Quantity > 1_000_000_000) throw new ValidationException(Messages.DocumentLineQuantityInvalid(Lang, p.Name), "lines");
            var line = t.Lines.FirstOrDefault(x => x.ProductId == l.ProductId);
            if (line is null)
            {
                line = new StockTransferLine { Id = Guid.NewGuid(), TransferId = t.Id, ProductId = l.ProductId };
                t.Lines.Add(line);
            }
            line.Name = p.Name;
            line.Article = p.Article;
            line.QuantitySent = Math.Round(l.Quantity, 3);
            line.SortOrder = ++order;
        }
    }

    /// <summary>Жіберу (§10.3): қалдық тексеріліп, жіберуші қоймадан атомарлы алынады; құжат «В пути».</summary>
    public async Task<TransferDto> DispatchAsync(Guid id, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.InventoryTransfer, ct);
        var t = await repo.GetTransferForUpdateAsync(orgId, id, ct) ?? throw new NotFoundException(Messages.TransferNotFound(Lang));
        if (t.SourceStoreId != m.StoreId) throw new ForbiddenException(Messages.StaffStoreForbidden(Lang));
        if (t.Status == TransferStatus.InTransit) return await GetAsync(t.Id, ct);
        if (t.Status != TransferStatus.Draft) throw new ValidationException(Messages.TransferNotDraft(Lang));
        if (t.Lines.Count == 0) throw new ValidationException(Messages.DocumentLinesRequired(Lang), "lines");
        _ = await repo.GetWarehouseAsync(t.SourceStoreId, t.SourceWarehouseId, ct) ?? throw new ValidationException(Messages.WarehouseArchived(Lang));
        _ = await repo.GetWarehouseAsync(t.DestinationStoreId, t.DestinationWarehouseId, ct) ?? throw new ValidationException(Messages.WarehouseArchived(Lang));
        var catalogProducts = (await repo.ProductsAsync(orgId, t.Lines.Select(l => l.ProductId).ToList(), ct)).ToDictionary(p => p.Id);

        await unitOfWork.InTransactionAsync(async () =>
        {
            var now = DateTime.UtcNow;
            await posting.ApplyAsync(orgId, t.SourceStoreId, t.SourceWarehouseId, t.Lines.Select(l => new StockDelta(l.ProductId, -l.QuantitySent)).ToList(),
                catalogProducts, StockMovementType.TransferOut, t.Id, t.Number, t.Comment, access.StaffUserId, allowNegative: false, now, ct);
            t.Status = TransferStatus.InTransit;
            t.SentAt = now;
            t.SentBy = access.StaffUserId;
            t.UpdatedAt = now;
            access.Audit(orgId, m.StoreId, "inventory.transfer.dispatch", "stock_transfer", t.Id, null, Snapshot(t));
            await unitOfWork.SaveChangesAsync(ct);
            return t;
        }, ct);
        return await GetAsync(t.Id, ct);
    }

    /// <summary>Қабылдау (§10.3): алушы нақты санды растайды, жіберілгеннен артық қабылдауға болмайды; айырмашылыққа себеп.</summary>
    public async Task<TransferDto> ReceiveAsync(Guid id, ReceiveTransferRequest r, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.InventoryTransfer, ct);
        var t = await repo.GetTransferForUpdateAsync(orgId, id, ct) ?? throw new NotFoundException(Messages.TransferNotFound(Lang));
        if (t.DestinationStoreId != m.StoreId) throw new ValidationException(Messages.TransferWrongStore(Lang));
        if (t.Status == TransferStatus.Completed) return await GetAsync(t.Id, ct);
        if (t.Status != TransferStatus.InTransit) throw new ValidationException(Messages.TransferNotInTransit(Lang));
        _ = await repo.GetWarehouseAsync(t.DestinationStoreId, t.DestinationWarehouseId, ct) ?? throw new ValidationException(Messages.WarehouseArchived(Lang));

        var received = (r.Lines ?? []).ToDictionary(l => l.ProductId);
        foreach (var line in t.Lines)
        {
            var got = received.GetValueOrDefault(line.ProductId);
            var qty = got is null ? line.QuantitySent : Math.Round(got.QuantityReceived, 3);
            if (qty < 0 || qty > line.QuantitySent) throw new ValidationException(Messages.TransferReceivedTooMuch(Lang, line.Name, line.QuantitySent), "lines");
            var reason = CatalogAccess.Optional(got?.DiscrepancyReason, 300);
            if (qty < line.QuantitySent && reason is null) throw new ValidationException(Messages.TransferDiscrepancyReason(Lang, line.Name), "lines");
            line.QuantityReceived = qty;
            line.DiscrepancyReason = qty < line.QuantitySent ? reason : null;
        }
        var catalogProducts = (await repo.ProductsAsync(orgId, t.Lines.Select(l => l.ProductId).ToList(), ct)).ToDictionary(p => p.Id);

        await unitOfWork.InTransactionAsync(async () =>
        {
            var now = DateTime.UtcNow;
            var deltas = t.Lines.Where(l => l.QuantityReceived > 0).Select(l => new StockDelta(l.ProductId, l.QuantityReceived!.Value)).ToList();
            if (deltas.Count > 0)
                await posting.ApplyAsync(orgId, t.DestinationStoreId, t.DestinationWarehouseId, deltas, catalogProducts, StockMovementType.TransferIn,
                    t.Id, t.Number, t.Comment, access.StaffUserId, allowNegative: true, now, ct);
            t.Status = TransferStatus.Completed;
            t.ReceivedAt = now;
            t.ReceivedBy = access.StaffUserId;
            t.UpdatedAt = now;
            access.Audit(orgId, m.StoreId, "inventory.transfer.receive", "stock_transfer", t.Id, null, Snapshot(t));
            await unitOfWork.SaveChangesAsync(ct);
            return t;
        }, ct);
        return await GetAsync(t.Id, ct);
    }

    /// <summary>Жою: черновик — жай «Отменён»; «В пути» — тауар жіберуші қоймаға толық қайтады (жол тоқтатылды).</summary>
    public async Task<TransferDto> CancelAsync(Guid id, ReverseRequest r, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.InventoryTransfer, ct);
        var t = await repo.GetTransferForUpdateAsync(orgId, id, ct) ?? throw new NotFoundException(Messages.TransferNotFound(Lang));
        if (t.SourceStoreId != m.StoreId) throw new ForbiddenException(Messages.StaffStoreForbidden(Lang));
        if (t.Status == TransferStatus.Cancelled) return await GetAsync(t.Id, ct);
        if (t.Status is not (TransferStatus.Draft or TransferStatus.InTransit)) throw new ValidationException(Messages.TransferNotDraft(Lang));
        var reason = CatalogAccess.Optional(r.Reason, 500);
        if (t.Status == TransferStatus.InTransit && reason is null) throw new ValidationException(Messages.ReversalReasonRequired(Lang), "reason");
        var catalogProducts = (await repo.ProductsAsync(orgId, t.Lines.Select(l => l.ProductId).ToList(), ct)).ToDictionary(p => p.Id);

        await unitOfWork.InTransactionAsync(async () =>
        {
            var now = DateTime.UtcNow;
            if (t.Status == TransferStatus.InTransit)
                await posting.ApplyAsync(orgId, t.SourceStoreId, t.SourceWarehouseId, t.Lines.Select(l => new StockDelta(l.ProductId, l.QuantitySent)).ToList(),
                    catalogProducts, StockMovementType.Reversal, t.Id, t.Number, $"Отмена {t.Number}: {reason}", access.StaffUserId, allowNegative: true, now, ct);
            t.Status = TransferStatus.Cancelled;
            t.UpdatedAt = now;
            access.Audit(orgId, m.StoreId, "inventory.transfer.cancel", "stock_transfer", t.Id, null, new { t.Number, Reason = reason });
            await unitOfWork.SaveChangesAsync(ct);
            return t;
        }, ct);
        return await GetAsync(t.Id, ct);
    }

    private sealed record Lookup(Dictionary<Guid, Warehouse> Warehouses, Dictionary<Guid, Domain.Entities.Store> Stores, Dictionary<Guid, string> Staff)
    {
        public string StoreName(Guid id) => Stores.GetValueOrDefault(id)?.Name ?? "?";
        public string WarehouseName(Guid id) => Warehouses.GetValueOrDefault(id)?.Name ?? "?";
    }

    private async Task<Lookup> LookupAsync(Guid orgId, IEnumerable<StockTransfer> transfers, CancellationToken ct)
    {
        var list = transfers.ToList();
        var ws = (await repo.ListOrgWarehousesAsync(orgId, ct)).ToDictionary(w => w.Id);
        var stores = await repo.StoresAsync(list.SelectMany(t => new[] { t.SourceStoreId, t.DestinationStoreId }).Distinct().ToList(), ct);
        var staffIds = list.SelectMany(t => new[] { t.CreatedBy, t.SentBy ?? Guid.Empty, t.ReceivedBy ?? Guid.Empty }).Where(x => x != Guid.Empty).Distinct().ToList();
        return new Lookup(ws, stores, await repo.StaffNamesAsync(staffIds, ct));
    }

    private async Task<TransferDto> ToDtoAsync(Guid orgId, StoreMembership m, StockTransfer t, CancellationToken ct)
    {
        var l = await LookupAsync(orgId, [t], ct);
        var ids = t.Lines.Select(x => x.ProductId).ToList();
        var catalogProducts = (await repo.ProductsAsync(orgId, ids, ct)).ToDictionary(p => p.Id);
        var units = (await catalog.ListUnitsAsync(orgId, ct)).ToDictionary(u => u.Id, u => u.ShortName);
        var sourceStock = await repo.BalancesAsync(t.SourceWarehouseId, ids, ct);
        var canReceive = t.Status == TransferStatus.InTransit && t.DestinationStoreId == m.StoreId && m.Has(StaffPermissions.InventoryTransfer);
        return new TransferDto(t.Id, t.Number, t.Status.ToString(),
            t.SourceStoreId, l.StoreName(t.SourceStoreId), t.SourceWarehouseId, l.WarehouseName(t.SourceWarehouseId),
            t.DestinationStoreId, l.StoreName(t.DestinationStoreId), t.DestinationWarehouseId, l.WarehouseName(t.DestinationWarehouseId),
            t.Comment, t.Lines.Count, t.Lines.Sum(x => x.QuantitySent), t.ReceivedAt is null ? null : t.Lines.Sum(x => x.QuantityReceived ?? 0),
            l.Staff.GetValueOrDefault(t.CreatedBy) ?? "", t.CreatedAt, t.SentAt, t.SentBy is { } sb ? l.Staff.GetValueOrDefault(sb) : null,
            t.ReceivedAt, t.ReceivedBy is { } rb ? l.Staff.GetValueOrDefault(rb) : null, canReceive,
            t.Lines.OrderBy(x => x.SortOrder).Select(x =>
            {
                var p = catalogProducts.GetValueOrDefault(x.ProductId);
                return new TransferLineDto(x.Id, x.ProductId, x.Name, x.Article,
                    p?.Barcodes.FirstOrDefault(b => b.IsPrimary)?.Barcode ?? p?.Barcodes.FirstOrDefault()?.Barcode,
                    p?.Images.FirstOrDefault(i => i.IsPrimary)?.Url ?? p?.Images.OrderBy(i => i.SortOrder).FirstOrDefault()?.Url,
                    p is null ? null : units.GetValueOrDefault(p.UnitId), x.QuantitySent, x.QuantityReceived, x.DiscrepancyReason,
                    sourceStock.GetValueOrDefault(x.ProductId));
            }).ToList());
    }

    private static object Snapshot(StockTransfer t) => new
    {
        t.Number, Status = t.Status.ToString(), t.SourceWarehouseId, t.DestinationStoreId, t.DestinationWarehouseId, t.Comment,
        Lines = t.Lines.OrderBy(x => x.SortOrder).Select(x => new { x.ProductId, x.Name, x.QuantitySent, x.QuantityReceived, x.DiscrepancyReason }),
    };
}
