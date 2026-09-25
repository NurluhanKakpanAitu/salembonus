using SalemBonus.Domain.Enums;

namespace SalemBonus.Domain.Entities;

/// <summary>
/// Дүкеннің бір мәртебесі: қанша сомадан басталады және сол мәртебеде қанша пайыз бонус беріледі.
/// Әр дүкен өз баспалдағын құрады, сондықтан пайыз да, шек те дүкен бойынша сақталады.
/// </summary>
public class StoreLevel
{
    public Guid Id { get; set; }
    public Guid StoreId { get; set; }
    public CustomerLevel Level { get; set; }
    /// <summary>Осы мәртебеге өтетін жалпы сатып алу сомасы (₸).</summary>
    public decimal FromAmount { get; set; }
    /// <summary>Осы мәртебедегі бонус пайызы.</summary>
    public decimal CashbackPercent { get; set; }

    public Store? Store { get; set; }
}
