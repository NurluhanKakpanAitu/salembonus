import { useState, type ReactNode } from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { Link, useSearchParams } from 'react-router-dom'
import {
  ArrowDownLeft, ArrowLeftRight, ArrowUpRight, Banknote, ExternalLink, Eye, FileText, Landmark, Loader2, Lock, Paperclip, PiggyBank, QrCode, RotateCcw, Scale,
  ShoppingCart, Truck, Undo2, User, Wallet,
} from 'lucide-react'
import { Button } from '../ui/Button'
import { Modal } from '../ui/Modal'
import { RowMenu } from '../ui/RowMenu'
import { toast } from '../ui/Toast'
import { confirmDialog } from '../ui/Confirm'
import { EmptyRow, ReasonModal, td, th } from '../inventory/inventoryUi'
import { presetRange } from '../stats/statsUi'
import { ApiError } from '../../lib/api'
import { activeStore, can, useAuth } from '../../lib/auth'
import { dateTime } from '../../lib/dates'
import { financeApi, financeKeys } from '../../lib/financeApi'
import type { AccountType, DebtStatus, FinanceDay, FinanceKpi, FinanceOperation, OperationStatus, OperationType } from '../../lib/financeTypes'
import type { Bucket, Kpi } from '../../lib/analyticsTypes'
import { num, tenge } from '../../lib/money'
import { Permission } from '../../lib/types'
import { useT, type TranslationKey } from '../../lib/i18n'
import type { ChartSeries } from '../stats/ComboChart'

// ---------- Сүзгілер (§2): кезең мен дүкендер URL-да, подраздел ауысқанда сақталады ----------

export interface FinanceFilters { from: string; to: string; storeIds: string[] }

export function useFinanceFilters() {
  const [params, setParams] = useSearchParams()
  const store = useAuth(activeStore)
  const def = presetRange('thisMonth')
  const filters: FinanceFilters = {
    from: params.get('from') ?? def.from,
    to: params.get('to') ?? def.to,
    storeIds: params.get('stores')?.split(',').filter(Boolean) ?? (store ? [store.id] : []),
  }
  const update = (patch: Record<string, string | null>, resetPage = true) =>
    setParams((prev) => {
      const next = new URLSearchParams(prev)
      for (const [k, v] of Object.entries(patch)) { if (v) next.set(k, v); else next.delete(k) }
      if (resetPage) next.delete('page')
      return next
    }, { replace: true })
  return { filters, params, update, storeIds: filters.storeIds.join(',') || undefined }
}

export function useFinanceMeta(storeIds?: string) {
  return useQuery({ queryKey: [...financeKeys.meta(), storeIds], queryFn: () => financeApi.meta(storeIds), staleTime: 60_000 })
}

export function useFinancePermissions() {
  const store = useAuth(activeStore)
  return {
    canOperate: can(store, Permission.FinanceOperate),
    canReverse: can(store, Permission.FinanceReverse),
    canManage: can(store, Permission.FinanceAccounts),
    canExport: can(store, Permission.StatisticsExport),
    canContacts: can(store, Permission.CustomersContacts),
  }
}

/** Сервер қатесін toast-қа: өріс қатесі болса — хабар сол күйінде. */
export function reportError(err: unknown, fallback: string) {
  toast(err instanceof ApiError ? err.message : fallback, 'error')
}

// ---------- Түстер мен белгілер (§2): түс әрқашан мәтінмен қатар ----------

/** Кіріс — жасыл, шығыс — қызыл, аударым/инкассация — көк/күлгін, түзету — сары. */
export const TYPE_TONE: Record<OperationType, string> = {
  Income: 'bg-success-soft text-success', SalePayment: 'bg-success-soft text-success', CustomerPayment: 'bg-success-soft text-success',
  Expense: 'bg-danger-soft text-danger', Refund: 'bg-danger-soft text-danger', SupplierPayment: 'bg-danger-soft text-danger',
  Transfer: 'bg-brand-soft text-brand', Collection: 'bg-violet-50 text-violet-600', Adjustment: 'bg-amber-50 text-amber-700',
}
export const TYPE_ICON: Record<OperationType, ReactNode> = {
  Income: <ArrowDownLeft size={14} />, SalePayment: <ShoppingCart size={14} />, CustomerPayment: <User size={14} />,
  Expense: <ArrowUpRight size={14} />, Refund: <Undo2 size={14} />, SupplierPayment: <Truck size={14} />,
  Transfer: <ArrowLeftRight size={14} />, Collection: <Lock size={14} />, Adjustment: <Scale size={14} />,
}
export const ACCOUNT_ICON: Record<AccountType, ReactNode> = {
  Cash: <Banknote size={18} />, Safe: <Lock size={18} />, Bank: <Landmark size={18} />, Kaspi: <QrCode size={18} />, InTransit: <Truck size={18} />, Other: <Wallet size={18} />,
}
export const ACCOUNT_TONE: Record<AccountType, string> = {
  Cash: 'bg-success-soft text-success', Safe: 'bg-amber-50 text-amber-700', Bank: 'bg-brand-soft text-brand', Kaspi: 'bg-danger-soft text-danger',
  InTransit: 'bg-violet-50 text-violet-600', Other: 'bg-field text-ink-2',
}
export const CHART = { inflow: '#16a34a', outflow: '#e11d48', balance: '#1261d8', transfer: '#8b5cf6' }
export const SHARE_COLORS = ['#1261d8', '#38bdf8', '#8b5cf6', '#14b8a6', '#f59e0b', '#e11d48', '#64748b', '#0ea5e9', '#a3e635', '#f472b6']

export const isInflow = (t: OperationType) => t === 'Income' || t === 'SalePayment' || t === 'CustomerPayment'
export const isOutflow = (t: OperationType) => t === 'Expense' || t === 'Refund' || t === 'SupplierPayment'
export const isInternal = (t: OperationType) => t === 'Transfer' || t === 'Collection'

export function TypeBadge({ type }: { type: OperationType }) {
  const t = useT()
  return (
    <span className={`inline-flex items-center gap-1 rounded-lg px-2 py-0.5 text-[12px] font-semibold whitespace-nowrap ${TYPE_TONE[type]}`}>
      {TYPE_ICON[type]}{t(`fin.type.${type}` as TranslationKey)}
    </span>
  )
}

export function OpStatusBadge({ status }: { status: OperationStatus }) {
  const t = useT()
  const tone = status === 'Posted' ? 'bg-success-soft text-success' : status === 'Reversed' ? 'bg-danger-soft text-danger' : 'bg-amber-50 text-amber-700'
  return <span className={`inline-block rounded-lg px-2 py-0.5 text-[12px] font-semibold whitespace-nowrap ${tone}`}>{t(`fin.status.${status}` as TranslationKey)}</span>
}

export function DebtBadge({ status }: { status: DebtStatus }) {
  const t = useT()
  const tone = status === 'Paid' ? 'bg-success-soft text-success' : status === 'Overdue' ? 'bg-danger-soft text-danger' : status === 'Partial' ? 'bg-amber-50 text-amber-700' : 'bg-brand-soft text-brand'
  return <span className={`inline-block rounded-lg px-2 py-0.5 text-[12px] font-semibold whitespace-nowrap ${tone}`}>{t(`fin.debt.${status}` as TranslationKey)}</span>
}

/** Сома бағыты: таңдалған шотқа қатысты (§8) не операция түріне қарай. */
export function Amount({ value, type, accountId, entries, muted }: {
  value: number; type: OperationType; accountId?: string; entries?: FinanceOperation['entries']; muted?: boolean
}) {
  const entry = accountId ? entries?.find((e) => e.accountId === accountId) : undefined
  // Таңба — проводкалардан (сторно мен түзетуде түріне қарама-қарсы болуы мүмкін); проводка жоқ болса — түрінен.
  const fromEntries = !isInternal(type) && entries && entries.length > 0 ? entries.reduce((s, e) => s + e.amount, 0) : null
  const signed = entry ? entry.amount : fromEntries !== null ? fromEntries : isInflow(type) ? value : isOutflow(type) ? -value : value
  const tone = muted ? 'text-ink-3 line-through' : signed > 0 && !isInternal(type) ? 'text-success' : signed < 0 && !isInternal(type) ? 'text-danger' : 'text-ink'
  return <span className={`font-semibold tabular-nums whitespace-nowrap ${tone}`}>{signed > 0 && !isInternal(type) ? '+' : ''}{tenge(signed)}</span>
}

/** Серверлік KPI → статистиканың KpiCard форматы. */
export function toKpi(k: FinanceKpi | undefined): Kpi | undefined {
  if (!k) return undefined
  return {
    key: k.key, value: k.value, previous: k.previous, changeAbs: k.previous === null ? null : k.value - k.previous, changePercent: k.changePercent,
    amount: null, previousAmount: null, numerator: null, denominator: null, available: k.available, note: k.note,
  }
}

/** Күндер қатары → ComboChart (§4): бағана — түсім/төлем, сызық — қалдық. */
export function daysToChart(days: FinanceDay[], t: (k: TranslationKey) => string): { buckets: Bucket[]; series: ChartSeries[] } {
  return {
    buckets: days.map((d) => ({ start: d.date, end: d.date })),
    series: [
      { key: 'inflow', label: t('fin.kpi.inflow'), kind: 'money', style: 'bar', color: CHART.inflow, values: days.map((d) => d.inflow) },
      { key: 'outflow', label: t('fin.kpi.outflow'), kind: 'money', style: 'bar', color: CHART.outflow, values: days.map((d) => d.outflow) },
      { key: 'balance', label: t('fin.kpi.cash'), kind: 'money', style: 'line', color: CHART.balance, values: days.map((d) => d.balance) },
    ],
  }
}

export function Money({ v, className = '' }: { v: number | null | undefined; className?: string }) {
  const t = useT()
  if (v === null || v === undefined) return <span className={`text-ink-3 ${className}`}>{t('fin.notCalculated')}</span>
  return <span className={`tabular-nums whitespace-nowrap ${className}`}>{tenge(v)}</span>
}

export function SortTh({ k, label, sort, dir, onSort, right }: { k: string; label: string; sort: string | null; dir: string | null; onSort: (k: string) => void; right?: boolean }) {
  return (
    <th className={`${th} ${right ? 'text-right' : ''}`} aria-sort={sort === k ? (dir === 'asc' ? 'ascending' : 'descending') : 'none'}>
      <button type="button" onClick={() => onSort(k)} className={`inline-flex items-center gap-1 hover:text-brand ${sort === k ? 'text-brand' : ''}`}>
        {label}{sort === k && <span aria-hidden="true">{dir === 'asc' ? '↑' : '↓'}</span>}
      </button>
    </th>
  )
}

// ---------- Операциялар журналы (§6, §8): қайта қолданылатын кесте ----------

export function OperationsTable({ items, loading, accountId, onOpen, onReverse, compact, emptyText }: {
  items: FinanceOperation[]; loading?: boolean; accountId?: string; onOpen: (op: FinanceOperation) => void; onReverse?: (op: FinanceOperation) => void
  compact?: boolean; emptyText?: string
}) {
  const t = useT()
  return (
    <div className="overflow-x-auto rounded-2xl border border-line bg-surface">
      <table className="w-full min-w-[880px]">
        <thead className="bg-field/60">
          <tr>
            <th className={th}>{t('fin.col.date')}</th>
            <th className={th}>{t('fin.col.type')}</th>
            <th className={th}>{t('fin.col.description')}</th>
            <th className={th}>{t('fin.col.counterparty')}</th>
            <th className={th}>{t('fin.col.accounts')}</th>
            <th className={`${th} text-right`}>{t('fin.col.amount')}</th>
            {!compact && <th className={th}>{t('fin.col.document')}</th>}
            {!compact && <th className={th}>{t('fin.col.responsible')}</th>}
            <th className={th}>{t('fin.col.status')}</th>
            <th className={`${th} w-12`}></th>
          </tr>
        </thead>
        <tbody>
          {loading && items.length === 0 ? (
            <tr><td colSpan={10} className="py-10 text-center text-ink-3"><Loader2 className="mx-auto animate-spin" size={22} /></td></tr>
          ) : items.length === 0 ? <EmptyRow colSpan={10} text={emptyText ?? t('fin.emptyPeriod')} /> : items.map((o) => (
            <tr key={o.id} className="border-t border-line hover:bg-field/40">
              <td className={`${td} whitespace-nowrap text-ink-2`}>{dateTime(o.date)}</td>
              <td className={td}><TypeBadge type={o.type} /></td>
              <td className={td}>
                <button type="button" onClick={() => onOpen(o)} className="text-left font-medium hover:text-brand">{o.description}</button>
                <div className="text-[12px] text-ink-3">{o.number}{o.categoryName ? ` · ${o.categoryName}` : ''}{o.sourceModule ? ` · ${t(`fin.source.${o.sourceModule}` as TranslationKey)}` : ''}</div>
              </td>
              <td className={`${td} text-ink-2`}>{o.counterpartyName ?? '—'}</td>
              <td className={`${td} text-[13px] text-ink-2`}>
                {o.fromAccountName && o.toAccountName ? `${o.fromAccountName} → ${o.toAccountName}` : o.fromAccountName ?? o.toAccountName ?? '—'}
              </td>
              <td className={`${td} text-right`}><Amount value={o.amount} type={o.type} accountId={accountId} entries={o.entries} muted={o.status === 'Reversed'} /></td>
              {!compact && <td className={`${td} text-[13px] text-ink-2`}>{o.documentNumber ?? '—'}</td>}
              {!compact && <td className={`${td} text-[13px] text-ink-2`}>{o.createdByName}</td>}
              <td className={td}><OpStatusBadge status={o.status} /></td>
              <td className={td}>
                <RowMenu items={[
                  { label: t('fin.open'), icon: <Eye size={16} />, onClick: () => onOpen(o) },
                  ...(o.sourceLink ? [{ label: t('fin.openSource'), icon: <ExternalLink size={16} />, onClick: () => { window.location.hash = ''; window.location.assign(o.sourceLink!) } }] : []),
                  ...(onReverse && o.canReverse ? [{ label: t('fin.reverse'), icon: <RotateCcw size={16} />, danger: true, onClick: () => onReverse(o) }] : []),
                ]} />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

// ---------- Транзакция карточкасы (§8.1) ----------

export function OperationCard({ id, onClose, onChanged, onEdit }: { id: string; onClose: () => void; onChanged: () => void; onEdit?: (op: FinanceOperation) => void }) {
  const t = useT()
  const { canReverse, canOperate } = useFinancePermissions()
  const [reversing, setReversing] = useState(false)
  const [busy, setBusy] = useState(false)
  const postDraft = async (op: FinanceOperation) => {
    setBusy(true)
    try { await financeApi.postDraft(op.id); toast(t('fin.posted')); onChanged(); void q.refetch() } catch (err) { reportError(err, t('fin.failed')) } finally { setBusy(false) }
  }
  const deleteDraft = async (op: FinanceOperation) => {
    if (!(await confirmDialog({ message: t('fin.deleteDraftConfirm'), confirmLabel: t('fin.deleteDraft'), danger: true }))) return
    setBusy(true)
    try { await financeApi.deleteDraft(op.id); toast(t('fin.draftDeleted')); onChanged(); onClose() } catch (err) { reportError(err, t('fin.failed')) } finally { setBusy(false) }
  }
  const q = useQuery({ queryKey: financeKeys.operation(id), queryFn: () => financeApi.operation(id), placeholderData: keepPreviousData })
  const o = q.data
  const row = (label: string, value: ReactNode) => (
    <div className="flex justify-between gap-4 border-b border-line py-2 text-[14px] last:border-0"><span className="text-ink-2">{label}</span><span className="text-right font-medium">{value ?? '—'}</span></div>
  )
  return (
    <Modal title={o ? `${t(`fin.type.${o.type}` as TranslationKey)} · ${o.number}` : t('fin.operation')} onClose={onClose} width={640}>
      {!o ? <div className="py-10 text-center text-ink-3"><Loader2 className="mx-auto animate-spin" size={22} /></div> : (
        <div className="flex flex-col gap-4">
          <div className="flex flex-wrap items-center gap-3">
            <span className="text-[26px] font-extrabold tracking-tight"><Amount value={o.amount} type={o.type} entries={o.entries} muted={o.status === 'Reversed'} /></span>
            <OpStatusBadge status={o.status} />
            <TypeBadge type={o.type} />
            {o.sourceModule && <span className="rounded-lg bg-field px-2 py-0.5 text-[12px] font-semibold text-ink-2">{t(`fin.source.${o.sourceModule}` as TranslationKey)}</span>}
          </div>
          <div>
            {row(t('fin.col.date'), dateTime(o.date))}
            {row(t('fin.col.description'), o.description)}
            {row(t('fin.store'), o.storeName)}
            {o.categoryName && row(t('fin.col.category'), o.categoryName)}
            {o.counterpartyName && row(t('fin.col.counterparty'), `${o.counterpartyName}${o.counterpartyType ? ` (${t(`fin.cp.${o.counterpartyType}` as TranslationKey)})` : ''}`)}
            {o.method && row(t('fin.method'), t(`pay.${o.method}` as TranslationKey))}
            {o.documentNumber && row(t('fin.col.document'), o.documentNumber)}
            {row(t('fin.col.responsible'), `${o.createdByName} · ${dateTime(o.createdAt)}`)}
            {o.postedAt && row(t('fin.postedAt'), `${o.postedByName ?? ''} · ${dateTime(o.postedAt)}`)}
            {o.reversedAt && row(t('fin.reversedAt'), `${o.reversedByName ?? ''} · ${dateTime(o.reversedAt)}${o.reversalReason ? ` · ${o.reversalReason}` : ''}`)}
            {o.comment && row(t('fin.comment'), o.comment)}
          </div>
          {o.entries.length > 0 && (
            <div className="rounded-xl border border-line">
              <div className="border-b border-line px-3 py-2 text-[13px] font-semibold text-ink-2">{t('fin.entries')}</div>
              {o.entries.map((e, i) => (
                <div key={i} className="flex items-center justify-between gap-3 px-3 py-2 text-[14px]">
                  <span>{e.accountName}</span>
                  <span className="flex items-center gap-3">
                    <span className={`font-semibold tabular-nums ${e.amount >= 0 ? 'text-success' : 'text-danger'}`}>{e.amount > 0 ? '+' : ''}{tenge(e.amount)}</span>
                    <span className="text-[12px] text-ink-3">{t('fin.balanceAfter')}: {num(e.balanceAfter)} ₸</span>
                  </span>
                </div>
              ))}
            </div>
          )}
          <div className="flex flex-wrap items-center gap-2">
            {o.sourceLink && <Link to={o.sourceLink} className="inline-flex h-10 items-center gap-2 rounded-xl border border-line px-4 text-[14px] font-semibold hover:border-brand"><ExternalLink size={16} />{t('fin.openSource')}</Link>}
            {o.attachmentUrl && <a href={o.attachmentUrl} target="_blank" rel="noopener" className="inline-flex h-10 items-center gap-2 rounded-xl border border-line px-4 text-[14px] font-semibold hover:border-brand"><Paperclip size={16} />{t('fin.attachment')}</a>}
            {o.reversalOfId && <Link to={`?op=${o.reversalOfId}`} replace className="inline-flex h-10 items-center gap-2 rounded-xl border border-line px-4 text-[14px] font-semibold hover:border-brand"><FileText size={16} />{t('fin.original')}</Link>}
            {o.reversedById && <Link to={`?op=${o.reversedById}`} replace className="inline-flex h-10 items-center gap-2 rounded-xl border border-line px-4 text-[14px] font-semibold hover:border-brand"><RotateCcw size={16} />{t('fin.reversalDoc')}</Link>}
            <span className="ml-auto" />
            {o.status === 'Draft' && canOperate && (
              <>
                <Button variant="ghost" disabled={busy} onClick={() => void deleteDraft(o)}>{t('fin.deleteDraft')}</Button>
                {onEdit && <Button variant="secondary" disabled={busy} onClick={() => onEdit(o)}>{t('fin.editDraft')}</Button>}
                <Button loading={busy} onClick={() => void postDraft(o)}>{t('fin.post')}</Button>
              </>
            )}
            {o.status !== 'Draft' && <Button variant="secondary" onClick={() => window.print()}>{t('fin.print')}</Button>}
            {canReverse && o.canReverse && <Button variant="danger" onClick={() => setReversing(true)}>{t('fin.reverse')}</Button>}
          </div>
          {!o.canReverse && o.status === 'Posted' && o.sourceModule && <p className="text-[12px] text-ink-3">{t('fin.sourceReverseHint')}</p>}
        </div>
      )}
      {reversing && o && (
        <ReasonModal title={`${t('fin.reverse')} · ${o.number}`} hint={t('fin.reverseHint')} confirmLabel={t('fin.reverse')} onClose={() => setReversing(false)}
          onSubmit={async (reason) => {
            try {
              await financeApi.reverse(o.id, reason)
              toast(t('fin.reversed'))
              setReversing(false)
              onChanged()
              void q.refetch()
            } catch (err) { reportError(err, t('fin.failed')) }
          }} />
      )}
    </Modal>
  )
}

/** Шағын ескерту жолағы (§5, §10: себестоимость жоқ — үнсіз нөл емес). */
export function Warn({ text, tone = 'amber' }: { text: string; tone?: 'amber' | 'blue' }) {
  return <div role="status" className={`rounded-xl px-4 py-2.5 text-[13px] ${tone === 'amber' ? 'bg-amber-50 text-amber-800' : 'bg-brand-soft text-brand'}`}>{text}</div>
}

export { PiggyBank }
