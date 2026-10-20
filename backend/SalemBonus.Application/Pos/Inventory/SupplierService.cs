using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Application.Pos.Catalog;
using SalemBonus.Domain.Core;
using SalemBonus.Domain.Pos.Catalog;
using SalemBonus.Domain.Pos.Inventory;

namespace SalemBonus.Application.Pos.Inventory;

/// <summary>Жеткізушілер (ТЗ «Склад» §12): анықтамалық, карточка (приёмкалар, төлемдер, қарыз), архив.</summary>
public class SupplierService(IInventoryRepository repo, StockDocumentService documents, CatalogAccess access, IUnitOfWork unitOfWork)
{
    private AppLanguage Lang => access.Lang;

    public async Task<IReadOnlyList<SupplierDto>> ListAsync(CancellationToken ct = default)
    {
        var (orgId, _) = await access.RequireAnyAsync(ct, StaffPermissions.InventorySuppliers, StaffPermissions.InventoryReceive);
        var totals = await repo.SupplierTotalsAsync(orgId, ct);
        return (await repo.ListSuppliersAsync(orgId, ct)).Select(s => ToDto(s, totals.GetValueOrDefault(s.Id))).ToList();
    }

    public async Task<SupplierCardDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var (orgId, _) = await access.RequireAnyAsync(ct, StaffPermissions.InventorySuppliers, StaffPermissions.InventoryReceive);
        var s = await repo.GetSupplierAsync(orgId, id, ct) ?? throw new NotFoundException(Messages.SupplierNotFound(Lang));
        var totals = (await repo.SupplierTotalsAsync(orgId, ct)).GetValueOrDefault(s.Id);
        var docs = await repo.ListSupplierDocumentsAsync(orgId, s.Id, ct);
        var ws = (await repo.ListOrgWarehousesAsync(orgId, ct)).ToDictionary(w => w.Id);
        var payments = await repo.ListPaymentsAsync(orgId, s.Id, null, ct);
        var staffIds = docs.Select(d => d.CreatedBy).Concat(payments.Select(p => p.CreatedBy)).Distinct().ToList();
        var staff = await repo.StaffNamesAsync(staffIds, ct);
        var numbers = docs.ToDictionary(d => d.Id, d => d.Number);
        var sup = new Dictionary<Guid, Supplier> { [s.Id] = s };
        return new SupplierCardDto(ToDto(s, totals),
            docs.Select(d => documents.ToListItemFor(d, ws, sup, staff)).ToList(),
            payments.Select(p => new SupplierPaymentDto(p.Id, p.SupplierId, s.Name, p.DocumentId,
                p.DocumentId is { } did ? numbers.GetValueOrDefault(did) : null, p.Amount, p.Method.ToString(), p.Reference, p.Date, p.Comment,
                p.Status.ToString(), staff.GetValueOrDefault(p.CreatedBy) ?? "", p.CreatedAt, p.ReversalReason)).ToList());
    }

    public async Task<SupplierDto> CreateAsync(SaveSupplierRequest r, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAnyAsync(ct, StaffPermissions.InventorySuppliers, StaffPermissions.InventoryReceive);
        var s = new Supplier { Id = Guid.NewGuid(), OrganizationId = orgId };
        await ApplyAsync(orgId, s, r, ct);
        repo.AddSupplier(s);
        access.Audit(orgId, m.StoreId, "inventory.supplier.create", "supplier", s.Id, null, Snapshot(s));
        await unitOfWork.SaveChangesAsync(ct);
        return ToDto(s, null);
    }

    public async Task<SupplierDto> UpdateAsync(Guid id, SaveSupplierRequest r, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.InventorySuppliers, ct);
        var s = await repo.GetSupplierForUpdateAsync(orgId, id, ct) ?? throw new NotFoundException(Messages.SupplierNotFound(Lang));
        var before = Snapshot(s);
        await ApplyAsync(orgId, s, r, ct);
        s.UpdatedAt = DateTime.UtcNow;
        access.Audit(orgId, m.StoreId, "inventory.supplier.update", "supplier", s.Id, before, Snapshot(s));
        await unitOfWork.SaveChangesAsync(ct);
        return ToDto(s, (await repo.SupplierTotalsAsync(orgId, ct)).GetValueOrDefault(s.Id));
    }

    public async Task SetStatusAsync(Guid id, bool archive, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.InventorySuppliers, ct);
        var s = await repo.GetSupplierForUpdateAsync(orgId, id, ct) ?? throw new NotFoundException(Messages.SupplierNotFound(Lang));
        // ТЗ §12.2: аяқталмаған құжаты (черновик) бар жеткізуші архивтелмейді.
        if (archive && await repo.SupplierHasOpenDocumentsAsync(s.Id, ct)) throw new ValidationException(Messages.SupplierHasOpenDocuments(Lang));
        s.Status = archive ? CatalogStatus.Archived : CatalogStatus.Active;
        s.UpdatedAt = DateTime.UtcNow;
        access.Audit(orgId, m.StoreId, archive ? "inventory.supplier.archive" : "inventory.supplier.restore", "supplier", s.Id, null, new { s.Name });
        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task ApplyAsync(Guid orgId, Supplier s, SaveSupplierRequest r, CancellationToken ct)
    {
        var name = access.Name(r.Name, 150);
        if (await repo.SupplierNameExistsAsync(orgId, name, s.Id, ct)) throw new ValidationException(Messages.CatalogDuplicate(Lang), "name");
        s.Name = name;
        s.Type = r.Type is null ? s.Type : Enum.TryParse<SupplierType>(r.Type, true, out var t) ? t
            : throw new ValidationException(Messages.SupplierTypeInvalid(Lang), "type");
        s.TaxId = CatalogAccess.Optional(r.TaxId, 20);
        s.Phone = CatalogAccess.Optional(r.Phone, 32);
        s.Email = CatalogAccess.Optional(r.Email, 120);
        s.Address = CatalogAccess.Optional(r.Address, 300);
        s.Comment = CatalogAccess.Optional(r.Comment, 1000);
    }

    private static SupplierDto ToDto(Supplier s, SupplierTotals? t) => new(
        s.Id, s.Name, s.Type.ToString(), s.TaxId, s.Phone, s.Email, s.Address, s.Comment, s.Status.ToString(),
        t?.ReceiptCount ?? 0, t?.TotalReceipts ?? 0, t?.TotalPaid ?? 0, (t?.TotalReceipts ?? 0) - (t?.TotalPaid ?? 0), s.CreatedAt);

    private static object Snapshot(Supplier s) => new { s.Name, Type = s.Type.ToString(), s.TaxId, s.Phone, s.Email, s.Address, s.Comment };
}
