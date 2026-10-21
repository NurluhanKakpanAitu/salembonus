import { Capacitor, registerPlugin } from '@capacitor/core'
import type { PushNotificationsPlugin } from '@capacitor/push-notifications'
import { devicesApi } from './api'
import { isLoggedIn } from './auth'
import { getLanguage } from './language'
import { isNative } from './native'

/**
 * Push-хабарламалар (тек App Store / Google Play қосымшасында). Android-та токенді FCM, iOS-та APNs береді,
 * сервер оны /me/devices-ке сақтап, жаңа хабарлама (бонус, туған күн, акция) жасалғанда жібереді.
 * Сайтта (PWA) push жоқ — бәрі үнсіз өткізіледі.
 */

/** Бэкендтегі FcmClient.ChannelId-мен бірдей. */
const ANDROID_CHANNEL = 'salembonus'
const TOKEN_KEY = 'salembonus-push-token'
const POSTPONED_KEY = 'salembonus-push-postponed'
/** «Кейінірек» басылса, түсіндіру парағы осынша уақыттан кейін ғана қайта шығады. */
const ASK_AGAIN_MS = 14 * 24 * 60 * 60 * 1000

/**
 * Android-та Firebase қосылған ба (google-services.json билдке кірді ме) — MainActivity-дегі PushSupportPlugin.
 * Firebase-сіз push плагинін тіркеу қосымшаны құлатады, сондықтан алдымен осыны сұраймыз.
 */
const PushSupport = registerPlugin<{ check(): Promise<{ available: boolean }> }>('PushSupport')

export interface PushHandlers {
  /** Қосымша ашық тұрғанда push келді — тізімдерді жаңарту. */
  onReceived: () => void
  /** Push басылды — сол дүкеннің хабарламаларына өту. */
  onOpen: (path: string) => void
}

let plugin: Promise<PushNotificationsPlugin | null> | null = null
let listening = false
let handlers: PushHandlers | null = null

const platform = () => (Capacitor.getPlatform() === 'ios' ? 'ios' : 'android')

function readStorage(key: string): string | null {
  try {
    return localStorage.getItem(key)
  } catch {
    return null
  }
}

function writeStorage(key: string, value: string) {
  try {
    localStorage.setItem(key, value)
  } catch {
    // сақтау мүмкін болмаса, келесі ашылғанда қайта сұралады — зиянсыз
  }
}

/** Плагин тек нативті платформада және (Android-та) Firebase бар болса. */
function loadPlugin(): Promise<PushNotificationsPlugin | null> {
  plugin ??= (async () => {
    if (!isNative) return null
    if (platform() === 'android') {
      const { available } = await PushSupport.check().catch(() => ({ available: false }))
      if (!available) return null
    }
    const { PushNotifications } = await import('@capacitor/push-notifications')
    return PushNotifications
  })()
  return plugin
}

async function sendToken(token: string) {
  writeStorage(TOKEN_KEY, token)
  if (!isLoggedIn()) return
  await devicesApi.register({ token, platform: platform(), language: getLanguage() }).catch(() => undefined)
}

async function register(push: PushNotificationsPlugin) {
  if (!listening) {
    listening = true
    // Әр register() сайын (әр ашылғанда) токен қайта келеді — FCM оны ауыстырса, сервер жаңасын алады.
    await push.addListener('registration', ({ value }) => void sendToken(value))
    await push.addListener('registrationError', (e) => console.warn('push registration failed', e.error))
    await push.addListener('pushNotificationReceived', () => handlers?.onReceived())
    await push.addListener('pushNotificationActionPerformed', ({ notification }) => {
      const path = (notification.data as { path?: unknown } | undefined)?.path
      handlers?.onOpen(typeof path === 'string' && path.startsWith('/notifications') ? path : '/notifications')
    })
  }
  if (platform() === 'android') {
    await push
      .createChannel({ id: ANDROID_CHANNEL, name: 'SalemBonus', importance: 4, visibility: 1 })
      .catch(() => undefined)
  }
  await push.register()
}

/**
 * Кірген соң бір рет: рұқсат бұрын берілсе — үнсіз тіркейді. Әлі сұралмаған болса 'ask' қайтарады:
 * жүйенің терезесінен бұрын қосымша неге керегін түсіндіреді (STORE.md 4), сосын enablePush().
 */
export async function initPush(h: PushHandlers): Promise<'ask' | 'done'> {
  handlers = h
  const push = await loadPlugin()
  if (!push) return 'done'
  const { receive } = await push.checkPermissions()
  if (receive === 'granted') {
    await register(push)
    return 'done'
  }
  if (receive === 'denied') return 'done'
  const postponed = Number(readStorage(POSTPONED_KEY) ?? 0)
  return Date.now() - postponed > ASK_AGAIN_MS ? 'ask' : 'done'
}

/** Түсіндіру парағындағы «Рұқсат беру»: жүйе сұрайды, рұқсат болса тіркейді. */
export async function enablePush() {
  const push = await loadPlugin()
  if (!push) return
  const { receive } = await push.requestPermissions()
  if (receive === 'granted') await register(push)
  else writeStorage(POSTPONED_KEY, String(Date.now()))
}

/** «Кейінірек» — біраз уақыт қайта сұрамаймыз. */
export function postponePush() {
  writeStorage(POSTPONED_KEY, String(Date.now()))
}

/** Тіл ауысты — сервер push мәтінін жаңа тілде жіберуі үшін токенді қайта тіркейміз. */
export function syncPushLanguage() {
  const token = readStorage(TOKEN_KEY)
  if (isNative && token && isLoggedIn()) void sendToken(token)
}

/**
 * Шығар алдында (токен әлі жарамды кезде): бұл құрылғыға енді push келмейді. Аккаунт өшірілгенде құрылғыларды
 * сервер өзі өшіреді. Токен құрылғыда қалады — келесі кірген аккаунтқа сол токен тіркеледі.
 */
export async function unregisterPush() {
  const token = readStorage(TOKEN_KEY)
  if (!isNative || !token) return
  await devicesApi.unregister(token).catch(() => undefined)
}
