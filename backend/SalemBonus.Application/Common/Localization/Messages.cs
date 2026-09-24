using SalemBonus.Application.Common.Interfaces;

namespace SalemBonus.Application.Common.Localization;

/// <summary>
/// Тұтынушыға көрінетін сервер мәтіндері. Тіл сұраныстың Accept-Language тақырыбынан алынады.
/// </summary>
public static class Messages
{
    private static string Pick(AppLanguage lang, string kk, string ru) => lang == AppLanguage.Ru ? ru : kk;

    // Қате тақырыптары
    public static string TitleNotFound(AppLanguage l) => Pick(l, "Табылмады", "Не найдено");
    public static string TitleBadRequest(AppLanguage l) => Pick(l, "Қате сұраныс", "Неверный запрос");
    public static string TitleUnauthorized(AppLanguage l) => Pick(l, "Рұқсат жоқ", "Нет доступа");

    // Аутентификация
    public static string PhoneInvalid(AppLanguage l) => Pick(l,
        "Телефон нөмірі дұрыс емес",
        "Неверный номер телефона");

    public static string PhoneNotKz(AppLanguage l) => Pick(l,
        "Қазақстан нөмірін енгізіңіз: +7 7XX XXX XX XX",
        "Введите номер Казахстана: +7 7XX XXX XX XX");

    public static string CodeRetryAfter(AppLanguage l, int seconds) => Pick(l,
        $"Жаңа кодты {seconds} секундтан кейін сұраңыз",
        $"Запросите новый код через {seconds} секунд");

    public static string CodeExpired(AppLanguage l) => Pick(l,
        "Кодтың мерзімі өтті, жаңа код сұраңыз",
        "Срок действия кода истёк, запросите новый");

    public static string CodeAttemptsExceeded(AppLanguage l) => Pick(l,
        "Әрекет саны асып кетті, жаңа код сұраңыз",
        "Превышено количество попыток, запросите новый код");

    public static string CodeWrong(AppLanguage l, int attemptsLeft) => attemptsLeft > 0
        ? Pick(l, $"Код қате, {attemptsLeft} әрекет қалды", $"Неверный код, осталось попыток: {attemptsLeft}")
        : Pick(l, "Код қате, жаңа код сұраңыз", "Неверный код, запросите новый");

    public static string SessionExpired(AppLanguage l) => Pick(l,
        "Сессияның мерзімі өтті, қайта кіріңіз",
        "Сессия истекла, войдите заново");

    public static string LoginRequired(AppLanguage l) => Pick(l,
        "Кіру қажет",
        "Необходимо войти");

    // Профиль
    public static string NameRequired(AppLanguage l) => Pick(l,
        "Атыңызды енгізіңіз",
        "Введите ваше имя");

    public static string NameTooLong(AppLanguage l) => Pick(l,
        "Аты тым ұзын",
        "Имя слишком длинное");

    public static string EmailInvalid(AppLanguage l) => Pick(l,
        "Email дұрыс емес",
        "Неверный email");

    public static string BirthDateInvalid(AppLanguage l) => Pick(l,
        "Туған күн дұрыс емес",
        "Неверная дата рождения");

    public static string AvatarFormat(AppLanguage l) => Pick(l,
        "Тек JPEG, PNG немесе WebP сурет жүктеуге болады",
        "Можно загрузить только JPEG, PNG или WebP");

    public static string AvatarTooLarge(AppLanguage l) => Pick(l,
        "Сурет тым үлкен, кішірек сурет таңдаңыз",
        "Изображение слишком большое, выберите меньше");

    public static string CustomerNotFound(AppLanguage l) => Pick(l,
        "Тұтынушы табылмады",
        "Клиент не найден");

    // Дүкендер
    public static string StoreNotFound(AppLanguage l) => Pick(l,
        "Дүкен табылмады",
        "Магазин не найден");

    public static string StoreQrNotFound(AppLanguage l) => Pick(l,
        "Дүкен табылмады. QR кодты тексеріп, қайта сканерлеңіз.",
        "Магазин не найден. Проверьте QR-код и отсканируйте ещё раз.");

    public static string QrCodeEmpty(AppLanguage l) => Pick(l,
        "QR коды бос",
        "QR-код пустой");

    // Касса
    public static string StoreApiKeyMissing(AppLanguage l) => Pick(l,
        "X-Store-Api-Key тақырыбы жоқ",
        "Отсутствует заголовок X-Store-Api-Key");

    public static string StoreApiKeyInvalid(AppLanguage l) => Pick(l,
        "API кілті жарамсыз",
        "Недействительный API-ключ");

    public static string CustomerCodeEmpty(AppLanguage l) => Pick(l,
        "Тұтынушы коды бос",
        "Код клиента пустой");

    public static string CustomerByQrNotFound(AppLanguage l) => Pick(l,
        "QR коды бойынша тұтынушы табылмады",
        "Клиент по QR-коду не найден");

    public static string PurchaseAmountPositive(AppLanguage l) => Pick(l,
        "Сатып алу сомасы оң болуы керек",
        "Сумма покупки должна быть положительной");

    public static string RedeemNegative(AppLanguage l) => Pick(l,
        "Шегерілетін бонус теріс бола алмайды",
        "Списываемый бонус не может быть отрицательным");

    public static string RedeemTooMuch(AppLanguage l, int maxRedeem, int balance, decimal limitPercent) => Pick(l,
        $"Ең көп {maxRedeem} Б шегеруге болады (баланс {balance} Б, лимит {limitPercent}%)",
        $"Можно списать максимум {maxRedeem} Б (баланс {balance} Б, лимит {limitPercent}%)");
}
