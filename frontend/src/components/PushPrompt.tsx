import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import { BellRing } from 'lucide-react'
import { enablePush, initPush, postponePush, syncPushLanguage } from '../lib/push'
import { useLanguage } from '../lib/language'
import { useT } from '../lib/i18n'

/**
 * Кірген соң push-ты қосу (тек App Store / Google Play қосымшасында). Рұқсат әлі сұралмаса — алдымен неге
 * керегін түсіндіретін парақ, сосын ғана жүйенің терезесі: «Рұқсат етпеу» бір басылса, қайта сұрау қиын.
 */
export function PushPrompt() {
  const t = useT()
  const [lang] = useLanguage()
  const navigate = useNavigate()
  const qc = useQueryClient()
  const [open, setOpen] = useState(false)

  useEffect(() => {
    void initPush({
      onReceived: () => {
        void qc.invalidateQueries({ queryKey: ['notifications'] })
        void qc.invalidateQueries({ queryKey: ['cards'] })
      },
      onOpen: (path) => navigate(path),
    })
      .then((state) => setOpen(state === 'ask'))
      .catch(() => undefined)
    // Бір рет: тыңдаушылар қосымшаның бүкіл өміріне.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  // Тіл ауысса, сервер push-ты жаңа тілде жіберсін (бірінші рендерде — initPush өзі тіркейді).
  const [initialLang] = useState(lang)
  useEffect(() => {
    if (lang !== initialLang) syncPushLanguage()
  }, [lang, initialLang])

  useEffect(() => {
    if (!open) return
    const onKey = (e: KeyboardEvent) => {
      if (e.key !== 'Escape') return
      postponePush()
      setOpen(false)
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [open])

  if (!open) return null

  const later = () => {
    postponePush()
    setOpen(false)
  }
  const allow = () => {
    setOpen(false)
    void enablePush()
  }

  return (
    <div className="fixed inset-0 z-50 flex items-end justify-center bg-black/50" onClick={later} role="dialog" aria-modal="true">
      <div
        className="w-full max-w-[480px] rounded-t-[28px] bg-surface px-5 pb-[max(20px,env(safe-area-inset-bottom))] pt-3"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="mx-auto mb-4 h-1 w-10 rounded-full bg-muted" />
        <div className="mx-auto mb-3 flex size-12 items-center justify-center rounded-full bg-brand-soft text-brand">
          <BellRing size={22} />
        </div>
        <h2 className="text-center text-[18px] font-extrabold">{t('push.title')}</h2>
        <p className="mt-2 text-center text-[14px] leading-snug text-ink-2">{t('push.body')}</p>
        <button
          type="button"
          onClick={allow}
          className="mt-5 flex h-13 w-full items-center justify-center rounded-2xl bg-brand text-[15px] font-bold text-white active:scale-[0.99]"
        >
          {t('push.allow')}
        </button>
        <button
          type="button"
          onClick={later}
          className="mt-2 flex h-13 w-full items-center justify-center rounded-2xl text-[15px] font-semibold text-ink-2"
        >
          {t('push.later')}
        </button>
      </div>
    </div>
  )
}
