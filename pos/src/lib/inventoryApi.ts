import { api } from './api'
import type {
  DocumentPage, Movement, Page, ReceiveTransfer, RegisterPayment, SaveReceipt, SaveSupplier, SaveTransfer, SaveWarehouse, SaveWriteOff,
  StockDocument, StockKpi, StockRow, Supplier, SupplierCard, Transfer, TransferPage, TransferTarget, WarehouseRow,
} from './inventoryTypes'

const base = '/pos/v1/inventory'
const json = (method: string, body?: unknown): RequestInit => ({ method, body: body === undefined ? undefined : JSON.stringify(body) })

function qs(params: object) {
  const p = new URLSearchParams()
  for (const [k, v] of Object.entries(params as Record<string, unknown>)) if (v !== undefined && v !== null && v !== '' && v !== false) p.set(k, String(v))
  const s = p.toString()
  return s ? `?${s}` : ''
}

export interface DocumentQuery {
  warehouseId?: string; supplierId?: string; status?: string; search?: string; from?: string; to?: string; page: number; pageSize: number
}

/** «Склад» API (ТЗ «Склад» §18). Дүкен — X-Store-Id; құқық серверде тексеріледі. */
export const inventoryApi = {
  warehouses: (all = false) => api<WarehouseRow[]>(`${base}/warehouses${qs({ all })}`),
  createWarehouse: (body: SaveWarehouse) => api<WarehouseRow>(`${base}/warehouses`, json('POST', body)),
  updateWarehouse: (id: string, body: SaveWarehouse) => api<WarehouseRow>(`${base}/warehouses/${id}`, json('PUT', body)),
  makeDefault: (id: string) => api<void>(`${base}/warehouses/${id}/make-default`, json('POST')),
  archiveWarehouse: (id: string) => api<void>(`${base}/warehouses/${id}/archive`, json('POST')),
  restoreWarehouse: (id: string) => api<void>(`${base}/warehouses/${id}/restore`, json('POST')),
  transferTargets: () => api<TransferTarget[]>(`${base}/transfer-targets`),

  kpi: (warehouseId?: string) => api<StockKpi>(`${base}/kpi${qs({ warehouseId })}`),
  stock: (q: { warehouseId?: string; search?: string; nodeId?: string; brandId?: string; availability?: string; status?: string; sort?: string; page: number; pageSize: number }) =>
    api<Page<StockRow>>(`${base}/stock${qs(q)}`),
  movements: (q: { warehouseId?: string; productId?: string; type?: string; document?: string; from?: string; to?: string; page: number; pageSize: number }) =>
    api<Page<Movement>>(`${base}/movements${qs(q)}`),

  receipts: (q: DocumentQuery) => api<DocumentPage>(`${base}/receipts${qs(q)}`),
  createReceipt: (body: SaveReceipt) => api<StockDocument>(`${base}/receipts`, json('POST', body)),
  updateReceipt: (id: string, body: SaveReceipt) => api<StockDocument>(`${base}/receipts/${id}`, json('PUT', body)),
  registerPayment: (id: string, body: RegisterPayment) => api<StockDocument>(`${base}/receipts/${id}/payments`, json('POST', body)),
  reversePayment: (id: string, reason: string) => api<void>(`${base}/payments/${id}/reverse`, json('POST', { reason })),

  writeOffs: (q: DocumentQuery) => api<DocumentPage>(`${base}/write-offs${qs(q)}`),
  createWriteOff: (body: SaveWriteOff) => api<StockDocument>(`${base}/write-offs`, json('POST', body)),
  updateWriteOff: (id: string, body: SaveWriteOff) => api<StockDocument>(`${base}/write-offs/${id}`, json('PUT', body)),

  document: (id: string) => api<StockDocument>(`${base}/documents/${id}`),
  postDocument: (id: string) => api<StockDocument>(`${base}/documents/${id}/post`, json('POST')),
  cancelDocument: (id: string) => api<void>(`${base}/documents/${id}/cancel`, json('POST')),
  reverseDocument: (id: string, reason: string) => api<StockDocument>(`${base}/documents/${id}/reverse`, json('POST', { reason })),

  transfers: (q: { status?: string; incoming?: boolean; search?: string; from?: string; to?: string; page: number; pageSize: number }) =>
    api<TransferPage>(`${base}/transfers${qs(q)}`),
  transfer: (id: string) => api<Transfer>(`${base}/transfers/${id}`),
  createTransfer: (body: SaveTransfer) => api<Transfer>(`${base}/transfers`, json('POST', body)),
  updateTransfer: (id: string, body: SaveTransfer) => api<Transfer>(`${base}/transfers/${id}`, json('PUT', body)),
  dispatchTransfer: (id: string) => api<Transfer>(`${base}/transfers/${id}/dispatch`, json('POST')),
  receiveTransfer: (id: string, body: ReceiveTransfer) => api<Transfer>(`${base}/transfers/${id}/receive`, json('POST', body)),
  cancelTransfer: (id: string, reason: string | null) => api<Transfer>(`${base}/transfers/${id}/cancel`, json('POST', { reason })),

  suppliers: () => api<Supplier[]>(`${base}/suppliers`),
  supplier: (id: string) => api<SupplierCard>(`${base}/suppliers/${id}`),
  createSupplier: (body: SaveSupplier) => api<Supplier>(`${base}/suppliers`, json('POST', body)),
  updateSupplier: (id: string, body: SaveSupplier) => api<Supplier>(`${base}/suppliers/${id}`, json('PUT', body)),
  archiveSupplier: (id: string) => api<void>(`${base}/suppliers/${id}/archive`, json('POST')),
  restoreSupplier: (id: string) => api<void>(`${base}/suppliers/${id}/restore`, json('POST')),
}

export const inventoryKeys = {
  all: ['inventory'] as const,
  warehouses: ['inventory', 'warehouses'] as const,
  kpi: ['inventory', 'kpi'] as const,
  stock: ['inventory', 'stock'] as const,
  movements: ['inventory', 'movements'] as const,
  receipts: ['inventory', 'receipts'] as const,
  writeOffs: ['inventory', 'writeOffs'] as const,
  document: (id: string) => ['inventory', 'document', id] as const,
  transfers: ['inventory', 'transfers'] as const,
  transfer: (id: string) => ['inventory', 'transfer', id] as const,
  suppliers: ['inventory', 'suppliers'] as const,
  supplier: (id: string) => ['inventory', 'supplier', id] as const,
  targets: ['inventory', 'targets'] as const,
}
