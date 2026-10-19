import { useEffect, useMemo, useRef, useState } from 'react'
import { Check, Plus, X } from 'lucide-react'
import { Input } from '../ui/Field'
import type { Brand } from '../../lib/catalogTypes'
import { useT } from '../../lib/i18n'

/**
 * Бренд — қолмен теріледі (макет: «Coca-Cola ×»), теру кезінде анықтамалықтан ұсыныстар шығады.
 * Тізімнен таңдалса — brandId; жаңа атау терілсе — brandName, сақтағанда сервер брендті анықтамалыққа
 * өзі қосады. Архивтегі бренд жаңа таңдауға ұсынылмайды, бірақ бұрын таңдалғаны көрінеді.
 */
export function BrandCombobox({ brands, brandId, brandName, onChange, error }: {
  brands: Brand[]
  brandId: string
  brandName: string
  onChange: (next: { brandId: string; brandName: string }) => void
  error?: boolean
}) {
  const t = useT()
  const selected = brandId ? brands.find((b) => b.id === brandId) : undefined
  const text = selected?.name ?? brandName
  const [open, setOpen] = useState(false)
  const [active, setActive] = useState(0)
  const ref = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!open) return
    const close = (e: MouseEvent) => !ref.current?.contains(e.target as Node) && setOpen(false)
    document.addEventListener('mousedown', close)
    return () => document.removeEventListener('mousedown', close)
  }, [open])

  const q = text.trim().toLowerCase()
  const matches = useMemo(
    () => brands.filter((b) => b.status === 'Active' && (!q || b.name.toLowerCase().includes(q)))
      .sort((a, b) => a.name.localeCompare(b.name)).slice(0, 8),
    [brands, q],
  )
  const exact = q ? brands.find((b) => b.name.toLowerCase() === q) : undefined
  const isNew = !!q && !selected && !exact

  const pick = (b: Brand) => {
    onChange({ brandId: b.id, brandName: b.name })
    setOpen(false)
  }
  const type = (value: string) => {
    onChange({ brandId: '', brandName: value })
    setOpen(true)
    setActive(0)
  }
  // Өрістен шыққанда терілген атау бар брендпен дәл сәйкес келсе — соны аламыз (жаңа жасалмайды).
  const blur = () => {
    if (!selected && exact) onChange({ brandId: exact.id, brandName: exact.name })
  }
  const onKey = (e: React.KeyboardEvent) => {
    if (!open || matches.length === 0) return
    if (e.key === 'ArrowDown') { e.preventDefault(); setActive((i) => Math.min(i + 1, matches.length - 1)) }
    else if (e.key === 'ArrowUp') { e.preventDefault(); setActive((i) => Math.max(i - 1, 0)) }
    else if (e.key === 'Enter') { e.preventDefault(); pick(matches[active]) }
    else if (e.key === 'Escape') setOpen(false)
  }

  return (
    <div ref={ref} className="relative">
      <div className="relative">
        <Input value={text} placeholder={t('product.brandPlaceholder')} error={error} maxLength={100} autoComplete="off"
          onChange={(e) => type(e.target.value)} onFocus={() => setOpen(true)} onBlur={blur} onKeyDown={onKey}
          className={text ? 'pr-10' : ''} />
        {text && (
          <button type="button" aria-label="Clear" onMouseDown={(e) => e.preventDefault()} onClick={() => onChange({ brandId: '', brandName: '' })}
            className="absolute right-3 top-1/2 -translate-y-1/2 text-ink-3 hover:text-ink"><X size={16} /></button>
        )}
      </div>
      {open && (matches.length > 0 || isNew) && (
        <ul className="absolute inset-x-0 top-full z-20 mt-1 max-h-64 overflow-y-auto rounded-xl border border-line bg-surface py-1 shadow-lg">
          {matches.map((b, i) => (
            <li key={b.id}>
              <button type="button" onMouseDown={(e) => e.preventDefault()} onClick={() => pick(b)}
                className={`flex w-full items-center gap-2 px-3.5 py-2 text-left text-[14px] ${i === active ? 'bg-field' : 'hover:bg-field'}`}>
                <span className="min-w-0 flex-1 truncate">{b.name}</span>
                {b.id === brandId && <Check size={15} className="text-brand" />}
              </button>
            </li>
          ))}
          {isNew && (
            <li className="flex items-center gap-2 border-t border-line px-3.5 py-2 text-[13px] text-ink-2">
              <Plus size={14} className="shrink-0 text-brand" /> {t('product.brandNew', { name: text.trim() })}
            </li>
          )}
        </ul>
      )}
      {isNew && !open && <p className="mt-1 text-[12px] text-ink-3">{t('product.brandNew', { name: text.trim() })}</p>}
    </div>
  )
}
