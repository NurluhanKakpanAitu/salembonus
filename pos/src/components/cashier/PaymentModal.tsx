import { useMemo, useState } from 'react'
import { Banknote, Check, CheckCircle2, Copy, CreditCard, Equal, HandCoins, Info, Landmark, QrCode, Shuffle, Smartphone, UserRound } from 'lucide-react'
import { Button } from '../ui/Button'
import { Modal } from '../ui/Modal'
import { toast } from '../ui/Toast'
import type { CashierContext, PaymentMethod, SalePaymentInput } from '../../lib/cashierTypes'
import { round2, tenge } from '../../lib/money'
import { addDays, isoDate } from '../../lib/dates'
import { useT } from '../../lib/i18n'

type Tab = PaymentMethod | 'Mixed'
const ICON: Record<Tab, typeof Banknote> = { Cash: Banknote, Card: CreditCard, Qr: QrCode, Transfer: Landmark, Debt: HandCoins, Mixed: Shuffle }

const parse = (v: string) => {
  const n = Number(v.replace(',', '.').replace(/\s/g, ''))
  return Number.isFinite(n) && n >= 0 ? round2(n) : 0
}

/**
 * Төлем терезесі (ТЗ «Касса» §7–10). Жылдам түрлер бүкіл соманы бір түрге береді; аралас төлемде
 * «=» қалған соманы таңдалған түрге береді. Кем не артық төлем расталмайды — сервер де тексереді.
 */
export function PaymentModal({ ctx, total, customerName, initial, busy, error, onClose, onConfirm }: {
  ctx: CashierContext
  total: number
  /** Қарыз тек анықталған клиентке (ТЗ §11.1). */
  customerName: string | null
  initial: Tab | null
  busy: boolean
  error: string | null
  onClose: () => void
  onConfirm: (payments: SalePaymentInput[]) => void
}) {
  const t = useT()
  const single = (['Cash', 'Card', 'Qr', 'Transfer', 'Debt'] as const).filter((m) => ctx.paymentMethods.includes(m))
  const tabs: Tab[] = [...single, ...(ctx.mixedEnabled && single.length > 1 ? (['Mixed'] as const) : [])]
  const [tab, setTab] = useState<Tab>(initial && tabs.includes(initial) ? initial : tabs[0])
  const [received, setReceived] = useState('')
  const [recipient, setRecipient] = useState<string | null>(ctx.transferRecipients.length === 1 ? ctx.transferRecipients[0].id : null)
  const [mixed, setMixed] = useState<Record<string, string>>({})
  const [dueDate, setDueDate] = useState(isoDate(addDays(new Date(), 7)))
  const [comment, setComment] = useState('')
  const debtInfo = { dueDate, comment: comment.trim() || null }

  const cash = parse(received)
  const change = round2(cash - total)

  // Жылдам сомалар: дәл, 1000-ға дейін дөңгелектеу, ірі купюралар.
  const quickCash = useMemo(() => {
    const list = [total, Math.ceil(total / 1000) * 1000, Math.ceil(total / 5000) * 5000, Math.ceil(total / 10000) * 10000, 20000]
    return [...new Set(list.filter((v) => v >= total))].slice(0, 5)
  }, [total])

  const mixedSum = round2(single.reduce((s, m) => s + parse(mixed[m] ?? ''), 0))
  const remaining = round2(total - mixedSum)

  const payments = (): SalePaymentInput[] | null => {
    if (total === 0) return []
    switch (tab) {
      case 'Cash': return cash >= total ? [{ method: 'Cash', amount: total, received: cash }] : null
      case 'Card': return [{ method: 'Card', amount: total }]
      case 'Qr': return [{ method: 'Qr', amount: total }]
      case 'Transfer': return recipient ? [{ method: 'Transfer', amount: total, transferRecipientId: recipient }] : null
      case 'Debt': return customerName && dueDate ? [{ method: 'Debt', amount: total, ...debtInfo }] : null
      case 'Mixed': {
        if (remaining !== 0) return null
        const list = single.map((m) => ({ method: m, amount: parse(mixed[m] ?? '') })).filter((p) => p.amount > 0)
        if (list.some((p) => p.method === 'Transfer') && !recipient) return null
        if (list.some((p) => p.method === 'Debt') && (!customerName || !dueDate)) return null
        return list.map((p) => (p.method === 'Transfer' ? { ...p, transferRecipientId: recipient }
          : p.method === 'Debt' ? { ...p, ...debtInfo } : p))
      }
    }
  }
  const ready = payments()

  const copy = (text: string) => {
    void navigator.clipboard?.writeText(text)
    toast(t('pay.copied'))
  }

  const debtFields = customerName ? (
    <div className="flex flex-col gap-3">
      <div className="flex items-center gap-2 rounded-xl bg-field px-3 py-2.5 text-[14px]">
        <UserRound size={17} className="text-brand" /> <span className="text-ink-2">{t('pos.customer')}:</span> <b>{customerName}</b>
      </div>
      <div className="grid grid-cols-2 gap-3">
        <label className="flex flex-col gap-1.5 text-[13px] font-medium text-ink-2">
          {t('pay.dueDate')}
          <input type="date" value={dueDate} min={isoDate(new Date())} onChange={(e) => setDueDate(e.target.value)}
            className="h-11 rounded-xl border border-line bg-surface px-3 text-[15px] text-ink outline-none focus:border-brand" />
        </label>
        <label className="flex flex-col gap-1.5 text-[13px] font-medium text-ink-2">
          {t('pay.comment')}
          <input value={comment} maxLength={300} onChange={(e) => setComment(e.target.value)}
            className="h-11 rounded-xl border border-line bg-surface px-3 text-[15px] text-ink outline-none focus:border-brand" />
        </label>
      </div>
    </div>
  ) : (
    <p className="flex items-center gap-2 rounded-xl bg-amber-50 px-4 py-3 text-[14px] text-amber-800"><Info size={17} /> {t('pay.debtNeedsCustomer')}</p>
  )

  const recipients = (
    <div className="flex flex-col gap-2">
      {ctx.transferRecipients.map((r) => (
        <label key={r.id} className={`flex cursor-pointer items-center gap-3 rounded-xl border-2 px-3 py-2.5 ${recipient === r.id ? 'border-brand bg-brand-soft/50' : 'border-line'}`}>
          <input type="radio" name="recipient" className="size-4 accent-brand" checked={recipient === r.id} onChange={() => setRecipient(r.id)} />
          <span className="flex size-10 items-center justify-center rounded-full bg-red-50 text-red-600"><Smartphone size={18} /></span>
          <span className="min-w-0 flex-1">
            <span className="block text-[14px] font-semibold">{r.bankName}</span>
            <span className="block text-[14px]">{r.account}</span>
            <span className="block text-[13px] text-ink-2">{r.holderName}</span>
          </span>
          <button type="button" aria-label={t('pay.copy')} onClick={(e) => { e.preventDefault(); copy(r.account) }}
            className="flex size-9 items-center justify-center rounded-lg border border-line text-brand hover:bg-field"><Copy size={16} /></button>
        </label>
      ))}
    </div>
  )

  return (
    <Modal title={t('pay.title')} onClose={busy ? () => undefined : onClose} width={560}>
      <div className="flex flex-col gap-4">
        <div className="rounded-2xl bg-field px-5 py-4 text-center">
          <div className="text-[14px] text-ink-2">{t('pay.toPay')}</div>
          <div className="text-[34px] font-extrabold tracking-tight">{tenge(total)}</div>
        </div>

        <div className="grid gap-2" style={{ gridTemplateColumns: `repeat(${tabs.length}, minmax(0, 1fr))` }}>
          {tabs.map((m) => {
            const Icon = ICON[m]
            return (
              <button key={m} type="button" onClick={() => setTab(m)}
                className={`flex flex-col items-center gap-1 rounded-xl border-2 px-2 py-2.5 text-[13px] font-medium ${tab === m ? 'border-brand bg-brand-soft text-brand' : 'border-line text-ink hover:bg-field'}`}>
                <Icon size={20} /> {t(`pay.${m}`)}
              </button>
            )
          })}
        </div>

        {tab === 'Cash' && (
          <div className="flex flex-col gap-3">
            <label className="text-[13px] font-medium text-ink-2">{t('pay.received')}</label>
            <input autoFocus value={received} inputMode="decimal" placeholder={String(total)}
              onChange={(e) => setReceived(e.target.value)}
              onKeyDown={(e) => e.key === 'Enter' && ready && onConfirm(ready)}
              className="h-14 rounded-xl border-2 border-line bg-surface px-4 text-right text-[24px] font-bold outline-none focus:border-brand" />
            <div className="flex flex-wrap gap-2">
              {quickCash.map((v) => (
                <button key={v} type="button" onClick={() => setReceived(String(v))}
                  className="rounded-lg bg-field px-3 py-2 text-[14px] font-medium hover:bg-brand-soft">{tenge(v)}</button>
              ))}
            </div>
            {received && (cash >= total
              ? <div className="flex items-center justify-between rounded-xl bg-success-soft px-4 py-3 text-success">
                  <span className="text-[15px] font-medium">{t('pay.change')}</span>
                  <span className="text-[22px] font-bold">{tenge(change)}</span>
                </div>
              : <div className="rounded-xl bg-danger-soft px-4 py-3 text-[15px] font-medium text-danger">{t('pay.notEnough', { sum: tenge(-change) })}</div>)}
          </div>
        )}

        {(tab === 'Card' || tab === 'Qr') && (
          <div className="flex flex-col items-center gap-2 rounded-2xl border border-line px-6 py-6 text-center">
            {tab === 'Card' ? <CreditCard size={40} className="text-brand" /> : <QrCode size={40} className="text-brand" />}
            <p className="text-[15px] font-semibold">{t(tab === 'Card' ? 'pay.cardHint' : 'pay.qrHint')}</p>
            <p className="text-[13px] text-ink-2">{t('pay.confirmAfter')}</p>
          </div>
        )}

        {tab === 'Transfer' && (
          <div className="flex flex-col gap-3">
            <div className="text-[14px] font-semibold">{t('pay.chooseAccount')}</div>
            {recipients}
            <p className="flex items-center gap-2 rounded-xl bg-field px-3 py-2.5 text-[13px] text-ink-2"><Info size={16} /> {t('pay.transferHint')}</p>
          </div>
        )}

        {tab === 'Debt' && (
          <div className="flex flex-col gap-3">
            {debtFields}
            {customerName && <p className="flex items-center gap-2 rounded-xl bg-field px-3 py-2.5 text-[13px] text-ink-2"><Info size={16} /> {t('pay.debtHint')}</p>}
          </div>
        )}

        {tab === 'Mixed' && (
          <div className="flex flex-col gap-2">
            {single.map((m) => {
              const Icon = ICON[m]
              return (
                <div key={m} className="flex items-center gap-2">
                  <span className="flex w-28 items-center gap-2 text-[14px] font-medium"><Icon size={17} className="text-brand" /> {t(`pay.${m}`)}</span>
                  <input value={mixed[m] ?? ''} inputMode="decimal" placeholder="0"
                    onChange={(e) => setMixed((x) => ({ ...x, [m]: e.target.value }))}
                    className="h-11 min-w-0 flex-1 rounded-xl border border-line bg-surface px-3 text-right text-[16px] font-semibold outline-none focus:border-brand" />
                  <button type="button" title={t('pay.fillRest')} aria-label={t('pay.fillRest')}
                    onClick={() => setMixed((x) => ({ ...x, [m]: String(round2(parse(x[m] ?? '') + remaining)) }))}
                    disabled={remaining <= 0}
                    className="flex size-11 items-center justify-center rounded-xl border border-line text-brand hover:bg-field disabled:text-ink-3"><Equal size={18} /></button>
                </div>
              )
            })}
            {parse(mixed.Transfer ?? '') > 0 && <div className="mt-2">{recipients}</div>}
            {parse(mixed.Debt ?? '') > 0 && <div className="mt-2">{debtFields}</div>}
            <div className={`mt-1 rounded-xl px-4 py-3 text-[15px] font-medium ${
              remaining === 0 ? 'bg-success-soft text-success' : remaining > 0 ? 'bg-amber-50 text-amber-700' : 'bg-danger-soft text-danger'}`}>
              {remaining === 0 ? <span className="flex items-center gap-2"><CheckCircle2 size={18} /> {t('pay.matched')}</span>
                : remaining > 0 ? t('pay.remaining', { sum: tenge(remaining) }) : t('pay.over', { sum: tenge(-remaining) })}
            </div>
          </div>
        )}

        {error && <p className="rounded-xl bg-danger-soft px-4 py-3 text-[14px] text-danger">{error}</p>}

        <div className="grid grid-cols-[1fr_2fr] gap-2">
          <Button type="button" variant="secondary" disabled={busy} onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="button" className="h-12!" icon={<Check size={19} />} loading={busy} disabled={!ready}
            onClick={() => ready && onConfirm(ready)}>
            {tab === 'Cash' ? t('pay.confirmCash') : tab === 'Debt' ? t('pay.confirmDebt') : t('pay.confirm')}
          </Button>
        </div>
      </div>
    </Modal>
  )
}
