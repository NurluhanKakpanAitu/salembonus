import { api } from './api'
import type { StaffRole } from './types'

// ---------- «Сотрудники» ----------

export interface StaffStoreAccess { storeId: string; storeName: string; storeActive: boolean; role: StaffRole; permissions: string[]; maxDiscountPercent: number; isActive: boolean }

export interface StaffRow {
  id: string; firstName: string; lastName: string; phone: string; language: 'ru' | 'kk'; role: StaffRole
  storeIds: string[]; storeNames: string[]; isActive: boolean; hasPin: boolean; hasPassword: boolean; isLocked: boolean
  lastLoginAt: string | null; createdAt: string; sales30: number; revenue30: number; lastSaleAt: string | null; isSelf: boolean; canEdit: boolean
}

export interface StaffSession { id: string; device: string | null; createdAt: string; expiresAt: string; isCurrent: boolean }

export interface StaffDetail { staff: StaffRow; permissions: string[]; maxDiscountPercent: number; access: StaffStoreAccess[]; sessions: StaffSession[] }

export interface StaffList { rows: StaffRow[]; stores: { id: string; name: string; isActive: boolean }[]; isOwner: boolean }

export interface SaveStaff {
  firstName: string; lastName?: string | null; phone: string; language: 'ru' | 'kk'; role: StaffRole
  storeIds: string[]; permissions: string[]; maxDiscountPercent: number; pin?: string | null; generatePassword?: boolean
}

export interface StaffSaved { staff: StaffDetail; tempPassword: string | null }

export interface AuditRow {
  id: string; createdAt: string; action: string; entity: string | null; entityId: string | null
  staffUserId: string | null; staffName: string | null; storeId: string | null; storeName: string | null; registerName: string | null
  success: boolean; details: string | null; oldValues: string | null; newValues: string | null; amount: number | null; ip: string | null
}

export interface AuditPage { rows: AuditRow[]; total: number; actions: string[] }

export interface AuditFilter { staffId?: string | null; storeId?: string | null; action?: string | null; from?: string | null; to?: string | null; page?: number; pageSize?: number }

function qs(params: Record<string, string | number | null | undefined>) {
  const p = new URLSearchParams()
  for (const [k, v] of Object.entries(params)) if (v !== undefined && v !== null && v !== '') p.set(k, String(v))
  const s = p.toString()
  return s ? `?${s}` : ''
}

const S = '/pos/v1/staff'
const post = <T>(path: string, body: unknown = {}) => api<T>(path, { method: 'POST', body: JSON.stringify(body) })
const put = <T>(path: string, body: unknown) => api<T>(path, { method: 'PUT', body: JSON.stringify(body) })

export const staffAdminApi = {
  list: () => api<StaffList>(S),
  get: (id: string) => api<StaffDetail>(`${S}/${id}`),
  create: (r: SaveStaff) => post<StaffSaved>(S, r),
  update: (id: string, r: Partial<SaveStaff>) => put<StaffSaved>(`${S}/${id}`, r),
  setActive: (id: string, active: boolean) => post<StaffDetail>(`${S}/${id}/active`, { active }),
  resetPassword: (id: string) => post<StaffSaved>(`${S}/${id}/reset-password`),
  setPin: (id: string, pin: string) => post<StaffDetail>(`${S}/${id}/pin`, { pin }),
  revokeSessions: (id: string) => post<StaffDetail>(`${S}/${id}/sessions/revoke`),
  audit: (f: AuditFilter) => api<AuditPage>(`${S}/audit${qs({ staffId: f.staffId, storeId: f.storeId, action: f.action, from: f.from, to: f.to, page: f.page, pageSize: f.pageSize })}`),

  // Өз сеанстарым («Настройка → Безопасность») — refresh cookie тек /auth жолында, сондықтан сол контроллерде.
  mySessions: () => api<StaffSession[]>('/staff/v1/auth/me/sessions'),
  revokeMySession: (id: string) => api<void>(`/staff/v1/auth/me/sessions/${id}`, { method: 'DELETE' }),
  revokeOtherSessions: () => post<void>('/staff/v1/auth/me/sessions/revoke-others'),
}

// ---------- «Настройка» ----------

export interface StoreProfile {
  id: string; name: string; category: string; description: string; address: string | null; phone: string | null; katoCode: string | null; timeZone: string
  joinCode: string; organizationName: string; organizationBin: string | null; canEditOrganization: boolean; timeZones: string[]
  /** Елді мекеннің ҚАТО жолы (кодтар) — таңдағыш үшін. */
  katoPath: string[]
}

export interface UpdateStoreProfile {
  name: string; category?: string | null; description?: string | null; address?: string | null; phone?: string | null; katoCode?: string | null; timeZone: string
  organizationName?: string | null; organizationBin?: string | null
}

export interface StoreRow {
  id: string; name: string; category: string; address: string | null; phone: string | null; timeZone: string; isActive: boolean; joinCode: string
  registers: number; staff: number; isCurrent: boolean; createdAt: string
}

export interface CreateStore { name: string; category?: string | null; address?: string | null; phone?: string | null; katoCode?: string | null; timeZone?: string | null }

export interface RegisterRow { id: string; name: string; autoLockMinutes: number | null; isBound: boolean; activatedAt: string | null; isActive: boolean; isThisDevice: boolean; sales30: number; lastSaleAt: string | null }

export interface TransferRecipient { id: string; bankName: string; account: string; holderName: string }

export interface StoreCashierSettings {
  enabledMethods: string[]; mixedEnabled: boolean; autoPrint: boolean; electronicReceipt: boolean
  notifyOutOfStock: boolean; notifyLowStock: boolean; notifyDebt: boolean; notifyReturn: boolean; notifyCashierChange: boolean; lowStockThreshold: number
  receiptHeader: string | null; receiptFooter: string | null; receiptShowAddress: boolean; receiptShowPhone: boolean; receiptShowBin: boolean
  recipients: TransferRecipient[]; storeName: string; storeAddress: string | null; storePhone: string | null; organizationBin: string | null
}

export type UpdateStoreCashierSettings = Omit<StoreCashierSettings, 'recipients' | 'storeName' | 'storeAddress' | 'storePhone' | 'organizationBin'>

export interface PlanInfo {
  planName: string | null; monthlyFee: number; status: 'trial' | 'active' | 'dueSoon' | 'overdue' | 'unpaid' | 'suspended'
  paidUntil: string | null; trialUntil: string | null; daysLeft: number | null; daysOverdue: number | null
  modules: { key: string; enabled: boolean }[]; supportUrl: string
}

const T = '/pos/v1/settings'

export const settingsApi = {
  store: () => api<StoreProfile>(`${T}/store`),
  updateStore: (r: UpdateStoreProfile) => put<StoreProfile>(`${T}/store`, r),
  stores: () => api<StoreRow[]>(`${T}/stores`),
  createStore: (r: CreateStore) => post<StoreRow>(`${T}/stores`, r),
  setStoreActive: (id: string, active: boolean) => post<StoreRow>(`${T}/stores/${id}/active`, { active }),
  registers: () => api<RegisterRow[]>(`${T}/registers`),
  createRegister: (r: { name: string; autoLockMinutes: number | null }) => post<RegisterRow>(`${T}/registers`, r),
  updateRegister: (id: string, r: { name: string; autoLockMinutes: number | null }) => put<RegisterRow>(`${T}/registers/${id}`, r),
  setRegisterActive: (id: string, active: boolean) => post<RegisterRow>(`${T}/registers/${id}/active`, { active }),
  unbindRegister: (id: string) => post<RegisterRow>(`${T}/registers/${id}/unbind`),
  cashier: () => api<StoreCashierSettings>(`${T}/cashier`),
  updateCashier: (r: UpdateStoreCashierSettings) => put<StoreCashierSettings>(`${T}/cashier`, r),
  saveRecipient: (id: string | null, r: { bankName: string; account: string; holderName: string }) =>
    id ? put<TransferRecipient>(`${T}/recipients/${id}`, r) : post<TransferRecipient>(`${T}/recipients`, r),
  removeRecipient: (id: string) => api<void>(`${T}/recipients/${id}`, { method: 'DELETE' }),
  plan: () => api<PlanInfo>(`${T}/plan`),
}
