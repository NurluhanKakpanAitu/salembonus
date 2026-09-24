using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Domain.Enums;

namespace SalemBonus.Application.BonusCards;

public static class CustomerLevels
{
    /// <summary>Клиент қосымшасына жіберілетін кілт — мәтінді қосымша өз тілінде көрсетеді.</summary>
    public static string Key(CustomerLevel level) => level.ToString();

    /// <summary>Сервер мәтін құрастырғанда ғана қолданылады (мысалы кассаға жауап).</summary>
    public static string Name(CustomerLevel level, AppLanguage lang) => lang switch
    {
        AppLanguage.Ru => level switch
        {
            CustomerLevel.Vip => "VIP клиент",
            CustomerLevel.Favorite => "Любимый клиент",
            CustomerLevel.Regular => "Постоянный клиент",
            _ => "Новый клиент",
        },
        _ => level switch
        {
            CustomerLevel.Vip => "VIP клиент",
            CustomerLevel.Favorite => "Сүйікті клиент",
            CustomerLevel.Regular => "Тұрақты клиент",
            _ => "Жаңа клиент",
        },
    };
}
