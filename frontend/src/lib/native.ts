import { Capacitor, SystemBars, SystemBarsStyle } from '@capacitor/core'

/**
 * App Store мен Google Play-дегі қосымша (Capacitor). Ядро кішкентай, бірден жүктеледі;
 * плагиндер тек нативті платформада, динамикалық import арқылы алынады.
 */
export const isNative = Capacitor.isNativePlatform()

/** Ашық модаль терезе (парақ, сканер, растау) — Android «артқа» батырмасы алдымен соны жабады. */
const hasOpenDialog = () => !!document.querySelector('[aria-modal="true"]')

/**
 * Қосымша іске қосылғанда бір рет: «артқа» батырмасы, жүйе жолақтарының түсі, splash.
 * `goBack` — роутердің артқа өтуі; тарих бос болса қосымша жабылады.
 */
export async function initNative(goBack: () => boolean) {
  if (!isNative) return
  const [{ App }, { SplashScreen }] = await Promise.all([import('@capacitor/app'), import('@capacitor/splash-screen')])

  await App.addListener('backButton', () => {
    // Барлық модаль Escape-пен жабылады (сайттағыдай) — соны қолданамыз.
    if (hasOpenDialog()) {
      window.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }))
      return
    }
    if (!goBack()) void App.exitApp()
  })

  syncSystemBars()
  // Бет салынып болғанша splash тұрады (capacitor.config.ts: launchAutoHide=false).
  requestAnimationFrame(() => void SplashScreen.hide({ fadeOutDuration: 200 }))
}

/** Жүйе жолақтарының белгілері тақырыпқа сай: ашық фонда — қараңғы, қараңғыда — ашық. */
export function syncSystemBars() {
  if (!isNative) return
  const dark = document.documentElement.dataset.theme === 'dark'
  void SystemBars.setStyle({ style: dark ? SystemBarsStyle.Dark : SystemBarsStyle.Light }).catch(() => undefined)
}

/** Сәтті әрекеттің сезілетін белгісі: iPhone-да navigator.vibrate жоқ, сондықтан нативті діріл. */
export function hapticSuccess() {
  if (!isNative) {
    navigator.vibrate?.(60)
    return
  }
  void import('@capacitor/haptics').then(({ Haptics, NotificationType }) =>
    Haptics.notification({ type: NotificationType.Success }).catch(() => undefined),
  )
}

/**
 * QR кассада көрсетілгенде экран толық жарық болады — сканер күңгірт экранды оқымай қалады.
 * Қайтарылған функция бұрынғы жарықтықты қалпына келтіреді.
 */
export function boostBrightness(): () => void {
  if (!isNative) return () => undefined
  let restore: number | null = null
  let cancelled = false
  void import('@capacitor-community/screen-brightness').then(async ({ ScreenBrightness }) => {
    try {
      const { brightness } = await ScreenBrightness.getBrightness()
      if (cancelled) return
      restore = brightness
      await ScreenBrightness.setBrightness({ brightness: 1 })
    } catch {
      // жарықтықты өзгерту мүмкін болмаса, QR бәрібір көрінеді
    }
  })
  return () => {
    cancelled = true
    if (restore === null) return
    const value = restore
    void import('@capacitor-community/screen-brightness').then(({ ScreenBrightness }) =>
      ScreenBrightness.setBrightness({ brightness: value }).catch(() => undefined),
    )
  }
}
