import { useEffect, useState } from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { RotateCcw } from 'lucide-react'
import { Button } from '../ui/Button'
import { Select } from '../ui/Field'
import { SearchInput } from '../ui/SearchInput'
import { inventoryApi, inventoryKeys } from '../../lib/inventoryApi'
import type { MovementType } from '../../lib/inventoryTypes'
import { dateTime } from '../../lib/dates'
import { useT, type TranslationKey } from '../../lib/i18n'
import { EmptyRow, Pager, PeriodSelect, periodRange, td, th, useDebounced, type Period } from './inventoryUi'

const TYPES: MovementType[] = ['Receipt', 'Sale', 'Return', 'TransferOut', 'TransferIn', 'WriteOff', 'Opening', 'Adjustment', 'Reversal']

const TYPE_TONE: Record<MovementType, string> = {
  Receipt: 'bg-success-soft text-success', Sale: 'bg-brand-soft text-brand', Return: 'bg-violet-100 text-violet-700',
  TransferOut: 'bg-amber-100 text-amber-800', TransferIn: 'bg-amber-100 text-amber-800', WriteOff: 'bg-danger-soft text-danger',
  Opening: 'bg-field text-ink-2', Adjustment: 'bg-field text-ink-2', Reversal: 'bg-danger-soft text-danger',
}

/** «История движений» (ТЗ «Склад» §14): өзгермейтін журнал, әр жолда «қалдық кейін». */
export function MovementsTab() {
  const t = useT()
  const navigate = useNavigate()
  const comment = useComment()
  const [params, setParams] = useSearchParams()
  const warehouses = useQuery({ queryKey: [...inventoryKeys.warehouses, 'all'], queryFn: () => inventoryApi.warehouses(true) }).data ?? []
  const [document, setDocument] = useState(params.get('document') ?? '')
  const debounced = useDebounced(document.trim(), 300)
  const warehouseId = params.get('warehouseId') ?? ''
  const productId = params.get('productId') ?? ''
  const type = params.get('type') ?? ''
  const period = (params.get('period') as Period) || 'month'
  const page = Math.max(1, Number(params.get('page')) || 1)
  const pageSize = Number(params.get('pageSize')) || 50

  const update = (patch: Record<string, string>, resetPage = true) =>
    setParams((prev) => {
      const next = new URLSearchParams(prev)
      for (const [k, v] of Object.entries(patch)) { if (v) next.set(k, v); else next.delete(k) }
      if (resetPage) next.delete('page')
      return next
    }, { replace: true })
  useEffect(() => { if ((params.get('document') ?? '') !== debounced) update({ document: debounced }) }, [debounced])

  const { from, to } = periodRange(period)
  const query = { warehouseId, productId, type, document: debounced, from, to, page, pageSize }
  const { data, isLoading, isFetching } = useQuery({
    queryKey: [...inventoryKeys.movements, query], queryFn: () => inventoryApi.movements(query), placeholderData: keepPreviousData,
  })
  const items = data?.items ?? []
  const filtered = !!(warehouseId || productId || type || debounced || period !== 'month')

  return (
    <div>
      <div className="mb-3 flex flex-wrap items-center gap-2">
        <SearchInput className="w-full sm:w-72" value={document} onChange={setDocument} placeholder={t('inv.mov.searchDoc')} />
        <PeriodSelect value={period} onChange={(p) => update({ period: p })} />
        <Select className="w-auto min-w-52" value={warehouseId} onChange={(e) => update({ warehouseId: e.target.value })}>
          <option value="">{t('inv.stock.allWarehouses')}</option>
          {warehouses.map((w) => <option key={w.id} value={w.id}>{w.name}{w.isActive ? '' : ` (${t('status.Archived.m')})`}</option>)}
        </Select>
        <Select className="w-auto min-w-52" value={type} onChange={(e) => update({ type: e.target.value })}>
          <option value="">{t('inv.mov.allTypes')}</option>
          {TYPES.map((k) => <option key={k} value={k}>{t(`inv.mov.type.${k}` as TranslationKey)}</option>)}
        </Select>
        {productId && <span className="rounded-lg bg-brand-soft px-2.5 py-1.5 text-[13px] font-medium text-brand">{t('inv.mov.oneProduct')}</span>}
        {filtered && (
          <Button variant="secondary" icon={<RotateCcw size={16} />} onClick={() => { setDocument(''); setParams({}, { replace: true }) }}>{t('product.filter.reset')}</Button>
        )}
      </div>
      <div className={`overflow-x-auto rounded-2xl border border-line bg-surface transition-opacity ${isFetching && !isLoading ? 'opacity-70' : ''}`}>
        <table className="w-full min-w-[1000px] border-collapse">
          <thead className="bg-field">
            <tr>
              <th className={th}>{t('inv.mov.date')}</th>
              <th className={th}>{t('inv.mov.type')}</th>
              <th className={th}>{t('inv.mov.document')}</th>
              <th className={th}>{t('inv.lines.product')}</th>
              <th className={th}>{t('inv.mov.warehouse')}</th>
              <th className={`${th} text-right`}>{t('inv.mov.qty')}</th>
              <th className={`${th} text-right`}>{t('inv.mov.after')}</th>
              <th className={th}>{t('inv.mov.user')}</th>
              <th className={th}>{t('inv.mov.comment')}</th>
            </tr>
          </thead>
          <tbody>
            {isLoading && <EmptyRow colSpan={9} text={t('common.loading')} />}
            {!isLoading && items.length === 0 && <EmptyRow colSpan={9} text={t('catalog.nothingFound')} />}
            {items.map((m) => (
              <tr key={m.id} className="border-t border-line">
                <td className={`${td} whitespace-nowrap text-[13px] text-ink-2`}>{dateTime(m.createdAt)}</td>
                <td className={td}><span className={`rounded-md px-2 py-0.5 text-[12px] font-semibold ${TYPE_TONE[m.type]}`}>{t(`inv.mov.type.${m.type}` as TranslationKey)}</span></td>
                <td className={`${td} text-[13px]`}>
                  {m.documentId && (m.type === 'Receipt' || m.type === 'WriteOff' || m.type === 'Reversal' && m.documentNumber?.startsWith('П')) ? (
                    <button type="button" className="font-medium text-brand hover:underline" onClick={() => navigate(m.documentNumber?.startsWith('ПМ') ? `/warehouse/transfers/${m.documentId}` : m.documentNumber?.startsWith('СП') ? `/warehouse/write-offs/${m.documentId}` : `/warehouse/receipts/${m.documentId}`)}>{m.documentNumber}</button>
                  ) : m.documentId && (m.type === 'TransferIn' || m.type === 'TransferOut') ? (
                    <button type="button" className="font-medium text-brand hover:underline" onClick={() => navigate(`/warehouse/transfers/${m.documentId}`)}>{m.documentNumber}</button>
                  ) : (m.documentNumber ?? '—')}
                </td>
                <td className={td}>
                  <button type="button" className="text-left font-semibold hover:text-brand" onClick={() => update({ productId: m.productId })}>{m.productName}</button>
                  {m.article && <span className="block text-[12px] text-ink-3">{m.article}</span>}
                </td>
                <td className={td}>{m.warehouseName}</td>
                <td className={`${td} text-right font-semibold ${m.quantity >= 0 ? 'text-success' : 'text-danger'}`}>{m.quantity > 0 ? '+' : ''}{num(m.quantity)}</td>
                <td className={`${td} text-right font-semibold`}>{num(m.balanceAfter)}</td>
                <td className={`${td} text-[13px] text-ink-2`}>{m.staffName ?? '—'}</td>
                <td className={`${td} max-w-64 truncate text-[13px] text-ink-2`} title={comment(m) ?? undefined}>{comment(m) ?? '—'}</td>
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

const num = (n: number) => new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 3 }).format(n)

const WO_REASONS = new Set(['Damage', 'Expired', 'Loss', 'InternalUse', 'Adjustment', 'Other'])

/** Списание қозғалысында себеп коды сақталады («Damage: ...») — экранда аудармасы көрсетіледі. */
function useComment() {
  const t = useT()
  return (m: { type: MovementType; comment: string | null }) => {
    if (!m.comment || m.type !== 'WriteOff') return m.comment
    const [code, ...rest] = m.comment.split(': ')
    if (!WO_REASONS.has(code)) return m.comment
    const label = t(`inv.wo.reasons.${code}` as TranslationKey)
    return rest.length ? `${label}: ${rest.join(': ')}` : label
  }
}
