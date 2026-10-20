import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Copy, Loader2, Mail, Printer, Undo2, X } from 'lucide-react'
import { Button } from '../ui/Button'
import { RowMenu } from '../ui/RowMenu'
import { toast } from '../ui/Toast'
import { openReceipt, printReceipt } from '../cashier/ReceiptView'
import type { Receipt } from '../../lib/cashierTypes'
import { dateOnly, dateTime } from '../../lib/dates'
import { num, tenge } from '../../lib/money'
import { formatPhoneInput } from '../../lib/phone'
import { salesApi, salesKeys } from '../../lib/salesApi'
import { RETURN_REASONS, type SaleEvent, type SaleHistoryStatus } from '../../lib/salesTypes'
import { useT, type TranslationKey } from '../../lib/i18n'

type Tab = 'info' | 'items' | 'payment' | 'history'

const STATUS_TONE: Record<SaleHistoryStatus, string> = {
  Completed: 'bg-success-soft text-success', PartiallyReturned: 'bg-amber-100 text-amber-800',
  Returned: 'bg-danger-soft text-danger', Debt: 'bg-amber-100 text-amber-800',
}

export function SaleStatusBadge({ status }: { status: SaleHistoryStatus }) {
  const t = useT()
  return (
    <span className={`inline-flex h-7 items-center whitespace-nowrap rounded-lg px-2.5 text-[12px] font-semibold ${STATUS_TONE[status]}`}>
      {t(`sales.status.${status}`)}
    </span>
  )
}

/** Чектің мәртебесі тізімдегідей: төленбеген қарыз — «Долг» (ТЗ «Продажи» §11). */
export const receiptStatus = (r: Receipt): SaleHistoryStatus =>
  r.status === 'Completed' && r.debt?.status === 'Open' ? 'Debt' : r.status

/**
 * Таңдалған чектің оң жақ карточкасы (ТЗ «Продажи» §8–9): «Информация», «Товары», «Оплата», «История».
 * Жабу сүзгі мен бетті тастамайды — бәрі URL-да.
 */
export function SaleCard({ saleId, canReturn, onClose, onReturn }: {
  saleId: string
  canReturn: boolean
  onClose: () => void
  onReturn: () => void
}) {
  const t = useT()
  const [tab, setTab] = useState<Tab>('info')
  const { data: r, error, refetch } = useQuery({ queryKey: salesKeys.receipt(saleId), queryFn: () => salesApi.receipt(saleId) })
  const events = useQuery({ queryKey: salesKeys.events(saleId), queryFn: () => salesApi.events(saleId), enabled: tab === 'history' })
  const status = r ? receiptStatus(r) : null
  const returnable = !!r && r.status !== 'Returned' && r.items.some((i) => i.quantity - i.returnedQuantity > 0)
  const tabs: { key: Tab; label: string }[] = [
    { key: 'info', label: t('sales.card.info') },
    { key: 'items', label: `${t('sales.card.items')} (${r?.items.length ?? 0})` },
    { key: 'payment', label: t('sales.card.payment') },
    { key: 'history', label: `${t('sales.card.history')}${events.data ? ` (${events.data.length})` : ''}` },
  ]

  const eReceipt = () => {
    if (!r) return
    if (r.customer) toast(t('sales.eReceiptApp'))
    else { toast(t('sales.eReceiptNoCustomer')); openReceipt(r, t) }
  }

  return (
    <aside className="flex min-h-0 flex-col rounded-2xl border border-line bg-surface">
      <div className="flex items-center justify-between gap-3 border-b border-line px-5 py-4">
        <div className="flex min-w-0 items-center gap-2.5">
          <h2 className="truncate text-[18px] font-bold">{r ? t('pos.receiptNo', { n: r.number }) : t('sales.open')}</h2>
          {status && <SaleStatusBadge status={status} />}
        </div>
        <button type="button" aria-label={t('common.close')} onClick={onClose}
          className="flex size-9 shrink-0 items-center justify-center rounded-lg text-ink-2 hover:bg-field"><X size={19} /></button>
      </div>

      <div className="flex gap-1 border-b border-line px-3">
        {tabs.map((x) => (
          <button key={x.key} type="button" onClick={() => setTab(x.key)}
            className={`-mb-px border-b-2 px-3 py-2.5 text-[13px] font-semibold ${tab === x.key ? 'border-brand text-brand' : 'border-transparent text-ink-2 hover:text-ink'}`}>
            {x.label}
          </button>
        ))}
      </div>

      <div className="min-h-0 flex-1 overflow-y-auto px-5 py-4 text-[13px]">
        {error && (
          <div className="rounded-xl bg-danger-soft px-4 py-3 text-danger">
            {error.message}
            <button type="button" className="ml-2 font-semibold underline" onClick={() => void refetch()}>{t('common.retry')}</button>
          </div>
        )}
        {!r && !error && <div className="flex justify-center py-10 text-ink-3"><Loader2 className="animate-spin" /></div>}
        {r && tab === 'info' && <InfoTab r={r} onAllItems={() => setTab('items')} />}
        {r && tab === 'items' && <ItemsTab r={r} canReturn={canReturn && returnable} onReturn={onReturn} />}
        {r && tab === 'payment' && <PaymentTab r={r} />}
        {r && tab === 'history' && <HistoryTab events={events.data} loading={events.isLoading} />}
      </div>

      {r && (
        <div className="flex flex-wrap items-center gap-2 border-t border-line px-5 py-3">
          <Button variant="secondary" className="border-danger! text-danger!" icon={<Undo2 size={16} />} disabled={!canReturn || !returnable} onClick={onReturn}>
            {t('sales.return')}
          </Button>
          <Button variant="secondary" icon={<Mail size={16} />} onClick={eReceipt}>{t('sales.eReceipt')}</Button>
          <Button variant="secondary" icon={<Printer size={16} />} onClick={() => printReceipt(r, t)}>{t('receipts.print')}</Button>
          <div className="ml-auto">
            <RowMenu items={[
              { label: t('sales.copyNumber'), icon: <Copy size={16} />, onClick: () => { void navigator.clipboard?.writeText(String(r.number)); toast(t('sales.copied')) } },
              { label: t('sales.card.history'), onClick: () => setTab('history') },
            ]} />
          </div>
        </div>
      )}
    </aside>
  )
}

function Row({ label, value }: { label: React.ReactNode; value: React.ReactNode }) {
  return (
    <div className="flex items-start justify-between gap-4 py-1">
      <span className="text-ink-2">{label}</span>
      <span className="text-right font-medium">{value}</span>
    </div>
  )
}

function Section({ title, children, action }: { title: string; children: React.ReactNode; action?: React.ReactNode }) {
  return (
    <section className="mb-5">
      <div className="mb-2 flex items-center justify-between">
        <h3 className="text-[14px] font-bold">{title}</h3>
        {action}
      </div>
      {children}
    </section>
  )
}

function methodsLabel(r: Receipt, t: ReturnType<typeof useT>) {
  const methods = [...new Set(r.payments.map((p) => p.method))]
  return methods.length > 1 ? `${t('pay.Mixed')}: ${methods.map((m) => t(`pay.${m}`)).join(' + ')}` : t(`pay.${methods[0] ?? 'Cash'}`)
}

function InfoTab({ r, onAllItems }: { r: Receipt; onAllItems: () => void }) {
  const t = useT()
  const status = receiptStatus(r)
  const preview = r.items.slice(0, 3)
  return (
    <>
      <Section title={t('sales.card.main')}>
        <Row label={t('sales.col.date')} value={dateTime(r.createdAt)} />
        <Row label={t('sales.card.store')} value={r.storeName} />
        <Row label={t('sales.card.register')} value={r.registerName} />
        <Row label={t('sales.card.cashier')} value={r.cashierName} />
        <Row label={t('sales.card.customer')} value={r.customer ? <>{r.customer.fullName}<span className="block text-[12px] font-normal text-ink-3">{formatPhoneInput(r.customer.phone)}</span></> : t('sales.card.noCustomer')} />
        <Row label={t('sales.card.method')} value={methodsLabel(r, t)} />
        <Row label={t('sales.col.status')} value={<SaleStatusBadge status={status} />} />
      </Section>
      <Section title={t('sales.card.composition')} action={r.items.length > preview.length && (
        <button type="button" className="text-[12px] font-semibold text-brand" onClick={onAllItems}>{t('sales.card.allItems')} ({r.items.length})</button>
      )}>
        <table className="w-full">
          <tbody>
            {preview.map((i) => (
              <tr key={i.id} className="border-t border-line first:border-0">
                <td className="py-1.5 pr-2">
                  <div className="font-medium">{i.name}</div>
                  {i.article && <div className="text-[11px] text-ink-3">{i.article}</div>}
                </td>
                <td className="py-1.5 text-right whitespace-nowrap text-ink-2">{num(i.quantity)} × {tenge(i.price)}</td>
                <td className="py-1.5 pl-3 text-right font-semibold whitespace-nowrap">{tenge(i.lineTotal)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </Section>
      <Section title={t('sales.card.calc')}>
        <Row label={t('sales.card.subtotal')} value={tenge(r.subtotal)} />
        <Row label={t('sales.card.discount')} value={r.discountAmount > 0 ? `− ${tenge(r.discountAmount)}` : tenge(0)} />
        <Row label={t('sales.card.bonus')} value={r.bonusRedeemed > 0 ? `− ${tenge(r.bonusRedeemed)}` : tenge(0)} />
        <div className="my-1 border-t border-line" />
        <Row label={t('sales.card.toPay')} value={<b className="text-[15px]">{tenge(r.total)}</b>} />
        <Row label={t('sales.card.paid')} value={tenge(r.total - (r.debt?.amount ?? 0))} />
        {r.debt && (
          <Row label={t('sales.card.debt')} value={<span className="text-amber-700">{r.debt.status === 'Open'
            ? t('sales.card.debtLeft', { sum: tenge(r.debt.remaining), date: dateOnly(r.debt.dueDate) }) : t('sales.card.debtPaid')}</span>} />
        )}
        <div className={`mt-2 flex justify-between rounded-lg px-3 py-2 ${r.returnedAmount > 0 ? 'bg-danger-soft text-danger' : 'bg-field text-ink-2'}`}>
          <span>{t('sales.card.returned')}</span><b>{r.returnedAmount > 0 ? `− ${tenge(r.returnedAmount)}` : tenge(0)}</b>
        </div>
        <div className="mt-2 flex justify-between rounded-lg bg-brand-soft px-3 py-2 text-brand">
          <span className="font-semibold">{t('sales.card.net')}</span><b className="text-[16px]">{tenge(r.total - r.returnedAmount)}</b>
        </div>
      </Section>
    </>
  )
}

function ItemsTab({ r, canReturn, onReturn }: { r: Receipt; canReturn: boolean; onReturn: () => void }) {
  const t = useT()
  return (
    <table className="w-full">
      <thead className="text-[12px] text-ink-3">
        <tr>
          <th className="pb-2 text-left font-medium">{t('receipt.item')}</th>
          <th className="pb-2 text-right font-medium">{t('sales.items.original')}</th>
          <th className="pb-2 text-right font-medium">{t('sales.items.returned')}</th>
          <th className="pb-2 text-right font-medium">{t('sales.items.available')}</th>
          <th className="pb-2 text-right font-medium">{t('sales.col.price')}</th>
          <th className="pb-2 text-right font-medium">{t('sales.col.sum')}</th>
        </tr>
      </thead>
      <tbody>
        {r.items.map((i) => {
          const available = i.quantity - i.returnedQuantity
          return (
            <tr key={i.id} className="border-t border-line">
              <td className="py-2 pr-2">
                <div className="font-medium">{i.name}</div>
                {i.article && <div className="text-[11px] text-ink-3">{i.article}</div>}
                {canReturn && available > 0 && (
                  <button type="button" className="mt-0.5 text-[12px] font-semibold text-danger" onClick={onReturn}>{t('sales.items.return')}</button>
                )}
              </td>
              <td className="py-2 text-right">{num(i.quantity)}</td>
              <td className={`py-2 text-right ${i.returnedQuantity > 0 ? 'text-danger' : 'text-ink-3'}`}>{num(i.returnedQuantity)}</td>
              <td className="py-2 text-right font-semibold">{num(available)}</td>
              <td className="py-2 text-right whitespace-nowrap">{tenge(i.price)}</td>
              <td className="py-2 pl-2 text-right font-semibold whitespace-nowrap">{tenge(i.lineTotal)}</td>
            </tr>
          )
        })}
      </tbody>
    </table>
  )
}

function PaymentTab({ r }: { r: Receipt }) {
  const t = useT()
  const refunds = r.returns.filter((x) => x.refunded > 0 || x.debtReduced > 0)
  const restored = r.returns.reduce((s, x) => s + x.bonusRestored, 0)
  const reversed = r.returns.reduce((s, x) => s + x.bonusReversed, 0)
  return (
    <>
      <Section title={t('sales.pay.components')}>
        <Row label={t('sales.card.toPay')} value={<b className="text-[15px]">{tenge(r.total)}</b>} />
        <div className="my-1 border-t border-line" />
        {r.payments.map((p, i) => (
          <Row key={i} label={t(`pay.${p.method}`)} value={<>{tenge(p.amount)}
            {p.method === 'Cash' && p.received != null && p.change ? <span className="block text-[12px] font-normal text-ink-3">{t('sales.pay.received', { sum: tenge(p.received), change: tenge(p.change) })}</span> : null}
            {p.transferRecipient && <span className="block text-[12px] font-normal text-ink-3">{p.transferRecipient}</span>}</>} />
        ))}
        <div className="my-1 border-t border-line" />
        <Row label={t('sales.card.paid')} value={tenge(r.total - (r.debt?.amount ?? 0))} />
        {r.debt && (
          <Row label={t('sales.card.debt')} value={<span className="text-amber-700">{tenge(r.debt.amount)} · {r.debt.status === 'Open'
            ? t('sales.card.debtLeft', { sum: tenge(r.debt.remaining), date: dateOnly(r.debt.dueDate) }) : t('sales.card.debtPaid')}</span>} />
        )}
      </Section>
      <Section title={t('sales.pay.refunds')}>
        {refunds.length === 0 && <p className="text-ink-3">{t('sales.pay.noRefunds')}</p>}
        {refunds.map((x) => (
          <Row key={x.id} label={<>{dateTime(x.createdAt)}<span className="block text-[12px] text-ink-3">{x.cashierName}{x.fromBackOffice ? ` · ${t('return.backOffice')}` : ''}</span></>}
            value={<span className="text-danger">{x.refunded > 0 && x.refundMethod ? `${t(`pay.${x.refundMethod}`)}: − ${tenge(x.refunded)}` : ''}
              {x.debtReduced > 0 && <span className="block text-[12px] text-amber-700">{t('receipt.debtReduced', { sum: tenge(x.debtReduced) })}</span>}</span>} />
        ))}
        {r.returnedAmount > 0 && <><div className="my-1 border-t border-line" /><Row label={t('sales.card.returned')} value={<b className="text-danger">− {tenge(r.returnedAmount)}</b>} /></>}
      </Section>
      {(restored > 0 || reversed > 0) && (
        <Section title={t('sales.pay.bonusAdjust')}>
          {restored > 0 && <Row label={t('sales.pay.restored')} value={`+${num(restored)} Б`} />}
          {reversed > 0 && <Row label={t('sales.pay.reversed')} value={`−${num(reversed)} Б`} />}
        </Section>
      )}
    </>
  )
}

function HistoryTab({ events, loading }: { events: SaleEvent[] | undefined; loading: boolean }) {
  const t = useT()
  if (loading) return <div className="flex justify-center py-10 text-ink-3"><Loader2 className="animate-spin" /></div>
  if (!events?.length) return <p className="text-ink-3">{t('sales.event.empty')}</p>
  const bonus = (e: SaleEvent) => e.type.startsWith('bonus.')
  return (
    <ol className="relative ml-2 border-l border-line pl-4">
      {events.map((e, i) => (
        <li key={i} className="relative pb-4 last:pb-0">
          <span className={`absolute -left-[21px] top-1 size-2.5 rounded-full ${e.type === 'return' ? 'bg-danger' : e.type.startsWith('debt') ? 'bg-amber-500' : bonus(e) ? 'bg-violet-500' : 'bg-brand'}`} />
          <div className="flex items-baseline justify-between gap-3">
            <span className="font-semibold">{t(`sales.event.${e.type}` as TranslationKey)}</span>
            {e.amount != null && <span className={`whitespace-nowrap font-semibold ${e.type === 'return' ? 'text-danger' : ''}`}>{bonus(e) ? `${num(e.amount)} Б` : tenge(e.amount)}</span>}
          </div>
          <div className="text-[12px] text-ink-3">
            {dateTime(e.at)}{e.staffName ? ` · ${e.staffName}` : ''}{e.method ? ` · ${t(`pay.${e.method}`)}` : ''}
            {e.type === 'discount' && e.note ? ` · ${t('sales.event.discountBy', { name: e.note })}` : e.type === 'return' && e.note ? ` · ${eventNote(e.note, t)}` : e.note && e.type !== 'sale' ? ` · ${e.note}` : ''}
          </div>
        </li>
      ))}
    </ol>
  )
}

/** Қайтарым оқиғасының ескертпесі: «Defect · комментарий · WriteOff» → аударылған себеп пен әрекет. */
function eventNote(note: string, t: ReturnType<typeof useT>) {
  return note.split(' · ').map((part) => {
    if ((RETURN_REASONS as string[]).includes(part)) return t(`return.reasonCode.${part}` as TranslationKey)
    if (part === 'WriteOff') return t('return.stock.WriteOff')
    return part
  }).join(' · ')
}
