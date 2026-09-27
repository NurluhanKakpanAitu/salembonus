import { api } from './api'
import type { StaffMe, StaffSession } from './types'

const base = '/staff/v1/auth'

export const staffApi = {
  login: (phone: string, password: string) =>
    api<StaffSession>(`${base}/login`, {
      method: 'POST',
      body: JSON.stringify({ phone, password, device: navigator.userAgent }),
    }),
  logout: () => api<void>(`${base}/logout`, { method: 'POST' }),
  setLanguage: (language: string) =>
    api<StaffMe>(`${base}/me/language`, { method: 'PUT', body: JSON.stringify({ language }) }),
}
