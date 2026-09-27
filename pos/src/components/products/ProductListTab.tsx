import { useEffect, useMemo, useState } from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useLocation, useNavigate, useSearchParams } from 'react-router-dom'
import { Archive, ArchiveRestore, ChevronLeft, ChevronRight, FolderTree, ImageOff, Pencil, Plus, RotateCcw } from 'lucide-react'
import { Button } from '../ui/Button'
import { Select } from '../ui/Field'
import { Modal } from '../ui/Modal'
import { RowMenu, type RowMenuItem } from '../ui/RowMenu'
import { SearchInput } from '../ui/SearchInput'
import { StatusBadge } from '../ui/StatusBadge'
import { ClassificationPicker } from './ClassificationPicker'
import { catalogKeys, treeOrder, useBrands, useCatalogAction, useCatalogNodes, useCatalogPermissions, useUnits } from '../../lib/catalogHooks'
import type { CatalogStatus, ProductListItem } from '../../lib/catalogTypes'
import { productApi } from '../../lib/productApi'
import { useT, type TranslationKey } from '../../lib/i18n'

const PAGE_SIZES = [10, 20, 50, 100]
const th = 'px-3 py-3 text-left text-[13px] font-semibold text-ink-2'
const td = 'px-3 py-2.5 text-[14px]'

/** Іздеуді әр әріпте емес, теру тоқтағанда жібереміз. */
function useDebounced<T>(value: T, ms: number) {
  const [debounced, setDebounced] = useState(value)
  useEffect(() => {
    const id = setTimeout(() => setDebounced(value), ms)
    return () => clearTimeout(id)
  }, [value, ms])
  return debounced
}

/**
 * «Список товаров» (ТЗ «Товар» §5): каталог кестесі. Қалдық пен баға мұнда жоқ — олар «Склад»-та.
 * Сүзгілер URL-де сақталады: карточкадан қайтқанда тізім сол күйінде ашылады.
 */
export function ProductListTab() {
  const t = useT()
  const navigate = useNavigate()
  const location = useLocation()
  const perms = useCatalogPermissions()
  const [params, setParams] = useSearchParams()
  const nodes = useCatalogNodes().data ?? []
  const brands = useBrands().data ?? []
  const units = useUnits().data ?? []
  const run = useCatalogAction(catalogKeys.products)
  const [moving, setMoving] = useState<ProductListItem | null>(null)

  const [search, setSearch] = useState(params.get('search') ?? '')
  const debounced = useDebounced(search.trim(), 300)
  const statusParam = params.get('status') ?? 'Active'
  const status = (statusParam === 'all' ? '' : statusParam) as CatalogStatus | ''
  const nodeId = params.get('nodeId') ?? ''
  const brandId = params.get('brandId') ?? ''
  const unitId = params.get('unitId') ?? ''
  const page = Math.max(1, Number(params.get('page')) || 1)
  const pageSize = PAGE_SIZES.includes(Number(params.get('pageSize'))) ? Number(params.get('pageSize')) : 20

  const update = (patch: Record<string, string>, resetPage = true) =>
    setParams((prev) => {
      const next = new URLSearchParams(prev)
      for (const [k, v] of Object.entries(patch)) {
        if (v) next.set(k, v)
        else next.delete(k)
      }
      if (resetPage) next.delete('page')
      return next
    }, { replace: true })

  useEffect(() => {
    if ((params.get('search') ?? '') !== debounced) update({ search: debounced })
  }, [debounced])

  const query = { search: debounced, status, nodeId, brandId, unitId, page, pageSize }
  const { data, isLoading, isFetching } = useQuery({
    queryKey: [...catalogKeys.products, query],
    queryFn: () => productApi.list(query),
    placeholderData: keepPreviousData,
  })
  const items = data?.items ?? []
  const total = data?.total ?? 0
  const pages = Math.max(1, Math.ceil(total / pageSize))
  const filtered = !!(debounced || nodeId || brandId || unitId || statusParam !== 'Active')

  const nodeOptions = useMemo(() => treeOrder(nodes), [nodes])
  const open = (id: string) => navigate(`/products/${id}`, { state: { from: location.search } })

  const menu = (p: ProductListItem): RowMenuItem[] => {
    const list: RowMenuItem[] = []
    if (perms.edit) list.push({ label: t('product.changeClass'), icon: <FolderTree size={16} />, onClick: () => setMoving(p) })
    if (perms.lifecycle && p.status === 'Active')
      list.push({
        label: t('catalog.archive'), icon: <Archive size={16} />,
        onClick: () => window.confirm(t('product.archiveConfirm', { name: p.name })) && void run(() => productApi.archive(p.id), t('catalog.archived')),
      })
    if (perms.lifecycle && p.status === 'Archived')
      list.push({ label: t('catalog.restore'), icon: <ArchiveRestore size={16} />, onClick: () => void run(() => productApi.restore(p.id), t('catalog.restored')) })
    return list
  }

  return (
    <div>
      <div className="mb-3 flex flex-wrap items-center gap-2">
        {perms.create && (
          <Button icon={<Plus size={18} />} onClick={() => navigate('/products/new', { state: { from: location.search } })}>
            {t('product.add')}
          </Button>
        )}
        <SearchInput className="ml-auto w-full sm:w-80" value={search} onChange={setSearch} placeholder={t('product.search')} />
      </div>

      <div className="mb-4 grid grid-cols-2 gap-2 md:grid-cols-4 xl:flex xl:items-center">
        <Select className="xl:w-44" value={statusParam} onChange={(e) => update({ status: e.target.value === 'Active' ? '' : e.target.value })}>
          <option value="Active">{t('product.filter.active')}</option>
          <option value="Archived">{t('product.filter.archived')}</option>
          <option value="all">{t('product.filter.allStatuses')}</option>
        </Select>
        <Select className="xl:w-64" value={nodeId} onChange={(e) => update({ nodeId: e.target.value })}>
          <option value="">{t('product.filter.allCategories')}</option>
          {nodeOptions.map((n) => (
            <option key={n.id} value={n.id}>{' '.repeat(n.depth)}{n.name}</option>
          ))}
        </Select>
        <Select className="xl:w-44" value={brandId} onChange={(e) => update({ brandId: e.target.value })}>
          <option value="">{t('product.filter.allBrands')}</option>
          {brands.map((b) => <option key={b.id} value={b.id}>{b.name}</option>)}
        </Select>
        <Select className="xl:w-44" value={unitId} onChange={(e) => update({ unitId: e.target.value })}>
          <option value="">{t('product.filter.allUnits')}</option>
          {units.map((u) => <option key={u.id} value={u.id}>{u.name}</option>)}
        </Select>
        {filtered && (
          <Button variant="secondary" icon={<RotateCcw size={16} />} onClick={() => { setSearch(''); setParams({}, { replace: true }) }}>
            {t('product.filter.reset')}
          </Button>
        )}
      </div>

      <div className={`overflow-x-auto rounded-2xl border border-line bg-surface transition-opacity ${isFetching && !isLoading ? 'opacity-70' : ''}`}>
        <table className="w-full min-w-[1080px] border-collapse">
          <thead className="bg-field">
            <tr>
              <th className={`${th} w-16`}>{t('product.col.photo')}</th>
              <th className={th}>{t('product.col.name')}</th>
              <th className={th}>{t('product.col.article')}</th>
              <th className={th}>{t('product.col.barcode')}</th>
              <th className={th}>{t('product.col.classification')}</th>
              <th className={th}>{t('product.col.brand')}</th>
              <th className={th}>{t('product.col.unit')}</th>
              <th className={th}>{t('catalog.col.status')}</th>
              <th className={`${th} text-right`}>{t('catalog.col.actions')}</th>
            </tr>
          </thead>
          <tbody>
            {isLoading && <tr><td colSpan={9} className="px-3 py-10 text-center text-[14px] text-ink-3">{t('common.loading')}</td></tr>}
            {!isLoading && items.length === 0 && (
              <tr><td colSpan={9} className="px-3 py-12 text-center text-[14px] text-ink-3">
                {filtered ? t('catalog.nothingFound') : t('product.empty')}
              </td></tr>
            )}
            {items.map((p) => {
              const archived = p.status === 'Archived'
              return (
                <tr key={p.id} onClick={() => open(p.id)}
                  className={`cursor-pointer border-t border-line hover:bg-field/40 ${archived ? 'text-ink-3' : ''}`}>
                  <td className={td}>
                    {p.imageUrl
                      ? <img src={p.imageUrl} alt="" loading="lazy" className="size-11 rounded-lg border border-line object-cover" />
                      : <span className="flex size-11 items-center justify-center rounded-lg bg-field text-ink-3"><ImageOff size={18} /></span>}
                  </td>
                  <td className={`${td} max-w-72 font-semibold`}>{p.name}</td>
                  <td className={`${td} text-ink-2`}>{p.article ?? '—'}</td>
                  <td className={`${td} font-mono text-[13px] text-ink-2`}>{p.barcode ?? '—'}</td>
                  <td className={`${td} max-w-64 text-[13px] text-ink-2`}>{p.pathNames.length ? p.pathNames.join(' → ') : '—'}</td>
                  <td className={td}>{p.brandName ?? '—'}</td>
                  <td className={td}>{p.unitShortName ?? '—'}</td>
                  <td className={td}>
                    <StatusBadge active={!archived} label={t(`status.${p.status}.m` as TranslationKey)} />
                  </td>
                  <td className={`${td} whitespace-nowrap text-right`} onClick={(e) => e.stopPropagation()}>
                    <div className="inline-flex items-center gap-1.5">
                      {perms.edit && (
                        <Button variant="secondary" className="h-9! px-3! text-[13px]! text-brand!" icon={<Pencil size={15} />} onClick={() => open(p.id)}>
                          {t('catalog.edit')}
                        </Button>
                      )}
                      <RowMenu items={menu(p)} />
                    </div>
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>

      {total > 0 && (
        <div className="mt-4 flex flex-wrap items-center gap-3 text-[14px] text-ink-2">
          <span>{t('product.shown', { from: (page - 1) * pageSize + 1, to: Math.min(page * pageSize, total), total })}</span>
          <div className="ml-auto flex items-center gap-2">
            <Select className="h-9! w-24" value={pageSize} onChange={(e) => update({ pageSize: e.target.value })}>
              {PAGE_SIZES.map((s) => <option key={s} value={s}>{s}</option>)}
            </Select>
            <button type="button" disabled={page <= 1} onClick={() => update({ page: String(page - 1) }, false)}
              className="flex size-9 items-center justify-center rounded-lg border border-line bg-surface disabled:opacity-40" aria-label="Previous">
              <ChevronLeft size={17} />
            </button>
            <span className="min-w-16 text-center">{page} / {pages}</span>
            <button type="button" disabled={page >= pages} onClick={() => update({ page: String(page + 1) }, false)}
              className="flex size-9 items-center justify-center rounded-lg border border-line bg-surface disabled:opacity-40" aria-label="Next">
              <ChevronRight size={17} />
            </button>
          </div>
        </div>
      )}

      {moving && <ChangeClassificationModal product={moving} onClose={() => setMoving(null)}
        onSaved={() => void run(async () => undefined, t('product.classChanged')).then(() => setMoving(null))} />}
    </div>
  )
}

/** Тауардың классификациясын өзгерту (ТЗ §14): сақтағанға дейін жаңа жол көрініп тұрады. */
function ChangeClassificationModal({ product, onClose, onSaved }: { product: ProductListItem; onClose: () => void; onSaved: () => void }) {
  const t = useT()
  const nodes = useCatalogNodes().data ?? []
  const [nodeId, setNodeId] = useState<string | null>(product.nodeId)
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)

  const save = async () => {
    setLoading(true)
    setError('')
    try {
      await productApi.changeClassification(product.id, nodeId)
      onSaved()
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err))
    } finally {
      setLoading(false)
    }
  }

  return (
    <Modal title={t('product.changeClass')} onClose={onClose} width={820}>
      <p className="mb-3 text-[14px] text-ink-2">
        {product.name} · {t('product.currentPath')}: <b>{product.pathNames.join(' → ') || t('product.noClassification')}</b>
      </p>
      <ClassificationPicker nodes={nodes} value={nodeId} onChange={setNodeId} />
      {error && <p className="mt-3 text-[14px] text-danger">{error}</p>}
      <div className="mt-5 flex justify-end gap-2">
        <Button variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
        <Button loading={loading} disabled={nodeId === product.nodeId} onClick={() => void save()}>{t('common.save')}</Button>
      </div>
    </Modal>
  )
}
