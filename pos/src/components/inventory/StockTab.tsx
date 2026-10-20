import { useEffect, useState } from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { History, Package, Pencil, Plus, RotateCcw } from 'lucide-react'
import { Button } from '../ui/Button'
import { Select } from '../ui/Field'
import { RowMenu } from '../ui/RowMenu'
import { SearchInput } from '../ui/SearchInput'
import { treeOrder, useBrands, useCatalogNodes, useCatalogPermissions } from '../../lib/catalogHooks'
import { inventoryApi, inventoryKeys } from '../../lib/inventoryApi'
import { dateTime } from '../../lib/dates'
import { useT, type TranslationKey } from '../../lib/i18n'
import { EmptyRow, Pager, ProductThumb, Qty, td, th, useDebounced, useInventoryPermissions } from './inventoryUi'

/**
 * «Остатки товаров» (ТЗ «Склад» §5): қалдық әр қойма бойынша, «Все склады» — жиынтық пен бөлініс.
 * Ақшалай бағандар жоқ (закупочная/розничная/құн) — олар «Финансы»-да.
 */
export function StockTab() {
  const t = useT()
  const navigate = useNavigate()
  const perms = useInventoryPermissions()
  const catalogPerms = useCatalogPermissions()
  const [params, setParams] = useSearchParams()
  const nodes = useCatalogNodes().data ?? []
  const brands = useBrands().data ?? []
  const warehouses = useQuery({ queryKey: inventoryKeys.warehouses, queryFn: () => inventoryApi.warehouses() }).data ?? []

  const [search, setSearch] = useState(params.get('search') ?? '')
  const debounced = useDebounced(search.trim(), 300)
  const warehouseId = params.get('warehouseId') ?? ''
  const nodeId = params.get('nodeId') ?? ''
  const brandId = params.get('brandId') ?? ''
  const availability = params.get('availability') ?? ''
  const status = params.get('status') ?? ''
  const sort = params.get('sort') ?? ''
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

  const query = { warehouseId, search: debounced, nodeId, brandId, availability, status, sort, page, pageSize }
  const { data, isLoading, isFetching } = useQuery({
    queryKey: [...inventoryKeys.stock, query], queryFn: () => inventoryApi.stock(query), placeholderData: keepPreviousData,
  })
  const items = data?.items ?? []
  const filtered = !!(debounced || warehouseId || nodeId || brandId || availability || status)
  const activeWarehouses = warehouses.filter((w) => w.isActive)

  return (
    <div>
      <div className="mb-3 flex flex-wrap items-center gap-2">
        <SearchInput className="w-full sm:w-80" value={search} onChange={setSearch} placeholder={t('inv.stock.search')} />
        <Select className="w-52!" value={warehouseId} onChange={(e) => update({ warehouseId: e.target.value })}>
          <option value="">{t('inv.stock.allWarehouses')}</option>
          {activeWarehouses.map((w) => <option key={w.id} value={w.id}>{w.name}</option>)}
        </Select>
        <Select className="w-52!" value={nodeId} onChange={(e) => update({ nodeId: e.target.value })}>
          <option value="">{t('product.filter.allCategories')}</option>
          {treeOrder(nodes).map((n) => <option key={n.id} value={n.id}>{' '.repeat(n.depth)}{n.name}</option>)}
        </Select>
        <Select className="w-44!" value={brandId} onChange={(e) => update({ brandId: e.target.value })}>
          <option value="">{t('product.filter.allBrands')}</option>
          {brands.map((b) => <option key={b.id} value={b.id}>{b.name}</option>)}
        </Select>
        <Select className="w-48!" value={availability} onChange={(e) => update({ availability: e.target.value })}>
          <option value="">{t('inv.stock.anyAvailability')}</option>
          <option value="in">{t('inv.stock.inStock')}</option>
          <option value="out">{t('inv.stock.outOfStock')}</option>
          <option value="belowMin">{t('inv.stock.belowMin')}</option>
        </Select>
        <Select className="w-40!" value={status} onChange={(e) => update({ status: e.target.value })}>
          <option value="">{t('product.filter.active')}</option>
          <option value="Archived">{t('product.filter.archived')}</option>
          <option value="all">{t('product.filter.allStatuses')}</option>
        </Select>
        {filtered && (
          <Button variant="secondary" icon={<RotateCcw size={16} />} onClick={() => { setSearch(''); setParams({}, { replace: true }) }}>
            {t('product.filter.reset')}
          </Button>
        )}
        {catalogPerms.create && (
          <Button className="ml-auto" icon={<Plus size={18} />} onClick={() => navigate('/products/new')}>{t('product.add')}</Button>
        )}
      </div>

      <div className={`overflow-x-auto rounded-2xl border border-line bg-surface transition-opacity ${isFetching && !isLoading ? 'opacity-70' : ''}`}>
        <table className="w-full min-w-[1000px] border-collapse">
          <thead className="bg-field">
            <tr>
              <th className={th}>{t('inv.lines.product')}</th>
              <th className={th}>{t('product.col.article')}</th>
              <th className={th}>{t('product.col.classification')}</th>
              <th className={th}>{t('product.col.brand')}</th>
              <th className={th}>{t('product.col.unit')}</th>
              <th className={`${th} cursor-pointer text-right`} onClick={() => update({ sort: sort === 'stockDesc' ? 'stockAsc' : 'stockDesc' })}>
                {t('inv.stock.total')} {sort === 'stockDesc' ? '↓' : sort === 'stockAsc' ? '↑' : ''}
              </th>
              <th className={th}>{t('inv.stock.byWarehouse')}</th>
              <th className={th}>{t('inv.stock.lastMove')}</th>
              <th className={`${th} text-right`}>{t('catalog.col.actions')}</th>
            </tr>
          </thead>
          <tbody>
            {isLoading && <EmptyRow colSpan={9} text={t('common.loading')} />}
            {!isLoading && items.length === 0 && <EmptyRow colSpan={9} text={filtered ? t('catalog.nothingFound') : t('product.empty')} />}
            {items.map((r) => (
              <tr key={r.productId} className="border-t border-line hover:bg-field/40">
                <td className={td}>
                  <button type="button" onClick={() => navigate(`/products/${r.productId}`)} className="flex items-center gap-2.5 text-left">
                    <ProductThumb url={r.imageUrl} />
                    <span className="min-w-0">
                      <span className="block max-w-72 truncate font-semibold hover:text-brand">{r.name}</span>
                      {r.minStock != null && <span className="block text-[12px] text-ink-3">{t('inv.stock.min', { n: r.minStock })}</span>}
                    </span>
                  </button>
                </td>
                <td className={`${td} text-ink-2`}>{r.article ?? '—'}</td>
                <td className={`${td} max-w-56 text-[13px] text-ink-2`}>{r.pathNames.join(' → ') || '—'}</td>
                <td className={td}>{r.brandName ?? '—'}</td>
                <td className={td}>{r.unitShortName ?? '—'}</td>
                <td className={`${td} text-right text-[15px]`}><Qty value={r.total} /></td>
                <td className={td}>
                  <div className="flex flex-wrap gap-1">
                    {r.byWarehouse.map((w) => (
                      <button key={w.warehouseId} type="button" onClick={() => update({ warehouseId: w.warehouseId })}
                        className={`rounded-md px-2 py-0.5 text-[12px] font-medium ${w.quantity > 0 ? 'bg-brand-soft text-brand' : 'bg-field text-ink-3'}`}>
                        {w.warehouseName} ({num(w.quantity)})
                      </button>
                    ))}
                  </div>
                </td>
                <td className={`${td} whitespace-nowrap text-[13px] text-ink-2`}>{r.lastMovementAt ? dateTime(r.lastMovementAt) : '—'}</td>
                <td className={`${td} text-right`}>
                  <RowMenu items={[
                    { label: t('inv.stock.openProduct'), icon: <Package size={16} />, onClick: () => navigate(`/products/${r.productId}`) },
                    { label: t('inv.tab.movements'), icon: <History size={16} />, onClick: () => navigate(`/warehouse/movements?productId=${r.productId}`) },
                    ...(perms.receive ? [{ label: t('inv.receipts.quick'), icon: <Plus size={16} />, onClick: () => navigate(`/warehouse/receipts/quick?productId=${r.productId}`) }] : []),
                    ...(perms.writeOff ? [{ label: t('inv.tab.writeOffs'), icon: <Pencil size={16} />, onClick: () => navigate(`/warehouse/write-offs/new?productId=${r.productId}`) }] : []),
                  ]} />
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

const num = (n: number) => new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 3 }).format(n)
export type StockStatusKey = TranslationKey
