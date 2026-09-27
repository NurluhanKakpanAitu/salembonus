import { useEffect } from 'react'
import { Check } from 'lucide-react'

export interface SheetOption<T extends string> {
  value: T
  label: string
}

/** Төменнен шығатын қарапайым таңдау парағы. */
export function OptionSheet<T extends string>({
  open,
  title,
  options,
  value,
  onSelect,
  onClose,
}: {
  open: boolean
  title: string
  options: SheetOption<T>[]
  value: T
  onSelect: (value: T) => void
  onClose: () => void
}) {
  useEffect(() => {
    if (!open) return
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && onClose()
    window.addEventListener('keydown', onKey)
    document.body.style.overflow = 'hidden'
    return () => {
      window.removeEventListener('keydown', onKey)
      document.body.style.overflow = ''
    }
  }, [open, onClose])

  if (!open) return null

  return (
    <div
      className="fixed inset-0 z-50 flex items-end justify-center bg-black/50"
      onClick={onClose}
      role="dialog"
      aria-modal="true"
    >
      <div
        className="w-full max-w-[480px] rounded-t-[28px] bg-surface px-4 pb-[max(20px,env(safe-area-inset-bottom))] pt-3"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="mx-auto mb-3 h-1 w-10 rounded-full bg-muted" />
        <h2 className="mb-2 px-1 text-[15px] font-bold">{title}</h2>
        <ul className="flex flex-col">
          {options.map((o, i) => (
            <li key={o.value}>
              <button
                type="button"
                onClick={() => {
                  onSelect(o.value)
                  onClose()
                }}
                className={`flex w-full items-center justify-between py-3.5 text-left text-[15px] ${
                  i < options.length - 1 ? 'border-b border-line' : ''
                } ${o.value === value ? 'font-bold text-brand' : ''}`}
              >
                {o.label}
                {o.value === value && <Check size={18} />}
              </button>
            </li>
          ))}
        </ul>
      </div>
    </div>
  )
}
