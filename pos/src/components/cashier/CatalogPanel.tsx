import { useMemo } from 'react'
import { keepPreviousData, useInfiniteQuery } from '@tanstack/react-query'
import { ImageOff, LayoutGrid, List, Plus, X } from 'lucide-react'
import { cashierApi } from '../../lib/cashierApi'
import type { CashierProduct, CashierSort } from '../../lib/cashierTypes'
import { CatalogIcon } from '../../lib/catalogIcons'
import { useCatalogNodes } from '../../lib/catalogHooks'
import { num, tenge } from '../../lib/money'
import { useT, type TranslationKey } from '../../lib/i18n'

const PAGE = 40
const SORTS: { key: CashierSort; label: TranslationKey }[] = [
  { key: 'popular', label: 'pos.sort.popular' },
  { key: 'priceAsc', label: 'pos.sort.priceAsc' },
  { key: 'priceDesc', label: 'pos.sort.priceDesc' },
  { key: 'stock', label: 'pos.sort.stock' },
]

export interface CatalogFilter {
  nodeId: string | null
  sort: CashierSort
  view: 'grid' | 'list'
  search: string
}

/** Сол жақта санаттар, ортада тауарлар (ТЗ «Касса» §2.6–2.8, §3.1–3.2). */
export function CatalogPanel({ filter, onFilter, onAdd }: {
  filter: CatalogFilter
  onFilter: (patch: Partial<CatalogFilter>) => void
  onAdd: (p: CashierProduct) => void
}) {
  const t = useT()
  const nodes = useCatalogNodes().data
  const roots = useMemo(() => (nodes ?? []).filter((n) => n.depth === 0 && n.status === 'Active')
    .sort((a, b) => a.sortOrder - b.sortOrder || a.name.localeCompare(b.name)), [nodes])

  const query = useInfiniteQuery({
    queryKey: ['cashier', 'catalog', filter.nodeId, filter.sort, filter.search],
    queryFn: ({ pageParam }) => cashierApi.catalog({ nodeId: filter.nodeId, sort: filter.sort, search: filter.search, page: pageParam, pageSize: PAGE }),
    initialPageParam: 1,
    getNextPageParam: (last) => (last.page * last.pageSize < last.total ? last.page + 1 : undefined),
    placeholderData: keepPreviousData,
  })
  const items = query.data?.pages.flatMap((p) => p.items) ?? []
  const total = query.data?.pages[0]?.total ?? 0
  const title = filter.search ? t('pos.searchResults', { q: filter.search })
    : filter.nodeId ? roots.find((r) => r.id === filter.nodeId)?.name ?? t('pos.allProducts') : t('pos.allProducts')

  const category = (id: string | null, label: string, icon?: string | null) => {
    const active = filter.nodeId === id && !filter.search
    return (
      <button key={id ?? 'all'} type="button" onClick={() => onFilter({ nodeId: id, search: '' })}
        className={`flex w-full items-center gap-2.5 rounded-xl px-3 py-2.5 text-left text-[14px] font-medium transition-colors ${
          active ? 'bg-brand text-white' : 'text-ink hover:bg-field'}`}>
        {id === null ? <LayoutGrid size={18} /> : <CatalogIcon name={icon} size={18} />}
        <span className="truncate">{label}</span>
      </button>
    )
  }

  return (
    <div className="flex min-h-[520px] min-w-0 flex-1 gap-3 lg:min-h-0">
      <aside className="hidden w-52 shrink-0 overflow-y-auto rounded-2xl bg-surface p-2 xl:block">
        {category(null, t('pos.allProducts'))}
        {roots.map((r) => category(r.id, r.name, r.icon))}
      </aside>

      <section className="flex min-h-0 min-w-0 flex-1 flex-col rounded-2xl bg-surface">
        <div className="flex flex-wrap items-center gap-2 border-b border-line px-4 py-3">
          <h2 className="mr-2 truncate text-[18px] font-bold">{title}</h2>
          {filter.search && (
            <button type="button" onClick={() => onFilter({ search: '' })}
              className="flex items-center gap-1 rounded-lg bg-field px-2 py-1 text-[12px] text-ink-2 hover:text-ink">
              <X size={13} /> {t('pos.clearSearch')}
            </button>
          )}
          <span className="text-[13px] text-ink-3">{t('pos.found', { n: total })}</span>
          <div className="ml-auto flex flex-wrap items-center gap-1.5">
            {SORTS.map((s) => (
              <button key={s.key} type="button" onClick={() => onFilter({ sort: s.key })}
                className={`rounded-lg px-3 py-1.5 text-[13px] font-medium ${filter.sort === s.key ? 'bg-brand text-white' : 'bg-field text-ink-2 hover:text-ink'}`}>
                {t(s.label)}
              </button>
            ))}
            <div className="ml-1 flex rounded-lg bg-field p-0.5">
              {(['grid', 'list'] as const).map((v) => (
                <button key={v} type="button" aria-label={v} onClick={() => onFilter({ view: v })}
                  className={`flex size-8 items-center justify-center rounded-md ${filter.view === v ? 'bg-surface text-brand shadow-sm' : 'text-ink-3'}`}>
                  {v === 'grid' ? <LayoutGrid size={16} /> : <List size={17} />}
                </button>
              ))}
            </div>
          </div>
        </div>

        {/* Санаттар тар экранда — жолақ түрінде. */}
        <div className="flex gap-1.5 overflow-x-auto border-b border-line px-4 py-2 xl:hidden">
          {[{ id: null as string | null, name: t('pos.allProducts') }, ...roots].map((r) => (
            <button key={r.id ?? 'all'} type="button" onClick={() => onFilter({ nodeId: r.id, search: '' })}
              className={`shrink-0 rounded-lg px-3 py-1.5 text-[13px] font-medium ${filter.nodeId === r.id && !filter.search ? 'bg-brand text-white' : 'bg-field text-ink-2'}`}>
              {r.name}
            </button>
          ))}
        </div>

        <div className="min-h-0 flex-1 overflow-y-auto p-3">
          {query.isLoading && <p className="py-16 text-center text-[14px] text-ink-3">{t('common.loading')}</p>}
          {!query.isLoading && items.length === 0 && <p className="py-16 text-center text-[14px] text-ink-3">{t('catalog.nothingFound')}</p>}
          {filter.view === 'grid' ? (
            <div className="grid grid-cols-[repeat(auto-fill,minmax(170px,1fr))] gap-3">
              {items.map((p) => <ProductCard key={p.id} p={p} onAdd={onAdd} />)}
            </div>
          ) : (
            <div className="divide-y divide-line rounded-xl border border-line">
              {items.map((p) => <ProductRow key={p.id} p={p} onAdd={onAdd} />)}
            </div>
          )}
          {query.hasNextPage && (
            <button type="button" onClick={() => void query.fetchNextPage()} disabled={query.isFetchingNextPage}
              className="mx-auto mt-4 block rounded-xl bg-field px-5 py-2.5 text-[14px] font-medium text-brand hover:bg-brand-soft">
              {query.isFetchingNextPage ? t('common.loading') : t('pos.showMore')}
            </button>
          )}
        </div>
      </section>
    </div>
  )
}

export function StockBadge({ p }: { p: Pick<CashierProduct, 'stock' | 'unitShortName'> }) {
  const t = useT()
  return p.stock > 0
    ? <span className="rounded-md bg-success-soft px-2 py-0.5 text-[12px] font-medium text-success">{t('pos.inStock', { n: num(p.stock) })}</span>
    : <span className="rounded-md bg-danger-soft px-2 py-0.5 text-[12px] font-medium text-danger">{t('pos.outOfStock')}</span>
}

export function ProductImage({ url, className }: { url: string | null; className: string }) {
  return url
    ? <img src={url} alt="" loading="lazy" className={`${className} bg-white object-contain`} />
    : <span className={`${className} flex items-center justify-center bg-field text-ink-3`}><ImageOff size={22} /></span>
}

function AddButton({ p, onAdd }: { p: CashierProduct; onAdd: (p: CashierProduct) => void }) {
  const t = useT()
  return (
    <button type="button" aria-label={t('pos.add')} onClick={() => onAdd(p)} disabled={p.price == null}
      className="flex size-9 shrink-0 items-center justify-center rounded-full bg-brand text-white shadow-sm hover:bg-brand-hover disabled:bg-line">
      <Plus size={19} />
    </button>
  )
}

function ProductCard({ p, onAdd }: { p: CashierProduct; onAdd: (p: CashierProduct) => void }) {
  const t = useT()
  return (
    <div className="flex flex-col rounded-2xl border border-line p-3 transition-shadow hover:shadow-md">
      <ProductImage url={p.imageUrl} className="aspect-[4/3] w-full rounded-xl" />
      <div className="mt-2 line-clamp-2 min-h-10 text-[14px] font-semibold leading-5">{p.name}</div>
      {p.article && <div className="text-[12px] text-ink-3">{t('pos.article', { a: p.article })}</div>}
      <div className="mt-2 flex items-end justify-between gap-2">
        <div className="min-w-0">
          <div className="text-[17px] font-bold">{p.price != null ? tenge(p.price) : <span className="text-[13px] font-medium text-ink-3">{t('pos.noPrice')}</span>}</div>
          <div className="mt-1"><StockBadge p={p} /></div>
        </div>
        <AddButton p={p} onAdd={onAdd} />
      </div>
    </div>
  )
}

function ProductRow({ p, onAdd }: { p: CashierProduct; onAdd: (p: CashierProduct) => void }) {
  const t = useT()
  return (
    <div className="flex items-center gap-3 px-3 py-2.5">
      <ProductImage url={p.imageUrl} className="size-12 shrink-0 rounded-lg" />
      <div className="min-w-0 flex-1">
        <div className="truncate text-[14px] font-semibold">{p.name}</div>
        <div className="text-[12px] text-ink-3">
          {[p.article && t('pos.article', { a: p.article }), p.brandName].filter(Boolean).join(' · ')}
        </div>
      </div>
      <StockBadge p={p} />
      <div className="w-28 text-right text-[15px] font-bold">{p.price != null ? tenge(p.price) : <span className="text-[13px] text-ink-3">{t('pos.noPrice')}</span>}</div>
      <AddButton p={p} onAdd={onAdd} />
    </div>
  )
}
