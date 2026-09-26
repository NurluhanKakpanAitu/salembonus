import type { Notification } from './api'
import { formatNumber, formatTenge } from './format'
import type { Translator, TranslationKey } from './i18n'

/**
 * Хабарлама мәтінін ағымдағы тілде құрастырады.
 * Сервер үлгі кілтін бермесе (дүкеннің еркін мәтіні, ескі жазбалар) сақталған мәтін көрсетіледі.
 */
export function notificationText(
  n: Notification,
  t: Translator,
  /** Дүкеннің өз жазысуында атауын қайталамау үшін қысқа нұсқа. */
  options?: { short?: boolean },
): { title: string; body: string; detail: string | null } {
  if (!n.templateKey) return { title: n.title, body: n.body, detail: n.detail }

  const vars = {
    store: n.storeName ?? '',
    amount: n.amount != null ? formatNumber(n.amount) : '',
    level: n.levelKey ? t(`level.${n.levelKey}` as TranslationKey) : '',
  }
  const title = t(`notif.${n.templateKey}.title` as TranslationKey)
  const shortKey = `notif.${n.templateKey}.short` as TranslationKey
  const short = options?.short ? t(shortKey, vars) : shortKey
  // Аудармасы жоқ кілт өзін қайтарады — сонда толық нұсқа қалады.
  const body = short !== shortKey ? short : t(`notif.${n.templateKey}.body` as TranslationKey, vars)

  const detail = n.purchaseAmount != null
    ? t('notif.purchaseAmount', { amount: formatTenge(n.purchaseAmount) })
    : n.templateKey === 'store_added'
      ? t('notif.store_added.detail')
      : null

  return { title, body, detail }
}
