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
}
