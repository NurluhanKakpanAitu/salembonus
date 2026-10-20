namespace SalemBonus.Domain.Enums;

public enum BonusTransactionType
{
    Accrual = 0,
    Redemption = 1,
    Birthday = 2,
    Promo = 3,
    Expiration = 4,
    /// <summary>Тауар қайтарылды — сатылымда шегерілген бонус клиентке қайтарылды (+).</summary>
    ReturnRestore = 5,
    /// <summary>Тауар қайтарылды — сатылымда есептелген бонус алынды (−).</summary>
    ReturnReversal = 6,
    /// <summary>Қолмен түзету (ТЗ «Бонусы» §8.3): оң — қолмен есептеу (партия), теріс — қолмен шегеру.</summary>
    Adjustment = 7,
    /// <summary>Сторно: түпнұсқаға кері операция, түпнұсқа журналда қалады.</summary>
    Reversal = 8,
}
