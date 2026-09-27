namespace SalemBonus.Application.BonusCards.Dtos;

public record BonusTransactionDto(
    Guid Id,
    string StoreName,
    string Type,
    int Amount,
    decimal? PurchaseAmount,
    DateTime CreatedAt,
    /// <summary>Сатып алу чегінің нөмірі. Бос болса, бұл операцияның чегі жоқ.</summary>
    Guid? ReceiptId);

public record TransactionPageDto(IReadOnlyList<BonusTransactionDto> Items, bool HasMore);

/// <summary>Бір сатып алудың чегі: бонус шегеру мен есептеу бірге көрсетіледі.</summary>
public record ReceiptDto(
    Guid ReceiptId,
    /// <summary>Адам оқитын қысқа нөмір, чек нөмірінің басы.</summary>
    string Number,
    Guid StoreId,
    string StoreName,
    string ThemeColor,
    DateTime CreatedAt,
    /// <summary>Сатып алудың толық сомасы.</summary>
    decimal PurchaseAmount,
    /// <summary>Осы сатып алуда жұмсалған бонус.</summary>
    int Redeemed,
    /// <summary>Осы сатып алуда есептелген бонус.</summary>
    int Accrued,
    /// <summary>Ақшамен төленген сома: сатып алу сомасынан жұмсалған бонусты шегергенде.</summary>
    decimal Paid,
    /// <summary>Есептеу қандай пайызбен жасалғаны.</summary>
    decimal CashbackPercent,
    /// <summary>Осы сатып алудан кейінгі баланс.</summary>
    int BalanceAfter,
    /// <summary>Есептелген бонустың жану күні. Жанбайтын болса — бос.</summary>
    DateTime? ExpiresAt,
    string? Comment);
