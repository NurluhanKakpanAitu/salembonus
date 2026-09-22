using SalemBonus.Domain.Enums;

namespace SalemBonus.Application.BonusCards;

public static class CustomerLevels
{
    public static string Name(CustomerLevel level) => level switch
    {
        CustomerLevel.Vip => "VIP клиент",
        CustomerLevel.Favorite => "Сүйікті клиент",
        CustomerLevel.Regular => "Тұрақты клиент",
        _ => "Жаңа клиент",
    };
}
