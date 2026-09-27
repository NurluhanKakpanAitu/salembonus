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
    public static string TitleForbidden(AppLanguage l) => Pick(l, "Рұқсат жоқ", "Недостаточно прав");

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

    public static string KatoInvalid(AppLanguage l) => Pick(l,
        "Елді мекен дұрыс таңдалмады",
        "Населённый пункт выбран неверно");

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

    // Қызметкер кіруі (SalemPos). Мәтіндер ТЗ «Авторизация» §10 бойынша.
    public static string StaffPhoneRequired(AppLanguage l) => Pick(l,
        "Телефон нөмірін енгізіңіз", "Введите номер телефона");

    public static string StaffPhoneInvalid(AppLanguage l) => Pick(l,
        "Телефон нөмірін дұрыс енгізіңіз", "Введите корректный номер телефона");

    public static string StaffPasswordRequired(AppLanguage l) => Pick(l,
        "Құпиясөзді енгізіңіз", "Введите пароль");

    public static string StaffNotFound(AppLanguage l) => Pick(l,
        "Бұл нөмірмен пайдаланушы табылмады", "Пользователь с таким номером телефона не найден");

    public static string StaffWrongPassword(AppLanguage l) => Pick(l,
        "Құпиясөз қате", "Неверный пароль");

    public static string StaffLockedOut(AppLanguage l, int minutes) => Pick(l,
        $"Тым көп сәтсіз әрекет. {minutes} минуттан кейін қайталаңыз",
        $"Слишком много неудачных попыток. Повторите через {minutes} мин.");

    public static string StaffDisabled(AppLanguage l) => Pick(l,
        "Аккаунт өшірілген. Иесіне хабарласыңыз", "Учётная запись отключена. Обратитесь к владельцу");

    public static string StaffNoStores(AppLanguage l) => Pick(l,
        "Сізге бірде-бір дүкенге рұқсат берілмеген", "У вас нет доступа ни к одному магазину");

    public static string StaffStoreForbidden(AppLanguage l) => Pick(l,
        "Бұл дүкенге рұқсатыңыз жоқ", "Нет доступа к этому магазину");

    public static string StaffPermissionDenied(AppLanguage l) => Pick(l,
        "Бұл әрекетке рұқсатыңыз жоқ", "Недостаточно прав для этого действия");

    public static string StaffLanguageInvalid(AppLanguage l) => Pick(l,
        "Бұл тіл қолдау көрсетілмейді", "Этот язык не поддерживается");

    // Құпиясөзді қалпына келтіру (ТЗ «Авторизация» §5–7, §10)
    public static string StaffCodeSendFailed(AppLanguage l) => Pick(l,
        "Кодты жіберу мүмкін болмады. Кейінірек қайталаңыз", "Не удалось отправить код. Попробуйте ещё раз позже");

    public static string StaffCodeTooMany(AppLanguage l) => Pick(l,
        "Код тым көп сұралды. Бір сағаттан кейін қайталаңыз", "Код запрашивался слишком часто. Попробуйте через час");

    public static string StaffCodeWrong(AppLanguage l) => Pick(l,
        "Растау коды қате", "Неверный код подтверждения");

    public static string StaffCodeExpired(AppLanguage l) => Pick(l,
        "Кодтың мерзімі өтті", "Срок действия кода истёк");

    public static string StaffCodeRequired(AppLanguage l) => Pick(l,
        "4 таңбалы кодты енгізіңіз", "Введите 4-значный код");

    public static string StaffResetExpired(AppLanguage l) => Pick(l,
        "Қалпына келтіру уақыты өтті. Қайтадан бастаңыз", "Время на восстановление истекло. Начните заново");

    public static string StaffPasswordTooShort(AppLanguage l, int min) => Pick(l,
        $"Құпиясөз кемінде {min} таңба болуы керек", $"Пароль должен содержать не менее {min} символов");

    public static string StaffPasswordWeak(AppLanguage l) => Pick(l,
        "Құпиясөзде әріп те, сан да болуы керек", "Пароль должен содержать буквы и цифры");

    public static string StaffPasswordsMismatch(AppLanguage l) => Pick(l,
        "Құпиясөздер сәйкес келмейді", "Пароли не совпадают");

    public static string StaffOtpText(string language, string code) => language == "kk"
        ? $"SalemPos: құпиясөзді қалпына келтіру коды {code}. Ешкімге айтпаңыз."
        : $"SalemPos: код для восстановления пароля {code}. Никому не сообщайте.";
}
