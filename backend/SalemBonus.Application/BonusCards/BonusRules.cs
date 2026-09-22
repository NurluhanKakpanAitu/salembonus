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

    public static CustomerLevel LevelFor(decimal totalSpent) => totalSpent switch
    {
        >= LevelThresholds.Vip => CustomerLevel.Vip,
        >= LevelThresholds.Favorite => CustomerLevel.Favorite,
        >= LevelThresholds.Regular => CustomerLevel.Regular,
        _ => CustomerLevel.New,
    };

    public static decimal AmountToNextLevel(BonusCard card)
    {
        var threshold = card.Level switch
        {
            CustomerLevel.New => LevelThresholds.Regular,
            CustomerLevel.Regular => LevelThresholds.Favorite,
            _ => LevelThresholds.Vip,
        };
        return Math.Max(0, threshold - card.TotalSpent);
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
