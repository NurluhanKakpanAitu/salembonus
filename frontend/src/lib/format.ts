import { getLanguage } from './language'
import { translate } from './i18n'
import type { TransactionType } from './api'

const MONTHS = {
  kk: [
    'қаңтар', 'ақпан', 'наурыз', 'сәуір', 'мамыр', 'маусым',
    'шілде', 'тамыз', 'қыркүйек', 'қазан', 'қараша', 'желтоқсан',
  ],
  ru: [
    'января', 'февраля', 'марта', 'апреля', 'мая', 'июня',
    'июля', 'августа', 'сентября', 'октября', 'ноября', 'декабря',
  ],
}

const month = (i: number) => MONTHS[getLanguage()][i]
const t = (key: Parameters<typeof translate>[1], vars?: Parameters<typeof translate>[2]) =>
  translate(getLanguage(), key, vars)

export const formatNumber = (n: number) =>
  Math.abs(Math.round(n)).toString().replace(/\B(?=(\d{3})+(?!\d))/g, ' ')

export const formatBonus = (n: number) => `${formatNumber(n)} ${t('common.bonusUnit')}`
export const formatTenge = (n: number) => `${formatNumber(n)} ₸`

export const formatSigned = (n: number, unit: 'Б' | '₸') =>
  `${n < 0 ? '- ' : '+ '}${formatNumber(n)} ${unit === 'Б' ? t('common.bonusUnit') : unit}`

const pad = (n: number) => n.toString().padStart(2, '0')

export function formatDateTime(iso: string): string {
  const d = new Date(iso.endsWith('Z') ? iso : `${iso}Z`)
  const now = new Date()
  const time = `${pad(d.getHours())}:${pad(d.getMinutes())}`
  const sameDay = d.toDateString() === now.toDateString()
  if (sameDay) return `${t('common.today')}, ${time}`
  const yesterday = new Date(now)
  yesterday.setDate(now.getDate() - 1)
  if (d.toDateString() === yesterday.toDateString()) return `${t('common.yesterday')}, ${time}`
  return `${d.getDate()} ${month(d.getMonth())}, ${time}`
}

export const transactionTitle = (type: TransactionType) => t(`tx.${type}`)

export const levelThresholdLabel = (amountToNext: number) =>
  amountToNext <= 0 ? t('cards.maxLevelShort') : t('cards.amountLeft', { amount: formatTenge(amountToNext) })

/** "1997-09-15" -> "15 қыркүйек 1997" / "15 сентября 1997" */
export function formatDate(iso: string, withYear = true): string {
  const [y, m, d] = iso.split('-').map(Number)
  return withYear ? `${d} ${month(m - 1)} ${y}` : `${d} ${month(m - 1)}`
}

export const initials = (fullName: string) =>
  fullName.split(' ').filter(Boolean).slice(0, 2).map((p) => p[0]?.toUpperCase() ?? '').join('')

/** Күн тобының тақырыбы: "Бүгін, 20 қыркүйек" / "Сегодня, 20 сентября" */
export function dayLabel(iso: string): string {
  const d = new Date(iso.endsWith('Z') ? iso : `${iso}Z`)
  const now = new Date()
  const label = `${d.getDate()} ${month(d.getMonth())}`
  if (d.toDateString() === now.toDateString()) return `${t('common.today')}, ${label}`
  const y = new Date(now)
  y.setDate(now.getDate() - 1)
  if (d.toDateString() === y.toDateString()) return `${t('common.yesterday')}, ${label}`
  return d.getFullYear() === now.getFullYear() ? label : `${label} ${d.getFullYear()}`
}

export const dayKey = (iso: string) => new Date(iso.endsWith('Z') ? iso : `${iso}Z`).toDateString()

export function formatTime(iso: string): string {
  const d = new Date(iso.endsWith('Z') ? iso : `${iso}Z`)
  return `${pad(d.getHours())}:${pad(d.getMinutes())}`
}
