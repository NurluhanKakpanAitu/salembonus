using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Pos.Sales;

namespace SalemBonus.Application.Pos.Inventory;

/// <summary>
/// Тізімдердегі «кезең» сүзгісі: күндер дүкеннің жергілікті уақытымен беріледі, базада — UTC.
/// «to» қоса алынады (сол күннің соңына дейін), сондықтан шекара — келесі күннің басы.
/// </summary>
public static class InventoryPeriod
{
    public static async Task<(DateTime? FromUtc, DateTime? ToUtc)> ResolveAsync(IStoreRepository stores, Guid storeId, DateOnly? from, DateOnly? to,
        CancellationToken ct)
    {
        if (from is null && to is null) return (null, null);
        var store = await stores.GetByIdAsync(storeId, ct);
        var zone = store is null ? TimeZoneInfo.Utc : StoreTime.Zone(store);
        if (from is { } f && to is { } t && t < f) (from, to) = (t, f);
        return (from is { } a ? StoreTime.StartOfDayUtc(a, zone) : null,
                to is { } b ? StoreTime.StartOfDayUtc(b.AddDays(1), zone) : null);
    }
}
