import { useEffect, useState } from 'react'

interface BeforeInstallPromptEvent extends Event {
  prompt: () => Promise<void>
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>
}

const DISMISS_KEY = 'salembonus-install-dismissed'
const DISMISS_DAYS = 7

let deferredPrompt: BeforeInstallPromptEvent | null = null
let installedNow = false
const listeners = new Set<() => void>()
const notify = () => listeners.forEach((l) => l())

if (typeof window !== 'undefined') {
  window.addEventListener('beforeinstallprompt', (e) => {
    e.preventDefault()
    deferredPrompt = e as BeforeInstallPromptEvent
    notify()
  })
  window.addEventListener('appinstalled', () => {
    deferredPrompt = null
    installedNow = true
    notify()
  })
}

export const isStandalone = () =>
  window.matchMedia('(display-mode: standalone)').matches ||
  (navigator as Navigator & { standalone?: boolean }).standalone === true

const ua = () => navigator.userAgent

export const isIos = () => /iphone|ipad|ipod/i.test(ua()) && !(window as Window & { MSStream?: unknown }).MSStream

/** Нағыз Safari (Chrome/Instagram/Telegram ішкі браузерлері емес). Тек Safari орнатуға рұқсат береді. */
export const isIosSafari = () =>
  isIos() && /safari/i.test(ua()) && !/crios|fxios|edgios|opios|instagram|fban|fbav|telegram|line\//i.test(ua())

export const isMobile = () => /android|iphone|ipad|ipod|mobile/i.test(ua()) || navigator.maxTouchPoints > 1

const isDismissed = () => {
  try {
    const t = Number(localStorage.getItem(DISMISS_KEY) ?? 0)
    return Date.now() - t < DISMISS_DAYS * 86_400_000
  } catch {
    return false
  }
}

export type InstallMode = 'none' | 'prompt' | 'ios'

export function useInstallPrompt() {
  const [, rerender] = useState(0)
  const [dismissed, setDismissed] = useState(isDismissed)

  useEffect(() => {
    const l = () => rerender((n) => n + 1)
    listeners.add(l)
    return () => {
      listeners.delete(l)
    }
  }, [])

  let mode: InstallMode = 'none'
  if (!isStandalone() && !dismissed) {
    if (deferredPrompt) mode = 'prompt'
    else if (isIos()) mode = 'ios'
  }

  const install = async () => {
    if (!deferredPrompt) return
    await deferredPrompt.prompt()
    const { outcome } = await deferredPrompt.userChoice
    if (outcome === 'accepted') deferredPrompt = null
    rerender((n) => n + 1)
  }

  const dismiss = () => {
    try {
      localStorage.setItem(DISMISS_KEY, String(Date.now()))
    } catch {
      /* ignore */
    }
    setDismissed(true)
  }

  return { mode, install, dismiss, installed: installedNow }
}
