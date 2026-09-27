import { api } from './api'
import type { Register, RegisterCashier, StaffSession } from './types'

const base = '/pos/v1/registers'

/** Касса (құрылғы). Құрылғы кілті httpOnly cookie-де — JavaScript оны көрмейді. */
export const registerApi = {
  /** Бұл құрылғы касса болса — кассаның мәліметі, әйтпесе null. */
  current: async () => (await api<Register | undefined>(`${base}/current`)) ?? null,
  cashiers: () => api<RegisterCashier[]>(`${base}/current/cashiers`),
  list: () => api<Register[]>(base),
  activate: (id: string) => api<Register>(`${base}/${id}/activate`, { method: 'POST' }),
  deactivate: () => api<void>(`${base}/current/deactivate`, { method: 'POST' }),
  pinLogin: (staffUserId: string, pin: string) =>
    api<StaffSession>(`${base}/current/pin-login`, { method: 'POST', body: JSON.stringify({ staffUserId, pin }) }),
  unlock: (pin: string) => api<void>(`${base}/current/unlock`, { method: 'POST', body: JSON.stringify({ pin }) }),
  lock: () => api<void>(`${base}/current/lock`, { method: 'POST' }),
}
