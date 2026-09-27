import { useEffect, type ReactNode } from 'react'
import { X } from 'lucide-react'

/** Ортадағы модалды терезе: Escape мен фонды басу жабады. */
export function Modal({ title, onClose, children, width = 440 }: {
  title: string
  onClose: () => void
  children: ReactNode
  width?: number
}) {
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && onClose()
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [onClose])

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/45 p-4" onMouseDown={onClose}>
      <div
        role="dialog"
        aria-modal="true"
        aria-label={title}
        className="max-h-[calc(100dvh-2rem)] w-full overflow-y-auto rounded-2xl bg-surface p-6 shadow-xl"
        style={{ maxWidth: width }}
        onMouseDown={(e) => e.stopPropagation()}
      >
        <div className="mb-5 flex items-center justify-between gap-3">
          <h2 className="text-[19px] font-bold">{title}</h2>
          <button type="button" onClick={onClose} aria-label="Close" className="text-ink-3 hover:text-ink">
            <X size={20} />
          </button>
        </div>
        {children}
      </div>
    </div>
  )
}
