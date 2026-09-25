namespace SalemBonus.Application.Stores.Dtos;

/// <summary>Каталогтағы дүкен. HasCard тұтынушының сол дүкенде картасы бар-жоғын көрсетеді.</summary>
public record StoreListItemDto(
    Guid Id,
    string Name,
    string Category,
    string Description,
    string ThemeColor,
    string Icon,
    /// <summary>Бастапқы (жаңа клиент) пайызы.</summary>
    decimal CashbackPercent,
    /// <summary>Ең жоғары мәртебедегі пайыз. Бірдей болса тізімде бір ғана сан көрсетіледі.</summary>
    decimal CashbackMaxPercent,
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
    string? PhotoUrl,
    string? Address,
    string? Phone,
    decimal CashbackPercent,
    decimal MaxRedeemPercent,
    bool HasCard,
    int Balance,
    string? Level,
    decimal? AmountToNextLevel,
    IReadOnlyList<StoreLevelDto> Levels);

/// <summary>QR сканерлеп дүкен қосу нәтижесі.</summary>
public record JoinStoreRequest(string Code);

public record JoinStoreResultDto(bool AlreadyJoined, StoreDetailDto Store);

/// <summary>Дүкендегі бір мәртебе: қай сомадан басталады және қанша пайыз береді.</summary>
public record StoreLevelDto(string Name, decimal FromAmount, decimal CashbackPercent, bool IsCurrent);
