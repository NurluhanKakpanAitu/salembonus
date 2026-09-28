using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Pos.Catalog;
using SalemBonus.Domain.Core;
using SalemBonus.Domain.Pos.Sales;

namespace SalemBonus.Application.Pos.Sales;

/// <summary>
/// Дүкен хабарламалары (ТЗ «Касса» §19). Баптауда өшірілген түрі жазылмайды. Жазба шақырушының
/// транзакциясымен бірге сақталады — сатылым сәтсіз болса, хабарлама да болмайды.
/// </summary>
public class StoreNotifier(ISalesRepository sales, CatalogAccess access)
{
    private StoreCashierSettings? _settings;

    public async Task<StoreCashierSettings> SettingsAsync(Guid storeId, CancellationToken ct) =>
        _settings ??= await sales.GetSettingsAsync(storeId, ct) ?? new StoreCashierSettings { StoreId = storeId };

    public async Task NotifyAsync(Guid storeId, StoreNotificationType type, string? subject = null, decimal? amount = null,
        long? number = null, string? staffName = null, CancellationToken ct = default)
    {
        var s = await SettingsAsync(storeId, ct);
        var enabled = type switch
        {
            StoreNotificationType.OutOfStock => s.NotifyOutOfStock,
            StoreNotificationType.LowStock => s.NotifyLowStock,
            StoreNotificationType.DebtCreated or StoreNotificationType.DebtRepaid => s.NotifyDebt,
            StoreNotificationType.SaleReturn => s.NotifyReturn,
            StoreNotificationType.CashierChange => s.NotifyCashierChange,
            _ => true,
        };
        if (!enabled) return;
        sales.AddNotification(new StoreNotification
        {
            Id = Guid.NewGuid(), StoreId = storeId, Type = type, Subject = subject, Amount = amount, Number = number,
            StaffName = staffName, CreatedAt = DateTime.UtcNow,
        });
    }

    /// <summary>Сатылымнан кейін: қалдық нөлге түссе — «таусылды», шекке түссе — «аз қалды».</summary>
    public async Task StockChangedAsync(Guid storeId, string productName, decimal before, decimal after, CancellationToken ct)
    {
        var s = await SettingsAsync(storeId, ct);
        if (before > 0 && after <= 0) await NotifyAsync(storeId, StoreNotificationType.OutOfStock, productName, after, ct: ct);
        else if (before > s.LowStockThreshold && after <= s.LowStockThreshold)
            await NotifyAsync(storeId, StoreNotificationType.LowStock, productName, after, ct: ct);
    }

    public async Task<IReadOnlyList<StoreNotificationDto>> ListAsync(CancellationToken ct = default)
    {
        var (_, m) = await access.RequireAsync(StaffPermissions.SalesCreate, ct);
        return (await sales.ListNotificationsAsync(m.StoreId, 50, ct))
            .Select(n => new StoreNotificationDto(n.Id, n.Type.ToString(), n.Subject, n.Amount, n.Number, n.StaffName, n.CreatedAt)).ToList();
    }
}
