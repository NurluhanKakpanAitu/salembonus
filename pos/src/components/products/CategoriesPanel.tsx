import { useMemo, useState } from 'react'
import { ChevronDown, ChevronRight, X } from 'lucide-react'
import { SearchInput } from '../ui/SearchInput'
import { CatalogIcon } from '../../lib/catalogIcons'
import type { CatalogNode } from '../../lib/catalogTypes'
import { useT } from '../../lib/i18n'

/**
 * «Категории товаров» — тізімнің оң жағындағы ағаш панелі (макет бойынша): әр түйінде тауар саны,
 * басқанда тізім сол түйінмен (ұрпақтарымен бірге) сүзіледі. Іздеу кез келген деңгейді табады.
 */
export function CategoriesPanel({ nodes, value, onSelect, onClose }: {
  nodes: CatalogNode[]
  value: string
  onSelect: (nodeId: string) => void
  onClose: () => void
}) {
  const t = useT()
  const [query, setQuery] = useState('')
  const [expanded, setExpanded] = useState<Set<string>>(() => {
    // Таңдалған түйінге дейінгі жол ашық тұрсын.
    const byId = new Map(nodes.map((n) => [n.id, n]))
    const open = new Set<string>()
    let cur = value ? byId.get(value) : undefined
    while (cur?.parentId) {
      open.add(cur.parentId)
      cur = byId.get(cur.parentId)
    }
    return open
  })

  const children = useMemo(() => {
    const map = new Map<string | null, CatalogNode[]>()
    for (const n of nodes) if (n.status === 'Active') map.set(n.parentId, [...(map.get(n.parentId) ?? []), n])
    for (const list of map.values()) list.sort((a, b) => a.sortOrder - b.sortOrder || a.name.localeCompare(b.name))
    return map
  }, [nodes])

  const q = query.trim().toLowerCase()
  const matches = q ? nodes.filter((n) => n.status === 'Active' && n.name.toLowerCase().includes(q)) : null
  const total = (children.get(null) ?? []).reduce((s, n) => s + n.productCount, 0)

  const toggle = (id: string) =>
    setExpanded((prev) => {
      const next = new Set(prev)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })

  const rows: { node: CatalogNode; level: number }[] = []
  const walk = (n: CatalogNode, level: number) => {
    rows.push({ node: n, level })
    if (expanded.has(n.id)) (children.get(n.id) ?? []).forEach((c) => walk(c, level + 1))
  }
  if (matches) matches.forEach((n) => rows.push({ node: n, level: 0 }))
  else (children.get(null) ?? []).forEach((n) => walk(n, 0))

  return (
    <aside className="fixed bottom-4 right-4 top-24 z-30 flex w-[340px] max-w-[calc(100vw-2rem)] flex-col rounded-2xl border border-line bg-surface shadow-xl">
      <div className="flex items-center justify-between gap-3 px-4 pt-4">
        <h3 className="text-[17px] font-bold">{t('product.categoriesPanelTitle')}</h3>
        <button type="button" onClick={onClose} aria-label="Close" className="text-ink-3 hover:text-ink"><X size={20} /></button>
      </div>
      <div className="px-4 pt-3">
        <SearchInput value={query} onChange={setQuery} placeholder={t('catalog.searchCategories')} />
      </div>
      <ul className="mt-2 min-h-0 flex-1 overflow-y-auto px-2 pb-2">
        {!matches && (
          <li>
            <button type="button" onClick={() => onSelect('')}
              className={`flex w-full items-center gap-2 rounded-xl px-2.5 py-2 text-left text-[14px] ${!value ? 'bg-brand-soft font-semibold text-brand' : 'hover:bg-field'}`}>
              <span className="min-w-0 flex-1 truncate">{t('product.filter.allCategories')}</span>
              <span className="rounded-md bg-field px-1.5 py-0.5 text-[12px] font-semibold text-ink-2">{total}</span>
            </button>
          </li>
        )}
        {rows.length === 0 && <li className="px-3 py-6 text-center text-[14px] text-ink-3">{t('catalog.nothingFound')}</li>}
        {rows.map(({ node: n, level }) => {
          const active = value === n.id
          const hasChildren = !matches && (children.get(n.id)?.length ?? 0) > 0
          return (
            <li key={n.id}>
              <div className={`flex items-center gap-1 rounded-xl pr-2 ${active ? 'bg-brand-soft text-brand' : 'hover:bg-field'}`}
                style={{ paddingLeft: matches ? 8 : 8 + level * 16 }}>
                <button type="button" aria-label="Toggle" disabled={!hasChildren} onClick={() => toggle(n.id)}
                  className="flex size-6 shrink-0 items-center justify-center text-ink-3 disabled:opacity-0">
                  {expanded.has(n.id) ? <ChevronDown size={15} /> : <ChevronRight size={15} />}
                </button>
                <button type="button" onClick={() => onSelect(n.id)} className="flex min-w-0 flex-1 items-center gap-2 py-2 text-left text-[14px]">
                  <CatalogIcon name={n.icon ?? (n.depth === 0 ? 'package' : null)} size={17} className={active ? 'text-brand' : 'text-ink-2'} />
                  <span className="min-w-0 flex-1">
                    <span className={`block truncate ${active ? 'font-semibold' : ''}`}>{n.name}</span>
                    {matches && n.pathNames.length > 1 && <span className="block truncate text-[11px] text-ink-3">{n.pathNames.slice(0, -1).join(' → ')}</span>}
                  </span>
                  <span className={`rounded-md px-1.5 py-0.5 text-[12px] font-semibold ${active ? 'bg-brand text-white' : 'bg-field text-ink-2'}`}>{n.productCount}</span>
                </button>
              </div>
            </li>
          )
        })}
      </ul>
    </aside>
  )
}
