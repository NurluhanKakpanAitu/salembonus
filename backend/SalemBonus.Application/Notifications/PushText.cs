using System.Globalization;
using SalemBonus.Domain.Entities;

namespace SalemBonus.Application.Notifications;

/// <summary>
/// Push мәтіні құрылғының тілінде. Қосымшадағы notificationText.ts-тің серверлік көшірмесі: үлгі кілті бар
/// хабарлама осы мәтіндерден құрастырылады, кілтсізі (дүкеннің еркін мәтіні) — сақталған Title/Body.
/// Мәтіндер frontend/src/lib/i18n.ts-тегі notif.* кілттерімен бірдей болуы керек.
/// </summary>
public static class PushText
{
    private record Template(string Title, string Body);

    private static readonly Dictionary<string, Template> Kk = new()
    {
        [NotificationTemplates.BonusAccrued] = new("Бонус есептелді!", "{store} — Сізге {amount} Б бонус есептелді."),
        [NotificationTemplates.BonusRedeemed] = new("Бонус жұмсалды", "{store} — {amount} Б бонус шегерілді."),
        [NotificationTemplates.Birthday] = new("Туған күн бонусы!", "{store} — Туған күніңізге {amount} Б бонус берілді!"),
        [NotificationTemplates.LevelUp] = new("Жаңа деңгей!", "{store} — Құттықтаймыз, сіз енді {level} деңгейіндесіз."),
        [NotificationTemplates.BonusExpired] = new("Бонус мерзімі өтті", "{store} — {amount} Б бонустың мерзімі аяқталды."),
        [NotificationTemplates.StoreAdded] = new("Жаңа дүкен қосылды", "{store} дүкені сіздің карталарыңызға қосылды."),
        [NotificationTemplates.ProfileUpdated] = new("Профиль жаңартылды", "Жеке деректеріңіз сәтті жаңартылды."),
    };

    private static readonly Dictionary<string, Template> Ru = new()
    {
        [NotificationTemplates.BonusAccrued] = new("Бонус начислен!", "{store} — вам начислено {amount} Б бонусов."),
        [NotificationTemplates.BonusRedeemed] = new("Бонус списан", "{store} — списано {amount} Б бонусов."),
        [NotificationTemplates.Birthday] = new("Бонус на день рождения!", "{store} — на день рождения начислено {amount} Б бонусов!"),
        [NotificationTemplates.LevelUp] = new("Новый уровень!", "{store} — поздравляем, теперь у вас статус «{level}»."),
        [NotificationTemplates.BonusExpired] = new("Срок бонуса истёк", "{store} — срок {amount} Б бонусов истёк."),
        [NotificationTemplates.StoreAdded] = new("Добавлен новый магазин", "Магазин {store} добавлен к вашим картам."),
        [NotificationTemplates.ProfileUpdated] = new("Профиль обновлён", "Ваши данные успешно сохранены."),
    };

    private static readonly Dictionary<string, (string Kk, string Ru)> Levels = new()
    {
        ["New"] = ("Жаңа клиент", "Новый клиент"),
        ["Regular"] = ("Тұрақты клиент", "Постоянный клиент"),
        ["Favorite"] = ("Сүйікті клиент", "Любимый клиент"),
        ["Vip"] = ("VIP клиент", "VIP клиент"),
    };

    private static readonly CultureInfo Numbers = CultureInfo.GetCultureInfo("ru-RU");

    /// <param name="language">kk не ru (құрылғыда таңдалған тіл).</param>
    public static (string Title, string Body) Render(Notification n, string language)
    {
        var ru = language == "ru";
        if (n.TemplateKey is null || !(ru ? Ru : Kk).TryGetValue(n.TemplateKey, out var template))
            return (n.Title, n.Body);

        var level = n.LevelKey is not null && Levels.TryGetValue(n.LevelKey, out var names) ? (ru ? names.Ru : names.Kk) : n.LevelKey ?? "";
        var body = template.Body
            .Replace("{store}", n.Store?.Name ?? "")
            // Сан ішіндегі бос орын үзілмейді: «1 250 Б» екі жолға бөлінбейді.
            .Replace("{amount}", n.Amount is { } a ? a.ToString("N0", Numbers).Replace(' ', ' ') : "")
            .Replace("{level}", level);
        return (template.Title, body);
    }
}
