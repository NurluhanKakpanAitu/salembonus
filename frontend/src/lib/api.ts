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
  balance: number
  level: string
  cashbackPercent: number
  amountToNextLevel: number
}

export const cardsApi = {
  list: () => api<BonusCard[]>('/cards'),
  get: (storeId: string) => api<BonusCard>(`/cards/${storeId}`),
}
