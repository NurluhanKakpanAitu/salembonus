import { api } from './api'

export interface Kpi { key: 'revenue' | 'sales' | 'average' | 'customers' | 'newCustomers' | 'bonusAccrued'; value: number; previous: number; changePercent: number | null }
export interface Share { key: string; label: string; amount: number; percent: number }

/** Статистиканың бір snapshot-ы (ТЗ «Статистика» §17): барлық көрсеткішті сервер есептейді. */
export interface Dashboard {
  meta: { storeName: string; from: string; to: string; generatedAt: string; timeZone: string; granularity: 'hour' | 'day' }
  kpi: Kpi[]
  chart: { start: string; label: string; revenue: number; count: number }[]
  paymentMethods: Share[]
  categories: Share[]
  financial: { revenue: number; cost: number; costComplete: boolean; expenses: number | null; profit: number }
  cash: { actualBalance: number | null; received: number; paid: number; expected: number }
  topProducts: { productId: string; name: string; imageUrl: string | null; quantity: number; unit: string | null; revenue: number }[]
  customers: { total: number; new: number; regular: number; vip: number; totalChange: number | null; newChange: number | null }
  bonuses: { accrued: number; redeemed: number; balance: number; accruedChange: number | null; redeemedChange: number | null }
  attention: { type: 'lowStock' | 'outOfStock' | 'debts' | 'overdueDebts'; count: number; amount: number | null }[]
}

export const dashboardApi = {
  get: (from: string, to: string) => api<Dashboard>(`/pos/v1/dashboard?from=${from}&to=${to}`),
}
