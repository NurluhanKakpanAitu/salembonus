import { useEffect, useState } from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { CheckCircle2, Clock, FileText, Plus, RotateCcw, Zap } from 'lucide-react'
import { Button } from '../ui/Button'
import { Select } from '../ui/Field'
import { SearchInput } from '../ui/SearchInput'
import { inventoryApi, inventoryKeys, type DocumentQuery } from '../../lib/inventoryApi'
import { dateTime } from '../../lib/dates'
import { num, tenge } from '../../lib/money'
import { useT } from '../../lib/i18n'
import { DocStatus, EmptyRow, Pager, PeriodSelect, periodRange, StatCard, td, th, useDebounced, useInventoryPermissions, type Period } from './inventoryUi'

/** «Приёмка» (ТЗ «Склад» §7.1): тізім, KPI, быстрая/полная приёмка. */
export function ReceiptsTab() {
  const t = useT()
  const navigate = useNavigate()
  const perms = useInventoryPermissions()
  const [params, setParams] = useSearchParams()
  const warehouses = useQuery({ queryKey: inventoryKeys.warehouses, queryFn: () => inventoryApi.warehouses() }).data ?? []
  const suppliers = useQuery({ queryKey: inventoryKeys.suppliers, queryFn: inventoryApi.suppliers }).data ?? []
  const [search, setSearch] = useState(params.get('search') ?? '')
  const debounced = useDebounced(search.trim(), 300)
  const period = (params.get('period') as Period) || 'month'
  const status = params.get('status') ?? ''
  const supplierId = params.get('supplierId') ?? ''
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

  const query: DocumentQuery = { ...periodRange(period), status, supplierId, warehouseId, search: debounced, page, pageSize }
  const { data, isLoading, isFetching } = useQuery({
    queryKey: [...inventoryKeys.receipts, query], queryFn: () => inventoryApi.receipts(query), placeholderData: keepPreviousData,
  })
  const items = data?.items ?? []
  const s = data?.stats

  return (
    <div>
      <div className="mb-4 grid grid-cols-2 gap-3 xl:grid-cols-4">
        <StatCard icon={<FileText size={20} />} label={t('inv.receipts.statTotal')} value={s ? num(s.total) : '…'} hint={t('inv.receipts.statPeriod')} />
        <StatCard icon={<FileText size={20} />} label={t('inv.doc.drafts')} value={s ? num(s.drafts) : '…'} hint={t('inv.doc.draftsHint')} tone="violet" />
        <StatCard icon={<CheckCircle2 size={20} />} label={t('inv.doc.posted')} value={s ? num(s.posted) : '…'} tone="success" />
        <StatCard icon={<Clock size={20} />} label={t('inv.receipts.awaiting')} value={s ? num(s.awaitingPayment) : '…'} hint={t('inv.receipts.awaitingHint')} tone="amber" />
      </div>
      <div className="mb-3 flex flex-wrap items-center gap-2">
        {perms.receive && (
          <>
            <Button variant="secondary" icon={<Zap size={17} />} onClick={() => navigate('/warehouse/receipts/quick')}>{t('inv.receipts.quick')}</Button>
            <Button icon={<Plus size={18} />} onClick={() => navigate('/warehouse/receipts/new')}>{t('inv.receipts.new')}</Button>
          </>
        )}
        <SearchInput className="ml-auto w-full sm:w-72" value={search} onChange={setSearch} placeholder={t('inv.receipts.search')} />
      </div>
      <div className="mb-4 flex flex-wrap items-center gap-2">
        <PeriodSelect value={period} onChange={(p) => update({ period: p })} />
        <Select className="w-44!" value={status} onChange={(e) => update({ status: e.target.value })}>
          <option value="">{t('inv.doc.allStatuses')}</option>
          {(['Draft', 'Posted', 'Reversed', 'Cancelled'] as const).map((k) => <option key={k} value={k}>{t(`inv.status.doc.${k}`)}</option>)}
        </Select>
        <Select className="w-52!" value={supplierId} onChange={(e) => update({ supplierId: e.target.value })}>
          <option value="">{t('inv.receipts.allSuppliers')}</option>
          {suppliers.map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
        </Select>
        <Select className="w-48!" value={warehouseId} onChange={(e) => update({ warehouseId: e.target.value })}>
          <option value="">{t('inv.stock.allWarehouses')}</option>
          {warehouses.map((w) => <option key={w.id} value={w.id}>{w.name}</option>)}
        </Select>
        {(status || supplierId || warehouseId || debounced || period !== 'month') && (
          <Button variant="secondary" icon={<RotateCcw size={16} />} onClick={() => { setSearch(''); setParams({}, { replace: true }) }}>{t('product.filter.reset')}</Button>
        )}
      </div>
      <div className={`overflow-x-auto rounded-2xl border border-line bg-surface transition-opacity ${isFetching && !isLoading ? 'opacity-70' : ''}`}>
        <table className="w-full min-w-[1000px] border-collapse">
          <thead className="bg-field">
            <tr>
              <th className={th}>{t('inv.doc.number')}</th>
              <th className={th}>{t('inv.mov.date')}</th>
              <th className={th}>{t('inv.receipts.supplier')}</th>
              <th className={th}>{t('inv.mov.warehouse')}</th>
              <th className={`${th} text-right`}>{t('inv.doc.lines')}</th>
              <th className={`${th} text-right`}>{t('inv.doc.total')}</th>
              <th className={`${th} text-right`}>{t('inv.doc.paid')}</th>
              <th className={`${th} text-right`}>{t('inv.doc.remaining')}</th>
              <th className={th}>{t('catalog.col.status')}</th>
            </tr>
          </thead>
          <tbody>
            {isLoading && <EmptyRow colSpan={9} text={t('common.loading')} />}
            {!isLoading && items.length === 0 && <EmptyRow colSpan={9} text={t('inv.receipts.empty')} />}
            {items.map((r) => (
              <tr key={r.id} className="cursor-pointer border-t border-line hover:bg-field/40" onClick={() => navigate(`/warehouse/receipts/${r.id}`)}>
                <td className={`${td} font-semibold text-brand`}>{r.number}{r.isQuick && <span className="ml-1.5 rounded bg-field px-1 text-[10px] font-semibold text-ink-3">{t('inv.receipts.quickTag')}</span>}</td>
                <td className={`${td} whitespace-nowrap text-[13px]`}>{dateTime(r.date)}</td>
                <td className={td}>{r.supplierName ?? '—'}</td>
                <td className={td}>{r.warehouseName}</td>
                <td className={`${td} text-right`}>{r.lineCount} <span className="text-ink-3">/ {num(r.totalQuantity)}</span></td>
                <td className={`${td} text-right font-semibold`}>{tenge(r.total)}</td>
                <td className={`${td} text-right`}>{tenge(r.paid)}</td>
                <td className={`${td} text-right ${r.status === 'Posted' && r.remaining > 0 ? 'font-semibold text-danger' : ''}`}>{r.supplierId ? tenge(r.remaining) : '—'}</td>
                <td className={td}>
                  <DocStatus status={r.status} kind="doc" />
                  {r.status === 'Posted' && r.supplierId && r.remaining > 0 && (
                    <span className="ml-1 rounded-lg bg-amber-100 px-2 py-0.5 text-[11px] font-semibold text-amber-800">{r.paid > 0 ? t('inv.pay.partial') : t('inv.pay.unpaid')}</span>
                  )}
                </td>
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
