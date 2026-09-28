import { create } from 'zustand'
import type { CashierCustomer, CashierProduct } from './cashierTypes'
import { round2 } from './money'

export interface CartLine {
  productId: string
  name: string
  article: string | null
  imageUrl: string | null
  unit: string | null
  price: number
  stock: number
  quantity: number
}

export interface Discount { kind: 'Percent' | 'Amount'; value: number }

interface CartState {
  lines: CartLine[]
  customer: CashierCustomer | null
  bonusOn: boolean
  bonus: number
  discount: Discount
  /** Сатылым кілті: «Оплатить» қайта басылса да бір чек (сервер қайталауды таниды). */
  requestId: string
}

const KEY = 'salempos.cart'

const empty = (): CartState => ({
  lines: [], customer: null, bonusOn: false, bonus: 0, discount: { kind: 'Percent', value: 0 }, requestId: crypto.randomUUID(),
})

/**
 * Себет браузерде сақталады: касса бұғатталса, бет жаңарса не кассир ауысса да жоғалмайды
 * (ТЗ «Касса» §15.6, §18.3). Бағаны сервер сатылым кезінде қайта тексереді.
 */
function read(): CartState {
  try {
    const raw = localStorage.getItem(KEY)
    if (raw) return { ...empty(), ...JSON.parse(raw) }
  } catch {
    /* бұзылған не қолжетімсіз — бос себет */
  }
  return empty()
}

export const useCart = create<CartState>(() => read())

useCart.subscribe((s) => {
  try {
    localStorage.setItem(KEY, JSON.stringify(s))
  } catch {
    /* сақталмаса — бет жаңарғанда себет бос болады */
  }
})

const set = (patch: Partial<CartState>) => useCart.setState(patch)

export const cart = {
  add(p: CashierProduct, quantity = 1) {
    const lines = useCart.getState().lines
    const existing = lines.find((l) => l.productId === p.id)
    if (existing)
      set({ lines: lines.map((l) => (l.productId === p.id ? { ...l, quantity: round2(l.quantity + quantity), price: p.price ?? l.price, stock: p.stock } : l)) })
    else
      set({ lines: [...lines, {
        productId: p.id, name: p.name, article: p.article, imageUrl: p.imageUrl, unit: p.unitShortName,
        price: p.price ?? 0, stock: p.stock, quantity,
      }] })
  },
  setQuantity(productId: string, quantity: number) {
    set({ lines: useCart.getState().lines.map((l) => (l.productId === productId ? { ...l, quantity: Math.max(0, round2(quantity)) } : l)) })
  },
  remove(productId: string) {
    set({ lines: useCart.getState().lines.filter((l) => l.productId !== productId) })
  },
  setCustomer(customer: CashierCustomer | null) {
    set({ customer, bonusOn: false, bonus: 0 })
  },
  setBonus(bonusOn: boolean, bonus: number) {
    set({ bonusOn, bonus })
  },
  setDiscount(discount: Discount) {
    set({ discount })
  },
  clear() {
    set(empty())
  },
}

/** Себеттің сомалары — сервердегі есеппен бірдей (жеңілдік пайызы бүтін теңгеге дөңгелектенеді). */
export function totals(s: CartState, maxRedeemPercent: number) {
  const subtotal = round2(s.lines.reduce((sum, l) => sum + round2(l.price * l.quantity), 0))
  const d = s.discount.value > 0 ? s.discount : null
  const discount = !d ? 0
    : d.kind === 'Percent' ? Math.round((subtotal * Math.min(d.value, 100)) / 100)
    : Math.min(round2(d.value), subtotal)
  const afterDiscount = round2(subtotal - discount)
  const maxBonus = s.customer ? Math.min(s.customer.balance, Math.floor((afterDiscount * maxRedeemPercent) / 100)) : 0
  const bonus = s.customer && s.bonusOn ? Math.min(Math.max(0, Math.floor(s.bonus)), maxBonus) : 0
  const total = round2(afterDiscount - bonus)
  const accrual = s.customer ? Math.floor((total * s.customer.accrualPercent) / 100) : 0
  const discountPercent = subtotal > 0 ? (discount / subtotal) * 100 : 0
  return { subtotal, discount, discountPercent, afterDiscount, maxBonus, bonus, total, accrual }
}
