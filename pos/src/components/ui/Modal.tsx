import { useEffect, type ReactNode } from 'react'
import { createPortal } from 'react-dom'
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

  // body-ге portal: модал беттің <form>-ы ішінде тұрса, ішкі форма ішкі болып қалады (HTML-де рұқсат жоқ) —
  // батырма беттің формасын жіберіп, бет қайта жүктеледі. submit оқиғасы React ағашы бойынша сыртқы формаға да жетпейді.
  return createPortal(
    <div className="fixed inset-0 z-[60] flex items-center justify-center bg-black/45 p-2 sm:p-4" onMouseDown={onClose} onSubmit={(e) => e.stopPropagation()}>
      <div
        role="dialog"
        aria-modal="true"
        aria-label={title}
        className="max-h-[calc(100dvh-1rem)] w-full overflow-y-auto rounded-2xl bg-surface p-4 shadow-xl sm:max-h-[calc(100dvh-2rem)] sm:p-6"
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
    </div>,
    document.body,
  )
}
