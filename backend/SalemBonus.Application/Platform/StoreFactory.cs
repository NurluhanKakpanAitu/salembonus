using System.Security.Cryptography;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Domain.Entities;
using SalemBonus.Domain.Enums;

namespace SalemBonus.Application.Platform;

/// <summary>
/// Жаңа дүкеннің дайын нұсқасы: SalemBonus кілттері (терминал API кілті, клиенттің қосылу коды), әдепкі бонус баспалдағы.
/// Әкімшілік те, иесінің «Настройка → Магазины» бөлімі де осыны қолданады — дүкен екі жерде бірдей жасалады.
/// </summary>
public static class StoreFactory
{
    private const string CodeChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static async Task<string> NewJoinCodeAsync(IPlatformRepository repo, CancellationToken ct)
    {
        string code;
        do code = new string(Enumerable.Range(0, 7).Select(_ => CodeChars[RandomNumberGenerator.GetInt32(CodeChars.Length)]).ToArray());
        while (await repo.JoinCodeExistsAsync(code, ct));
        return code;
    }

    public static Store New(Guid orgId, string name, string? category, string? address, string? phone, string? katoCode, string timeZone, string joinCode) => new()
    {
        Id = Guid.NewGuid(), Name = name, Category = category ?? "Магазин", Description = string.Empty, CashbackPercent = 2,
        Address = address, Phone = phone, KatoCode = katoCode,
        TimeZone = timeZone, ApiKey = "sk_live_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant(), JoinCode = joinCode,
        OrganizationId = orgId, MaxRedeemPercent = 30, BonusLifetimeDays = 365, CreatedAt = DateTime.UtcNow,
        Levels =
        [
            Level(CustomerLevel.New, 0, 2), Level(CustomerLevel.Regular, 50_000, 4), Level(CustomerLevel.Favorite, 150_000, 6), Level(CustomerLevel.Vip, 400_000, 8),
        ],
    };

    private static StoreLevel Level(CustomerLevel level, decimal from, decimal percent) =>
        new() { Id = Guid.NewGuid(), Level = level, FromAmount = from, CashbackPercent = percent };

    /// <summary>Уақыт белдеуі: бос болса Алматы; белгісіз болса — null (шақырушы қате береді).</summary>
    public static string? TimeZone(string? raw)
    {
        var tz = string.IsNullOrWhiteSpace(raw) ? "Asia/Almaty" : raw.Trim();
        try { TimeZoneInfo.FindSystemTimeZoneById(tz); return tz; }
        catch (TimeZoneNotFoundException) { return null; }
        catch (InvalidTimeZoneException) { return null; }
    }

    /// <summary>Қазақстан мен көрші елдердің белдеулері — «Настройка → Магазин» тізімі.</summary>
    public static readonly string[] TimeZones =
    [
        "Asia/Almaty", "Asia/Qostanay", "Asia/Qyzylorda", "Asia/Aqtobe", "Asia/Aqtau", "Asia/Atyrau", "Asia/Oral",
        "Asia/Bishkek", "Asia/Tashkent", "Europe/Moscow",
    ];
}
