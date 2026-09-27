import { ArrowDown, ArrowUp } from 'lucide-react'

/** Реті: ↑ нөмір ↓ (макеттегі «Порядок» бағаны). */
export function OrderControl({ value, onUp, onDown, disabled }: {
  value: number
  onUp: () => void
  onDown: () => void
  disabled?: boolean
}) {
  const btn = 'flex size-7 items-center justify-center text-brand hover:bg-brand-soft disabled:text-ink-3 disabled:hover:bg-transparent'
  return (
    <div className="inline-flex items-center overflow-hidden rounded-lg border border-line">
      <button type="button" aria-label="Up" disabled={disabled} onClick={onUp} className={btn}><ArrowUp size={14} /></button>
      <span className="w-9 border-x border-line text-center text-[13px] font-semibold">{value}</span>
      <button type="button" aria-label="Down" disabled={disabled} onClick={onDown} className={btn}><ArrowDown size={14} /></button>
    </div>
  )
}
