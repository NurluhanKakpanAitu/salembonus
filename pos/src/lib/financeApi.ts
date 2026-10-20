import { api, download } from './api'
import type {
  AccountStatement, AccountsSummary, AdjustmentRequest, CashFlow, CustomerDebtCard, CustomerDebts, CustomerPaymentRequest, FinanceAccount, FinanceCategory,
  FinanceMeta, FinanceOperation, FinanceOverview, FinanceReport, OperationsPage, OperationsQuery, Pnl, ProductProfit, Reconciliation, SaveAccount, SaveCategory,
  SaveOperation, SupplierDebtCard, SupplierDebts, SupplierPaymentRequest, TransferRequest,
} from './financeTypes'

const base = '/pos/v1/finance'

/** Параметрлер → query string; бос мәндер жіберілмейді. */
export function qs(params: Record<string, string | number | boolean | null | undefined>) {
  const p = new URLSearchParams()
  for (const [k, v] of Object.entries(params)) if (v !== undefined && v !== null && v !== '' && v !== false) p.set(k, String(v))
  const s = p.toString()
  return s ? `?${s}` : ''
}

export const financeKeys = {
  all: ['finance'] as const,
  meta: () => ['finance', 'meta'] as const,
  accounts: (all: boolean) => ['finance', 'accounts', all] as const,
  operations: (q: OperationsQuery) => ['finance', 'operations', q] as const,
  operation: (id: string) => ['finance', 'operation', id] as const,
}

export const financeApi = {
  meta: (storeIds?: string) => api<FinanceMeta>(`${base}/meta${qs({ storeIds })}`),

  accounts: (storeIds?: string, all = false) => api<AccountsSummary>(`${base}/accounts${qs({ storeIds, all })}`),
  createAccount: (r: SaveAccount) => api<FinanceAccount>(`${base}/accounts`, { method: 'POST', body: JSON.stringify(r) }),
  updateAccount: (id: string, r: SaveAccount) => api<FinanceAccount>(`${base}/accounts/${id}`, { method: 'PUT', body: JSON.stringify(r) }),
  setAccountActive: (id: string, active: boolean) => api<void>(`${base}/accounts/${id}/${active ? 'activate' : 'deactivate'}`, { method: 'POST', body: '{}' }),
  statement: (id: string, from?: string, to?: string) => api<AccountStatement>(`${base}/accounts/${id}/statement${qs({ from, to })}`),

  categories: () => api<FinanceCategory[]>(`${base}/categories`),
  createCategory: (r: SaveCategory) => api<FinanceCategory>(`${base}/categories`, { method: 'POST', body: JSON.stringify(r) }),
  updateCategory: (id: string, r: SaveCategory) => api<FinanceCategory>(`${base}/categories/${id}`, { method: 'PUT', body: JSON.stringify(r) }),
  setCategoryActive: (id: string, active: boolean) => api<void>(`${base}/categories/${id}/${active ? 'restore' : 'archive'}`, { method: 'POST', body: '{}' }),

  operations: (q: OperationsQuery) => api<OperationsPage>(`${base}/operations${qs(q as Record<string, string | number | boolean | undefined>)}`),
  operation: (id: string) => api<FinanceOperation>(`${base}/operations/${id}`),
  createOperation: (r: SaveOperation) => api<FinanceOperation>(`${base}/operations`, { method: 'POST', body: JSON.stringify(r) }),
  updateDraft: (id: string, r: SaveOperation) => api<FinanceOperation>(`${base}/operations/${id}`, { method: 'PUT', body: JSON.stringify(r) }),
  postDraft: (id: string) => api<FinanceOperation>(`${base}/operations/${id}/post`, { method: 'POST', body: '{}' }),
  deleteDraft: (id: string) => api<void>(`${base}/operations/${id}`, { method: 'DELETE' }),
  reverse: (id: string, reason: string) => api<FinanceOperation>(`${base}/operations/${id}/reverse`, { method: 'POST', body: JSON.stringify({ reason }) }),
  transfer: (r: TransferRequest) => api<FinanceOperation>(`${base}/transfers`, { method: 'POST', body: JSON.stringify(r) }),
  adjust: (r: AdjustmentRequest) => api<FinanceOperation>(`${base}/adjustments`, { method: 'POST', body: JSON.stringify(r) }),

  overview: (storeIds: string | undefined, from: string, to: string) => api<FinanceOverview>(`${base}/overview${qs({ storeIds, from, to })}`),
  pnl: (storeIds: string | undefined, from: string, to: string, compare: boolean) => api<Pnl>(`${base}/reports/pnl${qs({ storeIds, from, to, compare })}`),
  cashFlow: (storeIds: string | undefined, from: string, to: string, accountId?: string) => api<CashFlow>(`${base}/cash-flow${qs({ storeIds, from, to, accountId })}`),
  productProfit: (p: Record<string, string | number | undefined>) => api<ProductProfit>(`${base}/reports/product-profit${qs(p)}`),

  customerDebts: (p: Record<string, string | number | undefined>) => api<CustomerDebts>(`${base}/customer-debts${qs(p)}`),
  customerDebt: (customerId: string, storeIds?: string) => api<CustomerDebtCard>(`${base}/customer-debts/${customerId}${qs({ storeIds })}`),
  customerPayment: (r: CustomerPaymentRequest) => api<CustomerDebtCard>(`${base}/customer-debts/payments`, { method: 'POST', body: JSON.stringify(r) }),
  supplierDebts: (p: Record<string, string | number | undefined>) => api<SupplierDebts>(`${base}/supplier-debts${qs(p)}`),
  supplierDebt: (supplierId: string, storeIds?: string) => api<SupplierDebtCard>(`${base}/supplier-debts/${supplierId}${qs({ storeIds })}`),
  supplierPayment: (r: SupplierPaymentRequest) => api<SupplierDebtCard>(`${base}/supplier-debts/payments`, { method: 'POST', body: JSON.stringify(r) }),
  setDueDate: (documentId: string, dueDate: string | null) => api<void>(`${base}/supplier-debts/documents/${documentId}/due-date`, { method: 'PUT', body: JSON.stringify({ dueDate }) }),
  reconciliation: (supplierId: string, storeIds: string | undefined, from?: string, to?: string) =>
    api<Reconciliation>(`${base}/supplier-debts/${supplierId}/reconciliation${qs({ storeIds, from, to })}`),

  exportFile: (report: FinanceReport, format: 'xlsx' | 'csv', p: Record<string, string | number | boolean | null | undefined>) =>
    download(`${base}/export${qs({ ...p, report, format, page: undefined, pageSize: undefined })}`, `finance_${report}.${format}`),
  backfill: () => api<{ posted: number; skipped: number }>(`${base}/backfill`, { method: 'POST', body: '{}' }),
}
