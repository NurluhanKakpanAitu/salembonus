import { useState } from 'react'
import { num, tenge } from '../../lib/money'

/** Ось үшін ықшам сан: 150000 → «150 000», 1 250 000 → «1,25 млн». */
const short = (n: number) => (n >= 1_000_000 ? `${num(Math.round(n / 10_000) / 100)} млн` : num(Math.round(n)))

function niceMax(v: number) {
  if (v <= 0) return 1
  const p = 10 ** Math.floor(Math.log10(v))
  return Math.ceil(v / p / 2) * 2 * p
}

export interface BarPoint { label: string; revenue: number; count: number }

/**
 * «Продажи по времени» (ТЗ «Статистика» §7): бағана — түсім, сызық — чек саны; нүктеге апарғанда
 * нақты уақыт, түсім және чек саны көрінеді.
 */
export function SalesChart({ points, revenueLabel, countLabel, receiptsText }: {
  points: BarPoint[]
  revenueLabel: string
  countLabel: string
  receiptsText: (n: number) => string
}) {
  const [hover, setHover] = useState<number | null>(null)
  // Карточка ~450px: viewBox соған жақын — жазулар кішірейіп кетпейді.
  const W = 480, H = 250, L = 50, R = 28, T = 12, B = 26
  const iw = W - L - R, ih = H - T - B
  const maxRev = niceMax(Math.max(...points.map((p) => p.revenue), 0))
  const maxCnt = niceMax(Math.max(...points.map((p) => p.count), 0))
  const step = iw / Math.max(points.length, 1)
  const bw = Math.min(28, step * 0.6)
  const x = (i: number) => L + step * i + step / 2
  const yr = (v: number) => T + ih - (Math.max(v, 0) / maxRev) * ih
  const yc = (v: number) => T + ih - (v / maxCnt) * ih
  const line = points.map((p, i) => `${i ? 'L' : 'M'}${x(i)},${yc(p.count)}`).join(' ')
  const every = Math.ceil(points.length / 12)

  return (
    <div className="relative">
      <div className="mb-2 flex gap-4 text-[12px] text-ink-2">
        <span className="flex items-center gap-1.5"><span className="size-2.5 rounded-full bg-brand" />{revenueLabel}</span>
        <span className="flex items-center gap-1.5"><span className="size-2.5 rounded-full bg-sky-300" />{countLabel}</span>
      </div>
      <svg viewBox={`0 0 ${W} ${H}`} className="w-full" onMouseLeave={() => setHover(null)}>
        {[0, 0.25, 0.5, 0.75, 1].map((f) => (
          <g key={f}>
            <line x1={L} x2={W - R} y1={T + ih * (1 - f)} y2={T + ih * (1 - f)} stroke="var(--color-line)" />
            <text x={L - 8} y={T + ih * (1 - f) + 4} textAnchor="end" fontSize="11" fill="var(--color-ink-3)">{short(maxRev * f)}</text>
            <text x={W - R + 8} y={T + ih * (1 - f) + 4} fontSize="11" fill="var(--color-ink-3)">{Math.round(maxCnt * f)}</text>
          </g>
        ))}
        {points.map((p, i) => (
          <g key={i} onMouseEnter={() => setHover(i)}>
            <rect x={x(i) - step / 2} y={T} width={step} height={ih} fill="transparent" />
            <rect x={x(i) - bw / 2} y={yr(p.revenue)} width={bw} height={Math.max(0, T + ih - yr(p.revenue))} rx={5}
              fill={hover === i ? 'var(--color-brand-hover)' : 'var(--color-brand)'} opacity={hover === null || hover === i ? 1 : 0.55} />
            {i % every === 0 && <text x={x(i)} y={H - 8} textAnchor="middle" fontSize="11" fill="var(--color-ink-3)">{p.label}</text>}
          </g>
        ))}
        <path d={line} fill="none" stroke="#7dd3fc" strokeWidth={2.5} />
        {points.map((p, i) => <circle key={i} cx={x(i)} cy={yc(p.count)} r={hover === i ? 4.5 : 3} fill="#fff" stroke="#38bdf8" strokeWidth={2} />)}
      </svg>
      {hover !== null && points[hover] && (
        <div className="pointer-events-none absolute top-6 rounded-xl border border-line bg-surface px-3 py-2 text-[12px] shadow-lg"
          style={{ left: `${Math.min(80, (x(hover) / W) * 100)}%` }}>
          <div className="font-semibold">{points[hover].label}</div>
          <div className="text-[14px] font-bold">{tenge(points[hover].revenue)}</div>
          <div className="text-ink-2">{receiptsText(points[hover].count)}</div>
        </div>
      )}
    </div>
  )
}

export interface Slice { key: string; label: string; value: number; color: string; extra?: string }

/** Сақина диаграмма ортасында жиынтықпен (төлем түрлері, санаттар, клиенттер, бонус). */
export function Donut({ slices, center, sub, size = 150 }: { slices: Slice[]; center: string; sub?: string; size?: number }) {
  const total = slices.reduce((s, x) => s + x.value, 0)
  const r = 42, c = 2 * Math.PI * r
  let offset = 0
  return (
    <div className="relative shrink-0" style={{ width: size, height: size }}>
      <svg viewBox="0 0 100 100" className="size-full -rotate-90">
        <circle cx="50" cy="50" r={r} fill="none" stroke="var(--color-field)" strokeWidth="13" />
        {total > 0 && slices.filter((s) => s.value > 0).map((s) => {
          const len = (s.value / total) * c
          const el = <circle key={s.key} cx="50" cy="50" r={r} fill="none" stroke={s.color} strokeWidth="13"
            strokeDasharray={`${len} ${c - len}`} strokeDashoffset={-offset} />
          offset += len
          return el
        })}
      </svg>
      <div className="absolute inset-0 flex flex-col items-center justify-center text-center">
        {/* Ұзын сома (тиынмен) сақинаға сыйсын: шрифт ені бойынша кішірейеді. */}
        <div className="max-w-[74%] truncate font-extrabold leading-tight"
          style={{ fontSize: Math.min(15, (size * 0.72) / (center.length * 0.6)) }}>{center}</div>
        {sub && <div className="text-[11px] text-ink-3">{sub}</div>}
      </div>
    </div>
  )
}

export function Legend({ slices, format }: { slices: Slice[]; format: (s: Slice) => string }) {
  return (
    <ul className="flex min-w-0 flex-1 flex-col gap-2 text-[13px]">
      {slices.map((s) => (
        <li key={s.key} className="flex items-start gap-2">
          <span className="mt-1 size-2.5 shrink-0 rounded-full" style={{ background: s.color }} />
          <span className="min-w-0 flex-1">
            <span className="block truncate text-ink-2">{s.label}</span>
            <span className="font-semibold whitespace-nowrap">{format(s)}</span>
            {s.extra && <span className="ml-1.5 text-[12px] text-ink-3">{s.extra}</span>}
          </span>
        </li>
      ))}
    </ul>
  )
}
