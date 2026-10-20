using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Application.Pos.Sales;
using SalemBonus.Domain.Pos.Catalog;
using SalemBonus.Domain.Pos.Inventory;

namespace SalemBonus.Application.Pos.Inventory;

public record StockDelta(Guid ProductId, decimal Delta, decimal? UnitCost = null);

/// <summary>
/// Қалдықты өзгертудің жалғыз жолы (ТЗ «Склад» §1, §18): жолдар FOR UPDATE бұғатталады, теріс қалдыққа
/// жол берілмейді (рұқсат болмаса), әр өзгеріске журнал жазбасы «қалдық кейін» мәнімен жазылады.
/// Транзакция ішінде шақырылады — қате болса бәрі кері қайтады.
/// </summary>
public class StockPosting(IInventoryRepository inventory, StoreNotifier notifier, ICurrentLanguage language)
{
    public async Task<Dictionary<Guid, (decimal Before, decimal After)>> ApplyAsync(
        Guid orgId, Guid storeId, Guid warehouseId, IReadOnlyList<StockDelta> deltas, IReadOnlyDictionary<Guid, Product> products,
        StockMovementType type, Guid documentId, string documentNumber, string? comment, Guid staffUserId, bool allowNegative,
        DateTime now, CancellationToken ct)
    {
        var ids = deltas.Select(d => d.ProductId).Distinct().ToList();
        var balances = await inventory.BalancesForUpdateAsync(warehouseId, ids, ct);
        var result = new Dictionary<Guid, (decimal, decimal)>();

        foreach (var d in deltas)
        {
            if (!balances.TryGetValue(d.ProductId, out var balance))
            {
                balance = new StockBalance { WarehouseId = warehouseId, ProductId = d.ProductId };
                inventory.AddBalance(balance);
                balances[d.ProductId] = balance;
            }
            var before = balance.Quantity;
            var after = Math.Round(before + d.Delta, 3);
            var product = products.GetValueOrDefault(d.ProductId);
            if (after < 0 && !allowNegative)
                throw new ValidationException(Messages.SaleStockInsufficient(language.Value, product?.Name ?? "?", before), "lines");

            balance.Quantity = after;
            balance.UpdatedAt = now;
            inventory.AddMovement(new StockMovement
            {
                Id = Guid.NewGuid(), OrganizationId = orgId, WarehouseId = warehouseId, ProductId = d.ProductId,
                Type = type, Quantity = d.Delta, UnitCost = d.UnitCost, BalanceAfter = after, StaffUserId = staffUserId,
                DocumentId = documentId, DocumentNumber = documentNumber, Comment = comment, CreatedAt = now,
            });
            if (d.Delta < 0 && product is not null)
                await notifier.StockChangedAsync(storeId, product.Name, before, after, product.MinStock, ct);
            result[d.ProductId] = (before, after);
        }
        return result;
    }

    public static string FormatNumber(string prefix, long seq) => $"{prefix}-{seq:000000}";
}
