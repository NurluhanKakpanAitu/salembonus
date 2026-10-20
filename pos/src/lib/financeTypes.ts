/** «Финансы» (ТЗ «Финансы» v1.0): сервер DTO-ларының түрлері. Барлық сома серверде есептеледі (§1.1). */

export type AccountType = 'Cash' | 'Safe' | 'Bank' | 'Kaspi' | 'InTransit' | 'Other'
export const ACCOUNT_TYPES: AccountType[] = ['Cash', 'Safe', 'Bank', 'Kaspi', 'InTransit', 'Other']
export type PayMethod = 'Cash' | 'Card' | 'Qr' | 'Transfer'
export const PAY_METHODS: PayMethod[] = ['Cash', 'Card', 'Qr', 'Transfer']
export type CategoryType = 'Income' | 'Expense'
export type PnlClass = 'OperatingExpense' | 'OtherIncome' | 'OtherExpense' | 'Tax' | 'NotInPnl'
export type OperationType = 'Income' | 'Expense' | 'Transfer' | 'Collection' | 'Adjustment' | 'SalePayment' | 'Refund' | 'CustomerPayment' | 'SupplierPayment'
export const OPERATION_TYPES: OperationType[] = ['Income', 'Expense', 'Transfer', 'Collection', 'Adjustment', 'SalePayment', 'Refund', 'CustomerPayment', 'SupplierPayment']
export type OperationStatus = 'Draft' | 'Posted' | 'Reversed'
export type CounterpartyType = 'Customer' | 'Supplier' | 'Staff' | 'Other'
export type DebtStatus = 'Open' | 'Overdue' | 'Partial' | 'Paid'

export interface FinanceAccount {
  id: string; storeId: string; storeName: string; name: string; type: AccountType; currency: string; methods: PayMethod[]
  balance: number; openingBalance: number; openingDate: string | null; isActive: boolean; sortOrder: number; comment: string | null
  hasEntries: boolean; createdAt: string
}
export interface AccountsSummary { total: number; cash: number; nonCash: number; inTransit: number; accounts: FinanceAccount[] }
export interface SaveAccount {
  name: string; type: AccountType; methods: PayMethod[]; comment?: string | null; sortOrder?: number | null
  openingBalance?: number | null; openingDate?: string | null
}

export interface FinanceCategory { id: string; name: string; type: CategoryType; pnlClass: PnlClass; isActive: boolean; sortOrder: number; createdAt: string }
export interface SaveCategory { name: string; type: CategoryType; pnlClass: PnlClass; sortOrder?: number | null }

export interface FinanceEntry { accountId: string; accountName: string; amount: number; balanceAfter: number }
export interface FinanceOperation {
  id: string; number: string; type: OperationType; status: OperationStatus; date: string; amount: number; storeId: string; storeName: string
  categoryId: string | null; categoryName: string | null; description: string; counterpartyType: CounterpartyType | null; counterpartyId: string | null
  counterpartyName: string | null; fromAccountId: string | null; fromAccountName: string | null; toAccountId: string | null; toAccountName: string | null
  method: PayMethod | null; documentNumber: string | null; attachmentUrl: string | null; comment: string | null; sourceModule: string | null
  sourceDocumentId: string | null; sourceLink: string | null; reversalOfId: string | null; reversedById: string | null
  createdByName: string; createdAt: string; postedAt: string | null; postedByName: string | null; reversedAt: string | null; reversedByName: string | null
  reversalReason: string | null; entries: FinanceEntry[]; canReverse: boolean
}
export interface OperationsPage { items: FinanceOperation[]; total: number; page: number; pageSize: number; inflow: number; outflow: number; transfers: number; count: number }

/** Журнал сүзгілері (§6, §8): URL-да сақталады. */
export interface OperationsQuery {
  from?: string; to?: string; storeIds?: string; accountId?: string; type?: string; types?: string; categoryId?: string; source?: string
  counterpartyType?: string; counterpartyId?: string; status?: string; staffUserId?: string; search?: string; minAmount?: string; maxAmount?: string
  manualOnly?: boolean; sort?: string; dir?: string; page?: number; pageSize?: number
}

export interface SaveOperation {
  type: 'Income' | 'Expense'; date?: string | null; categoryId: string | null; description: string; amount: number; accountId: string | null
  counterpartyType?: CounterpartyType | null; counterpartyId?: string | null; counterpartyName?: string | null; documentNumber?: string | null
  attachmentUrl?: string | null; comment?: string | null; clientRequestId: string; post: boolean
}
export interface TransferRequest {
  fromAccountId: string; toAccountId: string; amount: number; date?: string | null; comment?: string | null; documentNumber?: string | null
  attachmentUrl?: string | null; collection: boolean; clientRequestId: string
}
export interface AdjustmentRequest { accountId: string; amount: number; date?: string | null; reason: string; documentNumber?: string | null; clientRequestId: string }

export interface StatementRow { date: string; operationId: string; number: string; type: OperationType; description: string; counterpartyName: string | null; amount: number; balanceAfter: number; documentNumber: string | null }
export interface AccountStatement { account: FinanceAccount; from: string; to: string; opening: number; inflow: number; outflow: number; closing: number; rows: StatementRow[] }

export interface FinanceKpi { key: string; value: number; previous: number | null; changePercent: number | null; available: boolean; note: string | null }
export interface FinanceDay { date: string; inflow: number; outflow: number; balance: number }
export interface Share { key: string; label: string; amount: number; percent: number }
export interface UpcomingPayment { kind: 'customer' | 'supplier'; id: string; counterparty: string; amount: number; dueDate: string | null; daysLeft: number | null; status: string }
export interface DebtsSummary { customerDebt: number; customerOverdue: number; customerCount: number; supplierDebt: number; supplierOverdue: number; supplierDocuments: number }
export interface FinanceOverview {
  from: string; to: string; generatedAt: string; kpis: FinanceKpi[]; days: FinanceDay[]; accounts: FinanceAccount[]; expenses: Share[]
  recent: FinanceOperation[]; upcoming: UpcomingPayment[]; debts: DebtsSummary
}

export interface PnlRow {
  key: string; level: number; label: string | null; amount: number; percentOfRevenue: number | null; previous: number | null; change: number | null
  changePercent: number | null; isTotal: boolean; link: string | null; categoryId: string | null
}
export interface Pnl { from: string; to: string; prevFrom: string | null; prevTo: string | null; costComplete: boolean; taxesTracked: boolean; rows: PnlRow[]; notes: string[] }

export interface CashFlow {
  from: string; to: string; opening: number; inflow: number; outflow: number; transfers: number; adjustments: number; closing: number
  days: FinanceDay[]; inflowByType: Share[]; outflowByType: Share[]
}

export interface ProductProfitRow {
  productId: string; name: string; article: string | null; barcode: string | null; category: string | null; brand: string | null; unit: string | null
  sold: number; returned: number; netQty: number; netRevenue: number; cost: number | null; profit: number | null; margin: number | null; averagePrice: number | null; costComplete: boolean
}
export interface ProductProfit { total: number; page: number; pageSize: number; netRevenue: number; cost: number | null; profit: number | null; margin: number | null; costComplete: boolean; withoutCost: number; rows: ProductProfitRow[] }

export interface CustomerDebtRow { customerId: string; name: string; phone: string | null; total: number; overdue: number; nextDue: string | null; documents: number; status: DebtStatus }
export interface CustomerDebtDoc { debtId: string; saleId: string | null; saleNumber: number | null; createdAt: string; dueDate: string; amount: number; paid: number; remaining: number; status: string; overdue: boolean; comment: string | null }
export interface CustomerDebtPayment { id: string; createdAt: string; debtId: string; saleNumber: number | null; amount: number; method: string | null; isReturn: boolean; remainingAfter: number; staffName: string; operationId: string | null; operationNumber: string | null }
export interface CustomerDebts { customers: number; total: number; overdue: number; dueSoon: number; rows: CustomerDebtRow[]; rowsTotal: number }
export interface CustomerDebtCard { customerId: string; name: string; phone: string | null; total: number; overdue: number; nextDue: string | null; documents: CustomerDebtDoc[]; payments: CustomerDebtPayment[] }
export interface CustomerPaymentRequest {
  customerId: string; accountId: string | null; method: PayMethod; date?: string | null; comment?: string | null; documentNumber?: string | null
  allocations: { debtId: string; amount: number }[]; clientRequestId: string
}

export interface SupplierDebtRow { supplierId: string; name: string; phone: string | null; lastReceipt: string | null; purchases: number; paid: number; debt: number; overdue: number; nextDue: string | null; documents: number; status: DebtStatus }
export interface SupplierDebtDoc { documentId: string; number: string; date: string; invoiceNumber: string | null; total: number; paid: number; remaining: number; dueDate: string | null; overdue: boolean; lines: number }
export interface SupplierPaymentRow { id: string; createdAt: string; date: string; documentId: string | null; documentNumber: string | null; amount: number; method: string; reference: string | null; status: string; staffName: string; operationId: string | null; operationNumber: string | null; accountName: string | null }
export interface SupplierDebts { total: number; overdue: number; dueSoon: number; documents: number; documentsAll: number; rows: SupplierDebtRow[]; rowsTotal: number }
export interface SupplierDebtCard { supplierId: string; name: string; phone: string | null; taxId: string | null; address: string | null; debt: number; overdue: number; openDocuments: number; nextDue: string | null; documents: SupplierDebtDoc[]; payments: SupplierPaymentRow[] }
export interface SupplierPaymentRequest {
  supplierId: string; accountId: string | null; method: PayMethod; date?: string | null; reference?: string | null; comment?: string | null; attachmentUrl?: string | null
  allocations: { documentId: string; amount: number }[]; clientRequestId: string
}
export interface ReconciliationRow { date: string; kind: 'receipt' | 'payment'; number: string; description: string | null; debit: number; credit: number; balance: number }
export interface Reconciliation { counterparty: string; taxId: string | null; from: string; to: string; opening: number; debit: number; credit: number; closing: number; rows: ReconciliationRow[]; generatedAt: string; storeName: string }

export interface FinanceMeta { accounts: FinanceAccount[]; categories: FinanceCategory[]; canOperate: boolean; canReverse: boolean; canManage: boolean; canContacts: boolean }

export type FinanceReport = 'operations' | 'statement' | 'pnl' | 'cashflow' | 'product-profit' | 'customer-debts' | 'supplier-debts' | 'reconciliation'
