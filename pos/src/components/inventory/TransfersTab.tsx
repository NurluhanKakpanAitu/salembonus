import { useEffect, useState } from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { ArrowLeftRight, CheckCircle2, FileText, Plus, RotateCcw, Truck } from 'lucide-react'
import { Button } from '../ui/Button'
import { Select } from '../ui/Field'
import { SearchInput } from '../ui/SearchInput'
import { inventoryApi, inventoryKeys } from '../../lib/inventoryApi'
import { dateTime } from '../../lib/dates'
import { num } from '../../lib/money'
import { useT } from '../../lib/i18n'
import { DocStatus, EmptyRow, Pager, PeriodSelect, periodRange, StatCard, td, th, useDebounced, useInventoryPermissions, type Period } from './inventoryUi'

/** «Перемещение» (ТЗ «Склад» §10.1–10.2): тізім, KPI, «Приёмка перемещения» — осы дүкенге келе жатқандар. */
export function TransfersTab() {
  const t = useT()
  const navigate = useNavigate()
  const perms = useInventoryPermissions()
  const [params, setParams] = useSearchParams()
  const [search, setSearch] = useState(params.get('search') ?? '')
  const debounced = useDebounced(search.trim(), 300)
  const period = (params.get('period') as Period) || 'month'
  const status = params.get('status') ?? ''
  const incoming = params.get('incoming') === '1'
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

  const query = { ...periodRange(period), status: incoming ? 'InTransit' : status, incoming, search: debounced, page, pageSize }
  const { data, isLoading, isFetching } = useQuery({
    queryKey: [...inventoryKeys.transfers, query], queryFn: () => inventoryApi.transfers(query), placeholderData: keepPreviousData,
  })
  const items = data?.items ?? []
  const s = data?.stats

  return (
    <div>
      <div className="mb-4 grid grid-cols-2 gap-3 xl:grid-cols-4">
        <StatCard icon={<ArrowLeftRight size={20} />} label={t('inv.tr.statTotal')} value={s ? num(s.total) : '…'} hint={t('inv.receipts.statPeriod')} />
        <StatCard icon={<FileText size={20} />} label={t('inv.doc.drafts')} value={s ? num(s.drafts) : '…'} tone="violet" />
        <StatCard icon={<Truck size={20} />} label={t('inv.status.transfer.InTransit')} value={s ? num(s.inTransit) : '…'} hint={t('inv.tr.inTransitHint')} tone="amber" />
        <StatCard icon={<CheckCircle2 size={20} />} label={t('inv.status.transfer.Completed')} value={s ? num(s.completed) : '…'} tone="success" />
      </div>
      <div className="mb-4 flex flex-wrap items-center gap-2">
        {perms.transfer && <Button icon={<Plus size={18} />} onClick={() => navigate('/warehouse/transfers/new')}>{t('inv.tr.new')}</Button>}
        <Button variant="secondary" className={incoming ? 'border-brand! text-brand!' : ''} icon={<Truck size={17} />} onClick={() => update({ incoming: incoming ? '' : '1', status: '' })}>
          {t('inv.tr.incoming')}
        </Button>
        <PeriodSelect value={period} onChange={(p) => update({ period: p })} />
        {!incoming && (
          <Select className="w-auto min-w-48" value={status} onChange={(e) => update({ status: e.target.value })}>
            <option value="">{t('inv.doc.allStatuses')}</option>
            {(['Draft', 'InTransit', 'Completed', 'Cancelled'] as const).map((k) => <option key={k} value={k}>{t(`inv.status.transfer.${k}`)}</option>)}
          </Select>
        )}
        <SearchInput className="ml-auto w-full sm:w-64" value={search} onChange={setSearch} placeholder={t('inv.tr.search')} />
        {(status || incoming || debounced || period !== 'month') && (
          <Button variant="secondary" icon={<RotateCcw size={16} />} onClick={() => { setSearch(''); setParams({}, { replace: true }) }}>{t('product.filter.reset')}</Button>
        )}
      </div>
      <div className={`overflow-x-auto rounded-2xl border border-line bg-surface transition-opacity ${isFetching && !isLoading ? 'opacity-70' : ''}`}>
        <table className="w-full min-w-[1000px] border-collapse">
          <thead className="bg-field">
            <tr>
              <th className={th}>{t('inv.doc.number')}</th>
              <th className={th}>{t('inv.mov.date')}</th>
              <th className={th}>{t('inv.tr.from')}</th>
              <th className={th}>{t('inv.tr.to')}</th>
              <th className={`${th} text-right`}>{t('inv.doc.lines')}</th>
              <th className={th}>{t('inv.doc.author')}</th>
              <th className={th}>{t('catalog.col.status')}</th>
            </tr>
          </thead>
          <tbody>
            {isLoading && <EmptyRow colSpan={7} text={t('common.loading')} />}
            {!isLoading && items.length === 0 && <EmptyRow colSpan={7} text={incoming ? t('inv.tr.noIncoming') : t('inv.tr.empty')} />}
            {items.map((r) => (
              <tr key={r.id} className="cursor-pointer border-t border-line hover:bg-field/40" onClick={() => navigate(`/warehouse/transfers/${r.id}`)}>
                <td className={`${td} font-semibold text-brand`}>{r.number}</td>
                <td className={`${td} whitespace-nowrap text-[13px]`}>{dateTime(r.sentAt ?? r.createdAt)}</td>
                <td className={td}>{r.sourceStoreName} <span className="text-ink-3">/ {r.sourceWarehouseName}</span></td>
                <td className={td}>{r.destinationStoreName} <span className="text-ink-3">/ {r.destinationWarehouseName}</span></td>
                <td className={`${td} text-right`}>{r.lineCount} <span className="text-ink-3">/ {num(r.totalQuantity)}{r.totalReceived != null && r.totalReceived !== r.totalQuantity ? ` → ${num(r.totalReceived)}` : ''}</span></td>
                <td className={`${td} text-[13px] text-ink-2`}>{r.createdByName}</td>
                <td className={td}><DocStatus status={r.status} kind="transfer" /></td>
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
