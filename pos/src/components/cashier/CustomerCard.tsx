import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Banknote, CreditCard, Landmark, Loader2, QrCode, UserRound } from 'lucide-react'
import { Button } from '../ui/Button'
import { Modal } from '../ui/Modal'
import { toast } from '../ui/Toast'
import { ApiError } from '../../lib/api'
import { cart } from '../../lib/cart'
import { cashierApi } from '../../lib/cashierApi'
import type { CashierContext, CashierCustomer, Debt, PaymentMethod } from '../../lib/cashierTypes'
import { dateOnly, dateTime } from '../../lib/dates'
import { num, round2, tenge } from '../../lib/money'
import { formatPhoneInput } from '../../lib/phone'
import { useT, type TranslationKey } from '../../lib/i18n'

type Tab = 'info' | 'debts'

/**
 * Клиент карточкасы (ТЗ «Касса» §5). Әзірге: ақпарат және қарыздар (өтеу тарихымен). Бонус тарихы,
 * сатып алулар мен қайтарулар — Касса 3-қадамында.
 */
export function CustomerCardModal({ ctx, customer, onClose }: { ctx: CashierContext; customer: CashierCustomer; onClose: () => void }) {
  const t = useT()
  const qc = useQueryClient()
  const [tab, setTab] = useState<Tab>(customer.debtTotal > 0 ? 'debts' : 'info')
  const [repaying, setRepaying] = useState<Debt | null>(null)
  const fresh = useQuery({ queryKey: ['cashier', 'customer', customer.id], queryFn: () => cashierApi.customer(customer.id), initialData: customer })
  const c = fresh.data
  const debts = useQuery({ queryKey: ['cashier', 'debts', customer.id], queryFn: () => cashierApi.customerDebts(customer.id) })
  const open = (debts.data ?? []).filter((d) => d.status === 'Open')

  const refresh = async () => {
    await qc.invalidateQueries({ queryKey: ['cashier', 'debts', customer.id] })
    const updated = await cashierApi.customer(customer.id)
    qc.setQueryData(['cashier', 'customer', customer.id], updated)
    cart.refreshCustomer(updated)
  }

  return (
    <Modal title={t('customer.title')} onClose={onClose} width={620}>
      <div className="flex items-center gap-4">
        <span className="flex size-14 items-center justify-center rounded-full bg-brand-soft text-brand"><UserRound size={26} /></span>
        <div className="min-w-0 flex-1">
          <div className="text-[18px] font-bold">{c.fullName || t('pos.noName')}</div>
          <div className="text-[14px] text-ink-2">{formatPhoneInput(c.phone)}{c.birthDate && ` · ${dateOnly(c.birthDate)}`}</div>
        </div>
        <span className="rounded-lg bg-amber-100 px-2.5 py-1 text-[12px] font-bold uppercase text-amber-800">{c.level}</span>
      </div>

      <div className="mt-4 grid grid-cols-2 gap-1 rounded-xl bg-field p-1">
        {(['info', 'debts'] as const).map((k) => (
          <button key={k} type="button" onClick={() => setTab(k)}
            className={`flex items-center justify-center gap-1.5 rounded-lg py-2 text-[14px] font-medium ${tab === k ? 'bg-brand text-white' : 'text-ink-2'}`}>
            {t(`customer.tab.${k}` as TranslationKey)}
            {k === 'debts' && open.length > 0 && <span className="rounded-full bg-danger px-1.5 text-[11px] text-white">{open.length}</span>}
          </button>
        ))}
      </div>

      {tab === 'info' && (
        <div className="mt-4 grid grid-cols-2 gap-3">
          <Stat label={t('pos.bonusBalance')} value={`${num(c.balance)} Б`} tone="success" />
          <Stat label={t('customer.accrual')} value={`${num(c.accrualPercent)}%`} />
          <Stat label={t('customer.debt')} value={tenge(c.debtTotal)} tone={c.debtTotal > 0 ? 'danger' : undefined} />
          <Stat label={t('customer.level')} value={c.level} />
        </div>
      )}

      {tab === 'debts' && (
        <div className="mt-4 flex flex-col gap-3">
          {debts.isLoading && <div className="flex justify-center py-6 text-ink-3"><Loader2 className="animate-spin" /></div>}
          {!debts.isLoading && (debts.data ?? []).length === 0 && <p className="py-6 text-center text-[14px] text-ink-3">{t('customer.noDebts')}</p>}
          {open.map((d) => (
            <div key={d.id} className="rounded-xl border border-danger/30 bg-danger-soft/40 p-3">
              <div className="flex items-center gap-3">
                <div className="min-w-0 flex-1">
                  <div className="flex items-center gap-2">
                    <span className="text-[20px] font-bold">{tenge(d.remaining)}</span>
                    <span className="rounded-md bg-danger px-1.5 py-0.5 text-[11px] font-medium text-white">{t('debt.status.Open')}</span>
                  </div>
                  <div className="text-[13px] text-ink-2">
                    {t('debt.issued')}: {dateOnly(d.createdAt)} · {t('debt.due')}: <b className={overdue(d) ? 'text-danger' : ''}>{dateOnly(d.dueDate)}</b>
                    {d.saleNumber && ` · ${t('pos.receiptNo', { n: d.saleNumber })}`}
                  </div>
                  {d.paid > 0 && <div className="text-[12px] text-ink-3">{t('debt.paidOf', { paid: tenge(d.paid), total: tenge(d.amount) })}</div>}
                </div>
                <Button className="h-10!" onClick={() => setRepaying(d)}>{t('debt.repay')}</Button>
              </div>
            </div>
          ))}
          {(debts.data ?? []).length > 0 && (
            <div className="overflow-hidden rounded-xl border border-line">
              <div className="bg-field px-3 py-2 text-[13px] font-semibold">{t('debt.history')}</div>
              <div className="max-h-64 overflow-y-auto">
                {(debts.data ?? []).map((d) => (
                  <div key={d.id} className="border-t border-line px-3 py-2 text-[13px]">
                    <div className="flex justify-between">
                      <span>{dateOnly(d.createdAt)} · {tenge(d.amount)} · {t('debt.due')} {dateOnly(d.dueDate)}</span>
                      <span className={d.status === 'Paid' ? 'text-success' : 'text-danger'}>{t(`debt.status.${d.status}` as TranslationKey)}</span>
                    </div>
                    {d.payments.map((p, i) => (
                      <div key={i} className="mt-0.5 flex justify-between pl-3 text-[12px] text-ink-2">
                        <span>{dateTime(p.createdAt)} · {p.isReturn ? t('debt.byReturn') : p.method ? t(`pay.${p.method}`) : ''} · {p.cashierName}</span>
                        <span>{tenge(p.amount)} → {t('debt.left', { sum: tenge(p.remainingAfter) })}</span>
                      </div>
                    ))}
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>
      )}

      {repaying && (
        <RepayModal ctx={ctx} debt={repaying} customerName={c.fullName || c.phone} onClose={() => setRepaying(null)}
          onDone={async (d) => {
            setRepaying(null)
            toast(d.status === 'Paid' ? t('debt.closed') : t('debt.repaid', { sum: tenge(d.remaining) }))
            await refresh()
          }} />
      )}
    </Modal>
  )
}

const overdue = (d: Debt) => d.status === 'Open' && new Date(d.dueDate) < new Date(new Date().toDateString())

function Stat({ label, value, tone }: { label: string; value: string; tone?: 'success' | 'danger' }) {
  return (
    <div className="rounded-xl border border-line px-4 py-3">
      <div className="text-[12px] text-ink-2">{label}</div>
      <div className={`text-[18px] font-bold ${tone === 'success' ? 'text-success' : tone === 'danger' ? 'text-danger' : ''}`}>{value}</div>
    </div>
  )
}

const METHODS: { m: Exclude<PaymentMethod, 'Debt'>; icon: typeof Banknote }[] = [
  { m: 'Cash', icon: Banknote }, { m: 'Card', icon: CreditCard }, { m: 'Qr', icon: QrCode }, { m: 'Transfer', icon: Landmark },
]

/** Қарызды өтеу (ТЗ §11.7–11.11): бөлшек не толық, түрі таңдалады, күні мен кассирі автоматты. */
function RepayModal({ ctx, debt, customerName, onClose, onDone }: {
  ctx: CashierContext
  debt: Debt
  customerName: string
  onClose: () => void
  onDone: (d: Debt) => void
}) {
  const t = useT()
  const [amount, setAmount] = useState(String(debt.remaining))
  const [method, setMethod] = useState<Exclude<PaymentMethod, 'Debt'>>('Cash')
  const [recipient, setRecipient] = useState(ctx.transferRecipients[0]?.id ?? '')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const value = round2(Number(amount.replace(',', '.').replace(/\s/g, '')) || 0)
  const available = METHODS.filter((x) => x.m !== 'Transfer' || ctx.transferRecipients.length > 0)

  const submit = async () => {
    setBusy(true)
    setError(null)
    try {
      onDone(await cashierApi.repayDebt(debt.id, { amount: value, method, transferRecipientId: method === 'Transfer' ? recipient : null }))
    } catch (err) {
      setError(err instanceof ApiError ? err.message : String(err))
    } finally {
      setBusy(false)
    }
  }

  return (
    <Modal title={t('debt.repayTitle')} onClose={onClose} width={460}>
      <div className="flex flex-col gap-4 text-[14px]">
        <div className="space-y-1 rounded-xl bg-field px-4 py-3">
          <div className="flex justify-between"><span className="text-ink-2">{t('pos.customer')}</span><b>{customerName}</b></div>
          <div className="flex justify-between"><span className="text-ink-2">{t('debt.toRepay')}</span><b>{tenge(debt.remaining)}</b></div>
          <div className="flex justify-between"><span className="text-ink-2">{t('debt.due')}</span><b>{dateOnly(debt.dueDate)}</b></div>
        </div>
        <label className="flex flex-col gap-1.5 font-medium text-ink-2">
          {t('debt.amount')}
          <div className="flex gap-2">
            <input value={amount} inputMode="decimal" onChange={(e) => setAmount(e.target.value)}
              className="h-12 min-w-0 flex-1 rounded-xl border-2 border-line px-3 text-right text-[20px] font-bold text-ink outline-none focus:border-brand" />
            <Button variant="secondary" className="h-12!" onClick={() => setAmount(String(debt.remaining))}>{t('pos.all')}</Button>
          </div>
        </label>
        {value > 0 && value < debt.remaining && (
          <p className="text-[13px] text-ink-2">{t('debt.willRemain', { sum: tenge(round2(debt.remaining - value)) })}</p>
        )}
        <div className="grid gap-2" style={{ gridTemplateColumns: `repeat(${available.length}, minmax(0, 1fr))` }}>
          {available.map(({ m, icon: Icon }) => (
            <button key={m} type="button" onClick={() => setMethod(m)}
              className={`flex flex-col items-center gap-1 rounded-xl border-2 py-2.5 text-[13px] font-medium ${method === m ? 'border-brand bg-brand-soft text-brand' : 'border-line'}`}>
              <Icon size={19} /> {t(`pay.${m}`)}
            </button>
          ))}
        </div>
        {method === 'Transfer' && (
          <select value={recipient} onChange={(e) => setRecipient(e.target.value)}
            className="h-11 rounded-xl border border-line bg-field px-3 outline-none focus:border-brand">
            {ctx.transferRecipients.map((r) => <option key={r.id} value={r.id}>{r.bankName} · {r.account} · {r.holderName}</option>)}
          </select>
        )}
        {error && <p className="rounded-xl bg-danger-soft px-4 py-3 text-danger">{error}</p>}
        <div className="grid grid-cols-2 gap-2">
          <Button variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
          <Button loading={busy} disabled={value <= 0 || value > debt.remaining} onClick={() => void submit()}>{t('debt.repay')}</Button>
        </div>
      </div>
    </Modal>
  )
}
