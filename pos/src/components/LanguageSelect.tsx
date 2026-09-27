import { useEffect, useRef, useState } from 'react'
import { Check, ChevronDown } from 'lucide-react'
import type { Language } from '../lib/language'
import { useT } from '../lib/i18n'

const LANGS: { value: Language; short: string }[] = [
  { value: 'ru', short: 'RU' },
  { value: 'kk', short: 'KZ' },
]

/** Шағын тіл ауыстырғыш (ТЗ §15.2): жоғарғы бұрышта, мобильдіде де көрінеді. */
export function LanguageSelect({ value, onChange, tone = 'dark' }: {
  value: Language
  onChange: (lang: Language) => void
  tone?: 'dark' | 'light'
}) {
  const t = useT()
  const [open, setOpen] = useState(false)
  const ref = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!open) return
    const close = (e: MouseEvent) => !ref.current?.contains(e.target as Node) && setOpen(false)
    document.addEventListener('mousedown', close)
    return () => document.removeEventListener('mousedown', close)
  }, [open])

  const current = LANGS.find((l) => l.value === value)!

  return (
    <div ref={ref} className="relative">
      <button
        type="button"
        onClick={() => setOpen((o) => !o)}
        aria-haspopup="listbox"
        aria-expanded={open}
        className={`flex h-10 items-center gap-2 rounded-xl border px-3.5 text-[14px] font-semibold ${
          tone === 'light'
            ? 'border-white/15 bg-white/10 text-white hover:bg-white/15'
            : 'border-line bg-surface text-ink hover:bg-field'
        }`}
      >
        {current.short}
        <ChevronDown size={16} />
      </button>
      {open && (
        <ul role="listbox" className="absolute right-0 z-20 mt-2 w-44 overflow-hidden rounded-xl border border-line bg-surface py-1 shadow-lg">
          {LANGS.map((l) => (
            <li key={l.value}>
              <button
                type="button"
                role="option"
                aria-selected={l.value === value}
                onClick={() => {
                  onChange(l.value)
                  setOpen(false)
                }}
                className="flex w-full items-center justify-between px-3.5 py-2.5 text-left text-[14px] text-ink hover:bg-field"
              >
                {t(`lang.${l.value}`)}
                {l.value === value && <Check size={16} className="text-brand" />}
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
