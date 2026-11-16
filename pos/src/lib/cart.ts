import { create } from 'zustand'
import type { CashierCustomer, CashierProduct } from './cashierTypes'
import { round2 } from './money'

export interface CartLine {
  productId: string
  name: string
  article: string | null
  imageUrl: string | null
  unit: string | null
  /** Сатылатын баға: кассир тек каталог бағасынан (basePrice) жоғары өзгерте алады. */
  price: number
  /** Каталог бағасы — төменгі шек (арзандату тек жеңілдікпен). */
  basePrice: number
  stock: number
  quantity: number
  bonusEligible: boolean
  /** Осы позицияның өз жеңілдігі (пайыз не сома); чек жеңілдігі бөлек. */
  discount?: Discount | null
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
    if (raw) {
      const saved = { ...empty(), ...JSON.parse(raw) } as CartState
      // Бұрын сақталған себетте basePrice жоқ — сол кездегі баға каталог бағасы деп саналады.
      return { ...saved, lines: saved.lines.map((l) => ({ ...l, basePrice: l.basePrice ?? l.price })) }
    }
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
    if (existing) {
      // Кассир көтерген баға сақталады; каталог бағасы одан да жоғары болып кетсе — каталогтікі.
      const base = p.price ?? existing.basePrice
      set({ lines: lines.map((l) => (l.productId === p.id
        ? { ...l, quantity: round2(l.quantity + quantity), basePrice: base, price: Math.max(l.price, base), stock: p.stock } : l)) })
    } else
      set({ lines: [...lines, {
        productId: p.id, name: p.name, article: p.article, imageUrl: p.imageUrl, unit: p.unitShortName,
        price: p.price ?? 0, basePrice: p.price ?? 0, stock: p.stock, quantity, bonusEligible: p.bonusEligible, discount: null,
      }] })
  },
  setQuantity(productId: string, quantity: number) {
    set({ lines: useCart.getState().lines.map((l) => (l.productId === productId ? { ...l, quantity: Math.max(0, round2(quantity)) } : l)) })
  },
  /** Бағаны тек көтеруге болады: каталог бағасынан төмені қабылданбайды (false). */
  setPrice(productId: string, price: number): boolean {
    const line = useCart.getState().lines.find((l) => l.productId === productId)
    if (!line || !(price >= line.basePrice)) return false
    set({ lines: useCart.getState().lines.map((l) => (l.productId === productId ? { ...l, price: round2(price) } : l)) })
    return true
  },
  setLineDiscount(productId: string, discount: Discount | null) {
    set({ lines: useCart.getState().lines.map((l) => (l.productId === productId ? { ...l, discount } : l)) })
  },
  remove(productId: string) {
    set({ lines: useCart.getState().lines.filter((l) => l.productId !== productId) })
  },
  setCustomer(customer: CashierCustomer | null) {
    set({ customer, bonusOn: false, bonus: 0 })
  },
  /** Клиенттің жаңа дерегі (баланс, қарыз): бонус таңдауы сақталады. */
  refreshCustomer(customer: CashierCustomer) {
    if (useCart.getState().customer?.id === customer.id) set({ customer })
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

/** Позиция: баға × саны, өз жеңілдігі (пайыз — бүтін теңгеге), жеңілдіктен кейінгі сома — сервердегі есеппен бірдей. */
export function lineMath(l: CartLine) {
  const gross = round2(l.price * l.quantity)
  const d = l.discount && l.discount.value > 0 ? l.discount : null
  const discount = !d ? 0 : d.kind === 'Percent' ? Math.round((gross * Math.min(d.value, 100)) / 100) : Math.min(round2(d.value), gross)
  return { gross, discount, net: round2(gross - discount) }
}

/** Себеттің сомалары — сервердегі есеппен бірдей: жол жеңілдіктері, сосын чек жеңілдігі (жол жеңілдіктерінен кейінгі сомадан). */
export function totals(s: CartState, maxRedeemPercent: number) {
  const math = s.lines.map(lineMath)
  const subtotal = round2(math.reduce((sum, m) => sum + m.gross, 0))
  const lineDiscounts = round2(math.reduce((sum, m) => sum + m.discount, 0))
  const net = round2(subtotal - lineDiscounts)
  const d = s.discount.value > 0 ? s.discount : null
  const checkDiscount = !d ? 0
    : d.kind === 'Percent' ? Math.round((net * Math.min(d.value, 100)) / 100)
    : Math.min(round2(d.value), net)
  const discount = round2(lineDiscounts + checkDiscount)
  const afterDiscount = round2(subtotal - discount)
  // Бонус тек «бонусқа қатысатын» тауарлардың үлесінен — сервердегі есеппен бірдей.
  const eligible = s.lines.reduce((sum, l, i) => sum + (l.bonusEligible !== false ? math[i].net : 0), 0)
  const eligibleShare = net > 0 ? eligible / net : 0
  const maxBonus = s.customer ? Math.min(s.customer.balance, Math.floor((round2(afterDiscount * eligibleShare) * maxRedeemPercent) / 100)) : 0
  const bonus = s.customer && s.bonusOn ? Math.min(Math.max(0, Math.floor(s.bonus)), maxBonus) : 0
  const total = round2(afterDiscount - bonus)
  const accrual = s.customer ? Math.floor((round2(total * eligibleShare) * s.customer.accrualPercent) / 100) : 0
  const discountPercent = subtotal > 0 ? (discount / subtotal) * 100 : 0
  return { subtotal, lineDiscounts, checkDiscount, discount, discountPercent, afterDiscount, maxBonus, bonus, total, accrual }
}
