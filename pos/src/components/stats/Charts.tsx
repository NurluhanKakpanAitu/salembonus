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
