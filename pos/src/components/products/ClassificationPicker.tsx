import { useMemo, useState } from 'react'
import { Check, ChevronRight, Plus, X } from 'lucide-react'
import { SearchInput } from '../ui/SearchInput'
import { NodeFormModal } from '../catalog/NodeFormModal'
import { CatalogIcon } from '../../lib/catalogIcons'
import { useCatalogAction, useCatalogPermissions, catalogKeys } from '../../lib/catalogHooks'
import type { CatalogNode } from '../../lib/catalogTypes'
import { useT } from '../../lib/i18n'

/** Түбірден түйінге дейінгі идентификаторлар. */
function trailOf(byId: Map<string, CatalogNode>, id: string | null): string[] {
  const out: string[] = []
  let cur = id ? byId.get(id) : undefined
  while (cur) {
    out.unshift(cur.id)
    cur = cur.parentId ? byId.get(cur.parentId) : undefined
  }
  return out
}

/**
 * Тауардың классификациясын таңдау (ТЗ «Товар» §6.3, §13): бағандар «Санат → Топ → Топша…»,
 * тікелей іздеу толық жолымен және әр бағанда «+ Қосу». Кез келген деңгейді таңдауға болады.
 */
export function ClassificationPicker({ nodes, value, onChange }: {
  nodes: CatalogNode[]
  value: string | null
  onChange: (nodeId: string | null) => void
}) {
  const t = useT()
  const perms = useCatalogPermissions()
  const refresh = useCatalogAction(catalogKeys.nodes)
  const byId = useMemo(() => new Map(nodes.map((n) => [n.id, n])), [nodes])
  const [trail, setTrail] = useState<string[]>(() => trailOf(byId, value))
  const [query, setQuery] = useState('')
  const [adding, setAdding] = useState<string | null | undefined>(undefined)

  // Архивтегі түйін жаңа тауарға ұсынылмайды (ТЗ §16), бірақ ағымдағы таңдау көрінеді.
  const childrenOf = (parentId: string | null) =>
    nodes.filter((n) => n.parentId === parentId && (n.status === 'Active' || trail.includes(n.id)))
      .sort((a, b) => a.sortOrder - b.sortOrder || a.name.localeCompare(b.name))

  const columns: { parentId: string | null; items: CatalogNode[] }[] = [{ parentId: null, items: childrenOf(null) }]
  for (const id of trail) {
    const items = childrenOf(id)
    if (items.length > 0 || perms.structure) columns.push({ parentId: id, items })
  }

  const select = (node: CatalogNode) => {
    setTrail(trailOf(byId, node.id))
    onChange(node.id)
    setQuery('')
  }

  const q = query.trim().toLowerCase()
  const matches = q
    ? nodes.filter((n) => n.status === 'Active' && n.name.toLowerCase().includes(q))
        .sort((a, b) => a.pathNames.join('/').localeCompare(b.pathNames.join('/'))).slice(0, 30)
    : []

  const columnTitle = (i: number) =>
    i === 0 ? t('catalog.col.category') : i === 1 ? t('product.col.group') : t('product.col.subgroup')

  const selected = value ? byId.get(value) : undefined

  return (
    <div className="flex flex-col gap-3">
      <div className="relative">
        <SearchInput value={query} onChange={setQuery} placeholder={t('product.classSearch')} />
        {q && (
          <ul className="absolute inset-x-0 top-full z-20 mt-1 max-h-72 overflow-y-auto rounded-xl border border-line bg-surface py-1 shadow-lg">
            {matches.length === 0 && <li className="px-3.5 py-2.5 text-[14px] text-ink-3">{t('catalog.nothingFound')}</li>}
            {matches.map((n) => (
              <li key={n.id}>
                <button type="button" onClick={() => select(n)} className="w-full px-3.5 py-2 text-left hover:bg-field">
                  <div className="text-[14px] font-medium">{n.name}</div>
                  <div className="text-[12px] text-ink-3">{n.pathNames.join(' → ')}</div>
                </button>
              </li>
            ))}
          </ul>
        )}
      </div>

      <div className="flex gap-3 overflow-x-auto pb-1">
        {columns.map((col, i) => (
          <div key={col.parentId ?? 'root'} className="flex w-60 shrink-0 flex-col rounded-xl border border-line bg-surface">
            <div className="border-b border-line px-3 py-2.5 text-[13px] font-semibold text-ink-2">{columnTitle(i)}</div>
            <ul className="max-h-72 flex-1 overflow-y-auto py-1">
              {col.items.length === 0 && <li className="px-3 py-2 text-[13px] text-ink-3">{t('catalog.empty')}</li>}
              {col.items.map((n) => {
                const inTrail = trail[i] === n.id
                const isValue = value === n.id
                return (
                  <li key={n.id}>
                    <button type="button" onClick={() => select(n)}
                      className={`flex w-full items-center gap-2 px-3 py-2 text-left text-[14px] transition-colors ${
                        inTrail ? 'bg-brand-soft text-brand' : 'hover:bg-field'} ${n.status === 'Archived' ? 'text-ink-3' : ''}`}>
                      {i === 0 && <CatalogIcon name={n.icon ?? 'package'} size={17} />}
                      <span className="min-w-0 flex-1 truncate">{n.name}</span>
                      {isValue ? <Check size={16} /> : n.childCount > 0 && <ChevronRight size={16} className="text-ink-3" />}
                    </button>
                  </li>
                )
              })}
            </ul>
            {perms.structure && (
              <button type="button" onClick={() => setAdding(col.parentId)}
                className="flex items-center gap-1.5 border-t border-line px-3 py-2.5 text-[13px] font-medium text-brand hover:bg-field">
                <Plus size={15} /> {t('product.addHere')}
              </button>
            )}
          </div>
        ))}
      </div>

      <div className="flex flex-wrap items-center gap-2 rounded-xl bg-field px-3.5 py-3 text-[14px]">
        <span className="text-ink-2">{t('product.selectedPath')}:</span>
        {selected
          ? <span className="font-semibold">{selected.pathNames.join(' → ')}</span>
          : <span className="text-ink-3">{t('product.noClassification')}</span>}
        {selected && (
          <button type="button" onClick={() => { setTrail([]); onChange(null) }}
            className="ml-auto flex items-center gap-1 text-[13px] text-ink-3 hover:text-danger">
            <X size={14} /> {t('product.clear')}
          </button>
        )}
      </div>

      {adding !== undefined && (
        <NodeFormModal parentId={adding} nodes={nodes} onClose={() => setAdding(undefined)}
          onSaved={(msg) => void refresh(async () => undefined, msg).then(() => setAdding(undefined))} />
      )}
    </div>
  )
}
