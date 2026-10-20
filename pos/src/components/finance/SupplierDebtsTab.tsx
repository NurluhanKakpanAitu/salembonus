import { useMemo, useRef, useState } from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { Link, useSearchParams } from 'react-router-dom'
import { CalendarClock, Download, Eye, FileCheck, Paperclip, Printer, Truck } from 'lucide-react'
import { Button } from '../ui/Button'
import { Field, Input, Select, Textarea } from '../ui/Field'
import { Modal } from '../ui/Modal'
import { RowMenu } from '../ui/RowMenu'
import { SearchInput } from '../ui/SearchInput'
import { toast } from '../ui/Toast'
import { EmptyRow, Pager, StatCard, td, th, useDebounced } from '../inventory/inventoryUi'
import { ApiError } from '../../lib/api'
import { dateOnly, dateTime, isoDate } from '../../lib/dates'
import { financeApi } from '../../lib/financeApi'
import { PAY_METHODS, type FinanceAccount, type PayMethod, type SupplierDebtCard } from '../../lib/financeTypes'
import { tenge } from '../../lib/money'
import { uploadImage } from '../../lib/upload'
import { useT, type TranslationKey } from '../../lib/i18n'
import { BlockError, Segmented, Skeleton } from '../stats/statsUi'
import { DebtBadge, reportError, useFinanceMeta, useFinancePermissions, type FinanceFilters } from './financeUi'

type Segment = 'all' | 'open' | 'overdue' | 'dueSoon' | 'paid'

/**
 * «Долги поставщикам» (ТЗ «Финансы» §12): қарыз = проведённый приёмкалар − проведённый төлемдер; төлем бір не бірнеше накладнаяға бөлінеді;
 * инкассация — төлем емес. Карточка: накладнаялар (Складқа сілтеме), төлемдер, акт сверки; «Оплатить поставщику».
 */
export function SupplierDebtsTab({ filters, storeIds }: { filters: FinanceFilters; storeIds?: string }) {
  const t = useT()
  const [params, setParams] = useSearchParams()
  const { canExport } = useFinancePermissions()
  const [exporting, setExporting] = useState(false)
  const g = (k: string) => params.get(k) ?? ''
  const search = useDebounced(g('q'), 300)
  const segment = (g('seg') || 'open') as Segment
  const p = { storeIds, from: filters.from, to: filters.to, segment, search: search || undefined, page: Number(g('page')) || 1, pageSize: Number(g('size')) || 15 }
  const q = useQuery({ queryKey: ['finance', 'supplier-debts', p], queryFn: () => financeApi.supplierDebts(p), placeholderData: keepPreviousData })
  const set = (patch: Record<string, string | null>, keepPage = false) => setParams((prev) => { const n = new URLSearchParams(prev); for (const [k, v] of Object.entries(patch)) { if (v) n.set(k, v); else n.delete(k) } if (!keepPage) n.delete('page'); return n }, { replace: true })
  const d = q.data
  const supplierId = g('supplier')
  const exportFile = async (format: 'xlsx' | 'csv') => {
    setExporting(true)
    try { await financeApi.exportFile('supplier-debts', format, { storeIds, from: filters.from, to: filters.to, status: segment, search: search || undefined }); toast(t('stats.exportReady')) } catch (err) { reportError(err, t('stats.exportFailed')) } finally { setExporting(false) }
  }
  if (q.isError) return <BlockError message={(q.error as Error).message} onRetry={() => void q.refetch()} />
  return (
    <div className="flex flex-col gap-3">
      {!d ? <Skeleton className="h-24" /> : (
        <div className="grid grid-cols-2 gap-3 xl:grid-cols-4">
          <StatCard icon={<Truck size={18} />} label={t('fin.sd.total')} value={tenge(d.total)} hint={t('fin.sd.documentsAll', { n: d.documentsAll })} />
          <StatCard icon={<CalendarClock size={18} />} label={t('fin.overdue')} value={tenge(d.overdue)} tone={d.overdue > 0 ? 'danger' : 'success'} />
          <StatCard icon={<CalendarClock size={18} />} label={t('fin.sd.dueSoon7')} value={tenge(d.dueSoon)} tone="amber" />
          <StatCard icon={<FileCheck size={18} />} label={t('fin.sd.documentsWithDebt')} value={d.documents} tone="violet" />
        </div>
      )}
      <div className="flex flex-wrap items-center gap-2">
        <SearchInput value={g('q')} onChange={(v) => set({ q: v })} placeholder={t('fin.sd.search')} className="h-10! w-64" />
        <Segmented value={segment} options={(['open', 'overdue', 'dueSoon', 'paid', 'all'] as Segment[]).map((s) => ({ key: s, label: t(`fin.sd.seg.${s}` as TranslationKey) }))} onChange={(s) => set({ seg: s })} />
        <span className="ml-auto" />
        {canExport && <span className="flex gap-1"><Button variant="secondary" className="h-10! px-3!" icon={<Download size={16} />} disabled={exporting} onClick={() => void exportFile('xlsx')}>Excel</Button><Button variant="secondary" className="h-10! px-3!" disabled={exporting} onClick={() => void exportFile('csv')}>CSV</Button></span>}
      </div>
      <div className="overflow-x-auto rounded-2xl border border-line bg-surface">
        <table className="w-full min-w-[960px]">
          <thead className="bg-field/60"><tr>
            <th className={th}>{t('fin.sd.supplier')}</th><th className={th}>{t('fin.sd.lastReceipt')}</th><th className={`${th} text-right`}>{t('fin.sd.purchases')}</th><th className={`${th} text-right`}>{t('fin.paid')}</th>
            <th className={`${th} text-right`}>{t('fin.remaining')}</th><th className={`${th} text-right`}>{t('fin.overdue')}</th><th className={th}>{t('fin.nextDue')}</th><th className={th}>{t('fin.col.status')}</th><th className={`${th} w-12`}></th>
          </tr></thead>
          <tbody>
            {!d ? null : d.rows.length === 0 ? <EmptyRow colSpan={9} text={t('fin.sd.empty')} /> : d.rows.map((r) => (
              <tr key={r.supplierId} className="border-t border-line hover:bg-field/40">
                <td className={td}><button type="button" className="font-medium hover:text-brand" onClick={() => set({ supplier: r.supplierId }, true)}>{r.name}</button>{r.phone && <span className="block text-[12px] text-ink-3">{r.phone}</span>}</td>
                <td className={`${td} text-ink-2`}>{r.lastReceipt ? dateOnly(r.lastReceipt) : '—'}</td>
                <td className={`${td} text-right tabular-nums`}>{tenge(r.purchases)}</td>
                <td className={`${td} text-right tabular-nums text-ink-2`}>{tenge(r.paid)}</td>
                <td className={`${td} text-right font-semibold tabular-nums`}>{tenge(r.debt)}</td>
                <td className={`${td} text-right tabular-nums ${r.overdue > 0 ? 'text-danger' : 'text-ink-3'}`}>{r.overdue > 0 ? tenge(r.overdue) : '—'}</td>
                <td className={`${td} text-ink-2`}>{r.nextDue ? dateOnly(r.nextDue) : '—'}</td>
                <td className={td}><DebtBadge status={r.status} /></td>
                <td className={td}><RowMenu items={[
                  { label: t('fin.open'), icon: <Eye size={16} />, onClick: () => set({ supplier: r.supplierId }, true) },
                  { label: t('fin.sd.pay'), icon: <Truck size={16} />, onClick: () => set({ supplier: r.supplierId, pay: '1' }, true) },
                  { label: t('fin.sd.reconciliation'), icon: <FileCheck size={16} />, onClick: () => set({ supplier: r.supplierId, act: '1' }, true) },
                ]} /></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {d && <Pager page={p.page} pageSize={p.pageSize} total={d.rowsTotal} onPage={(pg) => set({ page: String(pg) }, true)} onPageSize={(s) => set({ size: String(s) })} />}
      {supplierId && <SupplierCardModal supplierId={supplierId} storeIds={storeIds} filters={filters} pay={g('pay') === '1'} act={g('act') === '1'} onClose={() => set({ supplier: null, pay: null, act: null }, true)} onChanged={() => void q.refetch()} />}
    </div>
  )
}

/** Жеткізуші карточкасы (§12.1): сводка, «Накладные», «Оплаты»; «Оплатить», «Акт сверки», «Новая накладная» → Склад. */
function SupplierCardModal({ supplierId, storeIds, filters, pay, act, onClose, onChanged }: {
  supplierId: string; storeIds?: string; filters: FinanceFilters; pay: boolean; act: boolean; onClose: () => void; onChanged: () => void
}) {
  const t = useT()
  const [tab, setTab] = useState<'docs' | 'payments'>('docs')
  const [paying, setPaying] = useState(pay)
  const [actOpen, setActOpen] = useState(act)
  const meta = useFinanceMeta(storeIds)
  const { canOperate } = useFinancePermissions()
  const q = useQuery({ queryKey: ['finance', 'supplier-debt', supplierId, storeIds], queryFn: () => financeApi.supplierDebt(supplierId, storeIds) })
  const c = q.data
  const setDue = async (documentId: string, value: string) => {
    try { await financeApi.setDueDate(documentId, value || null); toast(t('fin.saved')); void q.refetch(); onChanged() } catch (err) { reportError(err, t('fin.failed')) }
  }
  return (
    <Modal title={c ? c.name : t('fin.sd.supplier')} onClose={onClose} width={820}>
      {!c ? <Skeleton className="h-48" /> : (
        <div className="flex flex-col gap-4">
          <div className="flex flex-wrap items-center gap-x-5 gap-y-1 text-[13px] text-ink-2">
            {c.phone && <span>{c.phone}</span>}{c.taxId && <span>{t('fin.sd.taxId')}: {c.taxId}</span>}
            <span>{t('fin.remaining')}: <b className="text-[15px] text-ink">{tenge(c.debt)}</b></span>
            <span>{t('fin.overdue')}: <b className={c.overdue > 0 ? 'text-danger' : ''}>{tenge(c.overdue)}</b></span>
            <span>{t('fin.sd.openDocs')}: <b>{c.openDocuments}</b></span>
            {c.nextDue && <span>{t('fin.nextDue')}: <b>{dateOnly(c.nextDue)}</b></span>}
          </div>
          <div className="flex flex-wrap gap-2">
            <Link to={`/warehouse/suppliers/${supplierId}`} className="inline-flex h-9 items-center gap-1 rounded-xl border border-line px-3 text-[13px] font-semibold hover:border-brand">{t('fin.sd.supplierCard')}</Link>
            <Link to={`/warehouse/receipts/new?supplierId=${supplierId}`} className="inline-flex h-9 items-center gap-1 rounded-xl border border-line px-3 text-[13px] font-semibold hover:border-brand">{t('fin.sd.newReceipt')}</Link>
            <Button variant="secondary" className="h-9! px-3! text-[13px]!" icon={<FileCheck size={15} />} onClick={() => setActOpen(true)}>{t('fin.sd.reconciliation')}</Button>
            <span className="ml-auto" />
            {canOperate && c.debt > 0 && <Button className="h-9! px-3! text-[13px]!" icon={<Truck size={15} />} onClick={() => setPaying(true)}>{t('fin.sd.pay')}</Button>}
          </div>
          <Segmented value={tab} options={[{ key: 'docs', label: `${t('fin.sd.receipts')} (${c.documents.length})` }, { key: 'payments', label: `${t('fin.payments')} (${c.payments.length})` }]} onChange={setTab} />
          {tab === 'docs' ? (
            <table className="w-full text-[14px]">
              <thead className="bg-field/60"><tr><th className={th}>{t('fin.sd.number')}</th><th className={th}>{t('fin.col.date')}</th><th className={`${th} text-right`}>{t('fin.col.amount')}</th><th className={`${th} text-right`}>{t('fin.paid')}</th><th className={`${th} text-right`}>{t('fin.remaining')}</th><th className={th}>{t('fin.dueDate')}</th></tr></thead>
              <tbody>{c.documents.length === 0 ? <EmptyRow colSpan={6} text="—" /> : c.documents.map((x) => (
                <tr key={x.documentId} className="border-t border-line">
                  <td className={td}><Link to={`/warehouse/receipts/${x.documentId}`} className="font-medium text-brand hover:underline">{x.number}</Link>{x.invoiceNumber && <span className="block text-[12px] text-ink-3">{x.invoiceNumber}</span>}</td>
                  <td className={`${td} text-ink-2`}>{dateOnly(x.date)}</td>
                  <td className={`${td} text-right tabular-nums`}>{tenge(x.total)}</td>
                  <td className={`${td} text-right tabular-nums text-ink-2`}>{tenge(x.paid)}</td>
                  <td className={`${td} text-right font-semibold tabular-nums ${x.remaining > 0 ? '' : 'text-ink-3'}`}>{tenge(x.remaining)}</td>
                  <td className={td}>
                    {x.remaining > 0 && canOperate ? (
                      <input type="date" defaultValue={x.dueDate ?? ''} onBlur={(e) => { if ((e.target.value || null) !== x.dueDate) void setDue(x.documentId, e.target.value) }}
                        className={`h-9 rounded-lg border px-2 text-[13px] ${x.overdue ? 'border-danger text-danger' : 'border-line'}`} aria-label={t('fin.dueDate')} />
                    ) : <span className={x.overdue ? 'text-danger' : 'text-ink-2'}>{x.dueDate ? dateOnly(x.dueDate) : '—'}</span>}
                  </td>
                </tr>
              ))}</tbody>
            </table>
          ) : (
            <table className="w-full text-[14px]">
              <thead className="bg-field/60"><tr><th className={th}>{t('fin.col.date')}</th><th className={th}>{t('fin.sd.number')}</th><th className={`${th} text-right`}>{t('fin.col.amount')}</th><th className={th}>{t('fin.method')}</th><th className={th}>{t('fin.account')}</th><th className={th}>{t('fin.col.document')}</th><th className={th}>{t('fin.col.status')}</th><th className={th}>{t('fin.operation')}</th></tr></thead>
              <tbody>{c.payments.length === 0 ? <EmptyRow colSpan={8} text="—" /> : c.payments.map((x) => (
                <tr key={x.id} className={`border-t border-line ${x.status === 'Reversed' ? 'text-ink-3 line-through' : ''}`}>
                  <td className={`${td} text-ink-2`}>{dateTime(x.createdAt)}</td>
                  <td className={td}>{x.documentNumber ?? '—'}</td>
                  <td className={`${td} text-right font-semibold tabular-nums ${x.status === 'Reversed' ? '' : 'text-danger'}`}>−{tenge(x.amount)}</td>
                  <td className={`${td} text-ink-2`}>{t(`pay.${x.method}` as TranslationKey)}</td>
                  <td className={`${td} text-ink-2`}>{x.accountName ?? '—'}</td>
                  <td className={`${td} text-ink-2`}>{x.reference ?? '—'}</td>
                  <td className={td}>{t(`fin.payStatus.${x.status}` as TranslationKey)}</td>
                  <td className={td}>{x.operationId ? <Link to={`/finance/transactions?op=${x.operationId}`} className="text-brand hover:underline">{x.operationNumber}</Link> : '—'}</td>
                </tr>
              ))}</tbody>
            </table>
          )}
        </div>
      )}
      {paying && c && <SupplierPaymentModal card={c} accounts={meta.data?.accounts ?? []} onClose={() => setPaying(false)} onDone={() => { setPaying(false); void q.refetch(); onChanged() }} />}
      {actOpen && <ReconciliationModal supplierId={supplierId} storeIds={storeIds} filters={filters} onClose={() => setActOpen(false)} />}
    </Modal>
  )
}

/** «Оплатить поставщику» (§12.2): ашық накладнаялар, бөлу = төлем, шот, әдіс, құжат нөмірі, вложение. */
function SupplierPaymentModal({ card, accounts, onClose, onDone }: { card: SupplierDebtCard; accounts: FinanceAccount[]; onClose: () => void; onDone: () => void }) {
  const t = useT()
  const open = useMemo(() => card.documents.filter((x) => x.remaining > 0), [card])
  const [alloc, setAlloc] = useState<Record<string, string>>(() => Object.fromEntries(open.map((x) => [x.documentId, ''])))
  const [method, setMethod] = useState<PayMethod>('Transfer')
  const [accountId, setAccountId] = useState('')
  const [reference, setReference] = useState('')
  const [comment, setComment] = useState('')
  const [attachmentUrl, setAttachmentUrl] = useState('')
  const [uploading, setUploading] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const fileRef = useRef<HTMLInputElement>(null)
  const requestId = useRef(crypto.randomUUID())
  const num = (id: string) => Number((alloc[id] ?? '').replace(',', '.')) || 0
  const sum = open.reduce((s, x) => s + num(x.documentId), 0)
  const over = open.some((x) => num(x.documentId) > x.remaining)
  const account = accounts.find((a) => a.id === accountId) ?? accounts.find((a) => a.isActive && a.methods.includes(method))
  const insufficient = !!account && sum > account.balance
  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!(sum > 0) || over || insufficient) return
    setBusy(true)
    try {
      await financeApi.supplierPayment({
        supplierId: card.supplierId, accountId: accountId || null, method, reference: reference.trim() || null, comment: comment.trim() || null, attachmentUrl: attachmentUrl || null,
        allocations: open.filter((x) => num(x.documentId) > 0).map((x) => ({ documentId: x.documentId, amount: num(x.documentId) })), clientRequestId: requestId.current,
      })
      toast(t('fin.sd.paidToast', { amount: tenge(sum) }))
      onDone()
    } catch (err) { setError(err instanceof ApiError ? err.message : t('fin.failed')); reportError(err, t('fin.failed')) } finally { setBusy(false) }
  }
  const pick = async (file: File | undefined) => {
    if (!file) return
    setUploading(true)
    try { setAttachmentUrl(await uploadImage(file, 'finance')) } catch (err) { reportError(err, t('fin.err.upload')) } finally { setUploading(false) }
  }
  return (
    <Modal title={`${t('fin.sd.pay')} · ${card.name}`} onClose={onClose} width={640}>
      <form onSubmit={submit} className="flex flex-col gap-4">
        <div className="rounded-xl border border-line">
          <div className="flex items-center justify-between border-b border-line px-3 py-2 text-[13px] font-semibold text-ink-2"><span>{t('fin.sd.receipts')}</span><button type="button" className="text-brand hover:underline" onClick={() => setAlloc(Object.fromEntries(open.map((x) => [x.documentId, String(x.remaining)])))}>{t('fin.payAll')}</button></div>
          {open.map((x) => (
            <div key={x.documentId} className="flex items-center gap-3 border-b border-line px-3 py-2 text-[14px] last:border-0">
              <span className="min-w-0 flex-1">{x.number} · {dateOnly(x.date)}<span className={`ml-2 text-[12px] ${x.overdue ? 'text-danger' : 'text-ink-3'}`}>{t('fin.remaining')}: {tenge(x.remaining)}{x.dueDate ? ` · ${t('fin.dueDate')} ${dateOnly(x.dueDate)}` : ''}</span></span>
              <Input inputMode="decimal" className="h-9! w-32 text-right" value={alloc[x.documentId] ?? ''} onChange={(e) => setAlloc((a) => ({ ...a, [x.documentId]: e.target.value }))} error={num(x.documentId) > x.remaining} placeholder="0" />
            </div>
          ))}
        </div>
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <Field label={t('fin.method')} required>
            <Select value={method} onChange={(e) => setMethod(e.target.value as PayMethod)}>{PAY_METHODS.map((m) => <option key={m} value={m}>{t(`pay.${m}` as TranslationKey)}</option>)}</Select>
          </Field>
          <Field label={t('fin.fromAccount')} hint={account ? `${t('fin.balance')}: ${tenge(account.balance)}${sum > 0 && !insufficient ? ` → ${tenge(account.balance - sum)}` : ''}` : undefined} error={insufficient ? t('fin.err.insufficient', { balance: tenge(account?.balance ?? 0) }) : null}>
            <Select value={accountId} onChange={(e) => setAccountId(e.target.value)} error={insufficient}>
              <option value="">{t('fin.byMethod')}</option>
              {accounts.filter((a) => a.isActive).map((a) => <option key={a.id} value={a.id}>{a.name} · {tenge(a.balance)}</option>)}
            </Select>
          </Field>
          <Field label={t('fin.sd.reference')}><Input value={reference} maxLength={64} onChange={(e) => setReference(e.target.value)} placeholder={t('fin.sd.referenceHint')} /></Field>
          <Field label={t('fin.comment')}><Textarea value={comment} maxLength={500} onChange={(e) => setComment(e.target.value)} className="min-h-11! h-11" /></Field>
        </div>
        <div className="flex flex-wrap items-center gap-3">
          <input ref={fileRef} type="file" accept="image/jpeg,image/png,image/webp,application/pdf" className="hidden" onChange={(e) => void pick(e.target.files?.[0])} />
          <Button type="button" variant="secondary" className="h-9! px-3! text-[13px]!" icon={<Paperclip size={15} />} loading={uploading} onClick={() => fileRef.current?.click()}>{t('fin.attachFile')}</Button>
          {attachmentUrl && <a href={attachmentUrl} target="_blank" rel="noopener" className="text-[13px] text-brand underline">{t('fin.attachment')}</a>}
        </div>
        {error && <p className="text-[13px] text-danger">{error}</p>}
        <div className="flex items-center gap-3 border-t border-line pt-4">
          <span className="text-[15px]">{t('fin.cd.paymentSum')}: <b>{tenge(sum)}</b>{over && <span className="ml-2 text-[13px] text-danger">{t('fin.err.allocationTooMuch')}</span>}</span>
          <span className="ml-auto" />
          <Button type="button" variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" loading={busy} disabled={!(sum > 0) || over || insufficient}>{t('fin.sd.payBtn')}</Button>
        </div>
        <p className="text-[12px] text-ink-3">{t('fin.sd.hint')}</p>
      </form>
    </Modal>
  )
}

/** Акт сверки (§12.1, §13.5): кезең, жеткізулер мен төлемдер, басы/соңындағы қалдық; басып шығару, экспорт. */
function ReconciliationModal({ supplierId, storeIds, filters, onClose }: { supplierId: string; storeIds?: string; filters: FinanceFilters; onClose: () => void }) {
  const t = useT()
  const { canExport } = useFinancePermissions()
  const [from, setFrom] = useState(filters.from)
  const [to, setTo] = useState(filters.to)
  const [exporting, setExporting] = useState(false)
  const q = useQuery({ queryKey: ['finance', 'reconciliation', supplierId, storeIds, from, to], queryFn: () => financeApi.reconciliation(supplierId, storeIds, from, to) })
  const d = q.data
  const exportFile = async (format: 'xlsx' | 'csv') => {
    setExporting(true)
    try { await financeApi.exportFile('reconciliation', format, { id: supplierId, storeIds, from, to }); toast(t('stats.exportReady')) } catch (err) { reportError(err, t('stats.exportFailed')) } finally { setExporting(false) }
  }
  return (
    <Modal title={t('fin.sd.reconciliation')} onClose={onClose} width={820}>
      <div className="flex flex-col gap-3">
        <div className="flex flex-wrap items-end gap-2 print:hidden">
          <Field label={t('fin.from')}><Input type="date" value={from} max={to} onChange={(e) => setFrom(e.target.value)} className="h-10!" /></Field>
          <Field label={t('fin.to')}><Input type="date" value={to} min={from} max={isoDate(new Date())} onChange={(e) => setTo(e.target.value)} className="h-10!" /></Field>
          <span className="ml-auto" />
          <Button variant="secondary" className="h-10! px-3!" icon={<Printer size={16} />} onClick={() => window.print()}>{t('fin.print')}</Button>
          {canExport && <><Button variant="secondary" className="h-10! px-3!" icon={<Download size={16} />} disabled={exporting} onClick={() => void exportFile('xlsx')}>Excel</Button><Button variant="secondary" className="h-10! px-3!" disabled={exporting} onClick={() => void exportFile('csv')}>CSV</Button></>}
        </div>
        {!d ? <Skeleton className="h-48" /> : (
          <div className="print-area">
            <div className="mb-3 text-[13px] text-ink-2">
              <div className="text-[16px] font-bold text-ink">{t('fin.sd.actTitle', { name: d.counterparty })}</div>
              <div>{d.storeName} · {dateOnly(d.from)} – {dateOnly(d.to)}{d.taxId ? ` · ${t('fin.sd.taxId')} ${d.taxId}` : ''} · {t('fin.generatedAt')} {dateTime(d.generatedAt)}</div>
            </div>
            <table className="w-full text-[14px]">
              <thead className="bg-field/60"><tr><th className={th}>{t('fin.col.date')}</th><th className={th}>{t('fin.sd.kind')}</th><th className={th}>{t('fin.sd.number')}</th><th className={`${th} text-right`}>{t('fin.sd.debit')}</th><th className={`${th} text-right`}>{t('fin.sd.credit')}</th><th className={`${th} text-right`}>{t('fin.balance')}</th></tr></thead>
              <tbody>
                <tr className="border-t border-line font-semibold"><td className={td} colSpan={5}>{t('fin.cf.opening')}</td><td className={`${td} text-right tabular-nums`}>{tenge(d.opening)}</td></tr>
                {d.rows.map((r, i) => (
                  <tr key={i} className="border-t border-line">
                    <td className={`${td} text-ink-2`}>{dateOnly(r.date)}</td>
                    <td className={td}>{t(`fin.sd.kind.${r.kind}` as TranslationKey)}</td>
                    <td className={td}>{r.number}{r.description && <span className="block text-[12px] text-ink-3">{r.description}</span>}</td>
                    <td className={`${td} text-right tabular-nums`}>{r.debit ? tenge(r.debit) : ''}</td>
                    <td className={`${td} text-right tabular-nums`}>{r.credit ? tenge(r.credit) : ''}</td>
                    <td className={`${td} text-right tabular-nums`}>{tenge(r.balance)}</td>
                  </tr>
                ))}
                <tr className="border-t border-line font-bold"><td className={td} colSpan={3}>{t('fin.cf.closing')}</td><td className={`${td} text-right tabular-nums`}>{tenge(d.debit)}</td><td className={`${td} text-right tabular-nums`}>{tenge(d.credit)}</td><td className={`${td} text-right tabular-nums`}>{tenge(d.closing)}</td></tr>
              </tbody>
            </table>
            <p className="mt-3 text-[12px] text-ink-3">{t('fin.sd.actFooter')}</p>
          </div>
        )}
      </div>
    </Modal>
  )
}
