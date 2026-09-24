namespace SalemBonus.Application.Stores.Dtos;

/// <summary>Каталогтағы дүкен. HasCard тұтынушының сол дүкенде картасы бар-жоғын көрсетеді.</summary>
public record StoreListItemDto(
    Guid Id,
    string Name,
    string Category,
    string Description,
    string ThemeColor,
    string Icon,
    decimal CashbackPercent,
    bool HasCard,
    int Balance,
    string? Level);

public record StoreDetailDto(
    Guid Id,
    string Name,
    string Category,
    string Description,
    string ThemeColor,
    string Icon,
    decimal CashbackPercent,
    decimal MaxRedeemPercent,
    bool HasCard,
    int Balance,
    string? Level,
    decimal? AmountToNextLevel,
    IReadOnlyList<StoreLevelDto> Levels);

/// <summary>Дүкендегі деңгейлер баспалдағы.</summary>
public record StoreLevelDto(string Name, decimal FromAmount, bool IsCurrent);
