namespace SalemBonus.Application.BonusCards.Dtos;

public record BonusTransactionDto(
    Guid Id,
    string StoreName,
    string Type,
    int Amount,
    decimal? PurchaseAmount,
    DateTime CreatedAt);

public record TransactionPageDto(IReadOnlyList<BonusTransactionDto> Items, bool HasMore);
