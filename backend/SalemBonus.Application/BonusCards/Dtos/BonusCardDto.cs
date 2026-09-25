namespace SalemBonus.Application.BonusCards.Dtos;

public record BonusCardDto(
    Guid StoreId,
    string StoreName,
    string Category,
    string ThemeColor,
    string Icon,
    int Balance,
    string Level,
    decimal CashbackPercent,
    decimal AmountToNextLevel,
    /// <summary>Осы дүкендегі жалпы сатып алу сомасы.</summary>
    decimal TotalSpent,
    /// <summary>Келесі мәртебенің кілті. Ең жоғарыда тұрса — бос.</summary>
    string? NextLevel,
    /// <summary>Келесі мәртебе басталатын сома.</summary>
    decimal? NextLevelAmount,
    /// <summary>Ең жақын жанатын бонус сомасы. Жанатыны жоқ болса — бос.</summary>
    int? ExpiringAmount,
    DateTime? ExpiringAt);
