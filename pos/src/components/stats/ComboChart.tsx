import { useState } from 'react'
import type { Bucket, SeriesKind } from '../../lib/analyticsTypes'
import { useT } from '../../lib/i18n'
import { bucketLabel, bucketTitle, compact, formatKind } from './statsFormat'

export interface ChartSeries {
  key: string
  label: string
  kind: SeriesKind
  style: 'bar' | 'line'
  color: string
  values: number[]
}

function niceMax(v: number) {
  if (v <= 0) return 1
  const p = 10 ** Math.floor(Math.log10(v))
  const n = v / p
  return (n <= 1 ? 1 : n <= 2 ? 2 : n <= 5 ? 5 : 10) * p
}

/**
 * Комбинирленген график (ТЗ «Статистика» §9): бір бакет — бір бағана/нүкте, хронологиялық, бос күн — 0.
 * Сол ось — ақша/дана/бонус, оң ось — тек бүтін «саны» (чек, клиент). Легенда серияны жасырады/көрсетеді,
 * tooltip — нақты күн (не апта/айдың басы мен соңы) және тек сол бакеттің мәндері. Соңғы бакет бүгін болса — «день не завершён».
 */
export function ComboChart({ buckets, series, granularity, incompleteLast, onBucketClick, leftTitle, rightTitle, height = 260 }: {
  buckets: Bucket[]
  series: ChartSeries[]
  granularity: string
  incompleteLast?: boolean
  onBucketClick?: (index: number) => void
  leftTitle?: string
  rightTitle?: string
  height?: number
}) {
  const t = useT()
  const [hidden, setHidden] = useState<Record<string, boolean>>({})
  const [hover, setHover] = useState<number | null>(null)
  const visible = series.filter((s) => !hidden[s.key])
  const right = visible.filter((s) => s.kind === 'count')
  const left = visible.filter((s) => s.kind !== 'count')
  const leftKind = left[0]?.kind ?? 'money'

  const W = 760, H = height, L = 62, R = right.length ? 44 : 16, T = 14, B = 28
  const iw = W - L - R, ih = H - T - B
  const maxLeft = niceMax(Math.max(0, ...left.flatMap((s) => s.values)))
  const maxRight = niceMax(Math.max(0, ...right.flatMap((s) => s.values)))
  const n = Math.max(buckets.length, 1)
  const step = iw / n
  const bars = visible.filter((s) => s.style === 'bar')
  const groupW = Math.min(34, step * 0.72)
  const bw = bars.length ? groupW / bars.length : 0
  const x = (i: number) => L + step * i + step / 2
  const y = (v: number, kind: SeriesKind) => {
    const max = kind === 'count' ? maxRight : maxLeft
    return T + ih - (Math.max(v, 0) / max) * ih
  }
  const every = Math.max(1, Math.ceil(buckets.length / 10))
  const empty = series.every((s) => s.values.every((v) => v === 0))

  return (
    <div className="relative">
      <div className="mb-2 flex flex-wrap items-center gap-x-4 gap-y-1 text-[12px]">
        {series.map((s) => (
          <button key={s.key} type="button" aria-pressed={!hidden[s.key]} onClick={() => setHidden((h) => ({ ...h, [s.key]: !h[s.key] }))}
            className={`flex items-center gap-1.5 rounded-md px-1 py-0.5 hover:bg-field focus-visible:outline-2 focus-visible:outline-brand ${hidden[s.key] ? 'text-ink-3 line-through' : 'text-ink-2'}`}>
            <span className={s.style === 'bar' ? 'size-2.5 rounded-sm' : 'h-0.5 w-3.5 rounded-full'} style={{ background: s.color }} />
            {s.label}
          </button>
        ))}
        <span className="ml-auto text-ink-3">{t(`stats.gran.${granularity}` as 'stats.gran.day')}</span>
      </div>
      <svg viewBox={`0 0 ${W} ${H}`} className="w-full select-none" role="img" aria-label={series.map((s) => s.label).join(', ')}
        onMouseLeave={() => setHover(null)}>
        {leftTitle && <text x={12} y={T + ih / 2} transform={`rotate(-90 12 ${T + ih / 2})`} textAnchor="middle" fontSize="11" fill="var(--color-ink-3)">{leftTitle}</text>}
        {rightTitle && right.length > 0 && <text x={W - 6} y={T + ih / 2} transform={`rotate(90 ${W - 6} ${T + ih / 2})`} textAnchor="middle" fontSize="11" fill="var(--color-ink-3)">{rightTitle}</text>}
        {[0, 0.25, 0.5, 0.75, 1].map((f) => (
          <g key={f}>
            <line x1={L} x2={W - R} y1={T + ih * (1 - f)} y2={T + ih * (1 - f)} stroke="var(--color-line)" />
            {left.length > 0 && <text x={L - 8} y={T + ih * (1 - f) + 4} textAnchor="end" fontSize="11" fill="var(--color-ink-3)">{compact(maxLeft * f, leftKind)}</text>}
            {right.length > 0 && <text x={W - R + 6} y={T + ih * (1 - f) + 4} fontSize="11" fill="var(--color-ink-3)">{Math.round(maxRight * f)}</text>}
          </g>
        ))}
        {buckets.map((b, i) => (
          <g key={b.start} onMouseEnter={() => setHover(i)} onClick={() => onBucketClick?.(i)} className={onBucketClick ? 'cursor-pointer' : ''}>
            <rect x={x(i) - step / 2} y={T} width={step} height={ih} fill={hover === i ? 'var(--color-brand-soft)' : 'transparent'} opacity={0.6} />
            {bars.map((s, k) => {
              const v = s.values[i] ?? 0
              const top = y(v, s.kind)
              return <rect key={s.key} x={x(i) - groupW / 2 + k * bw} y={top} width={Math.max(1, bw - 1)} height={Math.max(0, T + ih - top)} rx={3}
                fill={s.color} opacity={incompleteLast && i === buckets.length - 1 ? 0.55 : 0.9} />
            })}
            {i % every === 0 && <text x={x(i)} y={H - 9} textAnchor="middle" fontSize="11" fill="var(--color-ink-3)">{bucketLabel(b, granularity)}</text>}
          </g>
        ))}
        {visible.filter((s) => s.style === 'line').map((s) => (
          <g key={s.key} pointerEvents="none">
            <path d={s.values.map((v, i) => `${i ? 'L' : 'M'}${x(i)},${y(v, s.kind)}`).join(' ')} fill="none" stroke={s.color} strokeWidth={2.2} />
            {buckets.length <= 62 && s.values.map((v, i) => <circle key={i} cx={x(i)} cy={y(v, s.kind)} r={hover === i ? 4.2 : 2.6} fill="#fff" stroke={s.color} strokeWidth={2} />)}
          </g>
        ))}
      </svg>
      {empty && <div className="pointer-events-none absolute inset-x-0 top-1/2 -translate-y-1/2 text-center text-[13px] text-ink-3">{t('stats.noDataPeriod')}</div>}
      {hover !== null && buckets[hover] && (
        <div className="pointer-events-none absolute top-8 z-10 min-w-44 rounded-xl border border-line bg-surface px-3 py-2 text-[12px] shadow-lg"
          style={{ left: `${Math.min(70, Math.max(0, (x(hover) / W) * 100 - 8))}%` }}>
          <div className="font-semibold">{bucketTitle(buckets[hover], granularity)}</div>
          {incompleteLast && hover === buckets.length - 1 && <div className="text-amber-600">{t('stats.dayNotFinished')}</div>}
          {series.map((s) => (
            <div key={s.key} className="mt-0.5 flex items-center justify-between gap-3">
              <span className="flex items-center gap-1.5 text-ink-2"><span className="size-2 rounded-full" style={{ background: s.color }} />{s.label}</span>
              <b>{formatKind(s.values[hover] ?? 0, s.kind)}</b>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
