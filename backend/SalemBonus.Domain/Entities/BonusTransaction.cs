using SalemBonus.Domain.Enums;

namespace SalemBonus.Domain.Entities;

public class BonusTransaction
{
    public Guid Id { get; set; }
    public Guid BonusCardId { get; set; }
    public BonusTransactionType Type { get; set; }
    /// <summary>Оң сан — есептеу, теріс сан — шегеру.</summary>
    public int Amount { get; set; }
    public decimal? PurchaseAmount { get; set; }
    /// <summary>
    /// Бір сатып алудың чек нөмірі. Бір сатып алу екі операция жасай алады (бонус шегеру
    /// және есептеу) — олар бір чекке жатады. Сатып алумен байланысы жоқ операцияларда бос.
    /// </summary>
    public Guid? ReceiptId { get; set; }
    /// <summary>Есептелген бонустың жану күні. Бос болса — жанбайды.</summary>
    public DateTime? ExpiresAt { get; set; }
    /// <summary>
    /// Осы есептеуден қалған, әлі жұмсалмаған бонус. Шегеру ең ескі есептеуден басталады,
    /// сондықтан әр есептеу жеке "партия" ретінде саналады. Теріс операцияларда әрқашан 0.
    /// </summary>
    public int Remaining { get; set; }
    public string? Comment { get; set; }
    /// <summary>Операциядан кейінгі қолжетімді баланс (ТЗ «Бонусы» §8.1).</summary>
    public int? BalanceAfter { get; set; }
    /// <summary>Автор — қызметкер; касса терминалы, фондық тапсырма мен тарату — бос.</summary>
    public Guid? StaffUserId { get; set; }
    /// <summary>Көзі: sale, return, pos, birthday, campaign, manual, expiry, reversal.</summary>
    public string? Source { get; set; }
    /// <summary>Көздің құжаты (тарату id т.б.).</summary>
    public Guid? SourceId { get; set; }
    /// <summary>Байланысты операция: жану — партияға, сторно — түпнұсқаға, қайтару — шегеруге.</summary>
    public Guid? SourceTransactionId { get; set; }
    /// <summary>Қайталанбау кілті (тарату алушысы, қолмен операция сұранысы).</summary>
    public Guid? IdempotencyKey { get; set; }
    /// <summary>Есептеу пайызы (сатып алудан есептеуде).</summary>
    public decimal? Percent { get; set; }
    /// <summary>Сторноланған уақыты (түпнұсқада); операция журналда қалады.</summary>
    public DateTime? ReversedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
