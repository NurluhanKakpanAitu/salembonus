import { useLayoutEffect, useRef, useState, type RefObject } from 'react'
import { Search, X } from 'lucide-react'
import { cn } from '../../lib/cn'
import { useT } from '../../lib/i18n'

/**
 * Placeholder толық көрінуі үшін өрістің ең аз ені: 14px Inter-де кириллица әрпі орта есеппен 7.9px-тен аспайды,
 * оған белгі, аралық, тазалау батырмасы мен шеттер (≈ 64px) қосылады.
 */
const minWidthFor = (placeholder: string) => Math.ceil(placeholder.length * 7.9 + 64)

/** Өріс тар болса (телефон) — қысқа «Поиск», әйтпесе толық подсказка. ref — өлшенетін элемент. */
export function useFitPlaceholder(placeholder: string, ref: RefObject<HTMLElement | null>, extra = 64): string {
  const t = useT()
  const [narrow, setNarrow] = useState(false)
  useLayoutEffect(() => {
    const el = ref.current
    if (!el) return
    const check = () => setNarrow(el.clientWidth + 1 < Math.ceil(placeholder.length * 7.9 + extra))
    check()
    const ro = new ResizeObserver(check)
    ro.observe(el)
    return () => ro.disconnect()
  }, [placeholder, ref, extra])
  return narrow ? t('common.search') : placeholder
}

export function SearchInput({ value, onChange, placeholder, className = '' }: {
  value: string
  onChange: (value: string) => void
  placeholder: string
  className?: string
}) {
  const t = useT()
  const ref = useRef<HTMLLabelElement>(null)
  const [narrow, setNarrow] = useState(false)
  // Тар экранда (телефон) ұзын подсказка сыймайды — қысқасын көрсетеміз, өріс өзі контейнер енінде қалады.
  useLayoutEffect(() => {
    const el = ref.current
    if (!el) return
    const check = () => setNarrow(el.clientWidth + 1 < minWidthFor(placeholder))
    check()
    const ro = new ResizeObserver(check)
    ro.observe(el)
    return () => ro.disconnect()
  }, [placeholder])
  return (
    <label ref={ref} className={cn('flex h-11 items-center gap-2.5 rounded-xl border border-line bg-surface px-3.5 focus-within:border-brand', className)}
      style={{ minWidth: `min(100%, ${minWidthFor(placeholder)}px)` }}>
      <Search size={18} className="shrink-0 text-ink-3" />
      <input value={value} onChange={(e) => onChange(e.target.value)} placeholder={narrow ? t('common.search') : placeholder} aria-label={placeholder}
        className="min-w-0 flex-1 bg-transparent text-[14px] outline-none placeholder:text-ink-3" />
      {value && (
        <button type="button" aria-label="Clear" onClick={() => onChange('')} className="text-ink-3 hover:text-ink">
          <X size={16} />
        </button>
      )}
    </label>
  )
}
