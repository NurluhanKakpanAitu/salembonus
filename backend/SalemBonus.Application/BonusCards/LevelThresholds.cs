using SalemBonus.Domain.Entities;
using SalemBonus.Domain.Enums;

namespace SalemBonus.Application.BonusCards;

/// <summary>Дүкен өз баспалдағын бапталмаған кездегі үнсіз келісім шектері (₸).</summary>
public static class LevelThresholds
{
    public const decimal Regular = 50_000;
    public const decimal Favorite = 150_000;
    public const decimal Vip = 500_000;

    /// <summary>Барлық мәртебеде бірдей пайыз беретін қарапайым баспалдақ.</summary>
    public static IReadOnlyList<StoreLevel> Default(decimal cashbackPercent) =>
    [
        new() { Level = CustomerLevel.New, FromAmount = 0, CashbackPercent = cashbackPercent },
        new() { Level = CustomerLevel.Regular, FromAmount = Regular, CashbackPercent = cashbackPercent },
        new() { Level = CustomerLevel.Favorite, FromAmount = Favorite, CashbackPercent = cashbackPercent },
        new() { Level = CustomerLevel.Vip, FromAmount = Vip, CashbackPercent = cashbackPercent },
    ];
}
