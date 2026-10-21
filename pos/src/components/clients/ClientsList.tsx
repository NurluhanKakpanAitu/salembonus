import { useEffect, useState } from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import {
  AlarmClock, Archive, ArrowDown, ArrowUp, Columns3, Crown, Download, Eye, Gift, HandCoins, Layers, Megaphone, MessageSquare, Moon, Pencil, Repeat,
  ShoppingCart, SlidersHorizontal, Smartphone, UserPlus, Users, X, Zap,
} from 'lucide-react'
import { Button } from '../ui/Button'
import { Select } from '../ui/Field'
import { RowMenu } from '../ui/RowMenu'
import { SearchInput } from '../ui/SearchInput'
import { EmptyRow, Pager, td, th, useDebounced } from '../inventory/inventoryUi'
import { BlockError } from '../stats/statsUi'
import { Pill, Points, StatusBadge } from '../bonus/bonusUi'
import { clientsApi, handOffAudience, type ClientSearch } from '../../lib/clientsApi'
import { QUICK_FILTERS, type ClientKpis, type ClientRow, type QuickFilter } from '../../lib/clientsTypes'
import { dateOnly } from '../../lib/dates'
import { num, tenge } from '../../lib/money'
import { useT, type TranslationKey } from '../../lib/i18n'
import { FiltersDrawer } from './ClientFilters'
import { BulkSegmentModal, BulkStatusModal, ExportModal, SegmentModal, lastPurchaseText } from './ClientModals'
import { ActivityPill, Avatar, KpiTile, advancedCount, clientNo, compact, useClientMeta, useListFilter, useOpenClient } from './clientsUi'

const ALL_COLUMNS = ['phone', 'status', 'purchases', 'amount', 'average', 'lastPurchase', 'bonus', 'debt', 'registered', 'activity', 'birthDate', 'source', 'email'] as const
type Column = (typeof ALL_COLUMNS)[number]
const DEFAULT_COLUMNS: Column[] = ['phone', 'status', 'purchases', 'amount', 'average', 'lastPurchase', 'bonus', 'debt', 'activity']
const SORT_KEY: Partial<Record<Column | 'name', string>> = {
  name: 'name', purchases: 'purchases', amount: 'amount', average: 'average', lastPurchase: 'lastPurchase', bonus: 'bonus', debt: 'debt', registered: 'registered', birthDate: 'birthday',
}
const COLUMNS_KEY = 'salempos.clients.columns'

function loadColumns(): Column[] | null {
  try {
    const raw = localStorage.getItem(COLUMNS_KEY)
    const list = raw ? (JSON.parse(raw) as string[]) : null
    return list ? (list.filter((c) => (ALL_COLUMNS as readonly string[]).includes(c)) as Column[]) : null
  } catch { return null }
}

const KPIS: { key: keyof Omit<ClientKpis, 'generatedAt'>; quick: QuickFilter | null; icon: React.ReactNode; tone: 'blue' | 'green' | 'violet' | 'sky' | 'gray' | 'amber' | 'red' | 'pink' }[] = [
  { key: 'total', quick: null, icon: <Users size={17} />, tone: 'blue' },
  { key: 'active', quick: 'active', icon: <Zap size={17} />, tone: 'green' },
  { key: 'new', quick: 'new', icon: <UserPlus size={17} />, tone: 'violet' },
  { key: 'repeat', quick: 'repeat', icon: <Repeat size={17} />, tone: 'sky' },
  { key: 'passive', quick: 'passive', icon: <Moon size={17} />, tone: 'gray' },
  { key: 'withDebt', quick: 'debt', icon: <HandCoins size={17} />, tone: 'amber' },
  { key: 'overdue', quick: 'overdue', icon: <AlarmClock size={17} />, tone: 'red' },
  { key: 'withBonus', quick: 'bonus', icon: <Gift size={17} />, tone: 'pink' },
]

/**
 * «Все клиенты» (ТЗ «Клиенты» §2–§4): KPI (басылса — сол сүзгі; KPI мен тізім бір есептен), іздеу (аты, телефон, ID), жылдам сүзгілер,
 * мәртебе, сегмент, кеңейтілген сүзгі, бағандар, сұрыптау, топтық әрекеттер. Жол басылса — профиль оң жақта ашылады.
 */
export function ClientsList({ storeIds }: { storeIds?: string }) {
  const t = useT()
  const nav = useNavigate()
  const meta = useClientMeta()
  const can = meta.data?.can
  const { filter, adv, segmentId, sort, dir, page, pageSize, set, params } = useListFilter()
  const openClient = useOpenClient()
  const [search, setSearch] = useState(filter.search ?? '')
  const debounced = useDebounced(search, 300)
  useEffect(() => { if ((filter.search ?? '') !== debounced) set({ q: debounced || null }) }, [debounced]) // eslint-disable-line react-hooks/exhaustive-deps
  const request: ClientSearch = { storeIds, filter, segmentId, sort: sort || undefined, dir, page, pageSize }
  const kpiRequest: ClientSearch = { storeIds, filter: { ...filter, quick: null }, segmentId }
  const list = useQuery({ queryKey: ['clients', 'list', request], queryFn: () => clientsApi.search(request), placeholderData: keepPreviousData })
  const kpis = useQuery({ queryKey: ['clients', 'kpis', kpiRequest], queryFn: () => clientsApi.kpis(kpiRequest), placeholderData: keepPreviousData })
  const segments = useQuery({ queryKey: ['clients', 'segments', storeIds], queryFn: () => clientsApi.segments(storeIds), staleTime: 30_000 })
  const [selected, setSelected] = useState<Map<string, ClientRow>>(new Map())
  const [columns, setColumns] = useState<Column[]>(() => loadColumns() ?? DEFAULT_COLUMNS)
  const [columnsOpen, setColumnsOpen] = useState(false)
  const [modal, setModal] = useState<'filters' | 'segment' | 'saveSegment' | 'status' | null>(null)

  // Ұйым баптауындағы әдепкі бағандар — пайдаланушы өзі таңдамаған болса.
  const defaults = meta.data?.settings.defaultColumns
  useEffect(() => {
    if (!loadColumns() && defaults && defaults.length > 0) setColumns(defaults.filter((c) => (ALL_COLUMNS as readonly string[]).includes(c)) as Column[])
  }, [defaults])
  const toggleColumn = (c: Column) => {
    const next = columns.includes(c) ? columns.filter((x) => x !== c) : ALL_COLUMNS.filter((x) => x === c || columns.includes(x))
    setColumns(next)
    try { localStorage.setItem(COLUMNS_KEY, JSON.stringify(next)) } catch { /* жеке режим */ }
  }

  const rows = list.data?.rows ?? []
  const allOnPage = rows.length > 0 && rows.every((r) => selected.has(r.id))
  const toggleAll = () => setSelected((s) => {
    const n = new Map(s)
    if (allOnPage) rows.forEach((r) => n.delete(r.id)); else rows.forEach((r) => n.set(r.id, r))
    return n
  })
  const toggle = (r: ClientRow) => setSelected((s) => { const n = new Map(s); if (n.has(r.id)) n.delete(r.id); else n.set(r.id, r); return n })
  const ids = [...selected.keys()]
  const hasFilter = !!filter.search || !!filter.quick || (filter.statusIds?.length ?? 0) > 0 || advancedCount(adv) > 0
  const advCount = advancedCount(adv)

  const sortBy = (key: string) => set(sort === key ? { sort: key, dir: dir === 'desc' ? 'asc' : 'desc' } : { sort: key, dir: key === 'name' ? 'asc' : 'desc' })
  const head = (key: Column | 'name', label: string, right = false) => {
    const s = SORT_KEY[key]
    return (
      <th key={key} className={`${th} ${right ? 'text-right' : ''} whitespace-nowrap`}>
        {s ? (
          <button type="button" onClick={() => sortBy(s)} className={`inline-flex items-center gap-1 hover:text-ink ${sort === s ? 'text-brand' : ''}`}>
            {label}{sort === s && (dir === 'asc' ? <ArrowUp size={13} /> : <ArrowDown size={13} />)}
          </button>
        ) : label}
      </th>
    )
  }
  const RIGHT = new Set<Column>(['purchases', 'amount', 'average', 'bonus', 'debt'])
  const cell = (r: ClientRow, c: Column) => {
    switch (c) {
      case 'phone': return <span className="whitespace-nowrap">{r.phone ?? '—'}</span>
      case 'status': return r.hasCard ? <StatusBadge name={r.statusName} color={r.statusColor} /> : <span className="text-ink-3">—</span>
      case 'purchases': return num(r.purchases)
      case 'amount': return tenge(r.amount)
      case 'average': return r.purchases ? tenge(r.average) : '—'
      case 'lastPurchase': return <span className="whitespace-nowrap">{lastPurchaseText(t, r.lastPurchaseAt, r.daysSinceLastPurchase)}</span>
      case 'bonus': return <Points value={r.bonus} />
      case 'debt': return r.debt > 0 ? <span className={r.overdue > 0 ? 'font-semibold text-danger' : 'font-semibold'}>{tenge(r.debt)}</span> : <span className="text-ink-3">—</span>
      case 'registered': return dateOnly(r.registeredAt)
      case 'activity': return <ActivityPill activity={r.activity} />
      case 'birthDate': return r.birthDate ? dateOnly(r.birthDate) : '—'
      case 'source': return t(`cli.source.${r.source}` as TranslationKey)
      case 'email': return r.email ?? '—'
    }
  }

  const campaign = () => {
    handOffAudience([...selected.values()].map((r) => r.customerId))
    nav('/bonus/campaigns?new=1&audience=clients')
  }

  if (list.isError) return <BlockError message={(list.error as Error).message} onRetry={() => void list.refetch()} />

  return (
    <div className="flex flex-col gap-4">
      <div className="grid grid-cols-2 gap-2.5 sm:grid-cols-4 2xl:grid-cols-8">
        {KPIS.map((k) => (
          <KpiTile key={k.key} label={t(`cli.kpi.${k.key}` as TranslationKey)} value={kpis.data ? num(kpis.data[k.key]) : '…'} icon={k.icon} tone={k.tone}
            active={k.quick ? filter.quick === k.quick : !filter.quick} onClick={() => set({ quick: k.quick && filter.quick !== k.quick ? k.quick : null })} />
        ))}
      </div>

      <div className="flex flex-col gap-3 rounded-2xl border border-line bg-surface p-3">
        <div className="flex flex-wrap items-center gap-2">
          <SearchInput value={search} onChange={setSearch} placeholder={t('cli.searchPlaceholder')} className="min-w-60 flex-1" />
          <Select className="h-11! w-auto min-w-48" value={filter.statusIds?.[0] ?? ''} onChange={(e) => set({ status: e.target.value || null })} aria-label={t('cli.col.status')}>
            <option value="">{t('cli.allStatuses')}</option>
            {(meta.data?.statuses ?? []).map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
          </Select>
          <Select className="h-11! w-auto min-w-56" value={segmentId ?? ''} onChange={(e) => set({ segment: e.target.value || null })} aria-label={t('cli.seg.segment')}>
            <option value="">{t('cli.allClients')}</option>
            {(segments.data ?? []).map((s) => <option key={s.id} value={s.id}>{s.name} ({num(s.count)})</option>)}
          </Select>
          <Button variant="secondary" icon={<SlidersHorizontal size={16} />} onClick={() => setModal('filters')}>
            {t('cli.filters')}{advCount > 0 && <span className="rounded-full bg-brand px-1.5 text-[11px] text-white">{advCount}</span>}
          </Button>
          <div className="relative">
            <Button variant="secondary" icon={<Columns3 size={16} />} onClick={() => setColumnsOpen((o) => !o)} aria-label={t('cli.columns')} />
            {columnsOpen && (
              <div className="absolute right-0 top-12 z-30 w-56 rounded-2xl border border-line bg-surface p-3 shadow-xl" onMouseLeave={() => setColumnsOpen(false)}>
                <div className="mb-2 text-[12px] font-semibold text-ink-2">{t('cli.columns')}</div>
                {ALL_COLUMNS.map((c) => (
                  <label key={c} className="flex items-center gap-2 py-1 text-[13px]">
                    <input type="checkbox" className="accent-brand" checked={columns.includes(c)} onChange={() => toggleColumn(c)} />{t(`cli.col.${c}` as TranslationKey)}
                  </label>
                ))}
              </div>
            )}
          </div>
        </div>
        <div className="flex flex-wrap items-center gap-1.5">
          {QUICK_FILTERS.map((q) => (
            <button key={q} type="button" onClick={() => set({ quick: filter.quick === q ? null : q })}
              className={`rounded-full border px-3 py-1 text-[12px] font-semibold transition-colors ${filter.quick === q ? 'border-brand bg-brand text-white' : 'border-line text-ink-2 hover:border-brand/50 hover:text-ink'}`}>
              {t(`cli.quick.${q}` as TranslationKey)}
            </button>
          ))}
          {hasFilter && (
            <>
              <button type="button" className="ml-1 inline-flex items-center gap-1 text-[12px] font-semibold text-ink-3 hover:text-ink"
                onClick={() => { setSearch(''); set({ q: null, quick: null, status: null, f: null }) }}><X size={13} />{t('cli.f.reset')}</button>
              {can?.edit && <button type="button" className="ml-auto inline-flex items-center gap-1 text-[12px] font-semibold text-brand hover:underline" onClick={() => setModal('saveSegment')}><Layers size={13} />{t('cli.seg.saveAs')}</button>}
            </>
          )}
        </div>
      </div>

      {ids.length > 0 && (
        <div className="sticky top-2 z-20 flex flex-wrap items-center gap-2 rounded-2xl border border-brand/30 bg-brand-soft px-4 py-2.5 text-[13px]">
          <b className="text-brand">{t('cli.bulk.selected', { n: ids.length })}</b>
          {can?.edit && <Button variant="secondary" className="h-9! px-3!" icon={<Layers size={15} />} onClick={() => setModal('segment')}>{t('cli.bulk.toSegment')}</Button>}
          {can?.edit && can.bonusManage && <Button variant="secondary" className="h-9! px-3!" icon={<Crown size={15} />} onClick={() => setModal('status')}>{t('cli.bulk.status')}</Button>}
          {can?.export && <Button variant="secondary" className="h-9! px-3!" icon={<Download size={15} />} onClick={() => set({ export: '1' }, true)}>{t('cli.bulk.export')}</Button>}
          {can?.bonusCampaigns && <Button variant="secondary" className="h-9! px-3!" icon={<Megaphone size={15} />} onClick={campaign}>{t('cli.bulk.campaign')}</Button>}
          <button type="button" className="ml-auto text-[13px] font-semibold text-ink-2 hover:text-ink" onClick={() => setSelected(new Map())}>{t('cli.bulk.clear')}</button>
        </div>
      )}

      <div className="overflow-x-auto rounded-2xl border border-line bg-surface">
        <table className="w-full min-w-[900px]">
          <thead className="border-b border-line bg-field/60">
            <tr>
              <th className="w-10 px-3"><input type="checkbox" className="accent-brand" checked={allOnPage} onChange={toggleAll} aria-label={t('cli.bulk.selectAll')} /></th>
              {head('name', t('cli.col.client'))}
              {columns.map((c) => head(c, t(`cli.col.${c}` as TranslationKey), RIGHT.has(c)))}
              <th className="w-12" />
            </tr>
          </thead>
          <tbody>
            {!list.data && <EmptyRow colSpan={columns.length + 3} text={t('cli.loading')} />}
            {list.data && rows.length === 0 && <EmptyRow colSpan={columns.length + 3} text={hasFilter || segmentId ? t('cli.emptyFiltered') : t('cli.empty')} />}
            {rows.map((r) => (
              <tr key={r.id} className={`cursor-pointer border-b border-line last:border-0 hover:bg-field/60 ${params.get('client') === r.id ? 'bg-brand-soft/50' : ''}`} onClick={() => openClient(r.id)}>
                <td className="px-3" onClick={(e) => e.stopPropagation()}><input type="checkbox" className="accent-brand" checked={selected.has(r.id)} onChange={() => toggle(r)} aria-label={r.name} /></td>
                <td className={td}>
                  <span className="flex min-w-0 items-center gap-3">
                    <Avatar name={r.name} size={34} />
                    <span className="min-w-0">
                      <span className="flex items-center gap-1.5 font-semibold">
                        <span className="truncate">{r.name}</span>
                        {r.hasApp && <Smartphone size={13} className="shrink-0 text-brand" aria-label={t('cli.badge.app')} />}
                        {r.isNew && <Pill tone="violet">{t('cli.badge.new')}</Pill>}
                        {r.isArchived && <Pill tone="gray">{t('cli.archived')}</Pill>}
                      </span>
                      <span className="block text-[12px] text-ink-3">ID {clientNo(r.number)}</span>
                    </span>
                  </span>
                </td>
                {columns.map((c) => <td key={c} className={`${td} ${RIGHT.has(c) ? 'text-right tabular-nums' : ''}`}>{cell(r, c)}</td>)}
                <td className="px-2" onClick={(e) => e.stopPropagation()}>
                  <RowMenu items={[
                    { label: t('cli.menu.open'), icon: <Eye size={16} />, onClick: () => openClient(r.id) },
                    ...(can?.salesCreate && !r.isArchived ? [{ label: t('cli.prof.sale'), icon: <ShoppingCart size={16} />, onClick: () => nav(`/cashier?customer=${r.customerId}`) }] : []),
                    ...(can?.edit && !r.isArchived ? [
                      { label: t('cli.menu.edit'), icon: <Pencil size={16} />, onClick: () => set({ edit: r.id }, true) },
                      { label: t('cli.prof.message'), icon: <MessageSquare size={16} />, onClick: () => set({ client: r.id, message: '1' }, true) },
                    ] : []),
                    ...(can?.archive && !r.isArchived ? [{ label: t('cli.menu.archive'), icon: <Archive size={16} />, danger: true, onClick: () => set({ client: r.id, archive: '1' }, true) }] : []),
                  ]} />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {list.data && <Pager page={page} pageSize={pageSize} total={list.data.total} onPage={(p) => set({ page: String(p) }, true)} onPageSize={(s) => set({ size: String(s) })} />}

      {modal === 'filters' && (
        <FiltersDrawer value={adv} onClose={() => setModal(null)}
          onApply={(f) => { setModal(null); const c = compact(f); set({ f: Object.keys(c).length ? JSON.stringify(c) : null }) }} />
      )}
      {modal === 'saveSegment' && (
        <SegmentModal segment={null} initialFilter={compact({ ...filter })} onClose={() => setModal(null)}
          onSaved={(s) => { setModal(null); void segments.refetch(); setSearch(''); set({ segment: s.id, q: null, quick: null, status: null, f: null }) }} />
      )}
      {modal === 'segment' && <BulkSegmentModal ids={ids} storeIds={storeIds} onClose={() => setModal(null)} onDone={() => { setModal(null); setSelected(new Map()); void segments.refetch() }} />}
      {params.get('export') === '1' && can?.export && (
        <ExportModal total={list.data?.total ?? null} selected={ids} request={{ storeIds, filter, segmentId }} onClose={() => set({ export: null }, true)} />
      )}
      {modal === 'status' && <BulkStatusModal ids={ids} onClose={() => setModal(null)} onDone={() => { setModal(null); void list.refetch() }} />}
    </div>
  )
}
