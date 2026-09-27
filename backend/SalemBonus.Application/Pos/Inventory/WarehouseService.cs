using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Application.Pos.Catalog;
using SalemBonus.Domain.Core;
using SalemBonus.Domain.Pos.Inventory;

namespace SalemBonus.Application.Pos.Inventory;

/// <summary>
/// Қоймалар. Толық «Склад» модулі ТЗ келгенде; қазір дүкеннің қоймалар тізімі мен әдепкі қойманы
/// автоматты жасау ғана (жаңа дүкенде қойма болмаса, бастапқы қалдық енгізу мүмкін болмас еді).
/// </summary>
public class WarehouseService(IInventoryRepository repo, CatalogAccess access, IUnitOfWork unitOfWork)
{
    public async Task<IReadOnlyList<WarehouseDto>> ListAsync(CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.ProductsView, ct);
        var list = await repo.ListWarehousesAsync(m.StoreId, ct);
        if (list.Count == 0)
        {
            await EnsureDefaultAsync(orgId, m.StoreId, ct);
            await unitOfWork.SaveChangesAsync(ct);
            list = await repo.ListWarehousesAsync(m.StoreId, ct);
        }
        return list.Where(w => w.IsActive).Select(w => new WarehouseDto(w.Id, w.Name, w.IsDefault)).ToList();
    }

    /// <summary>Дүкеннің әдепкі қоймасы; жоқ болса — жасалады (сақтауды шақырушы жасайды).</summary>
    public async Task<Warehouse> EnsureDefaultAsync(Guid orgId, Guid storeId, CancellationToken ct = default)
    {
        var list = await repo.ListWarehousesAsync(storeId, ct);
        var existing = list.FirstOrDefault(w => w.IsDefault && w.IsActive) ?? list.FirstOrDefault(w => w.IsActive);
        if (existing is not null) return existing;

        var warehouse = new Warehouse
        {
            Id = Guid.NewGuid(), OrganizationId = orgId, StoreId = storeId,
            Name = Messages.DefaultWarehouseName(access.Lang), IsDefault = true,
        };
        repo.AddWarehouse(warehouse);
        return warehouse;
    }
}
