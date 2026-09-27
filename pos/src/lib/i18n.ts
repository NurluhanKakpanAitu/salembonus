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
