const BASE = import.meta.env.VITE_API_URL ?? '/api'

export async function api<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${BASE}${path}`, {
    credentials: 'include',
    headers: { 'Content-Type': 'application/json', ...init?.headers },
    ...init,
  })
  if (!res.ok) throw new Error(`${res.status} ${res.statusText}`)
  if (res.status === 204) return undefined as T
  return res.json() as Promise<T>
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

export interface Customer {
  id: string
  phone: string
  fullName: string
  firstName: string
  email: string | null
  birthDate: string | null
  isBirthdayToday: boolean
  topLevel: string
  totalBalance: number
  storeCount: number
}

export const cardsApi = {
  list: () => api<BonusCard[]>('/cards'),
  get: (storeId: string) => api<BonusCard>(`/cards/${storeId}`),
}

export const transactionsApi = {
  recent: (take = 20) => api<BonusTransaction[]>(`/transactions/recent?take=${take}`),
}

export const meApi = {
  get: () => api<Customer>('/me'),
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

export const notificationsApi = {
  list: (category: NotificationCategory | null, skip: number, take: number) =>
    api<NotificationPage>(`/notifications?${category ? `category=${category}&` : ''}skip=${skip}&take=${take}`),
  unreadCount: () => api<number>('/notifications/unread-count'),
  markRead: (id: string) => api<void>(`/notifications/${id}/read`, { method: 'POST' }),
  markAllRead: () => api<void>('/notifications/read-all', { method: 'POST' }),
}
