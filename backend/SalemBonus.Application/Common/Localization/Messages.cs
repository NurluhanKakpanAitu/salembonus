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

    // PIN, касса және кассир ауысуы (ТЗ «Касса» §15, §17, §18)
    public static string StaffPinRequired(AppLanguage l) => Pick(l,
        "PIN-кодты енгізіңіз", "Введите PIN-код");

    public static string StaffPinFormat(AppLanguage l) => Pick(l,
        "PIN-код дәл 4 цифрдан тұруы керек", "PIN-код должен состоять ровно из 4 цифр");

    public static string StaffPinSimple(AppLanguage l) => Pick(l,
        "PIN-код тым оңай. Бірдей не қатарынан келетін цифрларды қолданбаңыз",
        "Слишком простой PIN-код. Не используйте одинаковые или идущие подряд цифры");

    public static string StaffPinMismatch(AppLanguage l) => Pick(l,
        "PIN-кодтар сәйкес келмейді", "PIN-коды не совпадают");

    public static string StaffPinWrong(AppLanguage l, int attemptsLeft) => attemptsLeft > 0
        ? Pick(l, $"PIN-код қате, {attemptsLeft} әрекет қалды", $"Неверный PIN-код, осталось попыток: {attemptsLeft}")
        : Pick(l, "PIN-код қате", "Неверный PIN-код");

    public static string StaffPinLocked(AppLanguage l, int minutes) => Pick(l,
        $"PIN-код {minutes} минутқа бұғатталды. Құпиясөзбен кіріңіз",
        $"PIN-код заблокирован на {minutes} мин. Войдите по паролю");

    public static string StaffPinNotSet(AppLanguage l) => Pick(l,
        "PIN-код қойылмаған. Құпиясөзбен кіріп, PIN-код қойыңыз",
        "PIN-код не установлен. Войдите по паролю и установите PIN-код");

    public static string StaffCurrentPasswordWrong(AppLanguage l) => Pick(l,
        "Қазіргі құпиясөз қате", "Текущий пароль неверный");

    public static string StaffNotAllowedOnRegister(AppLanguage l) => Pick(l,
        "Бұл қызметкер осы кассада жұмыс істей алмайды", "Этот сотрудник не может работать на этой кассе");

    public static string RegisterNotActivated(AppLanguage l) => Pick(l,
        "Бұл құрылғы кассаға тіркелмеген", "Это устройство не подключено как касса");

    public static string RegisterNotFound(AppLanguage l) => Pick(l,
        "Касса табылмады", "Касса не найдена");

    // Каталог (ТЗ «Товар» §18)
    public static string CatalogNoOrganization(AppLanguage l) => Pick(l,
        "Дүкен бизнеске байланбаған", "Магазин не привязан к бизнесу");

    public static string CatalogNameRequired(AppLanguage l) => Pick(l,
        "Атауын енгізіңіз", "Введите название");

    public static string CatalogDuplicateName(AppLanguage l) => Pick(l,
        "Мұндай атау осы деңгейде бар", "Такое название уже есть на этом уровне");

    public static string CatalogDuplicate(AppLanguage l) => Pick(l,
        "Мұндай атау бұрыннан бар", "Такое название уже существует");

    public static string CatalogIconRequired(AppLanguage l) => Pick(l,
        "Санатқа белгіше таңдаңыз", "Выберите иконку для категории");

    public static string CatalogNodeNotFound(AppLanguage l) => Pick(l,
        "Санат не топ табылмады", "Категория или группа не найдена");

    public static string CatalogCycle(AppLanguage l) => Pick(l,
        "Түйінді өзіне не өз ұрпағына ауыстыруға болмайды",
        "Нельзя переместить узел в самого себя или в своего потомка");

    public static string CatalogParentArchived(AppLanguage l) => Pick(l,
        "Архивтегі түйіннің ішіне белсенді түйін жасауға болмайды. Алдымен оны қалпына келтіріңіз",
        "Нельзя создать активный узел внутри архивного. Сначала восстановите его");

    public static string CatalogTooDeep(AppLanguage l, int max) => Pick(l,
        $"Деңгейлер саны {max}-тен аспауы керек", $"Глубина не может превышать {max} уровней");

    public static string CatalogDeleteBlocked(AppLanguage l, int children, int products) => Pick(l,
        $"Өшіруге болмайды: ішінде {children} түйін және {products} тауар бар. Мазмұнын басқа жерге ауыстырыңыз немесе архивтеңіз",
        $"Нельзя удалить: внутри {children} узлов и {products} товаров. Перенесите содержимое или архивируйте");

    public static string CatalogMoveTargetInvalid(AppLanguage l) => Pick(l,
        "Мазмұнды өз ішіне ауыстыруға болмайды", "Нельзя перенести содержимое внутрь самого узла");

    public static string CatalogStatusInvalid(AppLanguage l) => Pick(l,
        "Күйі дұрыс емес", "Неверный статус");

    public static string BrandNotFound(AppLanguage l) => Pick(l, "Бренд табылмады", "Бренд не найден");

    public static string UnitNotFound(AppLanguage l) => Pick(l, "Өлшем бірлігі табылмады", "Единица измерения не найдена");

    public static string UnitShortRequired(AppLanguage l) => Pick(l,
        "Қысқа белгісін енгізіңіз", "Введите краткое обозначение");

    public static string CharacteristicNotFound(AppLanguage l) => Pick(l,
        "Сипаттама табылмады", "Характеристика не найдена");

    public static string CharacteristicTypeInvalid(AppLanguage l) => Pick(l,
        "Сипаттаманың түрін таңдаңыз", "Выберите тип характеристики");

    public static string CharacteristicOptionsRequired(AppLanguage l) => Pick(l,
        "Тізімге кемінде бір мән қосыңыз", "Добавьте хотя бы одно значение списка");

    // Тауар (ТЗ «Товар» §6, §18)
    public static string ProductNotFound(AppLanguage l) => Pick(l, "Тауар табылмады", "Товар не найден");

    public static string ProductUnitRequired(AppLanguage l) => Pick(l,
        "Өлшем бірлігін таңдаңыз", "Выберите единицу измерения");

    public static string ProductUnitArchived(AppLanguage l) => Pick(l,
        "Бұл өлшем бірлігі архивте — басқасын таңдаңыз", "Эта единица измерения в архиве — выберите другую");

    public static string ProductBrandArchived(AppLanguage l) => Pick(l,
        "Бұл бренд архивте — басқасын таңдаңыз", "Этот бренд в архиве — выберите другой");

    public static string ProductNodeArchived(AppLanguage l) => Pick(l,
        "Бұл санат не топ архивте — басқасын таңдаңыз", "Эта категория или группа в архиве — выберите другую");

    public static string ProductRestoreNodeArchived(AppLanguage l) => Pick(l,
        "Тауардың санаты архивте. Алдымен санатты қалпына келтіріңіз не классификациясын өзгертіңіз",
        "Категория товара в архиве. Сначала восстановите её или измените классификацию");

    public static string BarcodeInvalid(AppLanguage l, string code) => Pick(l,
        $"«{code}» штрихкоды дұрыс емес: 3–64 таңба, тек әріп, сан, «-» және «.»",
        $"Неверный штрихкод «{code}»: 3–64 символа, только буквы, цифры, «-» и «.»");

    public static string BarcodeRepeated(AppLanguage l, string code) => Pick(l,
        $"«{code}» штрихкоды екі рет енгізілген", $"Штрихкод «{code}» указан дважды");

    public static string BarcodeTaken(AppLanguage l, string code, string product) => Pick(l,
        $"«{code}» штрихкоды «{product}» тауарына тиесілі", $"Штрихкод «{code}» уже принадлежит товару «{product}»");

    public static string BarcodeGenerateFailed(AppLanguage l) => Pick(l,
        "Штрихкод жасалмады, қайталап көріңіз", "Не удалось сгенерировать штрихкод, попробуйте ещё раз");

    public static string ProductImagesTooMany(AppLanguage l, int max) => Pick(l,
        $"Фото саны {max}-тен аспауы керек", $"Не больше {max} фото");

    public static string ProductImageInvalid(AppLanguage l) => Pick(l,
        "Фото жүктелмеген не сілтемесі бөтен", "Фото не загружено или ссылка чужая");

    public static string CharacteristicRequired(AppLanguage l, string name) => Pick(l,
        $"«{name}» сипаттамасын толтырыңыз", $"Заполните характеристику «{name}»");

    public static string CharacteristicValueInvalid(AppLanguage l, string name) => Pick(l,
        $"«{name}» сипаттамасының мәні дұрыс емес", $"Неверное значение характеристики «{name}»");

    public static string ValueNegative(AppLanguage l) => Pick(l,
        "Мән теріс болмауы керек", "Значение не может быть отрицательным");

    public static string ValueOutOfRange(AppLanguage l, int min, int max) => Pick(l,
        $"Мән {min}–{max} аралығында болуы керек", $"Значение должно быть от {min} до {max}");

    public static string WarehouseNotFound(AppLanguage l) => Pick(l, "Қойма табылмады", "Склад не найден");

    public static string DefaultWarehouseName(AppLanguage l) => Pick(l, "Негізгі қойма", "Основной склад");

    // Файл жүктеу
    public static string UploadKindInvalid(AppLanguage l) => Pick(l, "Файл түрі белгісіз", "Неизвестный тип файла");

    public static string UploadTypeInvalid(AppLanguage l) => Pick(l,
        "Тек JPG, PNG не WebP суреті", "Только изображения JPG, PNG или WebP");

    public static string UploadTooLarge(AppLanguage l, int mb) => Pick(l,
        $"Файл {mb} МБ-тан аспауы керек", $"Файл не должен превышать {mb} МБ");

    public static string StorageNotConfigured(AppLanguage l) => Pick(l,
        "Файл қоймасы бапталмаған", "Хранилище файлов не настроено");

    // Импорт / экспорт
    public static string StatusActive(AppLanguage l) => Pick(l, "Белсенді", "Активный");
    public static string StatusArchived(AppLanguage l) => Pick(l, "Архивте", "Архивный");

    public static string ImportFileInvalid(AppLanguage l) => Pick(l,
        "Файл оқылмады. Оны Excel-де .xlsx пішімінде сақтаңыз", "Не удалось прочитать файл. Сохраните его в Excel в формате .xlsx");

    public static string ImportFileRequired(AppLanguage l) => Pick(l, "Файлды таңдаңыз", "Выберите файл");

    public static string ImportMissingColumn(AppLanguage l, string column) => Pick(l,
        $"Файлда «{column}» бағаны жоқ. Үлгіні жүктеп алыңыз", $"В файле нет колонки «{column}». Скачайте шаблон");

    public static string ImportTooManyRows(AppLanguage l, int max) => Pick(l,
        $"Бір файлда {max} жолдан артық болмауы керек", $"В одном файле не больше {max} строк");

    public static string ImportIdNotFound(AppLanguage l, string id) => Pick(l,
        $"ID «{id}» бойынша тауар табылмады", $"Товар с ID «{id}» не найден");

    public static string ImportNoCreatePermission(AppLanguage l) => Pick(l,
        "Жаңа тауар жасауға рұқсатыңыз жоқ", "Нет права создавать товары");

    public static string ImportNoEditPermission(AppLanguage l) => Pick(l,
        "Бар тауарды өзгертуге рұқсатыңыз жоқ", "Нет права изменять товары");

    public static string ImportDuplicateProduct(AppLanguage l, int row) => Pick(l,
        $"Бұл тауар {row}-жолда да бар", $"Этот товар уже есть в строке {row}");

    public static string ImportUnitNotFound(AppLanguage l, string unit) => Pick(l,
        $"«{unit}» өлшем бірлігі табылмады", $"Единица измерения «{unit}» не найдена");

    public static string ImportBrandNotFound(AppLanguage l, string brand) => Pick(l,
        $"«{brand}» бренді табылмады", $"Бренд «{brand}» не найден");

    public static string ImportCategoryNotFound(AppLanguage l, string path) => Pick(l,
        $"«{path}» санаты табылмады", $"Категория «{path}» не найдена");

    public static string ImportBarcodeInRow(AppLanguage l, string code, int row) => Pick(l,
        $"«{code}» штрихкоды {row}-жолда да бар", $"Штрихкод «{code}» уже указан в строке {row}");

    public static string ImportNumberInvalid(AppLanguage l, string column) => Pick(l,
        $"«{column}» бағанындағы мән дұрыс емес", $"Неверное значение в колонке «{column}»");

    // Касса (ТЗ «Касса» §21)
    public static string ApproverNotAllowed(AppLanguage l) => Pick(l,
        "Бұл қызметкер жеңілдікті растай алмайды", "Этот сотрудник не может подтверждать скидки");

    public static string RegisterOtherStore(AppLanguage l) => Pick(l,
        "Бұл касса басқа дүкенге тіркелген", "Эта касса подключена к другому магазину");

    public static string CustomerPhoneExists(AppLanguage l) => Pick(l,
        "Бұл нөмірмен клиент бар — іздеу арқылы табыңыз", "Клиент с таким номером уже есть — найдите его через поиск");

    public static string BirthDateRequired(AppLanguage l) => Pick(l, "Туған күнін енгізіңіз", "Укажите дату рождения");

    public static string SaleRequestIdRequired(AppLanguage l) => Pick(l, "Сұраныс кілті жоқ", "Нет ключа запроса");

    public static string SaleEmpty(AppLanguage l) => Pick(l, "Себет бос", "Корзина пуста");

    public static string SaleTooManyLines(AppLanguage l, int max) => Pick(l,
        $"Бір чекте {max} позициядан артық болмайды", $"В одном чеке не больше {max} позиций");

    public static string SaleQuantityInvalid(AppLanguage l) => Pick(l, "Саны дұрыс емес", "Неверное количество");

    public static string SaleProductUnavailable(AppLanguage l) => Pick(l,
        "Тауар сатылымда жоқ (архивте не кассада жасырылған)", "Товар недоступен для продажи (в архиве или скрыт в кассе)");

    public static string SaleNoPrice(AppLanguage l, string name) => Pick(l,
        $"«{name}» тауарының бағасы көрсетілмеген", $"У товара «{name}» не указана цена");

    public static string SaleStockInsufficient(AppLanguage l, string name, decimal available) => Pick(l,
        $"«{name}» жеткіліксіз: қалдығы {available:0.###}", $"Недостаточно «{name}»: в наличии {available:0.###}");

    public static string DiscountKindInvalid(AppLanguage l) => Pick(l, "Жеңілдік түрі дұрыс емес", "Неверный тип скидки");

    public static string DiscountTooLarge(AppLanguage l) => Pick(l,
        "Жеңілдік сомадан артық болмауы керек", "Скидка не может быть больше суммы чека");

    public static string DiscountNeedsApproval(AppLanguage l, decimal limit) => Pick(l,
        $"Жеңілдік сіздің шегіңізден ({limit:0.##}%) асады — әкімшінің растауы керек",
        $"Скидка превышает ваш лимит ({limit:0.##}%) — нужно подтверждение администратора");

    public static string SaleBonusNeedsCustomer(AppLanguage l) => Pick(l,
        "Бонус шегеру үшін клиентті таңдаңыз", "Чтобы списать бонусы, выберите клиента");

    public static string PaymentRequired(AppLanguage l) => Pick(l, "Төлем түрін таңдаңыз", "Выберите способ оплаты");

    public static string PaymentMethodInvalid(AppLanguage l) => Pick(l, "Төлем түрі белгісіз", "Неизвестный способ оплаты");

    public static string PaymentMethodDisabled(AppLanguage l) => Pick(l,
        "Бұл төлем түрі дүкенде қосылмаған", "Этот способ оплаты не включён в магазине");

    public static string PaymentMethodRepeated(AppLanguage l) => Pick(l,
        "Бір төлем түрі екі рет көрсетілген", "Способ оплаты указан дважды");

    public static string PaymentAmountInvalid(AppLanguage l) => Pick(l, "Төлем сомасы дұрыс емес", "Неверная сумма оплаты");

    public static string TransferRecipientRequired(AppLanguage l) => Pick(l,
        "Аударым алушысын таңдаңыз", "Выберите получателя перевода");

    public static string PaymentMismatch(AppLanguage l, decimal total, decimal sum) => sum < total
        ? Pick(l, $"Төлем жетпейді: тағы {total - sum:0.##} ₸", $"Недостаточно оплаты: осталось {total - sum:0.##} ₸")
        : Pick(l, $"Төлем артық: {sum - total:0.##} ₸", $"Оплата превышает итог на {sum - total:0.##} ₸");

    public static string CashNotEnough(AppLanguage l, decimal missing) => Pick(l,
        $"Жеткіліксіз {missing:0.##} ₸", $"Недостаточно {missing:0.##} ₸");

    public static string SaleNotFound(AppLanguage l) => Pick(l, "Чек табылмады", "Чек не найден");

    // Қарыз бен қайтару (ТЗ «Касса» §11, §13)
    public static string DebtNeedsCustomer(AppLanguage l) => Pick(l,
        "Қарызға беру үшін клиентті таңдаңыз", "Чтобы оформить долг, выберите клиента");

    public static string DebtDueDateRequired(AppLanguage l) => Pick(l, "Қайтару күнін таңдаңыз", "Укажите дату возврата долга");

    public static string DebtDueDateInvalid(AppLanguage l) => Pick(l, "Қайтару күні өтіп кеткен", "Дата возврата уже прошла");

    public static string DebtNotFound(AppLanguage l) => Pick(l, "Қарыз табылмады", "Долг не найден");

    public static string DebtAlreadyPaid(AppLanguage l) => Pick(l, "Қарыз толық өтелген", "Долг уже погашен");

    public static string DebtRepayTooMuch(AppLanguage l, decimal remaining) => Pick(l,
        $"Қалдықтан артық: қарыздың қалғаны {remaining:0.##} ₸", $"Больше остатка долга: осталось {remaining:0.##} ₸");

    public static string ReturnEmpty(AppLanguage l) => Pick(l,
        "Қайтаратын тауарды және санын таңдаңыз", "Выберите товары и количество к возврату");

    public static string ReturnTooMuch(AppLanguage l, string name, decimal available) => Pick(l,
        $"«{name}»: қайтаруға болатыны {available:0.###}", $"«{name}»: можно вернуть не больше {available:0.###}");

    public static string ReturnItemNotFound(AppLanguage l) => Pick(l, "Чекте мұндай позиция жоқ", "В чеке нет такой позиции");

    public static string ReturnRefundMethodRequired(AppLanguage l) => Pick(l,
        "Ақшаны қалай қайтаратыныңызды таңдаңыз", "Выберите способ возврата денег");

    public static string SettingsNeedPaymentMethod(AppLanguage l) => Pick(l,
        "Кемінде бір төлем түрі қосулы болуы керек", "Должен быть включён хотя бы один способ оплаты");
}
