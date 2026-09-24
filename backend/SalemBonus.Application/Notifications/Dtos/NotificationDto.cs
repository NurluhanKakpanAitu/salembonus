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
