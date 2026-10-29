import { useEffect, useState } from 'react'
import { cn } from '../../lib/cn'
import { Input } from './Field'

const pad = (n: number) => String(n).padStart(2, '0')

/** «0930» → «09:30»: теру кезінде қос нүкте өзі қойылады. */
const mask = (raw: string) => {
  const d = raw.replace(/\D/g, '').slice(0, 4)
  return d.length > 2 ? `${d.slice(0, 2)}:${d.slice(2)}` : d
}

/** «9:5», «930», «09:30» → «09:30»; сағат 0–23, минут 0–59, әйтпесе null. */
export function parseTime(raw: string): string | null {
  const m = /^(\d{1,2}):?(\d{2})$/.exec(raw.trim())
  if (!m) return null
  const h = Number(m[1])
  const min = Number(m[2])
  return h <= 23 && min <= 59 ? `${pad(h)}:${pad(min)}` : null
}

/**
 * Уақыт өрісі тек 24 сағаттық пішімде (Қазақстан: 14:30, «PM» жоқ). Браузердің уақыт өрісі (input time) пайдаланушының
 * Windows/Chrome тіліне қарай AM/PM көрсетеді, сондықтан өз өрісіміз: сандар теріледі, қос нүкте өзі қойылады.
 */
export function TimeInput({ value, onChange, error, className, 'aria-label': ariaLabel }: {
  value: string; onChange: (value: string) => void; error?: boolean; className?: string; 'aria-label'?: string
}) {
  const [draft, setDraft] = useState(value)
  useEffect(() => setDraft(value), [value])
  return (
    <Input value={draft} inputMode="numeric" placeholder="00:00" maxLength={5} aria-label={ariaLabel} error={error}
      className={cn('tabular-nums', className)}
      onChange={(e) => {
        const next = mask(e.target.value)
        setDraft(next)
        const valid = next.length === 5 ? parseTime(next) : null
        if (valid) onChange(valid)
      }}
      onBlur={() => setDraft(parseTime(draft) ?? value)} />
  )
}

/**
 * Күн мен уақыт (мәні «yyyy-MM-ddTHH:mm», datetime-local-пен бірдей): күн — браузердің күнтізбесімен, уақыт — 24 сағаттық өріспен.
 * max берілсе — тек күні шектеледі (уақытты сервер тексереді).
 */
export function DateTimeInput({ value, onChange, max, error }: {
  value: string; onChange: (value: string) => void; max?: string; error?: boolean
}) {
  const [date = '', time = ''] = value.split('T')
  return (
    <div className="grid grid-cols-[minmax(0,1fr)_6.5rem] gap-2">
      <Input type="date" value={date} max={max?.slice(0, 10)} error={error}
        onChange={(e) => e.target.value && onChange(`${e.target.value}T${time || '00:00'}`)} />
      <TimeInput value={time.slice(0, 5)} error={error} onChange={(t) => onChange(`${date}T${t}`)} />
    </div>
  )
}
