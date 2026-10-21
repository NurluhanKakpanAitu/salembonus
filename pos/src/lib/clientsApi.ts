import { api, download } from './api'
import { qs } from './financeApi'
import type { CustomerDebtCard, CustomerDebts } from './financeTypes'
import type {
  ClientActivityDetail, ClientBirthday, ClientCard, ClientCommunication, ClientDocument, ClientDuplicate, ClientFilter, ClientHistory, ClientKpis, ClientLookup,
  ClientMeta, ClientNote, ClientOverview, ClientPage, ClientReceipt, ClientSale, ClientSave, ClientSegment, ClientSettings, ImportPreview, ImportResult,
} from './clientsTypes'

const base = '/pos/v1/clients'
const post = <T>(path: string, body: unknown = {}) => api<T>(`${base}${path}`, { method: 'POST', body: JSON.stringify(body) })
const put = <T>(path: string, body: unknown) => api<T>(`${base}${path}`, { method: 'PUT', body: JSON.stringify(body) })

export interface ClientSearch { storeIds?: string; filter: ClientFilter; segmentId?: string | null; sort?: string; dir?: 'asc' | 'desc'; page?: number; pageSize?: number }

export const clientKeys = { all: ['clients'] as const, meta: ['clients', 'meta'] as const }

function form(file: File, fields: Record<string, string | undefined>) {
  const f = new FormData()
  f.append('file', file)
  for (const [k, v] of Object.entries(fields)) if (v !== undefined) f.append(k, v)
  return f
}

export const clientsApi = {
  meta: () => api<ClientMeta>(`${base}/meta`),
  search: (r: ClientSearch) => post<ClientPage>('/search', r),
  kpis: (r: ClientSearch) => post<ClientKpis>('/kpis', r),
  overview: (storeIds?: string) => api<ClientOverview>(`${base}/overview${qs({ storeIds })}`),
  birthdays: (storeIds: string | undefined, days: number) => api<ClientBirthday[]>(`${base}/birthdays${qs({ storeIds, days })}`),
  debts: (p: Record<string, string | number | undefined>) => api<CustomerDebts>(`${base}/debts${qs(p)}`),
  lookup: (phone: string) => api<ClientLookup>(`${base}/lookup${qs({ phone })}`),

  create: (r: ClientSave) => post<ClientCard>('', r),
  card: (id: string, storeIds?: string) => api<ClientCard>(`${base}/${id}${qs({ storeIds })}`),
  byCustomer: (customerId: string) => api<ClientCard>(`${base}/by-customer/${customerId}`),
  update: (id: string, r: ClientSave) => put<ClientCard>(`/${id}`, r),
  archive: (id: string, reason: string, force = false) => post<ClientCard>(`/${id}/archive`, { reason, force }),
  restore: (id: string) => post<ClientCard>(`/${id}/restore`),
  merge: (primaryId: string, duplicateId: string) => post<ClientCard>('/merge', { primaryId, duplicateId }),
  duplicates: (id: string) => api<ClientDuplicate[]>(`${base}/${id}/duplicates`),

  sales: (id: string, storeIds: string | undefined, page: number) => api<{ rows: ClientSale[]; total: number }>(`${base}/${id}/sales${qs({ storeIds, page, pageSize: 20 })}`),
  receipts: (id: string, storeIds?: string) => api<ClientReceipt[]>(`${base}/${id}/receipts${qs({ storeIds })}`),
  clientDebts: (id: string, storeIds?: string) => api<CustomerDebtCard>(`${base}/${id}/debts${qs({ storeIds })}`),
  documents: (id: string, storeIds?: string) => api<ClientDocument[]>(`${base}/${id}/documents${qs({ storeIds })}`),
  activity: (id: string, storeIds?: string) => api<ClientActivityDetail>(`${base}/${id}/activity${qs({ storeIds })}`),
  history: (id: string) => api<ClientHistory[]>(`${base}/${id}/history`),
  message: (id: string, channel: string, text: string) => post<ClientCommunication>(`/${id}/message`, { channel, text }),

  notes: (id: string) => api<ClientNote[]>(`${base}/${id}/notes`),
  addNote: (id: string, text: string, category: string) => post<ClientNote[]>(`/${id}/notes`, { text, category }),
  updateNote: (noteId: string, text: string, category: string) => put<ClientNote[]>(`/notes/${noteId}`, { text, category }),
  deleteNote: (noteId: string) => api<void>(`${base}/notes/${noteId}`, { method: 'DELETE' }),

  segments: (storeIds?: string) => api<ClientSegment[]>(`${base}/segments${qs({ storeIds })}`),
  createSegment: (r: { name: string; description?: string | null; filter: ClientFilter; isShared: boolean; includeIds?: string[] }) => post<ClientSegment>('/segments', r),
  updateSegment: (id: string, r: { name?: string; description?: string | null; filter?: ClientFilter; isShared?: boolean; includeIds?: string[] }) => put<ClientSegment>(`/segments/${id}`, r),
  deleteSegment: (id: string) => api<void>(`${base}/segments/${id}`, { method: 'DELETE' }),
  addToSegment: (ids: string[], segmentId: string) => post<{ processed: number; skipped: number }>('/bulk/segment', { ids, segmentId }),
  assignStatus: (ids: string[], statusId: string, reason: string) => post<{ processed: number; skipped: number }>('/bulk/status', { ids, statusId, reason }),

  importPreview: (file: File, mapping: Record<string, number> | null, strategy: string) =>
    api<ImportPreview>(`${base}/import/preview`, { method: 'POST', body: form(file, { mapping: mapping ? JSON.stringify(mapping) : undefined, strategy }) }),
  importCommit: (file: File, mapping: Record<string, number>, strategy: string, key: string) =>
    api<ImportResult>(`${base}/import/commit`, { method: 'POST', body: form(file, { mapping: JSON.stringify(mapping), strategy, key }) }),
  template: () => download(`${base}/import/template`, 'clients-template.xlsx'),
  exportFile: (r: { storeIds?: string; filter?: ClientFilter; segmentId?: string | null; ids?: string[]; format: 'xlsx' | 'csv'; fields: string[] }) =>
    download(`${base}/export`, `clients.${r.format}`, { method: 'POST', body: JSON.stringify(r) }),

  settings: () => api<ClientSettings>(`${base}/settings`),
  saveSettings: (r: Partial<Omit<ClientSettings, 'updatedAt'>>) => put<ClientSettings>('/settings', r),
}

/** «Создать кампанию» таңдалған клиенттерден: «Бонусы» шебері ашылғанда аудиторияны осыдан алады. */
export const CAMPAIGN_AUDIENCE_KEY = 'salempos.campaignAudience'
export function handOffAudience(customerIds: string[]) {
  try { sessionStorage.setItem(CAMPAIGN_AUDIENCE_KEY, JSON.stringify(customerIds)) } catch { /* жеке режим: аудиторияны қолмен таңдайды */ }
}
