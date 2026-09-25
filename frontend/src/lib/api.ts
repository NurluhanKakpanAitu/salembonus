import { useAuth, type AuthTokens } from './auth'
import { getLanguage } from './language'
import { translate } from './i18n'

const BASE = import.meta.env.VITE_API_URL ?? '/api'

export class ApiError extends Error {
  status: number
  constructor(status: number, message: string) {
    super(message)
    this.status = status
  }
}

async function readError(res: Response): Promise<string> {
  try {
    const body = await res.json()
    return body?.detail ?? body?.title ?? `${res.status} ${res.statusText}`
  } catch {
    return `${res.status} ${res.statusText}`
  }
}

let refreshing: Promise<boolean> | null = null

/** Refresh токенмен жаңа жұп алады. Бір уақытта бір ғана refresh жүреді. */
async function tryRefresh(): Promise<boolean> {
  if (refreshing) return refreshing
  refreshing = (async () => {
    const { refreshToken, setTokens, clear } = useAuth.getState()
    if (!refreshToken) return false
    const res = await fetch(`${BASE}/auth/refresh`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken }),
    })
    if (!res.ok) {
      clear()
      return false
    }
    setTokens((await res.json()) as AuthTokens)
    return true
  })().finally(() => {
    refreshing = null
  })
  return refreshing
}

async function request<T>(path: string, init: RequestInit | undefined, retry: boolean): Promise<T> {
  const { accessToken } = useAuth.getState()
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    // Сервер қате мәтіндерін осы тілде қайтарады.
    'Accept-Language': getLanguage(),
    ...(init?.headers as Record<string, string>),
  }
  if (accessToken) headers.Authorization = `Bearer ${accessToken}`

  const res = await fetch(`${BASE}${path}`, { ...init, headers })

  if (res.status === 401 && retry && accessToken) {
    if (await tryRefresh()) return request<T>(path, init, false)
    throw new ApiError(401, translate(getLanguage(), 'auth.loginRequired'))
  }
  if (!res.ok) throw new ApiError(res.status, await readError(res))
  if (res.status === 204) return undefined as T
  return res.json() as Promise<T>
}

export const api = <T>(path: string, init?: RequestInit) => request<T>(path, init, true)

const post = <T>(path: string, body: unknown) => api<T>(path, { method: 'POST', body: JSON.stringify(body) })

export interface RequestCodeResult {
  phone: string
  expiresInSeconds: number
  retryAfterSeconds: number
  devCode: string | null
}

export const authApi = {
  requestCode: (phone: string) => post<RequestCodeResult>('/auth/request-code', { phone }),
  verify: (phone: string, code: string) =>
    post<AuthTokens>('/auth/verify', { phone, code, device: navigator.userAgent.slice(0, 200) }),
  logout: (refreshToken: string) => post<void>('/auth/logout', { refreshToken }),
}

export interface BonusCard {
  storeId: string
  storeName: string
  category: string
  themeColor: string
  icon: string
  balance: number
  level: string
  cashbackPercent: number
  amountToNextLevel: number
  totalSpent: number
  nextLevel: string | null
  nextLevelAmount: number | null
  expiringAmount: number | null
  expiringAt: string | null
}

export type TransactionType = 'Accrual' | 'Redemption' | 'Birthday' | 'Promo' | 'Expiration'

export interface BonusTransaction {
  id: string
  storeName: string
  type: TransactionType
  amount: number
  purchaseAmount: number | null
  createdAt: string
}

export interface TransactionPage {
  items: BonusTransaction[]
  hasMore: boolean
}

export interface KatoNode {
  code: string
  name: string
  level: number
  hasChildren: boolean
}

export interface KatoMatch {
  code: string
  name: string
  level: number
  path: string
}

export interface Customer {
  id: string
  phone: string
  fullName: string
  firstName: string
  lastName: string
  email: string | null
  birthDate: string | null
  avatarUrl: string | null
  katoCode: string | null
  katoPath: KatoNode[]
  isBirthdayToday: boolean
  totalBalance: number
  storeCount: number
}

export interface UpdateProfile {
  firstName: string
  lastName: string | null
  email: string | null
  birthDate: string | null
  katoCode: string | null
}

export const katoApi = {
  children: (parent?: string | null) =>
    api<KatoNode[]>(`/kato/children${parent ? `?parent=${parent}` : ''}`),
  settlements: (parent: string) => api<KatoNode[]>(`/kato/settlements?parent=${parent}`),
  search: (q: string) => api<KatoMatch[]>(`/kato/search?q=${encodeURIComponent(q)}`),
}

export interface QrCode {
  code: string
  payload: string
}

export const cardsApi = {
  list: () => api<BonusCard[]>('/cards'),
  get: (storeId: string) => api<BonusCard>(`/cards/${storeId}`),
}

export const transactionsApi = {
  recent: (take = 20) => api<BonusTransaction[]>(`/transactions/recent?take=${take}`),
  list: (storeId: string | null, skip: number, take: number) =>
    api<TransactionPage>(`/transactions?${storeId ? `storeId=${storeId}&` : ''}skip=${skip}&take=${take}`),
}

export const meApi = {
  get: () => api<Customer>('/me'),
  update: (body: UpdateProfile) => api<Customer>('/me', { method: 'PUT', body: JSON.stringify(body) }),
  setAvatar: (avatarUrl: string | null) =>
    api<Customer>('/me/avatar', { method: 'PUT', body: JSON.stringify({ avatarUrl }) }),
  qr: () => api<QrCode>('/me/qr'),
}

export type NotificationType =
  | 'BonusAccrued' | 'BonusRedeemed' | 'Birthday' | 'Promo' | 'StoreAdded' | 'ProfileUpdated' | 'System'
export type NotificationCategory = 'Bonus' | 'Promo' | 'System' | 'Store'

export interface Notification {
  id: string
  type: NotificationType
  category: NotificationCategory
  title: string
  body: string
  detail: string | null
  /** Мәтін үлгісінің кілті — бар болса, мәтін қосымшаның тілінде құрастырылады. */
  templateKey: string | null
  amount: number | null
  purchaseAmount: number | null
  levelKey: string | null
  storeName: string | null
  storeIcon: string | null
  storeThemeColor: string | null
  isRead: boolean
  createdAt: string
}

export interface NotificationPage {
  items: Notification[]
  hasMore: boolean
}

/** Бір дүкеннің хабарламалары туралы қысқаша. storeId бос болса — жүйелік хабарламалар. */
export interface NotificationStore {
  storeId: string | null
  storeName: string | null
  storeIcon: string | null
  storeThemeColor: string | null
  total: number
  unread: number
  last: Notification
}

export const notificationsApi = {
  list: (category: NotificationCategory | null, skip: number, take: number, source?: string) =>
    api<NotificationPage>(
      `/notifications?${category ? `category=${category}&` : ''}${
        source ? (source === 'system' ? 'system=true&' : `storeId=${source}&`) : ''
      }skip=${skip}&take=${take}`,
    ),
  stores: () => api<NotificationStore[]>('/notifications/stores'),
  unreadCount: () => api<number>('/notifications/unread-count'),
  markRead: (id: string) => api<void>(`/notifications/${id}/read`, { method: 'POST' }),
  markAllRead: () => api<void>('/notifications/read-all', { method: 'POST' }),
}

export interface StoreListItem {
  id: string
  name: string
  category: string
  description: string
  themeColor: string
  icon: string
  cashbackPercent: number
  cashbackMaxPercent: number
  hasCard: boolean
  balance: number
  level: string | null
}

export interface StoreLevel {
  /** Мәртебе кілті: New, Regular, Favorite, Vip. */
  name: string
  fromAmount: number
  cashbackPercent: number
  isCurrent: boolean
}

export interface StoreDetail {
  id: string
  name: string
  category: string
  description: string
  themeColor: string
  icon: string
  photoUrl: string | null
  address: string | null
  phone: string | null
  cashbackPercent: number
  maxRedeemPercent: number
  hasCard: boolean
  balance: number
  level: string | null
  amountToNextLevel: number | null
  levels: StoreLevel[]
}

export interface JoinStoreResult {
  alreadyJoined: boolean
  store: StoreDetail
}

export const storesApi = {
  list: (search?: string) =>
    api<StoreListItem[]>(`/stores${search ? `?search=${encodeURIComponent(search)}` : ''}`),
  get: (id: string) => api<StoreDetail>(`/stores/${id}`),
  join: (code: string) => api<JoinStoreResult>('/stores/join', { method: 'POST', body: JSON.stringify({ code }) }),
}
