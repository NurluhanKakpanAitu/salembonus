import { useEffect, useRef, useState, type ReactNode } from 'react'
import { MoreVertical } from 'lucide-react'

export interface RowMenuItem {
  label: string
  icon?: ReactNode
  danger?: boolean
  onClick: () => void
}

/** Жолдағы «⋮» мәзірі (ТЗ «Товар» §17 контекстік мәзір). */
export function RowMenu({ items }: { items: RowMenuItem[] }) {
  const [open, setOpen] = useState(false)
  const ref = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!open) return
    const close = (e: MouseEvent) => !ref.current?.contains(e.target as Node) && setOpen(false)
    document.addEventListener('mousedown', close)
    return () => document.removeEventListener('mousedown', close)
  }, [open])

  if (items.length === 0) return <span className="inline-block w-8" />

  return (
    <div ref={ref} className="relative inline-block">
      <button type="button" aria-label="Menu" aria-haspopup="menu" onClick={() => setOpen((o) => !o)}
        className="flex size-8 items-center justify-center rounded-lg text-ink-2 hover:bg-field">
        <MoreVertical size={18} />
      </button>
      {open && (
        <ul role="menu" className="absolute right-0 z-30 mt-1 w-56 overflow-hidden rounded-xl border border-line bg-surface py-1 text-left shadow-lg">
          {items.map((item) => (
            <li key={item.label}>
              <button type="button" role="menuitem"
                onClick={() => { setOpen(false); item.onClick() }}
                className={`flex w-full items-center gap-2.5 px-3.5 py-2 text-[14px] hover:bg-field ${item.danger ? 'text-danger' : 'text-ink'}`}>
                {item.icon && <span className={item.danger ? '' : 'text-ink-3'}>{item.icon}</span>}
                {item.label}
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
