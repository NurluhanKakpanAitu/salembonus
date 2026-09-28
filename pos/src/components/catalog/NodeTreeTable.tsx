import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Archive, ArchiveRestore, ChevronDown, ChevronRight, ChevronsDownUp, ChevronsUpDown, FolderInput, PackagePlus, Pencil, Plus, Trash2 } from 'lucide-react'
import { Button } from '../ui/Button'
import { OrderControl } from '../ui/OrderControl'
import { RowMenu, type RowMenuItem } from '../ui/RowMenu'
import { SearchInput } from '../ui/SearchInput'
import { StatusBadge } from '../ui/StatusBadge'
import { catalogApi } from '../../lib/catalogApi'
import { catalogKeys, useCatalogAction, useCatalogNodes, useCatalogPermissions } from '../../lib/catalogHooks'
import type { CatalogNode } from '../../lib/catalogTypes'
import { CatalogIcon } from '../../lib/catalogIcons'
import { useT } from '../../lib/i18n'
import { ArchiveNodeModal, MoveContentModal, NodeFormModal } from './NodeFormModal'

type Mode = 'categories' | 'groups'

/**
 * Санаттар мен топтар қойындысы (ТЗ «Товар» §7, §8). Бір компонент: «Санаттар» түбірден
 * басталады, «Топтар» — бірінші деңгейден, санатына қарамай. Іздеу кез келген деңгейдегі түйінді
 * тауып, толық жолын көрсетеді (§13).
 */
export function NodeTreeTable({ mode }: { mode: Mode }) {
  const t = useT()
  const navigate = useNavigate()
  const nodesQuery = useCatalogNodes()
  const perms = useCatalogPermissions()
  const run = useCatalogAction(catalogKeys.nodes)
  const [query, setQuery] = useState('')
  const [expanded, setExpanded] = useState<Set<string>>(new Set())
  const [form, setForm] = useState<{ node?: CatalogNode; parentId?: string | null } | null>(null)
  const [moving, setMoving] = useState<CatalogNode | null>(null)
  const [archiving, setArchiving] = useState<CatalogNode | null>(null)

  const nodes = useMemo(() => nodesQuery.data ?? [], [nodesQuery.data])
  const children = useMemo(() => {
    const map = new Map<string | null, CatalogNode[]>()
    for (const n of nodes) map.set(n.parentId, [...(map.get(n.parentId) ?? []), n])
    for (const list of map.values()) list.sort((a, b) => a.sortOrder - b.sortOrder || a.name.localeCompare(b.name))
    return map
  }, [nodes])

  const topLevel = mode === 'categories'
    ? (children.get(null) ?? [])
    : nodes.filter((n) => n.depth === 1).sort((a, b) =>
        a.pathNames[0].localeCompare(b.pathNames[0]) || a.sortOrder - b.sortOrder)

  const q = query.trim().toLowerCase()
  const matches = q
    ? nodes.filter((n) => (mode === 'categories' || n.depth >= 1) && n.name.toLowerCase().includes(q))
        .sort((a, b) => a.pathNames.join('/').localeCompare(b.pathNames.join('/')))
    : null

  const toggle = (id: string) =>
    setExpanded((prev) => {
      const next = new Set(prev)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })

  const expandAll = () => setExpanded(new Set(nodes.filter((n) => n.childCount > 0).map((n) => n.id)))

  const menu = (n: CatalogNode): RowMenuItem[] => {
    const items: RowMenuItem[] = []
    // ТЗ §22.4: «+ Товар» — карточка осы жолмен толтырылып ашылады.
    if (perms.create && n.status === 'Active')
      items.push({ label: t('catalog.addProduct'), icon: <PackagePlus size={16} />, onClick: () => navigate(`/products/new?nodeId=${n.id}`) })
    if (perms.structure && n.status === 'Active')
      items.push({ label: n.depth === 0 ? t('catalog.addGroup') : t('catalog.addSubgroup'), icon: <Plus size={16} />, onClick: () => setForm({ parentId: n.id }) })
    if (perms.structure && (n.childCount > 0 || n.productCount > 0))
      items.push({ label: t('catalog.moveContent'), icon: <FolderInput size={16} />, onClick: () => setMoving(n) })
    if (perms.lifecycle && n.status === 'Active')
      items.push({
        label: t('catalog.archive'), icon: <Archive size={16} />,
        onClick: () => setArchiving(n),
      })
    if (perms.lifecycle && n.status === 'Archived')
      items.push({ label: t('catalog.restore'), icon: <ArchiveRestore size={16} />, onClick: () => void run(() => catalogApi.restoreNode(n.id), t('catalog.restored')) })
    if (perms.lifecycle)
      items.push({
        label: t('catalog.delete'), icon: <Trash2 size={16} />, danger: true,
        onClick: () => window.confirm(t('catalog.deleteConfirm', { name: n.name })) && void run(() => catalogApi.deleteNode(n.id), t('catalog.deleted')),
      })
    return items
  }

  const rows: { node: CatalogNode; level: number }[] = []
  const pushTree = (n: CatalogNode, level: number) => {
    rows.push({ node: n, level })
    if (expanded.has(n.id)) (children.get(n.id) ?? []).forEach((c) => pushTree(c, level + 1))
  }
  if (matches) matches.forEach((n) => rows.push({ node: n, level: 0 }))
  else topLevel.forEach((n) => pushTree(n, 0))

  const th = 'px-3 py-3 text-left text-[13px] font-semibold text-ink-2'
  const td = 'px-3 py-2.5 text-[14px]'

  return (
    <div>
      <div className="mb-4 flex flex-wrap items-center gap-2">
        {perms.structure && (
          <Button icon={<Plus size={18} />} onClick={() => setForm({ parentId: mode === 'groups' ? (children.get(null)?.[0]?.id ?? null) : null })}>
            {mode === 'categories' ? t('catalog.addCategory') : t('catalog.addGroup')}
          </Button>
        )}
        <Button variant="secondary" icon={<ChevronsUpDown size={17} />} onClick={expandAll}>{t('catalog.expandAll')}</Button>
        <Button variant="secondary" icon={<ChevronsDownUp size={17} />} onClick={() => setExpanded(new Set())}>{t('catalog.collapseAll')}</Button>
        <SearchInput className="ml-auto w-full sm:w-72" value={query} onChange={setQuery}
          placeholder={mode === 'categories' ? t('catalog.searchCategories') : t('catalog.searchGroups')} />
      </div>

      <div className="overflow-x-auto rounded-2xl border border-line bg-surface">
        <table className="w-full min-w-[980px] border-collapse">
          <thead className="bg-field">
            <tr>
              <th className="w-10" />
              <th className={`${th} w-16`}>{t('catalog.col.icon')}</th>
              <th className={th}>{mode === 'categories' ? t('catalog.col.categoryName') : t('catalog.col.groupName')}</th>
              {mode === 'groups' && <th className={th}>{t('catalog.col.category')}</th>}
              <th className={`${th} text-right`}>{t('catalog.col.products')}</th>
              <th className={`${th} text-right`}>{mode === 'categories' ? t('catalog.col.groups') : t('catalog.col.subgroups')}</th>
              {mode === 'categories' && <th className={`${th} text-right`}>{t('catalog.col.subcategories')}</th>}
              <th className={th}>{t('catalog.col.status')}</th>
              <th className={th}>{t('catalog.col.order')}</th>
              <th className={`${th} text-right`}>{t('catalog.col.actions')}</th>
            </tr>
          </thead>
          <tbody>
            {nodesQuery.isLoading && (
              <tr><td colSpan={10} className="px-3 py-10 text-center text-[14px] text-ink-3">{t('common.loading')}</td></tr>
            )}
            {!nodesQuery.isLoading && rows.length === 0 && (
              <tr><td colSpan={10} className="px-3 py-10 text-center text-[14px] text-ink-3">
                {matches ? t('catalog.nothingFound') : t('catalog.empty')}
              </td></tr>
            )}
            {rows.map(({ node: n, level }) => {
              const archived = n.status === 'Archived'
              return (
                <tr key={n.id} className={`border-t border-line ${level === 0 && !matches ? '' : 'bg-field/30'} ${archived ? 'text-ink-3' : ''}`}>
                  <td className="pl-3">
                    {!matches && n.childCount > 0 && (
                      <button type="button" aria-label="Toggle" onClick={() => toggle(n.id)}
                        className="flex size-7 items-center justify-center rounded-lg text-ink-2 hover:bg-field"
                        style={{ marginLeft: level * 20 }}>
                        {expanded.has(n.id) ? <ChevronDown size={17} /> : <ChevronRight size={17} />}
                      </button>
                    )}
                  </td>
                  <td className={td}>
                    <span className="flex size-9 items-center justify-center rounded-lg text-ink-2" style={{ marginLeft: matches ? 0 : level * 20 }}>
                      <CatalogIcon name={n.icon ?? (n.depth === 0 ? 'package' : null)} size={21} />
                    </span>
                  </td>
                  <td className={td}>
                    <button type="button" disabled={!perms.structure} onClick={() => setForm({ node: n })}
                      className={`text-left font-semibold ${level === 0 && !matches ? 'text-brand' : 'text-ink'} ${perms.structure ? 'hover:underline' : ''}`}>
                      {n.name}
                    </button>
                    {matches && n.pathNames.length > 1 && (
                      <div className="text-[12px] text-ink-3">{n.pathNames.join(' → ')}</div>
                    )}
                  </td>
                  {mode === 'groups' && <td className={`${td} text-ink-2`}>{n.pathNames[0]}</td>}
                  <td className={`${td} text-right`}>
                    <button type="button" title={t('catalog.showProducts')} onClick={() => navigate(`/products?nodeId=${n.id}&status=all`)}
                      className="font-medium text-brand hover:underline">{n.productCount}</button>
                  </td>
                  <td className={`${td} text-right`}>{n.childCount}</td>
                  {mode === 'categories' && <td className={`${td} text-right`}>{n.descendantCount - n.childCount}</td>}
                  <td className={td}>
                    <StatusBadge active={!archived} label={archived ? t('status.Archived.f') : t('status.Active.f')} />
                  </td>
                  <td className={td}>
                    <OrderControl value={n.sortOrder} disabled={!perms.structure}
                      onUp={() => void run(() => catalogApi.reorderNode(n.id, 'up'), t('catalog.saved'))}
                      onDown={() => void run(() => catalogApi.reorderNode(n.id, 'down'), t('catalog.saved'))} />
                  </td>
                  <td className={`${td} whitespace-nowrap text-right`}>
                    <div className="inline-flex items-center gap-1.5">
                      {perms.structure && mode === 'groups' && !archived && (
                        <Button variant="secondary" className="h-9! px-3! text-[13px]!" icon={<Plus size={15} />} onClick={() => setForm({ parentId: n.id })}>
                          {t('catalog.addSubgroup')}
                        </Button>
                      )}
                      {perms.structure && (
                        <Button variant="secondary" className="h-9! px-3! text-[13px]! text-brand!" icon={<Pencil size={15} />} onClick={() => setForm({ node: n })}>
                          {t('catalog.edit')}
                        </Button>
                      )}
                      <RowMenu items={menu(n)} />
                    </div>
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>

      {form && (
        <NodeFormModal node={form.node} parentId={form.parentId} nodes={nodes} onClose={() => setForm(null)}
          onSaved={(msg) => void run(async () => undefined, msg).then(() => setForm(null))} />
      )}
      {archiving && (
        <ArchiveNodeModal node={archiving} onClose={() => setArchiving(null)}
          onMoveContent={() => { setMoving(archiving); setArchiving(null) }}
          onArchived={(msg) => void run(async () => undefined, msg).then(() => setArchiving(null))} />
      )}
      {moving && (
        <MoveContentModal node={moving} nodes={nodes} onClose={() => setMoving(null)}
          onMoved={(msg) => void run(async () => undefined, msg).then(() => setMoving(null))} />
      )}
    </div>
  )
}
