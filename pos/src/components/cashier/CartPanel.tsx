import { useEffect, useState, type FormEvent } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Banknote, CreditCard, Gift, Minus, Percent, Plus, QrCode, ShoppingCart, Trash2, UserPlus, UserRound, X } from 'lucide-react'
import { Button } from '../ui/Button'
import { Field, Input } from '../ui/Field'
import { Modal } from '../ui/Modal'
import { ApiError } from '../../lib/api'
import { cart, totals, useCart } from '../../lib/cart'
import { cashierApi } from '../../lib/cashierApi'
import type { CashierContext, CashierCustomer, PaymentMethod } from '../../lib/cashierTypes'
import { num, tenge } from '../../lib/money'
import { formatPhoneInput, isCompletePhone, phoneDigits } from '../../lib/phone'
import { useT } from '../../lib/i18n'
import { ProductImage } from './CatalogPanel'

/** SalemBonus мәртебелері (CustomerLevel). */
const LEVEL_STYLE: Record<string, string> = {
  New: 'bg-field text-ink-2', Regular: 'bg-sky-100 text-sky-700', Favorite: 'bg-violet-100 text-violet-700',
  Vip: 'bg-amber-200 text-amber-800',
}

/**
 * Себет (ТЗ «Касса» §2.9, §4, §6): тауарлар, клиент, бонус, жеңілдік, итог және төлем батырмалары.
 * Итог әр өзгерістен кейін бірден қайта есептеледі (§6.7).
 */
export function CartPanel({ ctx, onPay }: { ctx: CashierContext; onPay: (method: PaymentMethod | 'Mixed' | null) => void }) {
  const t = useT()
  const state = useCart()
  const sum = totals(state, ctx.maxRedeemPercent)
  const count = state.lines.length
  const quick = (['Cash', 'Card', 'Qr'] as const).filter((m) => ctx.paymentMethods.includes(m))
  const QUICK_ICON = { Cash: Banknote, Card: CreditCard, Qr: QrCode }

  const changeQty = (productId: string, next: number, stock: number) => {
    if (next <= 0) return cart.remove(productId)
    if (!ctx.canSellWithoutStock && next > stock) return
    cart.setQuantity(productId, next)
  }

  return (
    <aside className="flex min-h-0 w-full shrink-0 flex-col rounded-2xl bg-surface lg:w-[360px] xl:w-[390px]">
      <div className="flex items-center justify-between border-b border-line px-4 py-3">
        <h2 className="text-[18px] font-bold">{t('pos.cart', { n: count })}</h2>
        {count > 0 && (
          <button type="button" onClick={() => window.confirm(t('pos.clearConfirm')) && cart.clear()}
            className="flex items-center gap-1.5 text-[13px] text-ink-2 hover:text-danger">
            <Trash2 size={15} /> {t('pos.clear')}
          </button>
        )}
      </div>

      <div className="min-h-0 flex-1 overflow-y-auto">
        {count === 0 ? (
          <div className="flex flex-col items-center gap-2 px-6 py-12 text-center text-ink-3">
            <ShoppingCart size={34} />
            <p className="text-[14px]">{t('pos.cartEmpty')}</p>
          </div>
        ) : (
          <ul className="divide-y divide-line">
            {state.lines.map((l) => {
              const over = !ctx.canSellWithoutStock && l.quantity > l.stock
              return (
                <li key={l.productId} className="flex gap-3 px-4 py-3">
                  <ProductImage url={l.imageUrl} className="size-14 shrink-0 rounded-lg border border-line" />
                  <div className="min-w-0 flex-1">
                    <div className="flex items-start gap-2">
                      <div className="min-w-0 flex-1">
                        <div className="line-clamp-2 text-[14px] font-semibold leading-5">{l.name}</div>
                        {l.article && <div className="text-[12px] text-ink-3">{t('pos.article', { a: l.article })}</div>}
                      </div>
                      <button type="button" aria-label={t('catalog.delete')} onClick={() => cart.remove(l.productId)}
                        className="flex size-7 shrink-0 items-center justify-center rounded-lg text-ink-3 hover:bg-field hover:text-danger">
                        <X size={16} />
                      </button>
                    </div>
                    <div className="mt-1.5 flex items-center gap-2">
                      <span className="text-[13px] text-ink-2">{tenge(l.price)}</span>
                      <div className="ml-auto flex items-center rounded-lg bg-field">
                        <button type="button" aria-label="-" onClick={() => changeQty(l.productId, l.quantity - 1, l.stock)}
                          className="flex size-8 items-center justify-center text-brand"><Minus size={15} /></button>
                        <input value={num(l.quantity)} inputMode="decimal" aria-label={t('product.quantity')}
                          onChange={(e) => {
                            const v = Number(e.target.value.replace(',', '.').replace(/\s/g, ''))
                            if (Number.isFinite(v)) changeQty(l.productId, v, l.stock)
                          }}
                          className="w-10 bg-transparent text-center text-[14px] font-semibold outline-none" />
                        <button type="button" aria-label="+" onClick={() => changeQty(l.productId, l.quantity + 1, l.stock)}
                          disabled={!ctx.canSellWithoutStock && l.quantity + 1 > l.stock}
                          className="flex size-8 items-center justify-center text-brand disabled:text-ink-3"><Plus size={15} /></button>
                      </div>
                      <span className="w-24 text-right text-[14px] font-bold">{tenge(Math.round(l.price * l.quantity * 100) / 100)}</span>
                    </div>
                    {over && <p className="mt-1 text-[12px] text-danger">{t('pos.stockLeft', { n: num(l.stock) })}</p>}
                  </div>
                </li>
              )
            })}
          </ul>
        )}

        <div className="flex flex-col gap-3 border-t border-line p-4">
          <CustomerBlock />
          {state.customer && <BonusBlock sum={sum} />}
          <DiscountBlock ctx={ctx} sum={sum} />
        </div>
      </div>

      <div className="border-t border-line p-4">
        {(sum.discount > 0 || sum.bonus > 0) && (
          <div className="mb-2 space-y-1 text-[13px] text-ink-2">
            <Row label={t('pos.subtotal')} value={tenge(sum.subtotal)} />
            {sum.discount > 0 && <Row label={t('pos.discount')} value={`− ${tenge(sum.discount)}`} />}
            {sum.bonus > 0 && <Row label={t('pos.bonusUsed')} value={`− ${tenge(sum.bonus)}`} />}
          </div>
        )}
        <div className="mb-3 flex items-baseline justify-between">
          <span className="text-[18px] font-bold">{t('pos.total')}</span>
          <span className="text-[28px] font-extrabold tracking-tight">{tenge(sum.total)}</span>
        </div>
        <Button className="h-14! w-full text-[17px]!" icon={<CreditCard size={20} />} disabled={count === 0} onClick={() => onPay(null)}>
          {t('pos.pay', { sum: tenge(sum.total) })}
        </Button>
        {quick.length > 0 && (
          <div className="mt-2 grid gap-2" style={{ gridTemplateColumns: `repeat(${quick.length}, minmax(0, 1fr))` }}>
            {quick.map((m) => {
              const Icon = QUICK_ICON[m]
              return (
                <button key={m} type="button" disabled={count === 0} onClick={() => onPay(m)}
                  className="flex h-12 items-center justify-center gap-2 rounded-xl border border-line text-[14px] font-medium hover:bg-field disabled:opacity-50">
                  <Icon size={18} className="text-brand" /> {t(`pay.${m}`)}
                </button>
              )
            })}
          </div>
        )}
      </div>
    </aside>
  )
}

function Row({ label, value }: { label: string; value: string }) {
  return <div className="flex justify-between"><span>{label}</span><span className="font-medium text-ink">{value}</span></div>
}

// ---------------- Клиент (ТЗ §4) ----------------

function CustomerBlock() {
  const t = useT()
  const customer = useCart((s) => s.customer)
  const [q, setQ] = useState('')
  const [term, setTerm] = useState('')
  const [registering, setRegistering] = useState(false)

  useEffect(() => {
    const id = setTimeout(() => setTerm(q.trim()), 300)
    return () => clearTimeout(id)
  }, [q])

  const { data, isFetching } = useQuery({
    queryKey: ['cashier', 'customers', term],
    queryFn: () => cashierApi.searchCustomers(term),
    enabled: term.length >= 2 && !customer,
  })

  if (customer) {
    return (
      <div className="flex items-center gap-3 rounded-xl bg-brand-soft/60 px-3 py-2.5">
        <span className="flex size-10 shrink-0 items-center justify-center rounded-full bg-surface text-brand"><UserRound size={20} /></span>
        <div className="min-w-0 flex-1">
          <div className="text-[12px] text-brand">{t('pos.customer')}</div>
          <div className="truncate text-[14px] font-semibold">{customer.fullName || t('pos.noName')}</div>
          <div className="text-[12px] text-ink-2">{formatPhoneInput(customer.phone)}</div>
        </div>
        <span className={`rounded-lg px-2 py-1 text-[11px] font-bold uppercase ${LEVEL_STYLE[customer.levelKey] ?? LEVEL_STYLE.New}`}>{customer.level}</span>
        <button type="button" aria-label={t('pos.removeCustomer')} onClick={() => cart.setCustomer(null)}
          className="flex size-8 items-center justify-center rounded-lg text-ink-3 hover:bg-surface hover:text-danger"><X size={16} /></button>
      </div>
    )
  }

  const results = term.length >= 2 ? data ?? [] : []
  const looksLikePhone = phoneDigits(q).length >= 10
  const pick = (c: CashierCustomer) => {
    cart.setCustomer(c)
    setQ('')
  }

  return (
    <div>
      <div className="relative">
        <UserRound size={17} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-ink-3" />
        <input value={q} onChange={(e) => setQ(e.target.value)} placeholder={t('pos.customerSearch')}
          className="h-11 w-full rounded-xl border border-line bg-field pl-9 pr-3 text-[14px] outline-none focus:border-brand focus:bg-surface" />
      </div>
      {term.length >= 2 && (
        <div className="mt-1.5 overflow-hidden rounded-xl border border-line">
          {results.map((c) => (
            <button key={c.id} type="button" onClick={() => pick(c)} className="flex w-full items-center gap-3 px-3 py-2 text-left hover:bg-field">
              <div className="min-w-0 flex-1">
                <div className="truncate text-[14px] font-medium">{c.fullName || t('pos.noName')}</div>
                <div className="text-[12px] text-ink-3">{formatPhoneInput(c.phone)}</div>
              </div>
              <span className="text-[12px] font-semibold text-success">{num(c.balance)} Б</span>
            </button>
          ))}
          {results.length === 0 && (
            <div className="px-3 py-2.5 text-[13px] text-ink-3">{isFetching ? t('common.loading') : t('pos.customerNotFound')}</div>
          )}
          {!isFetching && results.length === 0 && (
            <button type="button" onClick={() => setRegistering(true)}
              className="flex w-full items-center gap-2 border-t border-line px-3 py-2.5 text-[14px] font-medium text-brand hover:bg-field">
              <UserPlus size={16} /> {t('pos.registerCustomer')}
            </button>
          )}
        </div>
      )}
      {registering && (
        <RegisterCustomerModal phone={looksLikePhone ? formatPhoneInput(q) : ''}
          onClose={() => setRegistering(false)}
          onDone={(c) => { setRegistering(false); pick(c) }} />
      )}
    </div>
  )
}

/** Кассада тіркеу (ТЗ §4.4–4.10): аты, тегі, туған күні; мекенжайы — дүкеннің тіркелген жерінен. */
function RegisterCustomerModal({ phone: initialPhone, onClose, onDone }: {
  phone: string
  onClose: () => void
  onDone: (c: CashierCustomer) => void
}) {
  const t = useT()
  const [phone, setPhone] = useState(initialPhone || '+7 ')
  const [firstName, setFirstName] = useState('')
  const [lastName, setLastName] = useState('')
  const [birthDate, setBirthDate] = useState('')
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [loading, setLoading] = useState(false)

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    const local: Record<string, string> = {}
    if (!isCompletePhone(phone)) local.phone = t('login.phoneInvalid')
    if (!firstName.trim()) local.firstName = t('common.required')
    if (!lastName.trim()) local.lastName = t('common.required')
    if (!birthDate) local.birthDate = t('common.required')
    setErrors(local)
    if (Object.keys(local).length) return
    setLoading(true)
    try {
      onDone(await cashierApi.registerCustomer({ phone, firstName, lastName, birthDate }))
    } catch (err) {
      setErrors(err instanceof ApiError && err.field ? { [err.field]: err.message } : { form: err instanceof Error ? err.message : String(err) })
    } finally {
      setLoading(false)
    }
  }

  return (
    <Modal title={t('pos.registerCustomer')} onClose={onClose} width={460}>
      <form onSubmit={(e) => void submit(e)} noValidate className="flex flex-col gap-4">
        <Field label={t('login.phone')} required error={errors.phone}>
          <Input value={phone} inputMode="tel" onChange={(e) => setPhone(formatPhoneInput(e.target.value))} error={!!errors.phone} />
        </Field>
        <div className="grid grid-cols-2 gap-3">
          <Field label={t('pos.firstName')} required error={errors.firstName}>
            <Input autoFocus value={firstName} onChange={(e) => setFirstName(e.target.value)} error={!!errors.firstName} maxLength={60} />
          </Field>
          <Field label={t('pos.lastName')} required error={errors.lastName}>
            <Input value={lastName} onChange={(e) => setLastName(e.target.value)} error={!!errors.lastName} maxLength={60} />
          </Field>
        </div>
        <Field label={t('pos.birthDate')} required error={errors.birthDate}>
          <Input type="date" value={birthDate} max={new Date().toISOString().slice(0, 10)} onChange={(e) => setBirthDate(e.target.value)} error={!!errors.birthDate} />
        </Field>
        <p className="rounded-xl bg-field px-3.5 py-2.5 text-[13px] text-ink-2">{t('pos.geoAuto')}</p>
        {errors.form && <p className="text-[14px] text-danger">{errors.form}</p>}
        <div className="flex justify-end gap-2">
          <Button type="button" variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" loading={loading}>{t('pos.register')}</Button>
        </div>
      </form>
    </Modal>
  )
}

// ---------------- Бонус пен жеңілдік (ТЗ §6) ----------------

function BonusBlock({ sum }: { sum: ReturnType<typeof totals> }) {
  const t = useT()
  const { customer, bonusOn, bonus } = useCart()
  if (!customer) return null
  const left = customer.balance - sum.bonus
  return (
    <div className="rounded-xl border border-line p-3">
      <div className="flex items-center gap-2 text-[14px]">
        <Gift size={17} className="text-danger" />
        <span className="text-ink-2">{t('pos.bonusBalance')}</span>
        <span className="font-bold text-success">{num(customer.balance)} Б</span>
      </div>
      <div className="mt-2 flex items-center gap-2">
        <label className="flex cursor-pointer items-center gap-2 text-[14px]">
          <input type="checkbox" className="size-4 accent-brand" checked={bonusOn} disabled={sum.maxBonus <= 0}
            onChange={(e) => cart.setBonus(e.target.checked, e.target.checked ? sum.maxBonus : 0)} />
          {t('pos.redeemBonus')}
        </label>
        <input value={bonusOn ? String(bonus) : ''} disabled={!bonusOn} inputMode="numeric"
          onChange={(e) => cart.setBonus(true, Math.min(Number(e.target.value.replace(/\D/g, '')) || 0, sum.maxBonus))}
          className="ml-auto h-9 w-24 rounded-lg border border-line bg-field px-2 text-right text-[14px] outline-none focus:border-brand disabled:opacity-50" />
        <span className="text-[13px] text-ink-3">Б</span>
        <button type="button" disabled={sum.maxBonus <= 0} onClick={() => cart.setBonus(true, sum.maxBonus)}
          className="h-9 rounded-lg border border-brand px-3 text-[13px] font-medium text-brand disabled:border-line disabled:text-ink-3">
          {t('pos.all')}
        </button>
      </div>
      <div className="mt-2 flex justify-between text-[12px]">
        <span className="text-ink-2">{t('pos.bonusLeft')}: <b className="text-success">{num(left)} Б</b></span>
        <span className="text-ink-2">{t('pos.willAccrue')}: <b className="text-success">+{num(sum.accrual)} Б</b></span>
      </div>
      {sum.maxBonus < customer.balance && (
        <p className="mt-1 text-[12px] text-ink-3">{t('pos.bonusLimit', { n: num(sum.maxBonus) })}</p>
      )}
    </div>
  )
}

function DiscountBlock({ ctx, sum }: { ctx: CashierContext; sum: ReturnType<typeof totals> }) {
  const t = useT()
  const discount = useCart((s) => s.discount)
  const needsApproval = !ctx.canApproveDiscount && sum.discountPercent > ctx.maxDiscountPercent + 0.0001
  return (
    <div className="rounded-xl border border-line p-3">
      <div className="flex items-center gap-2">
        <span className="flex size-8 items-center justify-center rounded-full bg-violet-100 text-violet-600"><Percent size={16} /></span>
        <span className="text-[14px] text-ink-2">{t('pos.discountOnReceipt')}</span>
        <input value={discount.value ? String(discount.value) : ''} inputMode="decimal" placeholder="0"
          onChange={(e) => cart.setDiscount({ ...discount, value: Number(e.target.value.replace(',', '.').replace(/[^\d.]/g, '')) || 0 })}
          className="ml-auto h-9 w-20 rounded-lg border border-line bg-field px-2 text-right text-[14px] outline-none focus:border-brand" />
        <div className="flex rounded-lg bg-field p-0.5">
          {(['Percent', 'Amount'] as const).map((k) => (
            <button key={k} type="button" onClick={() => cart.setDiscount({ ...discount, kind: k })}
              className={`h-8 w-8 rounded-md text-[13px] font-semibold ${discount.kind === k ? 'bg-surface text-brand shadow-sm' : 'text-ink-3'}`}>
              {k === 'Percent' ? '%' : '₸'}
            </button>
          ))}
        </div>
      </div>
      {sum.discount > 0 && (
        <p className={`mt-1.5 text-[12px] ${needsApproval ? 'text-amber-600' : 'text-ink-3'}`}>
          {needsApproval ? t('pos.discountNeedsApproval', { limit: num(ctx.maxDiscountPercent) }) : `− ${tenge(sum.discount)}`}
        </p>
      )}
    </div>
  )
}
