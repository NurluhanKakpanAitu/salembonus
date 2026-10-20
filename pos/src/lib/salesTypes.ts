import type { PaymentMethod } from './cashierTypes'

/** Кесте мәртебесі (ТЗ «Продажи» §11): Debt — төленбеген қарызы бар чек («Ожидает оплаты» орнына). */
export type SaleHistoryStatus = 'Completed' | 'PartiallyReturned' | 'Returned' | 'Debt'

export interface SalesHistoryItem {
  id: string
  number: number
  createdAt: string
  customerId: string | null
  customerName: string | null
  cashierName: string
  registerName: string
  itemCount: number
  units: number
  /** Чектің тарихи сомасы — қайтарымнан кейін өзгермейді. */
  total: number
  returned: number
  /** total − returned. */
  net: number
  status: SaleHistoryStatus
  methods: PaymentMethod[]
  hasOpenDebt: boolean
  debtRemaining: number
}

export interface SalesHistoryPage {
  items: SalesHistoryItem[]
  total: number
  page: number
  pageSize: number
}

export interface SalesSummary {
  salesCount: number
  salesTotal: number
  salesChangePercent: number | null
  returnsCount: number
  returnsTotal: number
  returnsShare: number
  debtCount: number
  debtTotal: number
  debtOpen: number
  averageCheck: number
  units: number
  discounts: number
  discountCount: number
  discountsShare: number
  bonusRedeemed: number
  bonusCount: number
  bonusShare: number
  withReturn: number
  withReturnShare: number
}

export interface SalesFilterOption { id: string; name: string }
export interface SalesFilters { registers: SalesFilterOption[]; cashiers: SalesFilterOption[] }

export type SaleEventType =
  | 'sale' | 'payment' | 'debt' | 'discount' | 'bonus.accrued' | 'bonus.redeemed'
  | 'debt.repaid' | 'return' | 'debt.reduced' | 'bonus.restored' | 'bonus.reversed'

export interface SaleEvent {
  at: string
  type: SaleEventType
  staffName: string | null
  amount: number | null
  method: PaymentMethod | null
  note: string | null
  returnId: string | null
}

export type ReturnReason = 'NotSuitable' | 'Defect' | 'WrongSale' | 'Other'
export type ReturnCondition = 'Sellable' | 'Damaged' | 'NeedsCheck'
export type ReturnStockAction = 'ToStock' | 'WriteOff'
export const RETURN_REASONS: ReturnReason[] = ['NotSuitable', 'Defect', 'WrongSale', 'Other']
export const RETURN_CONDITIONS: ReturnCondition[] = ['Sellable', 'Damaged', 'NeedsCheck']

export type SalesSort = 'number' | 'date' | 'total' | 'returned' | 'net' | 'status'

export interface SalesQuery {
  from?: string
  to?: string
  search?: string
  registerId?: string
  staffUserId?: string
  method?: string
  status?: string
  hasReturn?: string
  customerId?: string
  noCustomer?: boolean
  sort?: string
  dir?: string
  page?: number
  pageSize?: number
}
