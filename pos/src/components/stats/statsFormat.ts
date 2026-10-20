import type { Bucket, ColumnKind, SeriesKind } from '../../lib/analyticsTypes'
import { dateTime } from '../../lib/dates'
import { num, tenge } from '../../lib/money'

const dm = (iso: string) => `${iso.slice(8, 10)}.${iso.slice(5, 7)}`
const dmy = (iso: string) => `${iso.slice(8, 10)}.${iso.slice(5, 7)}.${iso.slice(0, 4)}`
const monthName = (iso: string) => new Intl.DateTimeFormat('ru-RU', { month: 'short', year: '2-digit' }).format(new Date(`${iso}T00:00:00`))

/** Ось жазуы: күн — «15.09», апта — «15.09», ай — «сен. 26». */
export function bucketLabel(b: Bucket, granularity: string) {
  return granularity === 'month' ? monthName(b.start) : dm(b.start)
}

/** Tooltip: нақты күн не бакеттің басы мен соңы (§9). */
export function bucketTitle(b: Bucket, _granularity: string) {
  return b.start === b.end ? dmy(b.start) : `${dmy(b.start)} – ${dmy(b.end)}`
}

export const periodText = (start: string, end: string) => (start === end ? dmy(start) : `${dmy(start)} – ${dmy(end)}`)

/** Осьтегі ықшам сан: 1 250 000 → «1,25 млн». */
export function compact(v: number, kind: SeriesKind) {
  const n = v >= 1_000_000 ? `${num(Math.round(v / 10_000) / 100)} млн` : num(Math.round(v))
  return kind === 'money' && v < 1_000_000 ? n : n
}

export function formatKind(v: number | null | undefined, kind: SeriesKind | ColumnKind): string {
  if (v === null || v === undefined) return '—'
  switch (kind) {
    case 'money': return tenge(v)
    case 'points': return `${num(v)} Б`
    case 'percent': return `${num(v)}%`
    default: return num(v)
  }
}

export { dmy, dateTime }
