import { api } from './api'
import type { PasswordPolicy, ResetRequested, ResetVerified, StaffMe, StaffSession } from './types'

const base = '/staff/v1/auth'

export const staffApi = {
  login: (phone: string, password: string) =>
    api<StaffSession>(`${base}/login`, {
      method: 'POST',
      body: JSON.stringify({ phone, password, device: navigator.userAgent }),
    }),
  logout: () => api<void>(`${base}/logout`, { method: 'POST' }),
  passwordPolicy: () => api<PasswordPolicy>(`${base}/password-policy`),
  requestReset: (phone: string) =>
    api<ResetRequested>(`${base}/password-reset/request`, { method: 'POST', body: JSON.stringify({ phone }) }),
  verifyReset: (phone: string, code: string) =>
    api<ResetVerified>(`${base}/password-reset/verify`, { method: 'POST', body: JSON.stringify({ phone, code }) }),
  completeReset: (resetToken: string, newPassword: string, confirmPassword: string) =>
    api<void>(`${base}/password-reset/complete`, {
      method: 'POST',
      body: JSON.stringify({ resetToken, newPassword, confirmPassword }),
    }),
  setPin: (currentPassword: string, pin: string, confirmPin: string) =>
    api<StaffMe>(`${base}/me/pin`, { method: 'PUT', body: JSON.stringify({ currentPassword, pin, confirmPin }) }),
  changePassword: (currentPassword: string, newPassword: string, confirmPassword: string) =>
    api<void>(`${base}/me/password`, {
      method: 'PUT',
      body: JSON.stringify({ currentPassword, newPassword, confirmPassword }),
    }),
  setLanguage: (language: string) =>
    api<StaffMe>(`${base}/me/language`, { method: 'PUT', body: JSON.stringify({ language }) }),
}
