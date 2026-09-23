import { useState, type ReactNode } from 'react'
import { QRCodeSVG } from 'qrcode.react'
import { Check, Copy, Download, ExternalLink, Share, SquarePlus } from 'lucide-react'
import { LogoMark, Wordmark } from './Brand'
import { isIos, isIosSafari, isMobile, isStandalone, useInstallPrompt } from '../lib/install'

const GATE_ENABLED = import.meta.env.VITE_INSTALL_GATE !== 'false'
const BYPASS_KEY = 'salembonus-web-mode'

/**
 * Орнату қақпасы: телефонда қосымша орнатылмағанша сайтты көрсетпейді.
 * Компьютерде QR көрсетеді, ?web=1 параметрі тест үшін қақпаны айналып өтеді.
 */
export function InstallGate({ children }: { children: ReactNode }) {
  const { mode, install, installed } = useInstallPrompt()
  const [bypass, setBypass] = useState(() => {
    try {
      if (new URLSearchParams(location.search).get('web') === '1') {
        sessionStorage.setItem(BYPASS_KEY, '1')
        return true
      }
      return sessionStorage.getItem(BYPASS_KEY) === '1'
    } catch {
      return false
    }
  })

  if (!GATE_ENABLED || bypass || isStandalone()) return <>{children}</>

  if (!isMobile()) return <DesktopGate onContinue={() => setBypass(true)} />

  if (installed) return <InstalledScreen />

  if (isIos()) return isIosSafari() ? <IosGate /> : <OpenInSafariGate />

  return <AndroidGate canPrompt={mode === 'prompt'} onInstall={install} onContinue={() => setBypass(true)} />
}

function Shell({ children }: { children: ReactNode }) {
  return (
    <div className="mx-auto flex min-h-full max-w-[480px] flex-col items-center bg-bg px-6 pb-[max(28px,env(safe-area-inset-bottom))] pt-[max(48px,env(safe-area-inset-top))] text-center">
      <Wordmark height={28} />
      <div className="mt-10 flex w-full flex-1 flex-col items-center">{children}</div>
    </div>
  )
}

function Hero({ title, text }: { title: string; text: string }) {
  return (
    <>
      <div className="rounded-[28px] bg-white p-5 shadow-sm">
        <LogoMark size={88} />
      </div>
      <h1 className="mt-6 text-[24px] font-extrabold leading-tight tracking-tight">{title}</h1>
      <p className="mt-2 max-w-[320px] text-sm text-ink-2">{text}</p>
    </>
  )
}

function AndroidGate({ canPrompt, onInstall, onContinue }: { canPrompt: boolean; onInstall: () => void; onContinue: () => void }) {
  const [manual, setManual] = useState(false)
  const click = () => (canPrompt ? onInstall() : setManual(true))
  return (
    <Shell>
      <Hero title="SalemBonus қосымшасын орнатыңыз" text="Бір басу жеткілікті. Орнатылған соң бонустарыңыз бен QR кодыңыз әрқашан басты экранда болады." />
      <button type="button" onClick={click} className="mt-8 flex h-14 w-full items-center justify-center gap-2 rounded-2xl bg-brand text-[15px] font-semibold text-white active:scale-[0.99]">
        <Download size={20} /> Қосымшаны орнату
      </button>
      <p className="mt-4 text-xs text-ink-3">Тегін, 1 МБ-тан аз, Play Market қажет емес</p>
      {manual && (
        <div className="mt-6 w-full rounded-2xl bg-surface p-4 text-left text-sm">
          <div className="font-bold">Қолмен орнату</div>
          <p className="mt-1 text-ink-2">Chrome мәзірін (жоғарғы оң жақтағы <b>⋮</b>) ашып, <b>«Қосымшаны орнату»</b> немесе <b>«Басты экранға қосу»</b> таңдаңыз.</p>
          <button type="button" onClick={onContinue} className="mt-3 text-sm font-medium text-ink-2 underline">Браузерде жалғастыру</button>
        </div>
      )}
    </Shell>
  )
}

function InstalledScreen() {
  return (
    <Shell>
      <div className="flex size-20 items-center justify-center rounded-full bg-green-soft text-green">
        <Check size={40} />
      </div>
      <h1 className="mt-6 text-[24px] font-extrabold leading-tight tracking-tight">Орнатылды!</h1>
      <p className="mt-2 max-w-[320px] text-sm text-ink-2">
        Басты экранда <b>SalemBonus</b> иконкасы пайда болды. Қосымшаны сол жерден ашыңыз.
      </p>
      <a href="/" className="mt-8 flex h-14 w-full items-center justify-center gap-2 rounded-2xl bg-brand text-[15px] font-semibold text-white">
        <ExternalLink size={20} /> Қосымшаны ашу
      </a>
    </Shell>
  )
}

function IosGate() {
  return (
    <Shell>
      <Hero title="SalemBonus қосымшасын орнатыңыз" text="iPhone-да орнату үш қадам, 10 секунд." />
      <ol className="mt-8 flex w-full flex-col gap-3 text-left text-sm">
        <Step n={1} icon={<Share size={18} />}>Төмендегі <b>Бөлісу</b> батырмасын басыңыз</Step>
        <Step n={2} icon={<SquarePlus size={18} />}><b>«Басты экранға қосу»</b> таңдаңыз</Step>
        <Step n={3} icon={<LogoMark size={22} />}>Оң жақ жоғарыда <b>Қосу</b> басыңыз, сосын басты экраннан ашыңыз</Step>
      </ol>
      <div className="mt-auto pt-8 text-ink-3">
        <Share size={28} className="mx-auto animate-bounce text-brand" />
        <div className="mt-1 text-xs">Бөлісу батырмасы төменде</div>
      </div>
    </Shell>
  )
}

function OpenInSafariGate() {
  const [copied, setCopied] = useState(false)
  const url = location.origin
  const copy = async () => {
    try {
      await navigator.clipboard.writeText(url)
      setCopied(true)
    } catch {
      /* ignore */
    }
  }
  return (
    <Shell>
      <Hero title="Safari-де ашыңыз" text="iPhone-да қосымшаны тек Safari браузері арқылы орнатуға болады. Сілтемені көшіріп, Safari-ге қойыңыз." />
      <div className="mt-8 w-full rounded-2xl border border-line bg-surface px-4 py-3 text-sm font-semibold">{url}</div>
      <button type="button" onClick={copy} className="mt-3 flex h-14 w-full items-center justify-center gap-2 rounded-2xl bg-brand text-[15px] font-semibold text-white">
        {copied ? <><Check size={20} /> Көшірілді</> : <><Copy size={20} /> Сілтемені көшіру</>}
      </button>
    </Shell>
  )
}

function DesktopGate({ onContinue }: { onContinue: () => void }) {
  const url = location.origin
  return (
    <Shell>
      <Hero title="Телефоныңызбен сканерлеңіз" text="SalemBonus телефонға арналған. QR кодты камерамен сканерлеп, қосымшаны орнатыңыз." />
      <div className="mt-8 rounded-3xl border border-line bg-white p-4">
        <QRCodeSVG value={url} size={180} level="M" />
      </div>
      <button type="button" onClick={onContinue} className="mt-8 text-sm font-medium text-ink-2 underline">
        Браузерде жалғастыру
      </button>
    </Shell>
  )
}

function Step({ n, icon, children }: { n: number; icon: ReactNode; children: ReactNode }) {
  return (
    <li className="flex items-center gap-3 rounded-2xl bg-surface p-3">
      <span className="flex size-9 shrink-0 items-center justify-center rounded-xl bg-brand-soft text-brand">{icon}</span>
      <span className="flex-1"><span className="mr-1 text-ink-3">{n}.</span>{children}</span>
    </li>
  )
}
