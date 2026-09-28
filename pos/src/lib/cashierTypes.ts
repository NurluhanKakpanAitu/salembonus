export type PaymentMethod = 'Cash' | 'Card' | 'Qr' | 'Transfer' | 'Debt'

export interface TransferRecipient { id: string; bankName: string; account: string; holderName: string }

export interface CashierContext {
  storeId: string
  storeName: string
  storeAddress: string | null
  registerId: string
  registerName: string
  paymentMethods: PaymentMethod[]
  mixedEnabled: boolean
  transferRecipients: TransferRecipient[]
  maxDiscountPercent: number
  canApproveDiscount: boolean
  canSellWithoutStock: boolean
  canReturn: boolean
  maxRedeemPercent: number
  approvers: { id: string; name: string }[]
}

export interface CashierProduct {
  id: string
  name: string
  article: string | null
  barcode: string | null
  imageUrl: string | null
  brandName: string | null
  unitShortName: string | null
  price: number | null
  stock: number
  nodeId: string | null
}

export interface CashierCatalog { items: CashierProduct[]; total: number; page: number; pageSize: number }

export type CashierSort = 'popular' | 'priceAsc' | 'priceDesc' | 'stock'

export interface CashierCustomer {
  id: string
  fullName: string
  phone: string
  birthDate: string | null
  level: string
  levelKey: string
  balance: number
  accrualPercent: number
  hasCard: boolean
  debtTotal: number
}

export interface SalePaymentInput {
  method: PaymentMethod
  amount: number
  received?: number | null
  transferRecipientId?: string | null
  /** Қарызда: қайтару күні (yyyy-MM-dd) мен түсініктеме. */
  dueDate?: string | null
  comment?: string | null
}

export interface Receipt {
  id: string
  number: number
  createdAt: string
  storeName: string
  registerName: string
  cashierName: string
  customer: { id: string; fullName: string; phone: string; bonusBalance: number | null } | null
  items: { id: string; productId: string; name: string; article: string | null; unit: string | null; quantity: number; price: number;
    lineTotal: number; discount: number; returnedQuantity: number }[]
  subtotal: number
  discountKind: string | null
  discountValue: number | null
  discountAmount: number
  bonusRedeemed: number
  bonusAccrued: number
  total: number
  payments: { method: PaymentMethod; amount: number; received: number | null; change: number | null; transferRecipient: string | null }[]
  status: SaleStatus
  returnedAmount: number
  debt: { id: string; amount: number; paid: number; remaining: number; dueDate: string; status: DebtStatus; comment: string | null } | null
  returns: ReceiptReturn[]
}

export type SaleStatus = 'Completed' | 'PartiallyReturned' | 'Returned'
export type DebtStatus = 'Open' | 'Paid'

export interface ReceiptReturn {
  id: string
  createdAt: string
  cashierName: string
  amount: number
  refunded: number
  refundMethod: PaymentMethod | null
  debtReduced: number
  bonusRestored: number
  bonusReversed: number
  reason: string | null
  items: { name: string; quantity: number; amount: number }[]
}

export interface SaleListItem {
  id: string
  number: number
  createdAt: string
  customerName: string | null
  total: number
  methods: PaymentMethod[]
  status: SaleStatus
}

export interface SalePage { items: SaleListItem[]; total: number; page: number; pageSize: number }

export interface Debt {
  id: string
  saleId: string | null
  saleNumber: number | null
  amount: number
  paid: number
  remaining: number
  dueDate: string
  comment: string | null
  status: DebtStatus
  createdAt: string
  cashierName: string
  payments: { amount: number; method: PaymentMethod | null; isReturn: boolean; transferRecipient: string | null;
    remainingAfter: number; createdAt: string; cashierName: string }[]
}
