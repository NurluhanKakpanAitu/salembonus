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
}

export interface SalePaymentInput {
  method: PaymentMethod
  amount: number
  received?: number | null
  transferRecipientId?: string | null
}

export interface Receipt {
  id: string
  number: number
  createdAt: string
  storeName: string
  registerName: string
  cashierName: string
  customer: { id: string; fullName: string; phone: string; bonusBalance: number | null } | null
  items: { productId: string; name: string; article: string | null; unit: string | null; quantity: number; price: number;
    lineTotal: number; discount: number; returnedQuantity: number }[]
  subtotal: number
  discountKind: string | null
  discountValue: number | null
  discountAmount: number
  bonusRedeemed: number
  bonusAccrued: number
  total: number
  payments: { method: PaymentMethod; amount: number; received: number | null; change: number | null; transferRecipient: string | null }[]
  status: string
  returnedAmount: number
}
