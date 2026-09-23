import { useState } from 'react'
import { Download, Share, SquarePlus, X } from 'lucide-react'
import { useInstallPrompt } from '../lib/install'
import { LogoMark } from './Brand'

/** Қосымшаны басты экранға орнату баннері. Android: бір батырма, iOS: нұсқаулық. */
export function InstallBanner() {
  const { mode, install, dismiss } = useInstallPrompt()
  const [iosOpen, setIosOpen] = useState(false)

  if (mode === 'none') return null

  return (
    <>
      <div className="flex items-center gap-3 rounded-2xl bg-ink px-3.5 py-3 text-white">
        <LogoMark size={40} />
        <div className="min-w-0 flex-1">
          <div className="text-sm font-bold leading-tight">Қосымшаны орнатыңыз</div>
          <div className="text-xs text-white/70">Басты экраннан бір басып ашасыз, QR әрқашан қолда</div>
        </div>
        <button
          type="button"
          onClick={mode === 'prompt' ? install : () => setIosOpen(true)}
          className="flex shrink-0 items-center gap-1.5 rounded-xl bg-brand px-3 py-2 text-xs font-semibold text-white"
        >
          <Download size={14} /> Орнату
        </button>
        <button type="button" aria-label="Жабу" onClick={dismiss} className="shrink-0 text-white/60">
          <X size={18} />
        </button>
      </div>

      {iosOpen && (
        <div className="fixed inset-0 z-50 flex items-end justify-center bg-black/50" onClick={() => setIosOpen(false)} role="dialog" aria-modal="true">
          <div className="w-full max-w-[480px] rounded-t-[28px] bg-surface px-6 pb-[max(28px,env(safe-area-inset-bottom))] pt-3" onClick={(e) => e.stopPropagation()}>
            <div className="mx-auto mb-4 h-1 w-10 rounded-full bg-gray-300" />
            <div className="text-lg font-bold">iPhone-ға орнату</div>
            <ol className="mt-4 flex flex-col gap-3 text-sm">
              <li className="flex items-center gap-3">
                <span className="flex size-9 shrink-0 items-center justify-center rounded-xl bg-brand-soft text-brand"><Share size={18} /></span>
                <span>Safari-дің төменгі панелінде <b>Бөлісу</b> батырмасын басыңыз</span>
              </li>
              <li className="flex items-center gap-3">
                <span className="flex size-9 shrink-0 items-center justify-center rounded-xl bg-brand-soft text-brand"><SquarePlus size={18} /></span>
                <span>Тізімнен <b>«Басты экранға қосу»</b> (Add to Home Screen) таңдаңыз</span>
              </li>
              <li className="flex items-center gap-3">
                <span className="flex size-9 shrink-0 items-center justify-center rounded-xl bg-brand-soft text-brand"><LogoMark size={22} /></span>
                <span>Жоғарғы оң жақтағы <b>Қосу</b> батырмасын басыңыз, SalemBonus басты экранда пайда болады</span>
              </li>
            </ol>
            <p className="mt-4 text-xs text-ink-3">Chrome немесе басқа браузерден ашсаңыз, алдымен сілтемені Safari-де ашыңыз.</p>
            <button type="button" onClick={() => setIosOpen(false)} className="mt-5 h-12 w-full rounded-2xl bg-brand text-sm font-semibold text-white">
              Түсінікті
            </button>
          </div>
        </div>
      )}
    </>
  )
}
