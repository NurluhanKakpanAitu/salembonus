import { useEffect } from 'react'
import { create } from 'zustand'
import { AlertTriangle, HelpCircle } from 'lucide-react'
import { Button } from './Button'
import { useT } from '../../lib/i18n'

export interface ConfirmOptions {
  /** Тақырып; бос болса — «Подтвердите действие». */
  title?: string
  message: string
  /** Растау батырмасының мәтіні; бос болса — «Подтвердить». */
  confirmLabel?: string
  cancelLabel?: string
  /** Қайтарылмайтын не қауіпті әрекет: қызыл батырма мен ескерту белгісі. */
  danger?: boolean
}

interface Pending extends ConfirmOptions {
  resolve: (ok: boolean) => void
}

const useConfirmStore = create<{ pending: Pending | null }>(() => ({ pending: null }))

/**
 * Браузердің `window.confirm` орнына сайт стиліндегі диалог (ТЗ «Товар» §4: растау — модалды терезе).
 * Кез келген жерден шақырылады, нәтижесі — Promise<boolean>. Escape мен фонды басу — «жоқ».
 */
export function confirmDialog(options: ConfirmOptions | string): Promise<boolean> {
  const o = typeof options === 'string' ? { message: options } : options
  // Алдыңғысы әлі ашық болса — ол «жоқ» деп жабылады: екі диалог қатар тұрмайды.
  useConfirmStore.getState().pending?.resolve(false)
  return new Promise((resolve) => useConfirmStore.setState({ pending: { ...o, resolve } }))
}

export function ConfirmHost() {
  const t = useT()
  const pending = useConfirmStore((s) => s.pending)

  const close = (ok: boolean) => {
    pending?.resolve(ok)
    useConfirmStore.setState({ pending: null })
  }

  useEffect(() => {
    if (!pending) return
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        e.stopPropagation()
        close(false)
      }
    }
    // capture: астындағы Modal да Escape-ті тыңдайды — алдымен растау диалогы жабылсын.
    window.addEventListener('keydown', onKey, true)
    return () => window.removeEventListener('keydown', onKey, true)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [pending])

  if (!pending) return null
  const Icon = pending.danger ? AlertTriangle : HelpCircle
  return (
    <div className="fixed inset-0 z-[80] flex items-center justify-center bg-black/45 p-4" onMouseDown={() => close(false)}>
      <div role="alertdialog" aria-modal="true" aria-label={pending.title ?? t('confirm.title')}
        className="w-full max-w-[460px] rounded-2xl bg-surface p-6 shadow-xl" onMouseDown={(e) => e.stopPropagation()}>
        <div className="flex items-start gap-4">
          <span className={`flex size-12 shrink-0 items-center justify-center rounded-2xl ${pending.danger ? 'bg-danger-soft text-danger' : 'bg-brand-soft text-brand'}`}>
            <Icon size={24} />
          </span>
          <div className="min-w-0 flex-1">
            <h2 className="text-[18px] font-bold leading-snug">{pending.title ?? t('confirm.title')}</h2>
            <p className="mt-1.5 text-[14px] leading-relaxed text-ink-2">{pending.message}</p>
          </div>
        </div>
        <div className="mt-6 flex flex-wrap justify-end gap-2">
          <Button variant="secondary" onClick={() => close(false)}>{pending.cancelLabel ?? t('common.cancel')}</Button>
          <Button variant={pending.danger ? 'danger' : 'primary'} autoFocus onClick={() => close(true)}>
            {pending.confirmLabel ?? t('common.confirm')}
          </Button>
        </div>
      </div>
    </div>
  )
}
