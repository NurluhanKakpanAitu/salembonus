import { useEffect, useLayoutEffect, useRef, useState, type ReactNode } from 'react'
import { createPortal } from 'react-dom'
import { MoreVertical } from 'lucide-react'

export interface RowMenuItem {
  label: string
  icon?: ReactNode
  danger?: boolean
  onClick: () => void
}

const MENU_WIDTH = 224
const GAP = 4
const MARGIN = 8

/**
 * Жолдағы «⋮» мәзірі (ТЗ «Товар» §17 контекстік мәзір).
 * Мәзір body-ге (portal) fixed күйде шығады: кестенің overflow-ы оны кеспейді. Астында орын жетпесе —
 * жоғары ашылады; айналдыру не терезе өзгерсе — жабылады.
 */
export function RowMenu({ items }: { items: RowMenuItem[] }) {
  const [open, setOpen] = useState(false)
  const [pos, setPos] = useState<{ top: number; left: number } | null>(null)
  const buttonRef = useRef<HTMLButtonElement>(null)
  const menuRef = useRef<HTMLUListElement>(null)

  useLayoutEffect(() => {
    if (!open || !buttonRef.current || !menuRef.current) return
    const b = buttonRef.current.getBoundingClientRect()
    const h = menuRef.current.offsetHeight
    const below = window.innerHeight - b.bottom - GAP - MARGIN
    const top = below >= h || b.top - GAP - MARGIN < h ? b.bottom + GAP : b.top - GAP - h
    const left = Math.min(Math.max(MARGIN, b.right - MENU_WIDTH), window.innerWidth - MENU_WIDTH - MARGIN)
    setPos({ top: Math.max(MARGIN, top), left })
  }, [open])

  useEffect(() => {
    if (!open) return
    const outside = (e: MouseEvent) => {
      const target = e.target as Node
      if (!menuRef.current?.contains(target) && !buttonRef.current?.contains(target)) setOpen(false)
    }
    const close = () => setOpen(false)
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && setOpen(false)
    document.addEventListener('mousedown', outside)
    document.addEventListener('keydown', onKey)
    window.addEventListener('scroll', close, true)
    window.addEventListener('resize', close)
    return () => {
      document.removeEventListener('mousedown', outside)
      document.removeEventListener('keydown', onKey)
      window.removeEventListener('scroll', close, true)
      window.removeEventListener('resize', close)
    }
  }, [open])

  if (items.length === 0) return <span className="inline-block w-8" />

  return (
    <>
      <button ref={buttonRef} type="button" aria-label="Menu" aria-haspopup="menu" aria-expanded={open}
        onClick={() => { setPos(null); setOpen((o) => !o) }}
        className={`flex size-8 items-center justify-center rounded-lg text-ink-2 hover:bg-field ${open ? 'bg-field' : ''}`}>
        <MoreVertical size={18} />
      </button>
      {open && createPortal(
        <ul ref={menuRef} role="menu" onClick={(e) => e.stopPropagation()} onMouseDown={(e) => e.stopPropagation()}
          style={{ top: pos?.top ?? 0, left: pos?.left ?? 0, width: MENU_WIDTH, visibility: pos ? 'visible' : 'hidden' }}
          className="fixed z-[60] overflow-hidden rounded-xl border border-line bg-surface py-1 text-left shadow-lg">
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
        </ul>,
        document.body,
      )}
    </>
  )
}
