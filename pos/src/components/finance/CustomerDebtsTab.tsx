import { useMemo, useRef, useState } from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { Link, useSearchParams } from 'react-router-dom'
import { Download, Eye, HandCoins } from 'lucide-react'
import { Button } from '../ui/Button'
import { Field, Input, Select, Textarea } from '../ui/Field'
import { Modal } from '../ui/Modal'
import { RowMenu } from '../ui/RowMenu'
import { SearchInput } from '../ui/SearchInput'
import { toast } from '../ui/Toast'
import { EmptyRow, Pager, StatCard, td, th, useDebounced } from '../inventory/inventoryUi'
import { ApiError } from '../../lib/api'
import { dateOnly, dateTime } from '../../lib/dates'
import { financeApi, financeKeys } from '../../lib/financeApi'
import { PAY_METHODS, type CustomerDebtCard, type FinanceAccount, type PayMethod } from '../../lib/financeTypes'
import { tenge } from '../../lib/money'
import { useT, type TranslationKey } from '../../lib/i18n'
import { BlockError, Segmented, Skeleton } from '../stats/statsUi'
import { DebtBadge, reportError, useFinanceMeta, useFinancePermissions, type FinanceFilters } from './financeUi'

type Segment = 'all' | 'open' | 'overdue' | 'paid'

/**
 * «Долги клиентов» (ТЗ «Финансы» §11): қарыз тек проведённый сатылымнан; қолмен сома енгізілмейді.
 * Карточка: құжаттар, төлемдер; «Принять оплату» — бөлу нақты (FIFO жоқ), артық төлем жоқ; өтеу — ақша кіреді, қарыз азаяды, түсім емес.
 */
export function CustomerDebtsTab({ storeIds }: { filters: FinanceFilters; storeIds?: string }) {
  const t = useT()
  const [params, setParams] = useSearchParams()
  const { canExport } = useFinancePermissions()
  const [exporting, setExporting] = useState(false)
  const g = (k: string) => params.get(k) ?? ''
  const search = useDebounced(g('q'), 300)
  const segment = (g('seg') || 'open') as Segment
  const p = { storeIds, segment, search: search || undefined, page: Number(g('page')) || 1, pageSize: Number(g('size')) || 15 }
  const q = useQuery({ queryKey: ['finance', 'customer-debts', p], queryFn: () => financeApi.customerDebts(p), placeholderData: keepPreviousData })
  const set = (patch: Record<string, string | null>, keepPage = false) => setParams((prev) => { const n = new URLSearchParams(prev); for (const [k, v] of Object.entries(patch)) { if (v) n.set(k, v); else n.delete(k) } if (!keepPage) n.delete('page'); return n }, { replace: true })
  const d = q.data
  const customerId = g('customer')
  const exportFile = async (format: 'xlsx' | 'csv') => {
    setExporting(true)
    try { await financeApi.exportFile('customer-debts', format, { storeIds, status: segment, search: search || undefined }); toast(t('stats.exportReady')) } catch (err) { reportError(err, t('stats.exportFailed')) } finally { setExporting(false) }
  }
  if (q.isError) return <BlockError message={(q.error as Error).message} onRetry={() => void q.refetch()} />
  return (
    <div className="flex flex-col gap-3">
      {!d ? <Skeleton className="h-24" /> : (
        <div className="grid grid-cols-2 gap-3 xl:grid-cols-4">
          <StatCard icon={<HandCoins size={18} />} label={t('fin.cd.total')} value={tenge(d.total)} hint={t('fin.customersN', { n: d.customers })} />
          <StatCard icon={<HandCoins size={18} />} label={t('fin.overdue')} value={tenge(d.overdue)} tone={d.overdue > 0 ? 'danger' : 'success'} />
          <StatCard icon={<HandCoins size={18} />} label={t('fin.dueSoon')} value={tenge(d.dueSoon)} tone="amber" />
          <StatCard icon={<HandCoins size={18} />} label={t('fin.cd.customers')} value={d.customers} tone="violet" />
        </div>
      )}
      <div className="flex flex-wrap items-center gap-2">
        <SearchInput value={g('q')} onChange={(v) => set({ q: v })} placeholder={t('fin.cd.search')} className="h-10! w-64" />
        <Segmented value={segment} options={(['open', 'overdue', 'paid', 'all'] as Segment[]).map((s) => ({ key: s, label: t(`fin.cd.seg.${s}` as TranslationKey) }))} onChange={(s) => set({ seg: s })} />
        <span className="ml-auto" />
        {canExport && <span className="flex gap-1"><Button variant="secondary" className="h-10! px-3!" icon={<Download size={16} />} disabled={exporting} onClick={() => void exportFile('xlsx')}>Excel</Button><Button variant="secondary" className="h-10! px-3!" disabled={exporting} onClick={() => void exportFile('csv')}>CSV</Button></span>}
      </div>
      <div className="overflow-x-auto rounded-2xl border border-line bg-surface">
        <table className="w-full min-w-[800px]">
          <thead className="bg-field/60"><tr>
            <th className={th}>{t('fin.cd.customer')}</th><th className={th}>{t('fin.phone')}</th><th className={`${th} text-right`}>{t('fin.cd.debt')}</th><th className={`${th} text-right`}>{t('fin.overdue')}</th>
            <th className={th}>{t('fin.nextDue')}</th><th className={`${th} text-right`}>{t('fin.documents')}</th><th className={th}>{t('fin.col.status')}</th><th className={`${th} w-12`}></th>
          </tr></thead>
          <tbody>
            {!d ? null : d.rows.length === 0 ? <EmptyRow colSpan={8} text={t('fin.cd.empty')} /> : d.rows.map((r) => (
              <tr key={r.customerId} className="border-t border-line hover:bg-field/40">
                <td className={td}><button type="button" className="font-medium hover:text-brand" onClick={() => set({ customer: r.customerId }, true)}>{r.name}</button></td>
                <td className={`${td} text-ink-2`}>{r.phone ?? '•••'}</td>
                <td className={`${td} text-right font-semibold tabular-nums`}>{tenge(r.total)}</td>
                <td className={`${td} text-right tabular-nums ${r.overdue > 0 ? 'text-danger' : 'text-ink-3'}`}>{r.overdue > 0 ? tenge(r.overdue) : '—'}</td>
                <td className={`${td} text-ink-2`}>{r.nextDue ? dateOnly(r.nextDue) : '—'}</td>
                <td className={`${td} text-right tabular-nums`}>{r.documents}</td>
                <td className={td}><DebtBadge status={r.status} /></td>
                <td className={td}><RowMenu items={[
                  { label: t('fin.open'), icon: <Eye size={16} />, onClick: () => set({ customer: r.customerId }, true) },
                  { label: t('fin.cd.accept'), icon: <HandCoins size={16} />, onClick: () => set({ customer: r.customerId, pay: '1' }, true) },
                ]} /></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {d && <Pager page={p.page} pageSize={p.pageSize} total={d.rowsTotal} onPage={(pg) => set({ page: String(pg) }, true)} onPageSize={(s) => set({ size: String(s) })} />}
      {customerId && <CustomerCardModal customerId={customerId} storeIds={storeIds} pay={g('pay') === '1'} onClose={() => set({ customer: null, pay: null }, true)} onChanged={() => void q.refetch()} />}
    </div>
  )
}

/** Клиент карточкасы (§11.1): сводка, «Документы», «Оплаты»; «Принять оплату». */
function CustomerCardModal({ customerId, storeIds, pay, onClose, onChanged }: { customerId: string; storeIds?: string; pay: boolean; onClose: () => void; onChanged: () => void }) {
  const t = useT()
  const [tab, setTab] = useState<'docs' | 'payments'>('docs')
  const [paying, setPaying] = useState(pay)
  const meta = useFinanceMeta(storeIds)
  const q = useQuery({ queryKey: ['finance', 'customer-debt', customerId, storeIds], queryFn: () => financeApi.customerDebt(customerId, storeIds) })
  const c = q.data
  const { canOperate } = useFinancePermissions()
  return (
    <Modal title={c ? c.name : t('fin.cd.customer')} onClose={onClose} width={760}>
      {!c ? <Skeleton className="h-48" /> : (
        <div className="flex flex-col gap-4">
          <div className="flex flex-wrap items-center gap-x-5 gap-y-1 text-[13px] text-ink-2">
            {c.phone && <span>{c.phone}</span>}
            <span>{t('fin.cd.debt')}: <b className="text-[15px] text-ink">{tenge(c.total)}</b></span>
            <span>{t('fin.overdue')}: <b className={c.overdue > 0 ? 'text-danger' : ''}>{tenge(c.overdue)}</b></span>
            {c.nextDue && <span>{t('fin.nextDue')}: <b>{dateOnly(c.nextDue)}</b></span>}
            <span className="ml-auto flex gap-2">
              <Link to={`/sales?customer=${customerId}`} className="inline-flex h-9 items-center rounded-xl border border-line px-3 text-[13px] font-semibold hover:border-brand">{t('fin.cd.toSales')}</Link>
              {canOperate && c.total > 0 && <Button className="h-9! px-3! text-[13px]!" icon={<HandCoins size={15} />} onClick={() => setPaying(true)}>{t('fin.cd.accept')}</Button>}
            </span>
          </div>
          <Segmented value={tab} options={[{ key: 'docs', label: `${t('fin.documents')} (${c.documents.length})` }, { key: 'payments', label: `${t('fin.payments')} (${c.payments.length})` }]} onChange={setTab} />
          {tab === 'docs' ? (
            <table className="w-full text-[14px]">
              <thead className="bg-field/60"><tr><th className={th}>{t('fin.cd.doc')}</th><th className={th}>{t('fin.col.date')}</th><th className={th}>{t('fin.dueDate')}</th><th className={`${th} text-right`}>{t('fin.col.amount')}</th><th className={`${th} text-right`}>{t('fin.paid')}</th><th className={`${th} text-right`}>{t('fin.remaining')}</th><th className={th}></th></tr></thead>
              <tbody>{c.documents.map((x) => (
                <tr key={x.debtId} className="border-t border-line">
                  <td className={td}>{x.saleId ? <Link to={`/sales?sale=${x.saleId}`} className="font-medium text-brand hover:underline">№{x.saleNumber}</Link> : '—'}{x.comment && <span className="block text-[12px] text-ink-3">{x.comment}</span>}</td>
                  <td className={`${td} text-ink-2`}>{dateTime(x.createdAt)}</td>
                  <td className={`${td} ${x.overdue ? 'font-semibold text-danger' : 'text-ink-2'}`}>{dateOnly(x.dueDate)}</td>
                  <td className={`${td} text-right tabular-nums`}>{tenge(x.amount)}</td>
                  <td className={`${td} text-right tabular-nums text-ink-2`}>{tenge(x.paid)}</td>
                  <td className={`${td} text-right font-semibold tabular-nums`}>{tenge(x.remaining)}</td>
                  <td className={td}><DebtBadge status={x.remaining <= 0 ? 'Paid' : x.overdue ? 'Overdue' : x.paid > 0 ? 'Partial' : 'Open'} /></td>
                </tr>
              ))}</tbody>
            </table>
          ) : (
            <table className="w-full text-[14px]">
              <thead className="bg-field/60"><tr><th className={th}>{t('fin.col.date')}</th><th className={th}>{t('fin.cd.doc')}</th><th className={`${th} text-right`}>{t('fin.col.amount')}</th><th className={th}>{t('fin.method')}</th><th className={`${th} text-right`}>{t('fin.remainingAfter')}</th><th className={th}>{t('fin.col.responsible')}</th><th className={th}>{t('fin.operation')}</th></tr></thead>
              <tbody>{c.payments.length === 0 ? <EmptyRow colSpan={7} text="—" /> : c.payments.map((x) => (
                <tr key={x.id} className="border-t border-line">
                  <td className={`${td} text-ink-2`}>{dateTime(x.createdAt)}</td>
                  <td className={td}>{x.saleNumber ? `№${x.saleNumber}` : '—'}</td>
                  <td className={`${td} text-right font-semibold tabular-nums text-success`}>+{tenge(x.amount)}</td>
                  <td className={`${td} text-ink-2`}>{x.isReturn ? t('fin.cd.byReturn') : x.method ? t(`pay.${x.method}` as TranslationKey) : '—'}</td>
                  <td className={`${td} text-right tabular-nums text-ink-2`}>{tenge(x.remainingAfter)}</td>
                  <td className={`${td} text-ink-2`}>{x.staffName}</td>
                  <td className={td}>{x.operationId ? <Link to={`/finance/transactions?op=${x.operationId}`} className="text-brand hover:underline">{x.operationNumber}</Link> : '—'}</td>
                </tr>
              ))}</tbody>
            </table>
          )}
        </div>
      )}
      {paying && c && <CustomerPaymentModal card={c} accounts={meta.data?.accounts ?? []} onClose={() => setPaying(false)} onDone={() => { setPaying(false); void q.refetch(); onChanged() }} />}
    </Modal>
  )
}

/** «Принять оплату» (§11.1): құжаттар, сома, шот, әдіс, бөлу (қосындысы = төлем), коммент. Қарыздан артық — тыйым. */
function CustomerPaymentModal({ card, accounts, onClose, onDone }: { card: CustomerDebtCard; accounts: FinanceAccount[]; onClose: () => void; onDone: () => void }) {
  const t = useT()
  const open = useMemo(() => card.documents.filter((x) => x.remaining > 0), [card])
  const [alloc, setAlloc] = useState<Record<string, string>>(() => Object.fromEntries(open.map((x) => [x.debtId, ''])))
  const [method, setMethod] = useState<PayMethod>('Cash')
  const [accountId, setAccountId] = useState('')
  const [comment, setComment] = useState('')
  const [documentNumber, setDocumentNumber] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const requestId = useRef(crypto.randomUUID())
  const sum = open.reduce((s, x) => s + (Number((alloc[x.debtId] ?? '').replace(',', '.')) || 0), 0)
  const over = open.some((x) => (Number((alloc[x.debtId] ?? '').replace(',', '.')) || 0) > x.remaining)
  const defaultAccount = accounts.find((a) => a.isActive && a.methods.includes(method))
  const fillAll = () => setAlloc(Object.fromEntries(open.map((x) => [x.debtId, String(x.remaining)])))
  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!(sum > 0) || over) return
    setBusy(true)
    try {
      await financeApi.customerPayment({
        customerId: card.customerId, accountId: accountId || null, method, comment: comment.trim() || null, documentNumber: documentNumber.trim() || null,
        allocations: open.filter((x) => Number((alloc[x.debtId] ?? '').replace(',', '.')) > 0).map((x) => ({ debtId: x.debtId, amount: Number(alloc[x.debtId].replace(',', '.')) })),
        clientRequestId: requestId.current,
      })
      toast(t('fin.cd.accepted', { amount: tenge(sum) }))
      onDone()
    } catch (err) { setError(err instanceof ApiError ? err.message : t('fin.failed')); reportError(err, t('fin.failed')) } finally { setBusy(false) }
  }
  return (
    <Modal title={`${t('fin.cd.accept')} · ${card.name}`} onClose={onClose} width={620}>
      <form onSubmit={submit} className="flex flex-col gap-4">
        <div className="rounded-xl border border-line">
          <div className="flex items-center justify-between border-b border-line px-3 py-2 text-[13px] font-semibold text-ink-2"><span>{t('fin.cd.allocation')}</span><button type="button" className="text-brand hover:underline" onClick={fillAll}>{t('fin.payAll')}</button></div>
          {open.map((x) => (
            <div key={x.debtId} className="flex items-center gap-3 border-b border-line px-3 py-2 text-[14px] last:border-0">
              <span className="min-w-0 flex-1">№{x.saleNumber ?? '—'} · {dateOnly(x.dueDate)}<span className={`ml-2 text-[12px] ${x.overdue ? 'text-danger' : 'text-ink-3'}`}>{t('fin.remaining')}: {tenge(x.remaining)}</span></span>
              <Input inputMode="decimal" className="h-9! w-32 text-right" value={alloc[x.debtId] ?? ''} onChange={(e) => setAlloc((a) => ({ ...a, [x.debtId]: e.target.value }))} error={(Number((alloc[x.debtId] ?? '').replace(',', '.')) || 0) > x.remaining} placeholder="0" />
            </div>
          ))}
        </div>
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <Field label={t('fin.method')} required>
            <Select value={method} onChange={(e) => setMethod(e.target.value as PayMethod)}>{PAY_METHODS.map((m) => <option key={m} value={m}>{t(`pay.${m}` as TranslationKey)}</option>)}</Select>
          </Field>
          <Field label={t('fin.toAccount')} hint={!accountId && defaultAccount ? `${t('fin.byMethod')}: ${defaultAccount.name}` : undefined}>
            <Select value={accountId} onChange={(e) => setAccountId(e.target.value)}>
              <option value="">{t('fin.byMethod')}</option>
              {accounts.filter((a) => a.isActive).map((a) => <option key={a.id} value={a.id}>{a.name}</option>)}
            </Select>
          </Field>
          <Field label={t('fin.col.document')}><Input value={documentNumber} maxLength={64} onChange={(e) => setDocumentNumber(e.target.value)} /></Field>
          <Field label={t('fin.comment')}><Textarea value={comment} maxLength={500} onChange={(e) => setComment(e.target.value)} className="min-h-11! h-11" /></Field>
        </div>
        {error && <p className="text-[13px] text-danger">{error}</p>}
        <div className="flex items-center gap-3 border-t border-line pt-4">
          <span className="text-[15px]">{t('fin.cd.paymentSum')}: <b>{tenge(sum)}</b>{over && <span className="ml-2 text-[13px] text-danger">{t('fin.err.allocationTooMuch')}</span>}</span>
          <span className="ml-auto" />
          <Button type="button" variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" loading={busy} disabled={!(sum > 0) || over}>{t('fin.cd.acceptBtn')}</Button>
        </div>
        <p className="text-[12px] text-ink-3">{t('fin.cd.hint')}</p>
      </form>
    </Modal>
  )
}

export { financeKeys }
