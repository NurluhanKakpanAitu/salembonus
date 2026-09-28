import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Loader2, Monitor } from 'lucide-react'
import { RowMenu, type RowMenuItem } from '../ui/RowMenu'
import { toast } from '../ui/Toast'
import { ApiError } from '../../lib/api'
import { cart, totals, useCart } from '../../lib/cart'
import { cashierApi } from '../../lib/cashierApi'
import type { CashierProduct, PaymentMethod, Receipt, SalePaymentInput } from '../../lib/cashierTypes'
import { num } from '../../lib/money'
import { useT } from '../../lib/i18n'
import { CartPanel } from './CartPanel'
import { CatalogPanel, type CatalogFilter } from './CatalogPanel'
import { PaymentModal } from './PaymentModal'
import { ApprovalModal, ReceiptDoneModal } from './PosDialogs'
import { SearchBox, useBarcodeScanner } from './SearchBox'

const VIEW_KEY = 'salempos.pos.view'
const readView = (): 'grid' | 'list' => {
  try {
    return localStorage.getItem(VIEW_KEY) === 'list' ? 'list' : 'grid'
  } catch {
    return 'grid'
  }
}

type PayTab = Exclude<PaymentMethod, 'Debt'> | 'Mixed'

/**
 * Касса экраны (ТЗ «Касса» §2): сол жақта каталог, ортада тауарлар, оң жақта себет.
 * Сатылым сервердегі бір транзакциямен (§20.7); сәтті болса — себет тазарады, келесі сатылым.
 */
export function PosScreen({ menu = [] }: { menu?: RowMenuItem[] }) {
  const t = useT()
  const qc = useQueryClient()
  const ctxQuery = useQuery({ queryKey: ['cashier', 'context'], queryFn: cashierApi.context })
  const ctx = ctxQuery.data
  const cartState = useCart()
  const [filter, setFilter] = useState<CatalogFilter>({ nodeId: null, sort: 'popular', view: readView(), search: '' })
  const [pay, setPay] = useState<{ method: PayTab | null } | null>(null)
  const [busy, setBusy] = useState(false)
  const [payError, setPayError] = useState<string | null>(null)
  const [approval, setApproval] = useState<{ payments: SalePaymentInput[]; error: string | null } | null>(null)
  const [done, setDone] = useState<Receipt | null>(null)

  const updateFilter = (patch: Partial<CatalogFilter>) => {
    if (patch.view) try { localStorage.setItem(VIEW_KEY, patch.view) } catch { /* сақталмаса — келесіде тор */ }
    setFilter((f) => ({ ...f, ...patch }))
  }

  /** ТЗ §3.3: қалдық жетпесе — қате, себетке қосылмайды (минусқа сату құқығы болмаса). */
  const add = (p: CashierProduct) => {
    if (!ctx) return
    if (p.price == null) return toast(t('pos.noPriceError', { name: p.name }), 'error')
    const inCart = useCart.getState().lines.find((l) => l.productId === p.id)?.quantity ?? 0
    if (!ctx.canSellWithoutStock && inCart + 1 > p.stock)
      return toast(p.stock > 0 ? t('pos.stockLimit', { name: p.name, n: num(p.stock) }) : t('pos.outOfStockError', { name: p.name }), 'error')
    cart.add(p)
  }

  const scan = async (code: string) => {
    try {
      add(await cashierApi.byBarcode(code))
      return true
    } catch (err) {
      if (err instanceof ApiError && err.status === 404) toast(t('pos.barcodeNotFound', { code }), 'error')
      return false
    }
  }
  useBarcodeScanner((code) => void scan(code))

  const submit = async (payments: SalePaymentInput[], approvedBy?: { staffUserId: string; pin: string }) => {
    if (!ctx) return
    const state = useCart.getState()
    const sum = totals(state, ctx.maxRedeemPercent)
    // Шектен асқан жеңілдік — алдымен растау (сервер бәрібір тексереді).
    if (!approvedBy && !ctx.canApproveDiscount && sum.discountPercent > ctx.maxDiscountPercent + 0.0001) {
      setApproval({ payments, error: null })
      return
    }
    setBusy(true)
    setPayError(null)
    try {
      const receipt = await cashierApi.createSale({
        clientRequestId: state.requestId,
        items: state.lines.map((l) => ({ productId: l.productId, quantity: l.quantity })),
        customerId: state.customer?.id ?? null,
        bonusRedeem: sum.bonus,
        discount: sum.discount > 0 ? { kind: state.discount.kind, value: state.discount.value } : null,
        approval: approvedBy ?? null,
        payments,
      })
      cart.clear()
      setPay(null)
      setApproval(null)
      setDone(receipt)
      void qc.invalidateQueries({ queryKey: ['cashier', 'catalog'] })
      void qc.invalidateQueries({ queryKey: ['cashier', 'search'] })
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err)
      if (err instanceof ApiError && err.field === 'approval') setApproval({ payments, error: null })
      else if (err instanceof ApiError && err.field === 'pin' && approvedBy) setApproval({ payments, error: message })
      else {
        setApproval(null)
        setPayError(message)
      }
    } finally {
      setBusy(false)
    }
  }

  if (ctxQuery.error)
    return <p className="py-16 text-center text-[15px] text-danger">{ctxQuery.error.message}</p>
  if (!ctx)
    return <div className="flex h-full items-center justify-center text-ink-3"><Loader2 className="animate-spin" size={28} /></div>

  const total = totals(cartState, ctx.maxRedeemPercent).total

  return (
    <div className="-m-4 flex h-[calc(100%+2rem)] flex-col gap-3 overflow-y-auto bg-bg p-3 lg:-m-6 lg:h-[calc(100%+3rem)] lg:overflow-hidden">
      <div className="flex items-center gap-3">
        <SearchBox onAdd={add} onScan={scan} onShowAll={(q) => updateFilter({ search: q })} />
        <div className="hidden items-center gap-2 rounded-xl bg-surface px-3 py-2 md:flex">
          <Monitor size={18} className="text-brand" />
          <div className="leading-tight">
            <div className="text-[14px] font-semibold">{ctx.registerName}</div>
            <div className="max-w-52 truncate text-[12px] text-ink-3">{ctx.storeName}{ctx.storeAddress ? `, ${ctx.storeAddress}` : ''}</div>
          </div>
          <RowMenu items={menu} />
        </div>
      </div>

      <div className="flex min-h-0 flex-1 flex-col gap-3 lg:flex-row">
        <CatalogPanel filter={filter} onFilter={updateFilter} onAdd={add} />
        <CartPanel ctx={ctx} onPay={(method) => { setPayError(null); setPay({ method: method === 'Debt' ? null : method }) }} />
      </div>

      {pay && (
        <PaymentModal ctx={ctx} total={total} initial={pay.method as PayTab | null} busy={busy} error={payError}
          onClose={() => setPay(null)} onConfirm={(payments) => void submit(payments)} />
      )}
      {approval && (
        <ApprovalModal ctx={ctx} percent={totals(useCart.getState(), ctx.maxRedeemPercent).discountPercent}
          error={approval.error} busy={busy} onClose={() => setApproval(null)}
          onApprove={(staffUserId, pin) => void submit(approval.payments, { staffUserId, pin })} />
      )}
      {done && <ReceiptDoneModal receipt={done} onClose={() => setDone(null)} />}
    </div>
  )
}
