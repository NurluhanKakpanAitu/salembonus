import { useEffect, type ReactNode } from 'react'
import { X } from 'lucide-react'

/**
 * Оң жақ панель (ТЗ «Бонусы» §1.2: өңдеу формасы ~420–480 px): негізгі бет сәл қараяды, тақырып, «×», айналатын дене,
 * төменде бекітілген батырмалар. Escape мен фонды басу — <paramref name="onClose" /> (сақталмаған өзгерісті шақырушы сұрайды).
 */
export function Drawer({ title, subtitle, onClose, footer, width = 460, children }: {
  title: ReactNode
  subtitle?: ReactNode
  onClose: () => void
  footer?: ReactNode
  width?: number
  children: ReactNode
}) {
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && onClose()
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [onClose])

  return (
    <div className="fixed inset-0 z-50 flex justify-end bg-black/35" onMouseDown={onClose}>
      <aside
        role="dialog"
        aria-modal="true"
        className="flex h-full w-full flex-col bg-surface shadow-2xl"
        style={{ maxWidth: width }}
        onMouseDown={(e) => e.stopPropagation()}
      >
        <header className="flex items-start gap-3 border-b border-line px-5 py-4">
          <div className="min-w-0 flex-1">
            <h2 className="truncate text-[18px] font-bold text-[#0b1f44]">{title}</h2>
            {subtitle && <div className="mt-0.5 text-[13px] text-ink-2">{subtitle}</div>}
          </div>
          <button type="button" onClick={onClose} aria-label="Close" className="rounded-lg p-1 text-ink-3 hover:bg-field hover:text-ink">
            <X size={20} />
          </button>
        </header>
        <div className="min-h-0 flex-1 overflow-y-auto px-5 py-4">{children}</div>
        {footer && <footer className="flex flex-wrap justify-end gap-2 border-t border-line px-5 py-3">{footer}</footer>}
      </aside>
    </div>
  )
}
