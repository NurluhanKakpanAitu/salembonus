import { api } from './api'
import type { CashierCatalog, CashierContext, CashierCustomer, CashierProduct, CashierSort, Receipt, SalePaymentInput } from './cashierTypes'

const base = '/pos/v1/cashier'
const json = (method: string, body?: unknown): RequestInit => ({ method, body: body === undefined ? undefined : JSON.stringify(body) })

export interface CreateSale {
  clientRequestId: string
  items: { productId: string; quantity: number }[]
  customerId: string | null
  bonusRedeem: number
  discount: { kind: 'Percent' | 'Amount'; value: number } | null
  approval: { staffUserId: string; pin: string } | null
  payments: SalePaymentInput[]
}

/** Касса API: контекст, каталог (баға мен қалдықпен), клиент, сатылым. */
export const cashierApi = {
  context: () => api<CashierContext>(`${base}/context`),
  catalog: (q: { search?: string; nodeId?: string | null; sort: CashierSort; page: number; pageSize: number }) => {
    const p = new URLSearchParams({ sort: q.sort, page: String(q.page), pageSize: String(q.pageSize) })
    if (q.search?.trim()) p.set('search', q.search.trim())
    if (q.nodeId) p.set('nodeId', q.nodeId)
    return api<CashierCatalog>(`${base}/catalog?${p}`)
  },
  byBarcode: (code: string) => api<CashierProduct>(`${base}/catalog/by-barcode/${encodeURIComponent(code)}`),
  searchCustomers: (q: string) => api<CashierCustomer[]>(`${base}/customers?q=${encodeURIComponent(q)}`),
  customer: (id: string) => api<CashierCustomer>(`${base}/customers/${id}`),
  registerCustomer: (body: { phone: string; firstName: string; lastName: string; birthDate: string }) =>
    api<CashierCustomer>(`${base}/customers`, json('POST', body)),
  createSale: (body: CreateSale) => api<Receipt>('/pos/v1/sales', json('POST', body)),
  sale: (id: string) => api<Receipt>(`/pos/v1/sales/${id}`),
}
