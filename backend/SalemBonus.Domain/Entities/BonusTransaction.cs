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
    /// <summary>Есептелген бонустың жану күні. Бос болса — жанбайды.</summary>
    public DateTime? ExpiresAt { get; set; }
    /// <summary>
    /// Осы есептеуден қалған, әлі жұмсалмаған бонус. Шегеру ең ескі есептеуден басталады,
    /// сондықтан әр есептеу жеке "партия" ретінде саналады. Теріс операцияларда әрқашан 0.
    /// </summary>
    public int Remaining { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
