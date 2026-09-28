using SalemBonus.Domain.Entities;

namespace SalemBonus.Application.Pos.Sales;

/// <summary>
/// Дүкеннің жергілікті уақыты. «Бүгін», «Кеше» дүкен тұрған жердің күнімен есептеледі,
/// ал базада уақыт UTC-де сақталады.
/// </summary>
public static class StoreTime
{
    public static TimeZoneInfo Zone(Store store)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(string.IsNullOrWhiteSpace(store.TimeZone) ? "Asia/Almaty" : store.TimeZone);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.CreateCustomTimeZone("KZ", TimeSpan.FromHours(5), "KZ", "KZ");
        }
    }

    public static DateTime StartOfDayUtc(DateOnly day, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), zone);

    public static DateOnly Today(TimeZoneInfo zone) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone));
}
