/** «Статистика» v1.1 — серверлік DTO (backend: Pos/Analytics/AnalyticsDtos.cs). */

export type AnalyticsTab = 'overview' | 'sales' | 'products' | 'inventory' | 'suppliers' | 'finance' | 'bonus' | 'customers' | 'employees' | 'registers'
export const TABS: AnalyticsTab[] = ['overview', 'sales', 'products', 'inventory', 'suppliers', 'finance', 'bonus', 'customers', 'employees', 'registers']

export type Compare = 'none' | 'prev' | 'year'
export type Granularity = 'day' | 'week' | 'month'

export interface AnalyticsMeta {
  stores: { id: string; name: string }[]
  from: string
  to: string
  timeZone: string
  granularity: Granularity
  compare: Compare
  compareFrom: string | null
  compareTo: string | null
  includesToday: boolean
  generatedAt: string
  canFinance: boolean
  canContacts: boolean
  canExport: boolean
}

export interface Kpi {
  key: string
  value: number | null
  previous: number | null
  changeAbs: number | null
  changePercent: number | null
  amount: number | null
  previousAmount: number | null
  numerator: number | null
  denominator: number | null
  available: boolean
  note: string | null
}

export interface Bucket { start: string; end: string }
export type SeriesKind = 'money' | 'count' | 'qty' | 'points'
export interface Series { key: string; kind: SeriesKind; style: 'bar' | 'line'; values: number[] }
export interface Share { key: string; label: string | null; value: number; percent: number; count: number | null }
export interface Breakdown { key: string; kind: SeriesKind; items: Share[]; total: number }
export type ColumnKind = 'text' | 'money' | 'qty' | 'int' | 'percent' | 'date' | 'datetime' | 'points' | 'period' | 'enum'
export interface Column { key: string; kind: ColumnKind; sortable: boolean; link: string | null }
export type Row = Record<string, string | number | boolean | null>
export interface Table { columns: Column[]; rows: Row[]; total: number; page: number; pageSize: number; sort: string | null; dir: 'asc' | 'desc' | null }

export interface AnalyticsTabData {
  meta: AnalyticsMeta
  kpis: Kpi[]
  buckets: Bucket[]
  series: Series[]
  breakdowns: Breakdown[]
  table: Table | null
  notes: string[]
}

export interface TrendPoint { bucketStart: string; bucketEnd: string; revenue: number; salesCount: number; returns: number; netSales: number }
export interface SalesTrend { granularity: Granularity; timeZone: string; startDate: string; endDate: string; includesToday: boolean; generatedAt: string; data: TrendPoint[] }

export interface TopProduct { productId: string; name: string; article: string | null; imageUrl: string | null; unit: string | null; quantity: number; amount: number; cost: number | null; profit: number | null }
export interface ActivityCustomer { id: string; name: string; phone: string | null; purchases: number; amount: number; lastAt: string }

export interface Overview {
  meta: AnalyticsMeta
  kpis: Kpi[]
  paymentMethods: { items: { method: string; amount: number; percent: number; receipts: number }[]; total: number; mixedReceipts: number }
  topProducts: { byQuantity: TopProduct[]; byAmount: TopProduct[]; byProfit: TopProduct[] | null; profitAvailable: boolean }
  customers: {
    buyers: number; new: number; repeat: number; returningRate: number | null; unidentifiedReceipts: number
    newPrevious: number | null; repeatPrevious: number | null
    frequency: { key: string; customers: number; percent: number }[]; regularThreshold: number
    newList: ActivityCustomer[]; regularList: ActivityCustomer[]; inactiveList: ActivityCustomer[]; inactiveDays: number
  }
  inventory: { withStock: number; withoutStock: number; belowMin: number; movements: number; totalUnits: number; value: number | null; valueComplete: boolean }
  suppliers: { purchases: number; receiptDocuments: number; paid: number; debt: number; unpaidDocuments: number }
  finance: { inflow: number; outflow: number; netFlow: number; customerDebt: number } | null
}

/** Жалпы сүзгілер (URL-да сақталады, вкладка ауысқанда қалады — §24). */
export interface AnalyticsFilters {
  from: string
  to: string
  storeIds: string[]
  compare: Compare
}
