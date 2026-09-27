import { useLanguageStore, type Language } from './language'

/** Интерфейс мәтіндері. Кілт — екі тілді байланыстырады, айнымалылар {name} түрінде. */
const ru = {
  'common.loading': 'Загрузка…',
  'common.retry': 'Повторить',
  'common.today': 'Сегодня',

  'lang.ru': 'Русский',
  'lang.kk': 'Қазақша',

  'login.title': 'Вход',
  'login.subtitle': 'Войдите в свой аккаунт',
  'login.phone': 'Номер телефона',
  'login.password': 'Пароль',
  'login.showPassword': 'Показать пароль',
  'login.hidePassword': 'Скрыть пароль',
  'login.submit': 'Войти',
  'login.tagline': 'Больше чем касса',
  'login.phoneRequired': 'Введите номер телефона',
  'login.phoneInvalid': 'Введите корректный номер телефона',
  'login.passwordRequired': 'Введите пароль',
  'login.failed': 'Не удалось войти. Попробуйте ещё раз',
  'login.tooMany': 'Слишком много попыток. Подождите минуту',
  'login.forgot': 'Забыли пароль?',
  'login.passwordChanged': 'Пароль успешно изменён. Войдите с новым паролем',

  'reset.title': 'Восстановление пароля',
  'reset.phoneHint': 'Введите номер телефона, зарегистрированный в SalemPos',
  'reset.getCode': 'Получить код',
  'reset.codeTitle': 'Введите код подтверждения',
  'reset.codeHint': 'Мы отправили {length}-значный код в WhatsApp на номер {phone}',
  'reset.code': 'Код подтверждения',
  'reset.confirm': 'Подтвердить',
  'reset.resendIn': 'Отправить код повторно через {time}',
  'reset.resend': 'Отправить код повторно',
  'reset.changePhone': 'Изменить номер',
  'reset.devCode': 'Код (только в разработке): {code}',
  'reset.newTitle': 'Новый пароль',
  'reset.newHint': 'Придумайте новый пароль для входа',
  'reset.newPassword': 'Новый пароль',
  'reset.repeatPassword': 'Повторите новый пароль',
  'reset.policy': 'Не менее {min} символов',
  'reset.policyLettersDigits': 'Не менее {min} символов, буквы и цифры',
  'reset.save': 'Сохранить новый пароль',
  'reset.tooShort': 'Пароль должен содержать не менее {min} символов',
  'reset.weak': 'Пароль должен содержать буквы и цифры',
  'reset.mismatch': 'Пароли не совпадают',
  'reset.backToLogin': 'Вернуться ко входу',

  'nav.cashier': 'Касса',
  'nav.products': 'Товар',
  'nav.warehouse': 'Склад - Поставщики',
  'nav.sales': 'Продажа',
  'nav.statistics': 'Статистика',
  'nav.finance': 'Финанс',
  'nav.bonus': 'Бонус',
  'nav.clients': 'Клиенты',
  'nav.staff': 'Сотрудники',
  'nav.settings': 'Настройка',
  'nav.collapse': 'Свернуть меню',
  'nav.expand': 'Развернуть меню',

  'role.Owner': 'Владелец',
  'role.Admin': 'Администратор',
  'role.Cashier': 'Кассир',

  'top.store': 'Магазин',
  'top.notifications': 'Уведомления',
  'top.language': 'Язык',
  'top.logout': 'Выйти',

  'section.soon': 'Раздел в разработке',
  'section.soonHint': 'Этот раздел появится на следующих этапах.',
  'section.statisticsHint': 'Здесь будет обзор магазина: выручка, продажи, клиенты и бонусы.',
  'section.cashierHint': 'Здесь будет касса: каталог, корзина и оплата.',
  'section.productsHint': 'Здесь будет каталог: товары, категории, группы, бренды.',
  'section.noAccess': 'Нет доступа к этому разделу',
} as const

export type TranslationKey = keyof typeof ru

const kk: Record<TranslationKey, string> = {
  'common.loading': 'Жүктелуде…',
  'common.retry': 'Қайталау',
  'common.today': 'Бүгін',

  'lang.ru': 'Русский',
  'lang.kk': 'Қазақша',

  'login.title': 'Кіру',
  'login.subtitle': 'Аккаунтыңызға кіріңіз',
  'login.phone': 'Телефон нөмірі',
  'login.password': 'Құпиясөз',
  'login.showPassword': 'Құпиясөзді көрсету',
  'login.hidePassword': 'Құпиясөзді жасыру',
  'login.submit': 'Кіру',
  'login.tagline': 'Кассадан да артық',
  'login.phoneRequired': 'Телефон нөмірін енгізіңіз',
  'login.phoneInvalid': 'Телефон нөмірін дұрыс енгізіңіз',
  'login.passwordRequired': 'Құпиясөзді енгізіңіз',
  'login.failed': 'Кіру мүмкін болмады. Қайталап көріңіз',
  'login.tooMany': 'Тым көп әрекет. Бір минут күтіңіз',
  'login.forgot': 'Құпиясөзді ұмыттыңыз ба?',
  'login.passwordChanged': 'Құпиясөз сәтті өзгертілді. Жаңа құпиясөзбен кіріңіз',

  'reset.title': 'Құпиясөзді қалпына келтіру',
  'reset.phoneHint': 'SalemPos-та тіркелген телефон нөмірін енгізіңіз',
  'reset.getCode': 'Код алу',
  'reset.codeTitle': 'Растау кодын енгізіңіз',
  'reset.codeHint': '{phone} нөміріне WhatsApp арқылы {length} таңбалы код жібердік',
  'reset.code': 'Растау коды',
  'reset.confirm': 'Растау',
  'reset.resendIn': 'Кодты {time} кейін қайта жіберуге болады',
  'reset.resend': 'Кодты қайта жіберу',
  'reset.changePhone': 'Нөмірді өзгерту',
  'reset.devCode': 'Код (тек разработкада): {code}',
  'reset.newTitle': 'Жаңа құпиясөз',
  'reset.newHint': 'Кіру үшін жаңа құпиясөз ойлап табыңыз',
  'reset.newPassword': 'Жаңа құпиясөз',
  'reset.repeatPassword': 'Жаңа құпиясөзді қайталаңыз',
  'reset.policy': 'Кемінде {min} таңба',
  'reset.policyLettersDigits': 'Кемінде {min} таңба, әріп пен сан',
  'reset.save': 'Жаңа құпиясөзді сақтау',
  'reset.tooShort': 'Құпиясөз кемінде {min} таңба болуы керек',
  'reset.weak': 'Құпиясөзде әріп те, сан да болуы керек',
  'reset.mismatch': 'Құпиясөздер сәйкес келмейді',
  'reset.backToLogin': 'Кіру бетіне оралу',

  'nav.cashier': 'Касса',
  'nav.products': 'Тауар',
  'nav.warehouse': 'Қойма - Жеткізушілер',
  'nav.sales': 'Сату',
  'nav.statistics': 'Статистика',
  'nav.finance': 'Қаржы',
  'nav.bonus': 'Бонус',
  'nav.clients': 'Клиенттер',
  'nav.staff': 'Қызметкерлер',
  'nav.settings': 'Баптау',
  'nav.collapse': 'Мәзірді жию',
  'nav.expand': 'Мәзірді ашу',

  'role.Owner': 'Иесі',
  'role.Admin': 'Әкімші',
  'role.Cashier': 'Кассир',

  'top.store': 'Дүкен',
  'top.notifications': 'Хабарламалар',
  'top.language': 'Тіл',
  'top.logout': 'Шығу',

  'section.soon': 'Бөлім әзірленуде',
  'section.soonHint': 'Бұл бөлім келесі кезеңдерде қосылады.',
  'section.statisticsHint': 'Мұнда дүкен шолуы болады: түсім, сатылым, клиенттер және бонустар.',
  'section.cashierHint': 'Мұнда касса болады: каталог, себет және төлем.',
  'section.productsHint': 'Мұнда каталог болады: тауарлар, санаттар, топтар, брендтер.',
  'section.noAccess': 'Бұл бөлімге рұқсатыңыз жоқ',
}

const dictionaries: Record<Language, Record<TranslationKey, string>> = { ru, kk }

export function translate(lang: Language, key: TranslationKey, vars?: Record<string, string | number>) {
  let text = dictionaries[lang][key] ?? ru[key]
  if (vars) for (const [k, v] of Object.entries(vars)) text = text.replaceAll(`{${k}}`, String(v))
  return text
}

export type Translator = (key: TranslationKey, vars?: Record<string, string | number>) => string

export function useT(): Translator {
  const lang = useLanguageStore((s) => s.lang)
  return (key, vars) => translate(lang, key, vars)
}
