import { useState } from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { Link, useSearchParams } from 'react-router-dom'
import { Download } from 'lucide-react'
import { Button } from '../ui/Button'
import { Select } from '../ui/Field'
import { SearchInput } from '../ui/SearchInput'
import { toast } from '../ui/Toast'
import { EmptyRow, Pager, td, th, useDebounced } from '../inventory/inventoryUi'
import { useBrands, useCatalogNodes } from '../../lib/catalogHooks'
import { financeApi } from '../../lib/financeApi'
import { num, tenge } from '../../lib/money'
import { useT } from '../../lib/i18n'
import { BlockError } from '../stats/statsUi'
import { Money, reportError, SortTh, useFinancePermissions, Warn, type FinanceFilters } from './financeUi'

/**
 * «Прибыль по товарам» (ТЗ «Финансы» §10): тауар, сатылды/қайтарылды/таза саны, таза түсім, тарихи себестоимость, пайда, маржа %, орташа баға.
 * Себестоимость жоқ болса — «Не рассчитана» және ескерту; үнсіз нөл емес.
 */
export function ProductProfitTab({ filters, storeIds }: { filters: FinanceFilters; storeIds?: string }) {
  const t = useT()
  const [params, setParams] = useSearchParams()
  const { canExport } = useFinancePermissions()
  const [exporting, setExporting] = useState(false)
  const g = (k: string) => params.get(k) ?? ''
  const search = useDebounced(g('q'), 300)
  const nodes = useCatalogNodes()
  const brands = useBrands()
  const p = { storeIds, from: filters.from, to: filters.to, search: search || undefined, nodeId: g('node') || undefined, brandId: g('brand') || undefined, sort: g('sort') || 'revenue', dir: g('dir') || 'desc', page: Number(g('page')) || 1, pageSize: Number(g('size')) || 15 }
  const q = useQuery({ queryKey: ['finance', 'product-profit', p], queryFn: () => financeApi.productProfit(p), placeholderData: keepPreviousData })
  const set = (patch: Record<string, string | null>, keepPage = false) => setParams((prev) => { const n = new URLSearchParams(prev); for (const [k, v] of Object.entries(patch)) { if (v) n.set(k, v); else n.delete(k) } if (!keepPage) n.delete('page'); return n }, { replace: true })
  const onSort = (k: string) => set({ sort: k, dir: p.sort === k && p.dir === 'desc' ? 'asc' : 'desc' })
  const d = q.data
  const exportFile = async (format: 'xlsx' | 'csv') => {
    setExporting(true)
    try { await financeApi.exportFile('product-profit', format, p); toast(t('stats.exportReady')) } catch (err) { reportError(err, t('stats.exportFailed')) } finally { setExporting(false) }
  }
  if (q.isError) return <BlockError message={(q.error as Error).message} onRetry={() => void q.refetch()} />
  const search2 = `?from=${filters.from}&to=${filters.to}`
  return (
    <div className="flex flex-col gap-3">
      {d && (
        <div className="grid grid-cols-2 gap-3 xl:grid-cols-4">
          {[[t('fin.pp.netRevenue'), <Money key="r" v={d.netRevenue} />], [t('fin.pp.cost'), <Money key="c" v={d.cost} />], [t('fin.pp.profit'), <Money key="p" v={d.profit} className={d.profit !== null && d.profit < 0 ? 'text-danger' : 'text-success'} />], [t('fin.pp.margin'), d.margin === null ? '—' : `${d.margin}%`]].map(([l, v], i) => (
            <div key={i} className="rounded-2xl border border-line bg-surface p-3"><div className="text-[12px] font-medium text-ink-2">{l}</div><div className="mt-0.5 text-[20px] font-extrabold tracking-tight">{v}</div></div>
          ))}
        </div>
      )}
      {d && !d.costComplete && <Warn text={t('fin.pp.costIncomplete', { n: d.withoutCost })} />}
      <div className="flex flex-wrap items-center gap-2">
        <SearchInput value={g('q')} onChange={(v) => set({ q: v })} placeholder={t('fin.pp.search')} className="h-10! w-64" />
        <Select className="h-10! w-auto! min-w-40 text-[13px]" value={g('node')} onChange={(e) => set({ node: e.target.value })} aria-label={t('fin.col.category')}>
          <option value="">{t('fin.filter.allCategories')}</option>
          {(nodes.data ?? []).map((n) => <option key={n.id} value={n.id}>{n.name}</option>)}
        </Select>
        <Select className="h-10! w-auto! min-w-36 text-[13px]" value={g('brand')} onChange={(e) => set({ brand: e.target.value })} aria-label={t('fin.pp.brand')}>
          <option value="">{t('fin.pp.allBrands')}</option>
          {(brands.data ?? []).map((b) => <option key={b.id} value={b.id}>{b.name}</option>)}
        </Select>
        <span className="ml-auto" />
        {canExport && <span className="flex gap-1"><Button variant="secondary" className="h-10! px-3!" icon={<Download size={16} />} disabled={exporting} onClick={() => void exportFile('xlsx')}>Excel</Button><Button variant="secondary" className="h-10! px-3!" disabled={exporting} onClick={() => void exportFile('csv')}>CSV</Button></span>}
      </div>
      <div className="overflow-x-auto rounded-2xl border border-line bg-surface">
        <table className="w-full min-w-[900px]">
          <thead className="bg-field/60">
            <tr>
              <th className={th}>{t('fin.pp.product')}</th>
              <SortTh k="qty" label={t('fin.pp.sold')} sort={p.sort} dir={p.dir} onSort={onSort} right />
              <th className={`${th} text-right`}>{t('fin.pp.returned')}</th>
              <SortTh k="revenue" label={t('fin.pp.netRevenue')} sort={p.sort} dir={p.dir} onSort={onSort} right />
              <SortTh k="cost" label={t('fin.pp.cost')} sort={p.sort} dir={p.dir} onSort={onSort} right />
              <SortTh k="profit" label={t('fin.pp.profit')} sort={p.sort} dir={p.dir} onSort={onSort} right />
              <SortTh k="margin" label={t('fin.pp.margin')} sort={p.sort} dir={p.dir} onSort={onSort} right />
              <th className={`${th} text-right`}>{t('fin.pp.averagePrice')}</th>
            </tr>
          </thead>
          <tbody>
            {!d ? null : d.rows.length === 0 ? <EmptyRow colSpan={8} text={t('fin.emptyPeriod')} /> : d.rows.map((r) => (
              <tr key={r.productId} className="border-t border-line hover:bg-field/40">
                <td className={td}>
                  <Link to={`/products/${r.productId}`} className="font-medium hover:text-brand">{r.name}</Link>
                  <div className="text-[12px] text-ink-3">{[r.article, r.category, r.brand].filter(Boolean).join(' · ') || '—'}</div>
                </td>
                <td className={`${td} text-right tabular-nums`}>{num(r.sold)}{r.unit ? ` ${r.unit}` : ''}</td>
                <td className={`${td} text-right tabular-nums text-ink-2`}>{r.returned ? num(r.returned) : '—'}</td>
                <td className={`${td} text-right tabular-nums`}>{tenge(r.netRevenue)}</td>
                <td className={`${td} text-right`}><Money v={r.cost} className="text-ink-2" /></td>
                <td className={`${td} text-right font-semibold`}><Money v={r.profit} className={r.profit !== null && r.profit < 0 ? 'text-danger' : 'text-success'} /></td>
                <td className={`${td} text-right tabular-nums`}>{r.margin === null ? '—' : `${r.margin}%`}</td>
                <td className={`${td} text-right tabular-nums text-ink-2`}>{r.averagePrice === null ? '—' : tenge(r.averagePrice)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {d && <Pager page={d.page} pageSize={d.pageSize} total={d.total} onPage={(pg) => set({ page: String(pg) }, true)} onPageSize={(s) => set({ size: String(s) })} />}
      <p className="text-[12px] text-ink-3">{t('fin.pp.note')} <Link to={`/sales${search2}`} className="text-brand hover:underline">{t('fin.pp.toSales')}</Link></p>
    </div>
  )
}
