import { api, download } from './api'
import type { Product, ProductListItem, ProductPage, ProductQuery, SaveProduct, Warehouse } from './catalogTypes'

const base = '/pos/v1/products'
const json = (method: string, body?: unknown): RequestInit => ({ method, body: body === undefined ? undefined : JSON.stringify(body) })

export interface ImportResult {
  applied: boolean
  total: number
  created: number
  updated: number
  skipped: number
  createdCategories: number
  createdBrands: number
  errors: { row: number; message: string }[]
}

function filters(q: Omit<ProductQuery, 'page' | 'pageSize'>) {
  const p = new URLSearchParams()
  for (const key of ['search', 'status', 'nodeId', 'brandId', 'unitId'] as const) {
    const v = q[key]?.trim()
    if (v) p.set(key, v)
  }
  return p
}

function qs(q: ProductQuery) {
  const p = new URLSearchParams({ page: String(q.page), pageSize: String(q.pageSize) })
  for (const key of ['search', 'status', 'nodeId', 'brandId', 'unitId'] as const) {
    const v = q[key]?.trim()
    if (v) p.set(key, v)
  }
  return p.toString()
}

/** Тауарлар API (каталог бизнеске ортақ; баға мен қалдық — ағымдағы дүкендікі). */
export const productApi = {
  list: (q: ProductQuery) => api<ProductPage>(`${base}?${qs(q)}`),
  get: (id: string) => api<Product>(`${base}/${id}`),
  create: (body: SaveProduct) => api<Product>(base, json('POST', body)),
  update: (id: string, body: SaveProduct) => api<Product>(`${base}/${id}`, json('PUT', body)),
  changeClassification: (id: string, nodeId: string | null) => api<Product>(`${base}/${id}/classification`, json('PUT', { nodeId })),
  archive: (id: string) => api<void>(`${base}/${id}/archive`, json('POST')),
  restore: (id: string) => api<void>(`${base}/${id}/restore`, json('POST')),
  byBarcode: (code: string) => api<ProductListItem>(`${base}/by-barcode/${encodeURIComponent(code)}`),
  checkBarcode: (code: string, excludeProductId?: string) =>
    api<{ available: boolean; productId: string | null; productName: string | null }>(
      `${base}/barcodes/check?barcode=${encodeURIComponent(code)}${excludeProductId ? `&excludeProductId=${excludeProductId}` : ''}`),
  generateBarcode: () => api<{ barcode: string }>(`${base}/barcodes/generate`, json('POST')),
  warehouses: () => api<Warehouse[]>('/pos/v1/warehouses'),
  exportXlsx: (q: Omit<ProductQuery, 'page' | 'pageSize'>) => download(`${base}/export?${filters(q)}`, 'products.xlsx'),
  template: () => download(`${base}/export?template=true`, 'template.xlsx'),
  importXlsx: (file: File, dryRun: boolean, createMissing: boolean) => {
    const form = new FormData()
    form.set('file', file)
    form.set('dryRun', String(dryRun))
    form.set('createMissing', String(createMissing))
    return api<ImportResult>(`${base}/import`, { method: 'POST', body: form })
  },
}
