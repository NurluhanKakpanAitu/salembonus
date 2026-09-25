namespace SalemBonus.Application.Notifications;

/// <summary>
/// Хабарлама мәтінінің кілттері. Клиент осы кілт арқылы мәтінді өз тілінде құрастырады,
/// базадағы дайын Title/Body тек ескі жазбалар мен еркін мәтін үшін қалады.
/// </summary>
public static class NotificationTemplates
{
    public const string BonusAccrued = "bonus_accrued";
    public const string BonusRedeemed = "bonus_redeemed";
    public const string Birthday = "birthday";
    public const string LevelUp = "level_up";
    public const string BonusExpired = "bonus_expired";
    public const string StoreAdded = "store_added";
    public const string ProfileUpdated = "profile_updated";
}
