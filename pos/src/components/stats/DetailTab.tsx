import { useEffect, useState, type ReactNode } from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import {
  ArrowDown, ArrowUp, Banknote, Boxes, ChartColumn, ChartPie, Coins, FileText, Gift, HandCoins, Lock, Monitor, Package, Percent, ReceiptText,
  Repeat, RotateCcw, ShoppingCart, Truck, Undo2, UserCheck, UserCog, UserMinus, Users, Wallet, Warehouse,
} from 'lucide-react'
import { Button } from '../ui/Button'
import { Select } from '../ui/Field'
import { SearchInput } from '../ui/SearchInput'
import { Pager } from '../inventory/inventoryUi'
import { activeStore, can, useAuth } from '../../lib/auth'
import { analyticsApi } from '../../lib/analyticsApi'
import type { AnalyticsFilters, AnalyticsTab, Breakdown, Column, Kpi, Row, SeriesKind } from '../../lib/analyticsTypes'
import { useBrands, useCatalogNodes } from '../../lib/catalogHooks'
import { dateTime } from '../../lib/dates'
import { inventoryApi, inventoryKeys } from '../../lib/inventoryApi'
import { num } from '../../lib/money'
import { salesApi } from '../../lib/salesApi'
import { Permission } from '../../lib/types'
import { useT, type TranslationKey } from '../../lib/i18n'
import { Donut, type Slice } from './Charts'
import { ComboChart } from './ComboChart'
import { PAY_COLORS } from './OverviewTab'
import { formatKind, periodText } from './statsFormat'
import { BlockError, Card, Empty, KpiCard, KPI_TONES, Skeleton } from './statsUi'

type Tab = Exclude<AnalyticsTab, 'overview'>

/** KPI кілті → белгі, түс, мән түрі (кейбірінде екінші мән — сома). */
const KPI_META: Record<string, { icon: ReactNode; tone: keyof typeof KPI_TONES; kind: SeriesKind | 'percent'; amountKind?: SeriesKind; invert?: boolean }> = {
  revenue: { icon: <Wallet size={20} />, tone: 'blue', kind: 'money' },
  salesCount: { icon: <ShoppingCart size={20} />, tone: 'green', kind: 'count' },
  averageCheck: { icon: <ReceiptText size={20} />, tone: 'violet', kind: 'money' },
  returns: { icon: <Undo2 size={20} />, tone: 'coral', kind: 'count', invert: true },
  netSales: { icon: <Wallet size={20} />, tone: 'green', kind: 'money' },
  discounts: { icon: <Percent size={20} />, tone: 'amber', kind: 'money' },
  productKinds: { icon: <Package size={20} />, tone: 'blue', kind: 'count' },
  units: { icon: <Boxes size={20} />, tone: 'green', kind: 'qty' },
  productRevenue: { icon: <Wallet size={20} />, tone: 'violet', kind: 'money' },
  returnedUnits: { icon: <Undo2 size={20} />, tone: 'coral', kind: 'qty', invert: true },
  grossProfit: { icon: <Coins size={20} />, tone: 'green', kind: 'money' },
  withStock: { icon: <Package size={20} />, tone: 'green', kind: 'count' },
  withoutStock: { icon: <Package size={20} />, tone: 'coral', kind: 'count' },
  belowMin: { icon: <Boxes size={20} />, tone: 'amber', kind: 'count' },
  receiptDocs: { icon: <FileText size={20} />, tone: 'blue', kind: 'count' },
  transfers: { icon: <Truck size={20} />, tone: 'sky', kind: 'count' },
  writeOffDocs: { icon: <FileText size={20} />, tone: 'coral', kind: 'count' },
  stockValue: { icon: <Warehouse size={20} />, tone: 'amber', kind: 'money' },
  purchases: { icon: <Truck size={20} />, tone: 'blue', kind: 'money' },
  supplierPaid: { icon: <Banknote size={20} />, tone: 'green', kind: 'money' },
  supplierDebt: { icon: <HandCoins size={20} />, tone: 'coral', kind: 'money', invert: true },
  activeSuppliers: { icon: <Truck size={20} />, tone: 'violet', kind: 'count' },
  inflow: { icon: <ArrowDown size={20} />, tone: 'green', kind: 'money' },
  outflow: { icon: <ArrowUp size={20} />, tone: 'coral', kind: 'money' },
  netFlow: { icon: <Wallet size={20} />, tone: 'blue', kind: 'money' },
  debtIssued: { icon: <HandCoins size={20} />, tone: 'amber', kind: 'money' },
  customerDebt: { icon: <HandCoins size={20} />, tone: 'blue', kind: 'money', invert: true },
  expenses: { icon: <Banknote size={20} />, tone: 'coral', kind: 'money' },
  bonusAccrued: { icon: <Gift size={20} />, tone: 'violet', kind: 'points' },
  bonusRedeemed: { icon: <Gift size={20} />, tone: 'pink', kind: 'points' },
  bonusAdjustments: { icon: <RotateCcw size={20} />, tone: 'amber', kind: 'points' },
  bonusBalance: { icon: <Coins size={20} />, tone: 'blue', kind: 'points' },
  salesWithBonus: { icon: <ReceiptText size={20} />, tone: 'green', kind: 'count' },
  customersInBase: { icon: <Users size={20} />, tone: 'blue', kind: 'count' },
  buyers: { icon: <UserCheck size={20} />, tone: 'green', kind: 'count' },
  newCustomers: { icon: <Users size={20} />, tone: 'green', kind: 'count' },
  repeatCustomers: { icon: <Repeat size={20} />, tone: 'sky', kind: 'count' },
  returningRate: { icon: <Repeat size={20} />, tone: 'violet', kind: 'percent' },
  avgPurchases: { icon: <ShoppingCart size={20} />, tone: 'blue', kind: 'count' },
  inactiveCustomers: { icon: <UserMinus size={20} />, tone: 'amber', kind: 'count' },
  unidentifiedReceipts: { icon: <ReceiptText size={20} />, tone: 'coral', kind: 'count' },
  activeStaff: { icon: <UserCog size={20} />, tone: 'blue', kind: 'count' },
  activeRegisters: { icon: <Monitor size={20} />, tone: 'blue', kind: 'count' },
  shifts: { icon: <Monitor size={20} />, tone: 'violet', kind: 'count' },
}

const SERIES_COLORS = ['#93c5fd', '#1261d8', '#e5484d']
const SHARE_COLORS = ['#1261d8', '#38bdf8', '#8b5cf6', '#14b8a6', '#f59e0b', '#ec4899', '#22c55e', '#94a3b8', '#f97316', '#64748b']

/** Жергілікті сүзгілердің URL кілттері (§24: чиптер, «Сбросить фильтры»). */
const LOCAL: Record<Tab, string[]> = {
  sales: ['registerId', 'staffUserId', 'method'],
  products: ['search', 'nodeId', 'brandId', 'hasReturns'],
  inventory: ['warehouseId', 'search', 'segment'],
  suppliers: ['supplierId'],
  finance: [],
  bonus: [],
  customers: ['segment', 'inactiveDays'],
  employees: ['registerId', 'method'],
  registers: ['staffUserId', 'method'],
}

export function DetailTab({ tab, filters }: { tab: Tab; filters: AnalyticsFilters }) {
  const t = useT()
  const navigate = useNavigate()
  const store = useAuth(activeStore)
  const [params, setParams] = useSearchParams()
  const [search, setSearch] = useState(params.get('search') ?? '')
  const keys = [...LOCAL[tab], 'sort', 'dir', 'page', 'pageSize']
  const extra = Object.fromEntries(keys.map((k) => [k, params.get(k) ?? undefined]))
  const update = (patch: Record<string, string | null>, resetPage = true) =>
    setParams((prev) => {
      const next = new URLSearchParams(prev)
      for (const [k, v] of Object.entries(patch)) { if (v) next.set(k, v); else next.delete(k) }
      if (resetPage) next.delete('page')
      return next
    }, { replace: true })
  useEffect(() => {
    const id = setTimeout(() => { if ((params.get('search') ?? '') !== search.trim()) update({ search: search.trim() || null }) }, 350)
    return () => clearTimeout(id)
  }, [search])

  const financeLocked = tab === 'finance' && !can(store, Permission.FinanceView)
  const q = useQuery({
    queryKey: ['analytics', 'tab', tab, store?.id, filters, extra],
    queryFn: () => analyticsApi.tab(tab, filters, extra),
    placeholderData: keepPreviousData,
    enabled: !financeLocked,
  })
  const d = q.data

  if (financeLocked) {
    return (
      <div className="flex min-h-[40vh] flex-col items-center justify-center gap-2 rounded-2xl border border-[#e4ecf7] bg-surface text-center">
        <Lock size={26} className="text-ink-3" />
        <div className="text-[16px] font-bold">{t('section.noAccess')}</div>
        <p className="max-w-sm text-[13px] text-ink-2">{t('stats.noFinanceRight')}</p>
      </div>
    )
  }

  const activeLocal = LOCAL[tab].filter((k) => params.get(k))
  const multiStore = filters.storeIds.length > 1

  return (
    <div className={`flex flex-col gap-4 transition-opacity ${q.isFetching && d ? 'opacity-75' : ''}`} aria-busy={q.isFetching}>
      <LocalFilters tab={tab} params={params} search={search} onSearch={setSearch} update={update} />
      {activeLocal.length > 0 && (
        <div className="flex flex-wrap items-center gap-2">
          {activeLocal.map((k) => (
            <span key={k} className="inline-flex items-center gap-1.5 rounded-lg bg-brand-soft px-2.5 py-1 text-[12px] font-medium text-brand">
              {t(`stats.f.${k}` as TranslationKey)}
              <button type="button" aria-label={t('common.close')} onClick={() => { if (k === 'search') setSearch(''); update({ [k]: null }) }}>×</button>
            </span>
          ))}
          <button type="button" className="text-[12px] font-semibold text-brand" onClick={() => { setSearch(''); update(Object.fromEntries(LOCAL[tab].map((k) => [k, null]))) }}>
            {t('stats.resetLocal')}
          </button>
        </div>
      )}

      {q.isError && <BlockError message={q.error.message} onRetry={() => void q.refetch()} />}

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3 2xl:grid-cols-6">
        {(d?.kpis ?? Array.from({ length: 6 }, () => undefined)).map((k, i) => {
          const meta = k ? KPI_META[k.key] ?? { icon: <ChartColumn size={20} />, tone: 'blue' as const, kind: 'count' as const } : null
          return k && meta ? (
            <KpiCard key={k.key} small k={k} label={t(`stats.k.${k.key}` as TranslationKey)} icon={meta.icon} tone={meta.tone} kind={meta.kind}
              amountKind={meta.amountKind ?? 'money'} compare={filters.compare} invert={meta.invert} sub={kpiSub(k, t)} />
          ) : <Skeleton key={i} className="h-24" />
        })}
      </div>

      {d && d.series.length > 0 && (
        <Card title={t(`stats.chart.${tab}` as TranslationKey)} icon={<ChartColumn size={18} />}>
          <ComboChart buckets={d.buckets} granularity={d.meta.granularity} incompleteLast={d.meta.includesToday}
            leftTitle={d.series.some((s) => s.kind !== 'count') ? t(`stats.axis.${d.series.find((s) => s.kind !== 'count')!.kind}` as TranslationKey) : undefined} rightTitle={t('stats.axis.count')}
            onBucketClick={tab === 'sales' && !multiStore ? (i) => navigate(`/sales?period=custom&from=${d.buckets[i].start}&to=${d.buckets[i].end}`) : undefined}
            series={d.series.map((s, i) => ({ key: s.key, label: t(`stats.s.${s.key}` as TranslationKey), kind: s.kind, style: s.style, color: SERIES_COLORS[i] ?? '#94a3b8', values: s.values }))} />
        </Card>
      )}

      {d && d.breakdowns.length > 0 && (
        <div className={`grid gap-4 ${d.breakdowns.length > 1 ? 'lg:grid-cols-2' : ''}`}>
          {d.breakdowns.map((b) => <BreakdownCard key={b.key} b={b} />)}
        </div>
      )}

      {!d && !q.isError && <Skeleton className="h-72" />}

      {d?.table && (
        <Card title={t(`stats.table.${tab}` as TranslationKey)} icon={<ChartPie size={18} />}>
          <div className="-mx-4 overflow-x-auto">
            <table className="w-full min-w-[760px] border-collapse text-[13px]">
              <thead className="bg-field">
                <tr>
                  {d.table.columns.map((c) => {
                    const sorted = d.table!.sort === c.key
                    const numeric = !['text', 'period', 'enum', 'date', 'datetime'].includes(c.kind)
                    return (
                      <th key={c.key} scope="col" className={`px-3 py-2.5 text-[12px] font-semibold text-ink-2 ${numeric ? 'text-right' : 'text-left'}`}>
                        {c.sortable ? (
                          <button type="button" className="inline-flex items-center gap-1 hover:text-ink"
                            onClick={() => update({ sort: c.key, dir: sorted && d.table!.dir === 'desc' ? 'asc' : 'desc' }, true)}>
                            {t(`stats.c.${c.key}` as TranslationKey)}
                            {sorted && (d.table!.dir === 'asc' ? <ArrowUp size={12} /> : <ArrowDown size={12} />)}
                          </button>
                        ) : t(`stats.c.${c.key}` as TranslationKey)}
                      </th>
                    )
                  })}
                </tr>
              </thead>
              <tbody>
                {d.table.rows.length === 0 && (
                  <tr><td colSpan={d.table.columns.length}>
                    <Empty text={t('stats.noDataPeriod')} action={activeLocal.length > 0
                      ? <button type="button" className="text-[12px] font-semibold text-brand" onClick={() => update(Object.fromEntries(LOCAL[tab].map((k) => [k, null])))}>{t('stats.resetLocal')}</button>
                      : undefined} />
                  </td></tr>
                )}
                {d.table.rows.map((r, i) => (
                  <tr key={i} className="border-t border-line hover:bg-field/50">
                    {d.table!.columns.map((c) => <Cell key={c.key} c={c} r={r} multiStore={multiStore} />)}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <Pager page={d.table.page} pageSize={d.table.pageSize} total={d.table.total}
            onPage={(p) => update({ page: String(p) }, false)} onPageSize={(s) => update({ pageSize: String(s) })} />
        </Card>
      )}

      {d && d.notes.length > 0 && (
        <ul className="flex flex-col gap-1 rounded-2xl border border-dashed border-line px-4 py-3 text-[12px] text-ink-2">
          {d.notes.map((n) => <li key={n}>• {t(`stats.n.${n}` as TranslationKey)}</li>)}
          <li className="text-ink-3">• {t('stats.updatedAt', { time: dateTime(d.meta.generatedAt) })} · {periodText(d.meta.from, d.meta.to)} · {d.meta.stores.map((s) => s.name).join(', ')}</li>
        </ul>
      )}
    </div>
  )
}

function kpiSub(k: Kpi, t: ReturnType<typeof useT>): ReactNode | undefined {
  const line = (text: string) => <div className="mt-0.5 text-[11px] text-ink-3">{text}</div>
  switch (k.key) {
    case 'averageCheck': return k.denominator ? line(t('stats.sub.average', { n: num(k.denominator) })) : undefined
    case 'repeatCustomers':
    case 'returningRate':
    case 'salesWithBonus': return k.denominator !== null ? line(t('stats.sub.ofN', { a: num(k.numerator ?? 0), b: num(k.denominator) })) : undefined
    case 'inflow': return line(t('stats.sub.inflow', { a: formatKind(k.numerator, 'money'), b: formatKind(k.denominator, 'money') }))
    case 'outflow': return line(t('stats.sub.outflow', { a: formatKind(k.numerator, 'money'), b: formatKind(k.denominator, 'money') }))
    case 'inactiveCustomers': return line(t('stats.sub.inactive', { n: k.numerator ?? 30 }))
    case 'customerDebt':
    case 'supplierDebt':
    case 'bonusBalance': return line(t('stats.note.asOfNow'))
    case 'units': return line(t('stats.sub.units', { a: num(k.numerator ?? 0), b: num(k.denominator ?? 0) }))
    case 'unidentifiedReceipts': return line(t('stats.sub.ofReceipts', { n: num(k.numerator ?? 0) }))
    default: return undefined
  }
}

function itemLabel(b: Breakdown, key: string, label: string | null, t: ReturnType<typeof useT>) {
  if (label) return label
  if (key === 'none') return t('stats.none')
  const map: Record<string, string> = {
    paymentMethods: `pay.${key}`, inflowByMethod: `pay.${key}`, movementTypes: `inv.mov.type.${key}`, outflowByType: `stats.enum.${key}`,
    bonusByType: `stats.enum.${key}`, frequency: `stats.freq.${key}`, paymentStatus: `stats.ps.${key}`,
  }
  return map[b.key] ? t(map[b.key] as TranslationKey) : key
}

function BreakdownCard({ b }: { b: Breakdown }) {
  const t = useT()
  const items = b.items.filter((x) => x.value !== 0)
  const color = (key: string, i: number) => (b.key === 'paymentMethods' || b.key === 'inflowByMethod' ? PAY_COLORS[key] : undefined) ?? SHARE_COLORS[i % SHARE_COLORS.length]
  const slices: Slice[] = items.map((x, i) => ({ key: x.key, label: itemLabel(b, x.key, x.label, t), value: Math.abs(x.value), color: color(x.key, i) }))
  const bars = items.length > 6 || b.key.startsWith('top') || b.key.startsWith('revenueBy')
  return (
    <Card title={t(`stats.b.${b.key}` as TranslationKey)} icon={<ChartPie size={18} />}>
      {items.length === 0 ? <Empty text={t('stats.noDataPeriod')} /> : bars ? (
        <ul className="flex flex-col gap-2 text-[13px]">
          {items.map((x, i) => (
            <li key={x.key}>
              <div className="flex justify-between gap-3"><span className="truncate">{slices[i].label}</span><b className="whitespace-nowrap">{formatKind(x.value, b.kind)}</b></div>
              <div className="mt-1 h-2 overflow-hidden rounded-full bg-field"><div className="h-full rounded-full" style={{ width: `${Math.max(2, x.percent)}%`, background: slices[i].color }} /></div>
            </li>
          ))}
        </ul>
      ) : (
        <div className="flex flex-col items-center gap-4 sm:flex-row">
          <Donut slices={slices} center={formatKind(b.total, b.kind)} size={150} />
          <ul className="flex w-full min-w-0 flex-1 flex-col gap-1.5 text-[13px]">
            {items.map((x, i) => (
              <li key={x.key} className="flex items-center gap-2">
                <span className="size-2.5 shrink-0 rounded-full" style={{ background: slices[i].color }} />
                <span className="flex-1 truncate text-ink-2">{slices[i].label}</span>
                <span className="text-ink-3">{num(x.percent)}%</span>
                <b className="w-28 text-right whitespace-nowrap">{formatKind(x.value, b.kind)}</b>
              </li>
            ))}
          </ul>
        </div>
      )}
    </Card>
  )
}

function Cell({ c, r, multiStore }: { c: Column; r: Row; multiStore: boolean }) {
  const t = useT()
  const v = r[c.key]
  const numeric = !['text', 'period', 'enum', 'date', 'datetime'].includes(c.kind)
  let text: ReactNode
  switch (c.kind) {
    case 'period': text = periodText(String(r.bucketStart), String(r.bucketEnd)); break
    case 'datetime': text = v ? dateTime(String(v)) : '—'; break
    case 'enum': text = v ? t((c.key === 'method' ? `pay.${v}` : `stats.enum.${v}`) as TranslationKey) : '—'; break
    case 'text': text = v === null || v === undefined || v === '' ? '—' : String(v); break
    default: text = formatKind(v as number | null, c.kind)
  }
  const href = c.link && !(multiStore && c.link.startsWith('/sales')) && !c.link.match(/\{(\w+)\}/g)?.some((m) => r[m.slice(1, -1)] === null || r[m.slice(1, -1)] === undefined)
    ? c.link.replace(/\{(\w+)\}/g, (_, k: string) => String(r[k]))
    : null
  const neg = typeof v === 'number' && v < 0 && c.kind === 'money'
  return (
    <td className={`px-3 py-2.5 whitespace-nowrap ${numeric ? 'text-right' : ''} ${neg ? 'text-danger' : ''}`}>
      {href ? <Link to={href} className="font-medium text-brand hover:underline">{text}</Link> : text}
    </td>
  )
}

function LocalFilters({ tab, params, search, onSearch, update }: {
  tab: Tab
  params: URLSearchParams
  search: string
  onSearch: (v: string) => void
  update: (patch: Record<string, string | null>) => void
}) {
  const t = useT()
  const store = useAuth(activeStore)
  const needSalesFilters = tab === 'sales' || tab === 'employees' || tab === 'registers'
  const salesFilters = useQuery({ queryKey: ['sales', 'filters', store?.id], queryFn: salesApi.filters, enabled: needSalesFilters && can(store, Permission.SalesView), retry: false })
  const nodes = useCatalogNodes().data ?? []
  const brands = useBrands().data ?? []
  const warehouses = useQuery({ queryKey: inventoryKeys.warehouses, queryFn: () => inventoryApi.warehouses(), enabled: tab === 'inventory' })
  const suppliers = useQuery({ queryKey: inventoryKeys.suppliers, queryFn: inventoryApi.suppliers, enabled: tab === 'suppliers' && can(store, Permission.InventorySuppliers) })
  const get = (k: string) => params.get(k) ?? ''
  const sel = (k: string, placeholder: string, options: { id: string; name: string }[], width = 'w-auto min-w-48') => (
    <Select className={width} value={get(k)} onChange={(e) => update({ [k]: e.target.value || null })} aria-label={placeholder}>
      <option value="">{placeholder}</option>
      {options.map((o) => <option key={o.id} value={o.id}>{o.name}</option>)}
    </Select>
  )
  const methods = ['Cash', 'Card', 'Qr', 'Transfer', 'Debt'].map((m) => ({ id: m, name: t(`pay.${m}` as TranslationKey) }))

  const controls: ReactNode[] = []
  if (tab === 'products' || tab === 'inventory') controls.push(<SearchInput key="s" className="w-full sm:w-72" value={search} onChange={onSearch} placeholder={t('stats.searchProducts')} />)
  if ((tab === 'sales' || tab === 'employees') && salesFilters.data) controls.push(<span key="r">{sel('registerId', t('sales.filter.allRegisters'), salesFilters.data.registers)}</span>)
  if ((tab === 'sales' || tab === 'registers') && salesFilters.data) controls.push(<span key="c">{sel('staffUserId', t('sales.filter.allCashiers'), salesFilters.data.cashiers)}</span>)
  if (needSalesFilters) controls.push(<span key="m">{sel('method', t('sales.filter.allMethods'), methods)}</span>)
  if (tab === 'products') {
    controls.push(<span key="n">{sel('nodeId', t('product.filter.allCategories'), nodes.filter((n) => n.parentId === null).map((n) => ({ id: n.id, name: n.name })))}</span>)
    controls.push(<span key="b">{sel('brandId', t('product.filter.allBrands'), brands.map((b) => ({ id: b.id, name: b.name })), 'w-auto min-w-44')}</span>)
    controls.push(<span key="h">{sel('hasReturns', t('sales.filter.anyReturn'), [{ id: 'true', name: t('sales.filter.withReturn') }, { id: 'false', name: t('sales.filter.withoutReturn') }], 'w-auto min-w-44')}</span>)
  }
  if (tab === 'inventory') {
    controls.push(<span key="w">{sel('warehouseId', t('inv.stock.allWarehouses'), (warehouses.data ?? []).filter((w) => w.isActive).map((w) => ({ id: w.id, name: w.name })))}</span>)
    controls.push(<span key="g">{sel('segment', t('inv.stock.anyAvailability'), [
      { id: 'in', name: t('inv.stock.inStock') }, { id: 'out', name: t('inv.stock.outOfStock') }, { id: 'belowMin', name: t('inv.stock.belowMin') }])}</span>)
  }
  if (tab === 'suppliers' && suppliers.data) controls.push(<span key="sp">{sel('supplierId', t('inv.receipts.allSuppliers'), suppliers.data.map((s) => ({ id: s.id, name: s.name })), 'w-auto min-w-56')}</span>)
  if (tab === 'customers') {
    controls.push(<span key="seg">{sel('segment', t('stats.seg.buyers'), (['new', 'repeat', 'regular', 'inactive'] as const).map((s) => ({ id: s, name: t(`stats.seg.${s}`) })))}</span>)
    controls.push(<span key="d">{sel('inactiveDays', t('stats.daysN', { n: 30 }), [60, 90].map((d) => ({ id: String(d), name: t('stats.daysN', { n: d }) })), 'w-auto min-w-36')}</span>)
  }
  if (controls.length === 0) return null
  return (
    <div className="flex flex-wrap items-center gap-2">
      {controls}
      <Button variant="secondary" className="ml-auto" icon={<RotateCcw size={16} />}
        onClick={() => { onSearch(''); update(Object.fromEntries(LOCAL[tab].map((k) => [k, null]))) }}>{t('stats.resetLocal')}</Button>
    </div>
  )
}
