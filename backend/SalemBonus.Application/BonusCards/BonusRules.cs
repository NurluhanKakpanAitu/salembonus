using SalemBonus.Domain.Entities;
using SalemBonus.Domain.Enums;

namespace SalemBonus.Application.BonusCards;

/// <summary>Бонус есептеу мен деңгей ережелері. Кейін дүкен бойынша бапталатын болады.</summary>
public static class BonusRules
{
    public const int QrCodeLength = 10;

    public static int CalculateAccrual(decimal purchaseAmount, decimal cashbackPercent) =>
        (int)Math.Floor(purchaseAmount * cashbackPercent / 100m);

    public static int MaxRedeemable(decimal purchaseAmount, decimal maxRedeemPercent, int balance) =>
        Math.Min(balance, (int)Math.Floor(purchaseAmount * maxRedeemPercent / 100m));

    /// <summary>Дүкеннің баспалдағы. Бапталмаған дүкенге үнсіз келісім шектері мен бірыңғай пайыз.</summary>
    public static IReadOnlyList<StoreLevel> LadderOf(Store? store) =>
        store is { Levels.Count: > 0 }
            ? store.Levels.OrderBy(l => l.FromAmount).ToList()
            : LevelThresholds.Default(store?.CashbackPercent ?? 0);

    public static CustomerLevel LevelFor(decimal totalSpent, IReadOnlyList<StoreLevel> ladder) =>
        ladder.Where(l => totalSpent >= l.FromAmount)
              .OrderByDescending(l => l.FromAmount)
              .Select(l => l.Level)
              .DefaultIfEmpty(CustomerLevel.New)
              .First();

    /// <summary>Клиенттің ағымдағы мәртебесіндегі бонус пайызы.</summary>
    public static decimal PercentFor(CustomerLevel level, IReadOnlyList<StoreLevel> ladder) =>
        ladder.FirstOrDefault(l => l.Level == level)?.CashbackPercent
        ?? ladder.FirstOrDefault()?.CashbackPercent
        ?? 0;

    public static decimal AmountToNextLevel(decimal totalSpent, IReadOnlyList<StoreLevel> ladder)
    {
        var next = ladder.Where(l => l.FromAmount > totalSpent).OrderBy(l => l.FromAmount).FirstOrDefault();
        return next is null ? 0 : Math.Max(0, next.FromAmount - totalSpent);
    }

    public static string NormalizePhone(string raw)
    {
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (digits.Length == 10) digits = "7" + digits;
        if (digits.Length == 11 && digits[0] == '8') digits = "7" + digits[1..];
        return "+" + digits;
    }

    public static bool LooksLikePhone(string code) => code.Count(char.IsDigit) >= 10;

    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static string GenerateQrCode() =>
        new(Enumerable.Range(0, QrCodeLength).Select(_ => Alphabet[Random.Shared.Next(Alphabet.Length)]).ToArray());
}
