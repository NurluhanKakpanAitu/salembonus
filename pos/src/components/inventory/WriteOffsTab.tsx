import { useEffect, useState } from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { CheckCircle2, FileText, Plus, RotateCcw } from 'lucide-react'
import { Button } from '../ui/Button'
import { Select } from '../ui/Field'
import { SearchInput } from '../ui/SearchInput'
import { inventoryApi, inventoryKeys, type DocumentQuery } from '../../lib/inventoryApi'
import { dateTime } from '../../lib/dates'
import { num } from '../../lib/money'
import { useT, type TranslationKey } from '../../lib/i18n'
import { DocStatus, EmptyRow, Pager, PeriodSelect, periodRange, StatCard, td, th, useDebounced, useInventoryPermissions, type Period } from './inventoryUi'

/** «Списание» (ТЗ «Склад» §11): тізім, себеп, проведение. */
export function WriteOffsTab() {
  const t = useT()
  const navigate = useNavigate()
  const perms = useInventoryPermissions()
  const [params, setParams] = useSearchParams()
  const warehouses = useQuery({ queryKey: inventoryKeys.warehouses, queryFn: () => inventoryApi.warehouses() }).data ?? []
  const [search, setSearch] = useState(params.get('search') ?? '')
  const debounced = useDebounced(search.trim(), 300)
  const period = (params.get('period') as Period) || 'month'
  const status = params.get('status') ?? ''
  const warehouseId = params.get('warehouseId') ?? ''
  const page = Math.max(1, Number(params.get('page')) || 1)
  const pageSize = Number(params.get('pageSize')) || 20

  const update = (patch: Record<string, string>, resetPage = true) =>
    setParams((prev) => {
      const next = new URLSearchParams(prev)
      for (const [k, v] of Object.entries(patch)) { if (v) next.set(k, v); else next.delete(k) }
      if (resetPage) next.delete('page')
      return next
    }, { replace: true })
  useEffect(() => { if ((params.get('search') ?? '') !== debounced) update({ search: debounced }) }, [debounced])

  const query: DocumentQuery = { ...periodRange(period), status, warehouseId, search: debounced, page, pageSize }
  const { data, isLoading, isFetching } = useQuery({
    queryKey: [...inventoryKeys.writeOffs, query], queryFn: () => inventoryApi.writeOffs(query), placeholderData: keepPreviousData,
  })
  const items = data?.items ?? []
  const s = data?.stats

  return (
    <div>
      <div className="mb-4 grid grid-cols-3 gap-3">
        <StatCard icon={<FileText size={20} />} label={t('inv.wo.statTotal')} value={s ? num(s.total) : '…'} hint={t('inv.receipts.statPeriod')} />
        <StatCard icon={<FileText size={20} />} label={t('inv.doc.drafts')} value={s ? num(s.drafts) : '…'} tone="violet" />
        <StatCard icon={<CheckCircle2 size={20} />} label={t('inv.doc.posted')} value={s ? num(s.posted) : '…'} tone="danger" />
      </div>
      <div className="mb-4 flex flex-wrap items-center gap-2">
        {perms.writeOff && <Button icon={<Plus size={18} />} onClick={() => navigate('/warehouse/write-offs/new')}>{t('inv.wo.new')}</Button>}
        <PeriodSelect value={period} onChange={(p) => update({ period: p })} />
        <Select className="w-auto min-w-44" value={status} onChange={(e) => update({ status: e.target.value })}>
          <option value="">{t('inv.doc.allStatuses')}</option>
          {(['Draft', 'Posted', 'Reversed', 'Cancelled'] as const).map((k) => <option key={k} value={k}>{t(`inv.status.doc.${k}`)}</option>)}
        </Select>
        <Select className="w-auto min-w-48" value={warehouseId} onChange={(e) => update({ warehouseId: e.target.value })}>
          <option value="">{t('inv.stock.allWarehouses')}</option>
          {warehouses.map((w) => <option key={w.id} value={w.id}>{w.name}</option>)}
        </Select>
        <SearchInput className="ml-auto w-full sm:w-64" value={search} onChange={setSearch} placeholder={t('inv.wo.search')} />
        {(status || warehouseId || debounced || period !== 'month') && (
          <Button variant="secondary" icon={<RotateCcw size={16} />} onClick={() => { setSearch(''); setParams({}, { replace: true }) }}>{t('product.filter.reset')}</Button>
        )}
      </div>
      <div className={`overflow-x-auto rounded-2xl border border-line bg-surface transition-opacity ${isFetching && !isLoading ? 'opacity-70' : ''}`}>
        <table className="w-full min-w-[860px] border-collapse">
          <thead className="bg-field">
            <tr>
              <th className={th}>{t('inv.doc.number')}</th>
              <th className={th}>{t('inv.mov.date')}</th>
              <th className={th}>{t('inv.mov.warehouse')}</th>
              <th className={th}>{t('inv.wo.reason')}</th>
              <th className={`${th} text-right`}>{t('inv.doc.lines')}</th>
              <th className={th}>{t('inv.doc.author')}</th>
              <th className={th}>{t('catalog.col.status')}</th>
            </tr>
          </thead>
          <tbody>
            {isLoading && <EmptyRow colSpan={7} text={t('common.loading')} />}
            {!isLoading && items.length === 0 && <EmptyRow colSpan={7} text={t('inv.wo.empty')} />}
            {items.map((r) => (
              <tr key={r.id} className="cursor-pointer border-t border-line hover:bg-field/40" onClick={() => navigate(`/warehouse/write-offs/${r.id}`)}>
                <td className={`${td} font-semibold text-brand`}>{r.number}</td>
                <td className={`${td} whitespace-nowrap text-[13px]`}>{dateTime(r.date)}</td>
                <td className={td}>{r.warehouseName}</td>
                <td className={td}>{r.reason ? t(`inv.wo.reasons.${r.reason}` as TranslationKey) : '—'}</td>
                <td className={`${td} text-right`}>{r.lineCount} <span className="text-ink-3">/ {num(r.totalQuantity)}</span></td>
                <td className={`${td} text-[13px] text-ink-2`}>{r.createdByName}</td>
                <td className={td}><DocStatus status={r.status} kind="doc" /></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <Pager page={page} pageSize={pageSize} total={data?.total ?? 0}
        onPage={(p) => update({ page: String(p) }, false)} onPageSize={(s) => update({ pageSize: String(s) })} />
    </div>
  )
}
