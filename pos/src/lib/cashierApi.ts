import { api } from './api'
import type {
  CashierCatalog, CashierContext, CashierCustomer, CashierProduct, CashierSettings, CashierSort, CustomerCard, Debt, Finance, PaymentMethod,
  Receipt, SalePage, SalePaymentInput, StoreNotification, TransferRecipient,
} from './cashierTypes'

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
  sales: (q: { from?: string | null; to?: string | null; search?: string; page: number; pageSize: number }) => {
    const p = new URLSearchParams({ page: String(q.page), pageSize: String(q.pageSize) })
    if (q.from) p.set('from', q.from)
    if (q.to) p.set('to', q.to)
    if (q.search?.trim()) p.set('search', q.search.trim())
    return api<SalePage>(`/pos/v1/sales?${p}`)
  },
  createReturn: (saleId: string, body: { clientRequestId: string; items: { saleItemId: string; quantity: number }[];
    refundMethod: PaymentMethod | null; reason: string | null }) =>
    api<Receipt>(`/pos/v1/sales/${saleId}/returns`, json('POST', body)),
  customerCard: (customerId: string) => api<CustomerCard>(`${base}/customers/${customerId}/card`),
  finance: (from: string, to: string) => api<Finance>(`${base}/finance?from=${from}&to=${to}`),
  settings: () => api<CashierSettings>(`${base}/settings`),
  updateSettings: (body: Omit<CashierSettings, 'storeName' | 'storeAddress' | 'registerId' | 'recipients' | 'canManage'>) =>
    api<CashierSettings>(`${base}/settings`, json('PUT', body)),
  saveRecipient: (id: string | null, body: { bankName: string; account: string; holderName: string }) =>
    api<TransferRecipient>(id ? `${base}/settings/recipients/${id}` : `${base}/settings/recipients`, json(id ? 'PUT' : 'POST', body)),
  removeRecipient: (id: string) => api<void>(`${base}/settings/recipients/${id}`, json('DELETE')),
  notifications: () => api<StoreNotification[]>(`${base}/notifications`),
  customerDebts: (customerId: string) => api<Debt[]>(`${base}/customers/${customerId}/debts`),
  repayDebt: (debtId: string, body: { amount: number; method: PaymentMethod; transferRecipientId: string | null }) =>
    api<Debt>(`/pos/v1/debts/${debtId}/payments`, json('POST', body)),
}
