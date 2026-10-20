import type { CatalogStatus } from './catalogTypes'

export interface WarehouseRow {
  id: string
  name: string
  storeId: string
  storeName: string
  type: string | null
  comment: string | null
  isDefault: boolean
  isActive: boolean
  productKinds: number
  totalUnits: number
  createdAt: string
}

export interface SaveWarehouse { name: string; type: string | null; comment: string | null; makeDefault: boolean }

export interface StockKpi { productKinds: number; totalUnits: number; withStock: number; withoutStock: number; belowMin: number }

export interface StockWarehouseQty { warehouseId: string; warehouseName: string; quantity: number }

export interface StockRow {
  productId: string
  name: string
  article: string | null
  barcode: string | null
  imageUrl: string | null
  pathNames: string[]
  brandName: string | null
  unitShortName: string | null
  status: CatalogStatus
  total: number
  minStock: number | null
  byWarehouse: StockWarehouseQty[]
  lastMovementAt: string | null
}

export interface Page<T> { items: T[]; total: number; page: number; pageSize: number }

export type MovementType = 'Opening' | 'Sale' | 'Return' | 'Receipt' | 'WriteOff' | 'TransferOut' | 'TransferIn' | 'Adjustment' | 'Reversal'

export interface Movement {
  id: string
  createdAt: string
  type: MovementType
  documentId: string | null
  documentNumber: string | null
  productId: string
  productName: string
  article: string | null
  warehouseId: string
  warehouseName: string
  quantity: number
  balanceAfter: number
  staffName: string | null
  comment: string | null
}

export type DocumentStatus = 'Draft' | 'Posted' | 'Cancelled' | 'Reversed'
export type WriteOffReason = 'Damage' | 'Expired' | 'Loss' | 'InternalUse' | 'Adjustment' | 'Other'
export const WRITE_OFF_REASONS: WriteOffReason[] = ['Damage', 'Expired', 'Loss', 'InternalUse', 'Adjustment', 'Other']

export interface DocumentLine {
  id: string
  productId: string
  name: string
  article: string | null
  barcode: string | null
  imageUrl: string | null
  unit: string | null
  quantity: number
  purchasePrice: number
  retailPrice: number | null
  lineTotal: number
  stockNow: number
  catalogRetailPrice: number | null
}

export interface SupplierPayment {
  id: string
  supplierId: string
  supplierName: string
  documentId: string | null
  documentNumber: string | null
  amount: number
  method: 'Cash' | 'Card' | 'Transfer' | 'Qr'
  reference: string | null
  date: string
  comment: string | null
  status: 'Registered' | 'Reversed'
  createdByName: string
  createdAt: string
  reversalReason: string | null
}

export interface StockDocument {
  id: string
  type: 'Receipt' | 'WriteOff'
  number: string
  date: string
  status: DocumentStatus
  storeId: string
  warehouseId: string
  warehouseName: string
  supplierId: string | null
  supplierName: string | null
  supplierInvoiceNumber: string | null
  basis: string | null
  attachmentUrl: string | null
  comment: string | null
  reason: WriteOffReason | null
  isQuick: boolean
  total: number
  paid: number
  remaining: number
  lineCount: number
  totalQuantity: number
  createdByName: string
  createdAt: string
  postedAt: string | null
  postedByName: string | null
  reversedAt: string | null
  reversalReason: string | null
  lines: DocumentLine[]
  payments: SupplierPayment[]
}

export interface StockDocumentListItem {
  id: string
  number: string
  date: string
  status: DocumentStatus
  warehouseId: string
  warehouseName: string
  supplierId: string | null
  supplierName: string | null
  reason: WriteOffReason | null
  isQuick: boolean
  lineCount: number
  totalQuantity: number
  total: number
  paid: number
  remaining: number
  createdByName: string
  createdAt: string
}

export interface DocumentStats { total: number; drafts: number; posted: number; awaitingPayment: number }
export interface DocumentPage extends Page<StockDocumentListItem> { stats: DocumentStats }

export interface SaveLine { productId: string; quantity: number; purchasePrice: number | null; retailPrice: number | null }

export interface SaveReceipt {
  warehouseId: string | null
  date: string | null
  supplierId: string | null
  supplierInvoiceNumber: string | null
  basis: string | null
  attachmentUrl: string | null
  comment: string | null
  isQuick: boolean
  lines: SaveLine[]
}

export interface SaveWriteOff {
  warehouseId: string | null
  date: string | null
  reason: WriteOffReason | null
  comment: string | null
  attachmentUrl: string | null
  lines: SaveLine[]
}

export interface RegisterPayment {
  clientRequestId: string
  documentId: string
  amount: number
  method: string
  reference: string | null
  date: string | null
  comment: string | null
}

export type SupplierType = 'Company' | 'Entrepreneur' | 'Person'

export interface Supplier {
  id: string
  name: string
  type: SupplierType
  taxId: string | null
  phone: string | null
  email: string | null
  address: string | null
  comment: string | null
  status: CatalogStatus
  receiptCount: number
  totalReceipts: number
  totalPaid: number
  debt: number
  createdAt: string
}

export interface SaveSupplier {
  name: string
  type: SupplierType
  taxId: string | null
  phone: string | null
  email: string | null
  address: string | null
  comment: string | null
}

export interface SupplierCard { supplier: Supplier; receipts: StockDocumentListItem[]; payments: SupplierPayment[] }

export type TransferStatus = 'Draft' | 'InTransit' | 'Completed' | 'Cancelled'

export interface TransferLine {
  id: string
  productId: string
  name: string
  article: string | null
  barcode: string | null
  imageUrl: string | null
  unit: string | null
  quantitySent: number
  quantityReceived: number | null
  discrepancyReason: string | null
  sourceStock: number
}

export interface Transfer {
  id: string
  number: string
  status: TransferStatus
  sourceStoreId: string
  sourceStoreName: string
  sourceWarehouseId: string
  sourceWarehouseName: string
  destinationStoreId: string
  destinationStoreName: string
  destinationWarehouseId: string
  destinationWarehouseName: string
  comment: string | null
  lineCount: number
  totalQuantity: number
  totalReceived: number | null
  createdByName: string
  createdAt: string
  sentAt: string | null
  sentByName: string | null
  receivedAt: string | null
  receivedByName: string | null
  canReceive: boolean
  lines: TransferLine[]
}

export interface TransferListItem {
  id: string
  number: string
  status: TransferStatus
  sourceStoreName: string
  sourceWarehouseName: string
  destinationStoreName: string
  destinationWarehouseName: string
  destinationStoreId: string
  lineCount: number
  totalQuantity: number
  totalReceived: number | null
  createdByName: string
  createdAt: string
  sentAt: string | null
  receivedAt: string | null
}

export interface TransferStats { total: number; drafts: number; inTransit: number; completed: number }
export interface TransferPage extends Page<TransferListItem> { stats: TransferStats }

export interface SaveTransfer {
  sourceWarehouseId: string | null
  destinationStoreId: string | null
  destinationWarehouseId: string | null
  comment: string | null
  lines: { productId: string; quantity: number }[]
}

export interface ReceiveTransfer { lines: { productId: string; quantityReceived: number; discrepancyReason: string | null }[] }

export interface TransferTarget { storeId: string; storeName: string; warehouses: { id: string; name: string; isDefault: boolean }[] }

// ---------- Накладнаяны тану (ТЗ «Склад» §8) ----------

export type RecognizedStatus = 'Matched' | 'Review' | 'NotFound'

export interface RecognizedProduct {
  id: string
  name: string
  article: string | null
  barcode: string | null
  imageUrl: string | null
  unit: string | null
  stockNow: number | null
  purchasePrice: number | null
  catalogRetailPrice: number | null
}

/** Накладнаядағы бір жол: түпнұсқа мәндер + каталогпен сәйкестендіру нәтижесі. */
export interface RecognizedLine {
  index: number
  name: string | null
  article: string | null
  barcode: string | null
  quantity: number | null
  price: number | null
  lineTotal: number | null
  status: RecognizedStatus
  matchedBy: 'None' | 'Barcode' | 'Article' | 'Name'
  confidence: number
  product: RecognizedProduct | null
}

export interface InvoiceRecognition {
  supplierName: string | null
  supplierId: string | null
  invoiceNumber: string | null
  date: string | null
  lines: RecognizedLine[]
  matched: number
  review: number
  notFound: number
}
