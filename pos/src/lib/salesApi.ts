import { api, download } from './api'
import type { Receipt } from './cashierTypes'
import type { SaleEvent, SalesFilters, SalesHistoryPage, SalesQuery, SalesSummary } from './salesTypes'

const base = '/pos/v1/sales'

function qs(params: SalesQuery) {
  const p = new URLSearchParams()
  for (const [k, v] of Object.entries(params)) {
    if (v === undefined || v === null || v === '' || v === false) continue
    p.set(k, String(v))
  }
  const s = p.toString()
  return s ? `?${s}` : ''
}

export const salesKeys = {
  all: ['sales'] as const,
  history: ['sales', 'history'] as const,
  summary: ['sales', 'summary'] as const,
  filters: ['sales', 'filters'] as const,
  receipt: (id: string) => ['sales', 'receipt', id] as const,
  events: (id: string) => ['sales', 'events', id] as const,
}

/** «Продажи» бөлімі (ТЗ «Продажи» §14): тарих, KPI, чек, хронология, экспорт. Қайтару — cashierApi.createReturn (ортақ). */
export const salesApi = {
  history: (q: SalesQuery) => api<SalesHistoryPage>(`${base}/history${qs(q)}`),
  summary: (q: SalesQuery) => api<SalesSummary>(`${base}/summary${qs(q)}`),
  filters: () => api<SalesFilters>(`${base}/filters`),
  receipt: (id: string) => api<Receipt>(`${base}/${id}/details`),
  events: (id: string) => api<SaleEvent[]>(`${base}/${id}/history`),
  exportXlsx: (q: SalesQuery) => download(`${base}/export${qs({ ...q, page: undefined, pageSize: undefined })}`, 'sales.xlsx'),
}
