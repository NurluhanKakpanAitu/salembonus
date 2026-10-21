import { create } from 'zustand'

// ---------- Сеанс: токен вкладканың sessionStorage-ында (refresh жоқ — жұмыс күні бойы жарамды) ----------

export interface Admin { id: string; email: string; name: string; isActive: boolean; lastLoginAt: string | null; createdAt: string }

const KEY = 'salemtech.admin.session'

function load(): { token: string; admin: Admin } | null {
  try {
    const raw = sessionStorage.getItem(KEY)
    const v = raw ? (JSON.parse(raw) as { token: string; admin: Admin; expiresAt: string }) : null
    return v && new Date(v.expiresAt).getTime() > Date.now() ? { token: v.token, admin: v.admin } : null
  } catch { return null }
}

export const useAdminAuth = create<{ token: string | null; admin: Admin | null }>(() => {
  const s = load()
  return { token: s?.token ?? null, admin: s?.admin ?? null }
})

export function setSession(token: string, expiresAt: string, admin: Admin) {
  try { sessionStorage.setItem(KEY, JSON.stringify({ token, admin, expiresAt })) } catch { /* жеке режим: сеанс тек жадта */ }
  useAdminAuth.setState({ token, admin })
}

export function logout() {
  try { sessionStorage.removeItem(KEY) } catch { /* жеке режим */ }
  useAdminAuth.setState({ token: null, admin: null })
}

export class AdminApiError extends Error {
  status: number
  field: string | null
  constructor(status: number, message: string, field: string | null = null) {
    super(message)
    this.status = status
    this.field = field
  }
}

export async function adminFetch<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers)
  headers.set('Accept-Language', 'ru')
  if (typeof init.body === 'string') headers.set('Content-Type', 'application/json')
  const token = useAdminAuth.getState().token
  if (token) headers.set('Authorization', `Bearer ${token}`)
  const res = await fetch(`/api/admin/v1${path}`, { ...init, headers })
  if (res.status === 401 && token) logout()
  if (!res.ok) {
    let body: { detail?: string; title?: string; field?: string } | null = null
    try { body = await res.json() } catch { /* бос жауап */ }
    throw new AdminApiError(res.status, body?.detail ?? body?.title ?? (res.status === 429 ? 'Слишком много попыток, подождите минуту' : res.statusText), body?.field ?? null)
  }
  if (res.status === 204) return undefined as T
  return (await res.json()) as T
}

const post = <T>(path: string, body: unknown = {}) => adminFetch<T>(path, { method: 'POST', body: JSON.stringify(body) })
const put = <T>(path: string, body: unknown) => adminFetch<T>(path, { method: 'PUT', body: JSON.stringify(body) })

export function qs(params: Record<string, string | number | boolean | null | undefined>) {
  const p = new URLSearchParams()
  for (const [k, v] of Object.entries(params)) if (v !== undefined && v !== null && v !== '' && v !== false) p.set(k, String(v))
  const s = p.toString()
  return s ? `?${s}` : ''
}

// ---------- Түрлер ----------

export type BillingStatus = 'trial' | 'active' | 'dueSoon' | 'overdue' | 'unpaid' | 'suspended'
export type ModuleKey = 'cashier' | 'products' | 'warehouse' | 'sales' | 'statistics' | 'finance' | 'bonus' | 'clients'

export interface BusinessRow {
  id: string; name: string; bin: string | null; city: string | null; contactName: string | null; contactPhone: string | null; planId: string | null
  planName: string | null; fee: number; customFee: boolean; status: BillingStatus; paidUntil: string | null; trialUntil: string | null; daysOverdue: number | null
  modules: ModuleKey[]; stores: number; staff: number; sales30: number; revenue30: number; lastSaleAt: string | null; clients: number; ownerName: string | null
  ownerPhone: string | null; lastLoginAt: string | null; createdAt: string
}
export interface BusinessPage { rows: BusinessRow[]; total: number; counts: Record<string, number> }
export interface BusinessStore { id: string; name: string; category: string; address: string | null; phone: string | null; katoCode: string | null; timeZone: string; isActive: boolean; joinCode: string; registers: number; createdAt: string }
export interface StaffAccount { id: string; name: string; phone: string; isActive: boolean; hasPassword: boolean; hasPin: boolean; lastLoginAt: string | null; createdAt: string; roles: { storeId: string; storeName: string; role: string; active: boolean }[] }
export interface Payment {
  id: string; organizationId: string; organizationName: string; amount: number; paidOn: string; periodFrom: string; periodTo: string; method: string
  reference: string | null; comment: string | null; createdBy: string; createdAt: string; voided: boolean; voidedAt: string | null; voidedBy: string | null; voidReason: string | null
}
export interface AuditItem { id: string; createdAt: string; admin: string; action: string; data: string | null }
export interface BusinessDetail {
  business: BusinessRow; contactEmail: string | null; comment: string | null; suspendedAt: string | null; suspendReason: string | null
  stores: BusinessStore[]; staff: StaffAccount[]; payments: Payment[]; paidTotal: number; history: AuditItem[]
}
export interface Plan { id: string; name: string; monthlyPrice: number; modules: ModuleKey[]; description: string | null; isActive: boolean; sortOrder: number; businesses: number; isFeatured: boolean }
export interface Overview {
  businesses: number; byStatus: Record<string, number>; mrr: number; paidThisMonth: number; paymentsThisMonth: number; paidLastMonth: number; newThisMonth: number
  revenue: { month: string; amount: number; count: number }[]; overdue: BusinessRow[]; renewals: BusinessRow[]; recentPayments: Payment[]; modules: { module: ModuleKey; businesses: number }[]
  newLeads: number
}

export type LeadStatus = 'new' | 'contacted' | 'won' | 'lost'
export interface Lead {
  id: string; name: string; phone: string; businessName: string | null; city: string | null; businessType: string | null; interest: 'pos' | 'bonus' | 'both'
  comment: string | null; language: 'ru' | 'kk'; status: LeadStatus; adminComment: string | null; organizationId: string | null; createdAt: string; updatedAt: string | null; updatedBy: string | null
}

export interface CreateBusiness {
  name: string; bin?: string | null; city?: string | null; contactName?: string | null; contactPhone?: string | null; contactEmail?: string | null; comment?: string | null
  planId?: string | null; monthlyFee?: number | null; trialDays: number; modules: ModuleKey[]
  store: { name?: string | null; category?: string | null; address?: string | null; phone?: string | null; timeZone?: string | null }
  owner: { phone: string; firstName: string; lastName?: string | null }
}
export interface UpdateBusiness {
  name: string; bin?: string | null; city?: string | null; contactName?: string | null; contactPhone?: string | null; contactEmail?: string | null; comment?: string | null
  planId?: string | null; applyPlanModules: boolean; customFee: boolean; monthlyFee?: number | null; trialUntil?: string | null
}

// ---------- API ----------

export const adminApi = {
  login: (email: string, password: string) => post<{ accessToken: string; expiresAt: string; admin: Admin }>('/auth/login', { email, password }),
  me: () => adminFetch<Admin>('/auth/me'),
  changePassword: (currentPassword: string, newPassword: string) => post<void>('/auth/password', { currentPassword, newPassword }),

  overview: () => adminFetch<Overview>('/overview'),

  businesses: (p: { search?: string; status?: string; planId?: string; module?: string; sort?: string; page?: number; pageSize?: number }) =>
    adminFetch<BusinessPage>(`/businesses${qs(p)}`),
  business: (id: string) => adminFetch<BusinessDetail>(`/businesses/${id}`),
  createBusiness: (r: CreateBusiness) => post<{ business: BusinessDetail; ownerPhone: string; tempPassword: string }>('/businesses', r),
  updateBusiness: (id: string, r: UpdateBusiness) => put<BusinessDetail>(`/businesses/${id}`, r),
  setModules: (id: string, modules: ModuleKey[]) => put<BusinessDetail>(`/businesses/${id}/modules`, { modules }),
  suspend: (id: string, reason: string) => post<BusinessDetail>(`/businesses/${id}/suspend`, { reason }),
  activate: (id: string) => post<BusinessDetail>(`/businesses/${id}/activate`),
  addStore: (id: string, r: { name: string; category?: string | null; address?: string | null; phone?: string | null; timeZone?: string | null }) =>
    post<BusinessDetail>(`/businesses/${id}/stores`, r),
  setStoreActive: (id: string, storeId: string, active: boolean) => post<BusinessDetail>(`/businesses/${id}/stores/${storeId}/active`, { active }),
  addAccount: (id: string, r: { phone: string; firstName: string; lastName?: string | null; role: 'Owner' | 'Admin'; storeId?: string | null }) =>
    post<{ account: StaffAccount; tempPassword: string }>(`/businesses/${id}/accounts`, r),
  resetStaffPassword: (id: string, staffId: string) => post<{ account: StaffAccount; tempPassword: string }>(`/businesses/${id}/accounts/${staffId}/reset-password`),
  setStaffActive: (id: string, staffId: string, active: boolean) => post<StaffAccount>(`/businesses/${id}/accounts/${staffId}/active`, { active }),

  payments: (p: { organizationId?: string; from?: string; to?: string; method?: string; includeVoided?: boolean; page?: number; pageSize?: number }) =>
    adminFetch<{ rows: Payment[]; total: number; sum: number }>(`/payments${qs(p)}`),
  suggest: (organizationId: string, months: number) =>
    adminFetch<{ periodFrom: string; periodTo: string; amount: number; monthlyFee: number }>(`/payments/suggest${qs({ organizationId, months })}`),
  createPayment: (r: { organizationId: string; amount: number; paidOn?: string | null; months?: number | null; periodFrom?: string | null; periodTo?: string | null; method: string; reference?: string | null; comment?: string | null }) =>
    post<Payment>('/payments', r),
  voidPayment: (id: string, reason: string) => post<Payment>(`/payments/${id}/void`, { reason }),

  leads: (p: { status?: string; search?: string; page?: number; pageSize?: number }) => adminFetch<{ rows: Lead[]; total: number; counts: Record<string, number> }>(`/leads${qs(p)}`),
  updateLead: (id: string, r: { status?: LeadStatus; adminComment?: string | null; organizationId?: string | null }) => put<Lead>(`/leads/${id}`, r),

  plans: () => adminFetch<Plan[]>('/plans'),
  createPlan: (r: Partial<Omit<Plan, 'id' | 'businesses'>>) => post<Plan>('/plans', r),
  updatePlan: (id: string, r: Partial<Omit<Plan, 'id' | 'businesses'>>) => put<Plan>(`/plans/${id}`, r),

  admins: () => adminFetch<Admin[]>('/admins'),
  createAdmin: (email: string, name: string) => post<{ admin: Admin; tempPassword: string }>('/admins', { email, name }),
  setAdminActive: (id: string, active: boolean) => post<Admin>(`/admins/${id}/active`, { active }),
  resetAdminPassword: (id: string) => post<{ admin: Admin; tempPassword: string }>(`/admins/${id}/reset-password`),
}
