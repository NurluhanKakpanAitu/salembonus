const BASE = import.meta.env.VITE_API_URL ?? '/api'

export async function api<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${BASE}${path}`, {
    credentials: 'include',
    headers: { 'Content-Type': 'application/json', ...init?.headers },
    ...init,
  })
  if (!res.ok) throw new Error(`${res.status} ${res.statusText}`)
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
