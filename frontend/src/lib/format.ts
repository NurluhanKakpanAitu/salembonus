import type { TransactionType } from './api'

const MONTHS = [
  'қаңтар', 'ақпан', 'наурыз', 'сәуір', 'мамыр', 'маусым',
  'шілде', 'тамыз', 'қыркүйек', 'қазан', 'қараша', 'желтоқсан',
]

export const formatNumber = (n: number) =>
  Math.abs(Math.round(n)).toString().replace(/\B(?=(\d{3})+(?!\d))/g, ' ')

export const formatBonus = (n: number) => `${formatNumber(n)} Б`
export const formatTenge = (n: number) => `${formatNumber(n)} ₸`

export const formatSigned = (n: number, unit: 'Б' | '₸') =>
  `${n < 0 ? '- ' : '+ '}${formatNumber(n)} ${unit}`

const pad = (n: number) => n.toString().padStart(2, '0')

export function formatDateTime(iso: string): string {
  const d = new Date(iso.endsWith('Z') ? iso : `${iso}Z`)
  const now = new Date()
  const time = `${pad(d.getHours())}:${pad(d.getMinutes())}`
  const sameDay = d.toDateString() === now.toDateString()
  if (sameDay) return `Бүгін, ${time}`
  const yesterday = new Date(now)
  yesterday.setDate(now.getDate() - 1)
  if (d.toDateString() === yesterday.toDateString()) return `Кеше, ${time}`
  return `${d.getDate()} ${MONTHS[d.getMonth()]}, ${time}`
}

export const transactionTitle: Record<TransactionType, string> = {
  Accrual: 'Бонус есептелді',
  Redemption: 'Бонус жұмсалды',
  Birthday: 'Туған күн бонусы',
  Promo: 'Акция бонусы',
  Expiration: 'Бонус мерзімі өтті',
}

export const levelThresholdLabel = (amountToNext: number) =>
  amountToNext <= 0 ? 'Ең жоғары деңгей' : `Қалды: ${formatTenge(amountToNext)}`

/** "1997-09-15" -> "15 қыркүйек 1997" */
export function formatDate(iso: string, withYear = true): string {
  const [y, m, d] = iso.split('-').map(Number)
  return withYear ? `${d} ${MONTHS[m - 1]} ${y}` : `${d} ${MONTHS[m - 1]}`
}

export const initials = (fullName: string) =>
  fullName.split(' ').filter(Boolean).slice(0, 2).map((p) => p[0]?.toUpperCase() ?? '').join('')

/** Күн тобының тақырыбы: "Бүгін, 20 қыркүйек" / "Кеше, 19 қыркүйек" / "18 қыркүйек" */
export function dayLabel(iso: string): string {
  const d = new Date(iso.endsWith('Z') ? iso : `${iso}Z`)
  const now = new Date()
  const label = `${d.getDate()} ${MONTHS[d.getMonth()]}`
  if (d.toDateString() === now.toDateString()) return `Бүгін, ${label}`
  const y = new Date(now)
  y.setDate(now.getDate() - 1)
  if (d.toDateString() === y.toDateString()) return `Кеше, ${label}`
  return d.getFullYear() === now.getFullYear() ? label : `${label} ${d.getFullYear()}`
}

export const dayKey = (iso: string) => new Date(iso.endsWith('Z') ? iso : `${iso}Z`).toDateString()

export function formatTime(iso: string): string {
  const d = new Date(iso.endsWith('Z') ? iso : `${iso}Z`)
  return `${pad(d.getHours())}:${pad(d.getMinutes())}`
}
