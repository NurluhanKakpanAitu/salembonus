namespace SalemBonus.Application.Notifications.Dtos;

public record NotificationDto(
    Guid Id,
    string Type,
    string Category,
    string Title,
    string Body,
    string? Detail,
    string? TemplateKey,
    int? Amount,
    decimal? PurchaseAmount,
    string? LevelKey,
    string? StoreName,
    string? StoreIcon,
    string? StoreThemeColor,
    bool IsRead,
    DateTime CreatedAt);

public record NotificationPageDto(IReadOnlyList<NotificationDto> Items, bool HasMore);

/// <summary>Бір дүкеннің хабарламалары туралы қысқаша. StoreId бос болса — жүйелік хабарламалар.</summary>
public record NotificationStoreDto(
    Guid? StoreId,
    string? StoreName,
    string? StoreIcon,
    string? StoreThemeColor,
    int Total,
    int Unread,
    NotificationDto Last);
