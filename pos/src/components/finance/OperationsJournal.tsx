import { useState, type ReactNode } from 'react'
import { keepPreviousData, useQuery, useQueryClient } from '@tanstack/react-query'
import { useSearchParams } from 'react-router-dom'
import { Download, FilterX, Loader2 } from 'lucide-react'
import { Button } from '../ui/Button'
import { Select } from '../ui/Field'
import { SearchInput } from '../ui/SearchInput'
import { toast } from '../ui/Toast'
import { Pager, ReasonModal, useDebounced } from '../inventory/inventoryUi'
import { financeApi, financeKeys } from '../../lib/financeApi'
import { OPERATION_TYPES, type FinanceOperation, type OperationsQuery } from '../../lib/financeTypes'
import { tenge } from '../../lib/money'
import { useT, type TranslationKey } from '../../lib/i18n'
import { OperationCard, OperationsTable, reportError, useFinanceMeta, useFinancePermissions, type FinanceFilters } from './financeUi'

type FilterKey = 'type' | 'account' | 'category' | 'source' | 'status' | 'counterparty' | 'amount' | 'search'
const ALL: FilterKey[] = ['type', 'account', 'category', 'source', 'status', 'counterparty', 'amount', 'search']
const LOCAL = ['type', 'types', 'account', 'category', 'source', 'status', 'cp', 'min', 'max', 'q', 'page', 'size', 'sort', 'dir', 'flow']

/**
 * Операциялар журналы (ТЗ «Финансы» §6, §8): сүзгілер URL-да, жиын жолағы, кесте, карточка, сторно, экспорт (сүзгінің барлық жолы).
 * fixed — подраздел бекіткен сүзгі (мысалы, «Доходы и расходы» тек қолмен операцияларды көрсетеді).
 */
export function OperationsJournal({ filters, storeIds, fixed = {}, show = ALL, actions, compact, emptyText, onEditDraft }: {
  filters: FinanceFilters; storeIds?: string; fixed?: Partial<OperationsQuery>; show?: FilterKey[]; actions?: ReactNode; compact?: boolean; emptyText?: string
  onEditDraft?: (op: FinanceOperation) => void
}) {
  const t = useT()
  const qc = useQueryClient()
  const [params, setParams] = useSearchParams()
  const { canReverse, canExport } = useFinancePermissions()
  const meta = useFinanceMeta(storeIds)
  const [reversing, setReversing] = useState<FinanceOperation | null>(null)
  const [exporting, setExporting] = useState(false)
  const g = (k: string) => params.get(k) ?? ''
  const search = useDebounced(g('q'), 300)
  const flow = g('flow')
  const query: OperationsQuery = {
    from: filters.from, to: filters.to, storeIds,
    accountId: g('account') || undefined, type: g('type') || undefined,
    types: g('types') || (flow === 'in' ? 'Income,SalePayment,CustomerPayment' : flow === 'out' ? 'Expense,Refund,SupplierPayment' : undefined),
    categoryId: g('category') || undefined, source: g('source') || undefined, status: g('status') || undefined, counterpartyType: g('cp') || undefined,
    search: search || undefined, minAmount: g('min') || undefined, maxAmount: g('max') || undefined,
    sort: g('sort') || undefined, dir: g('dir') || undefined, page: Number(g('page')) || 1, pageSize: Number(g('size')) || 15, ...fixed,
  }
  const q = useQuery({ queryKey: financeKeys.operations(query), queryFn: () => financeApi.operations(query), placeholderData: keepPreviousData })
  const set = (patch: Record<string, string | null>, keepPage = false) => setParams((p) => {
    const n = new URLSearchParams(p)
    for (const [k, v] of Object.entries(patch)) { if (v) n.set(k, v); else n.delete(k) }
    if (!keepPage) n.delete('page')
    return n
  }, { replace: true })
  const reset = () => setParams((p) => { const n = new URLSearchParams(p); for (const k of LOCAL) n.delete(k); n.delete('op'); return n }, { replace: true })
  const dirty = LOCAL.some((k) => params.has(k) && k !== 'page' && k !== 'size')
  const invalidate = () => void qc.invalidateQueries({ queryKey: financeKeys.all })
  const exportFile = async (format: 'xlsx' | 'csv') => {
    setExporting(true)
    try { await financeApi.exportFile('operations', format, { ...query, manualOnly: fixed.manualOnly ? true : undefined }); toast(t('stats.exportReady')) }
    catch (err) { reportError(err, t('stats.exportFailed')) } finally { setExporting(false) }
  }
  const d = q.data
  const has = (k: FilterKey) => show.includes(k)
  const sel = 'h-10! w-auto! min-w-36 text-[13px]'
  const openId = g('op')

  return (
    <div className="flex flex-col gap-3">
      <div className="flex flex-wrap items-center gap-2">
        {has('search') && <SearchInput value={g('q')} onChange={(v) => set({ q: v })} placeholder={t('fin.searchOps')} className="h-10! w-64" />}
        {has('type') && (
          <Select className={sel} value={g('type')} onChange={(e) => set({ type: e.target.value, flow: null, types: null })} aria-label={t('fin.col.type')}>
            <option value="">{t('fin.filter.allTypes')}</option>
            {OPERATION_TYPES.filter((x) => !fixed.manualOnly || x === 'Income' || x === 'Expense').map((x) => <option key={x} value={x}>{t(`fin.type.${x}` as TranslationKey)}</option>)}
          </Select>
        )}
        {has('account') && (
          <Select className={sel} value={g('account')} onChange={(e) => set({ account: e.target.value })} aria-label={t('fin.account')}>
            <option value="">{t('fin.filter.allAccounts')}</option>
            {(meta.data?.accounts ?? []).map((a) => <option key={a.id} value={a.id}>{a.name}</option>)}
          </Select>
        )}
        {has('category') && (
          <Select className={sel} value={g('category')} onChange={(e) => set({ category: e.target.value })} aria-label={t('fin.col.category')}>
            <option value="">{t('fin.filter.allCategories')}</option>
            {(meta.data?.categories ?? []).filter((c) => !g('type') || c.type === g('type')).map((c) => <option key={c.id} value={c.id}>{c.name}{c.isActive ? '' : ` (${t('fin.archived')})`}</option>)}
          </Select>
        )}
        {has('source') && (
          <Select className={sel} value={g('source')} onChange={(e) => set({ source: e.target.value })} aria-label={t('fin.col.source')}>
            <option value="">{t('fin.filter.allSources')}</option>
            {['finance', 'sales', 'inventory', 'debts'].map((s) => <option key={s} value={s}>{t(`fin.source.${s}` as TranslationKey)}</option>)}
          </Select>
        )}
        {has('counterparty') && (
          <Select className={sel} value={g('cp')} onChange={(e) => set({ cp: e.target.value })} aria-label={t('fin.col.counterparty')}>
            <option value="">{t('fin.filter.allCounterparties')}</option>
            {['Customer', 'Supplier', 'Staff', 'Other'].map((c) => <option key={c} value={c}>{t(`fin.cp.${c}` as TranslationKey)}</option>)}
          </Select>
        )}
        {has('status') && (
          <Select className={sel} value={g('status')} onChange={(e) => set({ status: e.target.value })} aria-label={t('fin.col.status')}>
            <option value="">{t('fin.filter.allStatuses')}</option>
            {['Posted', 'Draft', 'Reversed'].map((s) => <option key={s} value={s}>{t(`fin.status.${s}` as TranslationKey)}</option>)}
          </Select>
        )}
        {has('amount') && (
          <span className="flex items-center gap-1">
            <input inputMode="decimal" value={g('min')} onChange={(e) => set({ min: e.target.value })} placeholder={t('fin.filter.min')} className="h-10 w-24 rounded-xl border border-line bg-surface px-3 text-[13px]" aria-label={t('fin.filter.min')} />
            <span className="text-ink-3">–</span>
            <input inputMode="decimal" value={g('max')} onChange={(e) => set({ max: e.target.value })} placeholder={t('fin.filter.max')} className="h-10 w-24 rounded-xl border border-line bg-surface px-3 text-[13px]" aria-label={t('fin.filter.max')} />
          </span>
        )}
        {dirty && <Button variant="ghost" className="h-10! px-3!" icon={<FilterX size={16} />} onClick={reset}>{t('fin.resetFilters')}</Button>}
        <span className="ml-auto" />
        {canExport && (
          <span className="flex items-center gap-1">
            <Button variant="secondary" className="h-10! px-3!" icon={exporting ? <Loader2 className="animate-spin" size={16} /> : <Download size={16} />} disabled={exporting} onClick={() => void exportFile('xlsx')}>Excel</Button>
            <Button variant="secondary" className="h-10! px-3!" disabled={exporting} onClick={() => void exportFile('csv')}>CSV</Button>
          </span>
        )}
        {actions}
      </div>

      {d && (
        <div className="flex flex-wrap gap-x-6 gap-y-1 rounded-xl bg-field/60 px-4 py-2 text-[13px] text-ink-2">
          <span>{t('fin.sum.count', { n: d.count })}</span>
          <span>{t('fin.sum.inflow')}: <b className="text-success">{tenge(d.inflow)}</b></span>
          <span>{t('fin.sum.outflow')}: <b className="text-danger">{tenge(d.outflow)}</b></span>
          {d.transfers > 0 && <span>{t('fin.sum.transfers')}: <b className="text-brand">{tenge(d.transfers)}</b></span>}
          <span>{t('fin.sum.net')}: <b>{tenge(d.inflow - d.outflow)}</b></span>
        </div>
      )}

      <OperationsTable items={d?.items ?? []} loading={q.isPending} accountId={g('account') || undefined} compact={compact} emptyText={emptyText}
        onOpen={(o) => set({ op: o.id }, true)} onReverse={canReverse ? (o) => setReversing(o) : undefined} />
      {d && <Pager page={d.page} pageSize={d.pageSize} total={d.total} onPage={(p) => set({ page: String(p) }, true)} onPageSize={(s) => set({ size: String(s) })} />}

      {openId && <OperationCard id={openId} onClose={() => set({ op: null }, true)} onChanged={invalidate} onEdit={onEditDraft ? (o) => { set({ op: null }, true); onEditDraft(o) } : undefined} />}
      {reversing && (
        <ReasonModal title={`${t('fin.reverse')} · ${reversing.number}`} hint={t('fin.reverseHint')} confirmLabel={t('fin.reverse')} onClose={() => setReversing(null)}
          onSubmit={async (reason) => {
            try { await financeApi.reverse(reversing.id, reason); toast(t('fin.reversed')); setReversing(null); invalidate() }
            catch (err) { reportError(err, t('fin.failed')) }
          }} />
      )}
    </div>
  )
}
