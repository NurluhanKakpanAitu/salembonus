import { create } from 'zustand'
import { getLanguage, useLanguageStore } from './language'
import type { StaffMe, StaffSession, StaffStore } from './types'

const STORE_KEY = 'salempos.store'

type Status = 'loading' | 'guest' | 'authed'

interface AuthState {
  status: Status
  /** Тек жадта: бет жаңарса, httpOnly cookie-дегі refresh арқылы қайта алынады. */
  accessToken: string | null
  me: StaffMe | null
  storeId: string | null
}

export const useAuth = create<AuthState>(() => ({
  status: 'loading',
  accessToken: null,
  me: null,
  storeId: readStoreId(),
}))

function readStoreId() {
  try {
    return localStorage.getItem(STORE_KEY)
  } catch {
    return null
  }
}

function saveStoreId(id: string | null) {
  try {
    if (id) localStorage.setItem(STORE_KEY, id)
    else localStorage.removeItem(STORE_KEY)
  } catch {
    /* сақталмаса — келесі жолы бірінші дүкен ашылады */
  }
}

/** Сеансты қабылдау: токен, профиль, белсенді дүкен және қызметкердің тілі. */
export function applySession(session: StaffSession) {
  applyMe(session.me)
  useAuth.setState({ status: 'authed', accessToken: session.accessToken })
}

export function applyMe(me: StaffMe) {
  const current = useAuth.getState().storeId
  const storeId = me.stores.some((s) => s.id === current) ? current : (me.stores[0]?.id ?? null)
  saveStoreId(storeId)
  useAuth.setState({ me, storeId })
  if (me.language !== getLanguage()) useLanguageStore.getState().set(me.language)
}

export function setActiveStore(id: string) {
  saveStoreId(id)
  useAuth.setState({ storeId: id })
}

export function clearSession() {
  useAuth.setState({ status: 'guest', accessToken: null, me: null })
}

let refreshing: Promise<boolean> | null = null

/** Cookie-дегі refresh токенмен жаңа access токен. Бір уақытта бір ғана сұраныс жүреді. */
export function refreshSession(): Promise<boolean> {
  refreshing ??= (async () => {
    try {
      const res = await fetch('/api/staff/v1/auth/refresh', {
        method: 'POST',
        headers: { 'Accept-Language': getLanguage() },
      })
      if (!res.ok) {
        clearSession()
        return false
      }
      applySession((await res.json()) as StaffSession)
      return true
    } catch {
      clearSession()
      return false
    } finally {
      refreshing = null
    }
  })()
  return refreshing
}

export const activeStore = (s: AuthState): StaffStore | null =>
  s.me?.stores.find((x) => x.id === s.storeId) ?? null

export const can = (store: StaffStore | null, permission: string) =>
  !!store && (store.role === 'Owner' || store.permissions.includes(permission))
