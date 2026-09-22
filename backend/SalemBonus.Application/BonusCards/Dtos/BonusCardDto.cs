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
    decimal AmountToNextLevel);
