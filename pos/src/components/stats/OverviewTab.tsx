import { useState } from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import {
  ArrowRight, Banknote, Boxes, ChartColumn, ChartPie, Coins, CreditCard, Gift, HandCoins, Package, ReceiptText, Repeat, ShoppingCart,
  TrendingUp, Truck, Undo2, UserCheck, Users, Wallet, Warehouse,
} from 'lucide-react'
import { activeStore, useAuth } from '../../lib/auth'
import { analyticsApi } from '../../lib/analyticsApi'
import type { ActivityCustomer, AnalyticsFilters, Kpi, Overview, TopProduct } from '../../lib/analyticsTypes'
import { dateTime } from '../../lib/dates'
import { num, tenge } from '../../lib/money'
import { useT, type TranslationKey } from '../../lib/i18n'
import { Donut, type Slice } from './Charts'
import { ComboChart } from './ComboChart'
import { BlockError, Card, Empty, KpiCard, presetOf, presetRange, Segmented, Skeleton, type Preset } from './statsUi'

/** Төлем түрлерінің тұрақты түстері (§11): кезең ауысса да өзгермейді. */
export const PAY_COLORS: Record<string, string> = { Cash: '#1261d8', Card: '#38bdf8', Qr: '#8b5cf6', Transfer: '#14b8a6', Debt: '#f59e0b' }
const CHART_PRESETS: Preset[] = ['today', 'yesterday', 'week', 'days30', 'thisMonth', 'quarter', 'year']

export function OverviewTab({ filters }: { filters: AnalyticsFilters }) {
  const t = useT()
  const navigate = useNavigate()
  const location = useLocation()
  const store = useAuth(activeStore)
  const [inactiveDays, setInactiveDays] = useState(30)
  const overview = useQuery({
    queryKey: ['analytics', 'overview', store?.id, filters, inactiveDays],
    queryFn: () => analyticsApi.overview(filters, { inactiveDays }),
    placeholderData: keepPreviousData,
  })
  const trend = useQuery({ queryKey: ['analytics', 'trend', store?.id, filters], queryFn: () => analyticsApi.trend(filters), placeholderData: keepPreviousData })
  const o = overview.data
  const k = (key: string) => o?.kpis.find((x) => x.key === key)
  const go = (path: string) => navigate(`${path}${path.includes('?') ? '&' : '?'}${location.search.slice(1)}`)
  // Операциялық «Продажи» бөліміне: ағымдағы дүкен, кезең сол.
  const sales = (extra = '') => navigate(`/sales?period=custom&from=${filters.from}&to=${filters.to}${extra}`)
  const multiStore = filters.storeIds.length > 1
  const setPeriod = (p: Preset) => {
    const r = presetRange(p)
    navigate({ search: new URLSearchParams({ ...Object.fromEntries(new URLSearchParams(location.search)), from: r.from, to: r.to }).toString() }, { replace: true })
  }

  return (
    <div className={`flex flex-col gap-4 transition-opacity ${overview.isFetching && overview.data ? 'opacity-75' : ''}`} aria-busy={overview.isFetching}>
      {overview.isFetching && overview.data && <span className="sr-only">{t('stats.updating')}</span>}
      {overview.isError && <BlockError message={overview.error.message} onRetry={() => void overview.refetch()} />}

      {/* KPI ряд 1 */}
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <KpiCard k={k('revenue')} label={t('stats.kpi.revenueSales')} icon={<Wallet size={22} />} tone="blue" compare={filters.compare}
          info={t('stats.info.revenue')} onClick={() => go('/statistics/sales')} />
        <KpiCard k={k('salesCount')} label={t('stats.kpi.salesCount')} icon={<ShoppingCart size={22} />} tone="green" kind="count" compare={filters.compare}
          info={t('stats.info.salesCount')} onClick={() => go('/statistics/sales')} />
        <KpiCard k={k('averageCheck')} label={t('stats.kpi.averageCheck')} icon={<ReceiptText size={22} />} tone="violet" compare={filters.compare}
          info={k('averageCheck') ? t('stats.info.averageCheckN', { sum: tenge(k('averageCheck')!.numerator ?? 0), n: num(k('averageCheck')!.denominator ?? 0) }) : t('stats.info.averageCheck')}
          onClick={() => go('/statistics/sales')} />
        <KpiCard k={k('returns')} label={t('stats.kpi.returns')} icon={<Undo2 size={22} />} tone="coral" kind="count" compare={filters.compare} invert
          info={t('stats.info.returns')} onClick={() => sales('&hasReturn=yes')} />
      </div>

      {/* KPI ряд 2 */}
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3 2xl:grid-cols-6">
        <KpiCard small k={k('netSales')} label={t('stats.kpi.netSales')} icon={<TrendingUp size={20} />} tone="green" compare={filters.compare}
          info={t('stats.info.netSales')} onClick={() => go('/statistics/sales')} />
        <KpiCard small k={k('customersInBase')} label={t('stats.kpi.customersInBase')} icon={<Users size={20} />} tone="blue" kind="count" compare={filters.compare}
          info={t('stats.info.customersInBase')} onClick={() => go('/statistics/customers')}
          sub={k('customersInBase') && <div className="mt-0.5 text-[11px] text-ink-3">{t('stats.buyersInPeriod', { n: num(k('customersInBase')!.numerator ?? 0) })}</div>} />
        <KpiCard small k={k('repeatCustomers')} label={t('stats.kpi.repeatCustomers')} icon={<Repeat size={20} />} tone="sky" kind="count" compare={filters.compare}
          info={k('repeatCustomers') ? t('stats.info.repeatN', { a: num(k('repeatCustomers')!.numerator ?? 0), b: num(k('repeatCustomers')!.denominator ?? 0) }) : undefined}
          onClick={() => go('/statistics/customers?segment=repeat')} />
        <KpiCard small k={k('bonusRedeemed')} label={t('stats.kpi.bonusRedeemed')} icon={<Gift size={20} />} tone="pink" kind="points" compare={filters.compare}
          info={t('stats.info.bonusRedeemed')} onClick={() => go('/statistics/bonus')} />
        <KpiCard small k={k('stock')} label={t('stats.kpi.stock')} icon={<Boxes size={20} />} tone="amber" kind="qty" compare="none"
          info={t('stats.info.stock')} onClick={() => go('/statistics/inventory')}
          sub={k('stock') && <div className="mt-0.5 text-[11px] text-ink-3">{t('stats.stockSub', { n: num(k('stock')!.numerator ?? 0) })}
            {k('stock')!.amount !== null ? ` · ${tenge(k('stock')!.amount!)}` : ''}</div>} />
        <KpiCard small k={k('customerDebt')} label={t('stats.kpi.customerDebt')} icon={<HandCoins size={20} />} tone="blue" compare="none" invert
          info={t('stats.info.customerDebt')} onClick={() => navigate('/sales?status=Debt&period=all')}
          sub={k('customerDebt') && <div className="mt-0.5 text-[11px] text-ink-3">{t('stats.debtSub', { n: num(k('customerDebt')!.numerator ?? 0) })}</div>} />
      </div>

      {/* Графиктер */}
      <div className="grid gap-4 xl:grid-cols-[1.75fr_1fr]">
        <Card title={t('stats.trend')} icon={<ChartColumn size={18} />} info={t('stats.info.trend')}
          action={(
            <div className="hidden flex-wrap gap-1 md:flex">
              {CHART_PRESETS.map((p) => (
                <button key={p} type="button" onClick={() => setPeriod(p)}
                  className={`rounded-lg px-2.5 py-1 text-[12px] font-semibold ${presetOf(filters.from, filters.to) === p ? 'bg-brand text-white' : 'bg-field text-ink-2 hover:text-ink'}`}>
                  {t(`stats.preset.${p}` as TranslationKey)}
                </button>
              ))}
            </div>
          )}>
          {trend.isError ? <BlockError message={trend.error.message} onRetry={() => void trend.refetch()} />
            : !trend.data ? <Skeleton className="h-64" />
            : (
              <>
                <ComboChart
                  buckets={trend.data.data.map((d) => ({ start: d.bucketStart, end: d.bucketEnd }))}
                  granularity={trend.data.granularity}
                  incompleteLast={trend.data.includesToday}
                  leftTitle={t('stats.axis.revenue')} rightTitle={t('stats.axis.count')}
                  onBucketClick={multiStore ? undefined : (i) => {
                    const d = trend.data!.data[i]
                    navigate(`/sales?period=custom&from=${d.bucketStart}&to=${d.bucketEnd}`)
                  }}
                  series={[
                    { key: 'revenue', label: t('stats.series.revenueTg'), kind: 'money', style: 'bar', color: '#93c5fd', values: trend.data.data.map((d) => d.revenue) },
                    { key: 'count', label: t('stats.series.salesCount'), kind: 'count', style: 'line', color: '#1261d8', values: trend.data.data.map((d) => d.salesCount) },
                  ]} />
                <p className="mt-1 text-[11px] text-ink-3">
                  {t('stats.updatedAt', { time: dateTime(trend.data.generatedAt) })}{trend.data.includesToday ? ` · ${t('stats.todayPartial')}` : ''}
                  {multiStore ? ` · ${t('stats.clickOneStore')}` : ''}
                </p>
              </>
            )}
        </Card>
        <PaymentsCard o={o} loading={!o} onPick={multiStore ? undefined : (m) => sales(`&method=${m}`)} />
      </div>

      {/* Орта қатар */}
      <div className="grid gap-4 lg:grid-cols-2 2xl:grid-cols-3">
        <TopProductsCard o={o} onAll={() => go('/statistics/products')} />
        <CustomersCard o={o} onAll={() => go('/statistics/customers')} />
        <ActivityCard o={o} inactiveDays={inactiveDays} onInactiveDays={setInactiveDays} onMore={() => go('/statistics/customers')} />
      </div>

      {/* Төменгі қатар */}
      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
        <SummaryCard title={t('stats.block.inventory')} icon={<Warehouse size={18} />} onMore={() => go('/statistics/inventory')}
          items={o ? [
            { label: t('stats.inv.withStock'), value: num(o.inventory.withStock), onClick: () => navigate('/warehouse?availability=in') },
            { label: t('stats.inv.withoutStock'), value: num(o.inventory.withoutStock), onClick: () => navigate('/warehouse?availability=out') },
            { label: t('stats.inv.belowMin'), value: num(o.inventory.belowMin), tone: o.inventory.belowMin > 0 ? 'text-amber-600' : undefined, onClick: () => navigate('/warehouse?availability=belowMin') },
            { label: t('stats.inv.value'), value: o.inventory.value !== null ? tenge(o.inventory.value) : t('stats.unavailable') },
          ] : null} />
        <SummaryCard title={t('stats.block.suppliers')} icon={<Truck size={18} />} onMore={() => go('/statistics/suppliers')}
          items={o ? [
            { label: t('stats.sup.purchases'), value: tenge(o.suppliers.purchases) },
            { label: t('stats.sup.documents'), value: num(o.suppliers.receiptDocuments) },
            { label: t('stats.sup.paid'), value: tenge(o.suppliers.paid) },
            { label: t('stats.sup.debt'), value: tenge(o.suppliers.debt), tone: o.suppliers.debt > 0 ? 'text-danger' : undefined },
          ] : null} />
        <SummaryCard title={t('stats.block.finance')} icon={<Banknote size={18} />} onMore={o?.finance ? () => go('/statistics/finance') : undefined}
          items={o ? (o.finance ? [
            { label: t('stats.fin.inflow'), value: tenge(o.finance.inflow), tone: 'text-success' },
            { label: t('stats.fin.outflow'), value: tenge(o.finance.outflow), tone: 'text-danger' },
            { label: t('stats.fin.net'), value: tenge(o.finance.netFlow) },
            { label: t('stats.fin.customerDebt'), value: tenge(o.finance.customerDebt) },
          ] : [{ label: t('stats.noFinanceRight'), value: '—' }]) : null}
          hint={o?.finance ? t('stats.fin.notProfit') : undefined} />
      </div>
    </div>
  )
}

function PaymentsCard({ o, loading, onPick }: { o: Overview | undefined; loading: boolean; onPick?: (method: string) => void }) {
  const t = useT()
  if (loading || !o) return <Card title={t('stats.payments')} icon={<CreditCard size={18} />}><Skeleton className="h-64" /></Card>
  const items = o.paymentMethods.items
  const slices: Slice[] = items.map((m) => ({
    key: m.method, label: t(`pay.${m.method}` as TranslationKey), value: m.amount, color: PAY_COLORS[m.method] ?? '#94a3b8',
    extra: `${num(m.percent)}% · ${t('stats.receiptsN', { n: m.receipts })}`,
  }))
  return (
    <Card title={t('stats.payments')} icon={<ChartPie size={18} />} info={t('stats.info.payments')}>
      {o.paymentMethods.total === 0 ? <Empty text={t('stats.noPayments')} /> : (
        <>
          <div className="flex flex-col items-center gap-4 sm:flex-row">
            <Donut slices={slices} center={tenge(o.paymentMethods.total)} sub={t('stats.paymentsTotal')} size={170} />
            <ul className="flex w-full min-w-0 flex-1 flex-col gap-1.5 text-[13px]">
              {slices.map((s) => (
                <li key={s.key}>
                  <button type="button" disabled={!onPick || s.value === 0} onClick={() => onPick?.(s.key)}
                    className="flex w-full items-center gap-2 rounded-lg px-1.5 py-1 text-left enabled:hover:bg-field">
                    <span className="size-2.5 shrink-0 rounded-full" style={{ background: s.color }} />
                    <span className="flex-1 truncate text-ink-2">{s.label}</span>
                    <span className="w-12 text-right text-ink-3">{num(items.find((x) => x.method === s.key)!.percent)}%</span>
                    <b className="w-28 text-right whitespace-nowrap">{tenge(s.value)}</b>
                  </button>
                </li>
              ))}
            </ul>
          </div>
          {o.paymentMethods.mixedReceipts > 0 && (
            <p className="mt-2 text-[11px] text-ink-3">{t('stats.mixedNote', { n: o.paymentMethods.mixedReceipts })}</p>
          )}
        </>
      )}
    </Card>
  )
}

function TopProductsCard({ o, onAll }: { o: Overview | undefined; onAll: () => void }) {
  const t = useT()
  const [by, setBy] = useState<'qty' | 'amount' | 'profit'>('qty')
  const list: TopProduct[] = !o ? [] : by === 'qty' ? o.topProducts.byQuantity : by === 'amount' ? o.topProducts.byAmount : o.topProducts.byProfit ?? []
  return (
    <Card title={t('stats.topProducts')} icon={<Package size={18} />}
      action={<button type="button" onClick={onAll} className="flex items-center gap-1 text-[12px] font-semibold text-brand">{t('stats.allProducts')}<ArrowRight size={14} /></button>}>
      <Segmented value={by} onChange={setBy}
        options={[{ key: 'qty', label: t('stats.byQty') }, { key: 'amount', label: t('stats.byAmount') }, { key: 'profit', label: t('stats.byProfit') }]}
        disabled={{ profit: o && !o.topProducts.profitAvailable ? t('stats.profitDisabled') : undefined }} />
      {!o ? <Skeleton className="mt-3 h-48" /> : list.length === 0 ? (
        <Empty text={t('stats.noProductSales')} action={<Link to="/products" className="text-[12px] font-semibold text-brand">{t('stats.openProducts')}</Link>} />
      ) : (
        <table className="mt-3 w-full text-[13px]">
          <thead className="text-[11px] text-ink-3">
            <tr><th className="pb-1.5 text-left font-medium">#</th><th className="pb-1.5 text-left font-medium">{t('stats.col.product')}</th>
              <th className="pb-1.5 text-right font-medium">{t('stats.col.qty')}</th>
              <th className="pb-1.5 text-right font-medium">{by === 'profit' ? t('stats.col.profit') : t('stats.col.sum')}</th></tr>
          </thead>
          <tbody>
            {list.map((p, i) => (
              <tr key={p.productId} className="border-t border-line">
                <td className="py-2 pr-2 text-ink-3">{i + 1}</td>
                <td className="py-2 pr-2">
                  <Link to={`/products/${p.productId}`} className="flex items-center gap-2 hover:text-brand">
                    {p.imageUrl ? <img src={p.imageUrl} alt="" className="size-8 shrink-0 rounded-lg object-cover" /> : <span className="size-8 shrink-0 rounded-lg bg-field" />}
                    <span className="min-w-0"><span className="block truncate font-medium">{p.name}</span>{p.article && <span className="block text-[11px] text-ink-3">{p.article}</span>}</span>
                  </Link>
                </td>
                <td className="py-2 text-right whitespace-nowrap">{num(p.quantity)} {p.unit ?? ''}</td>
                <td className="py-2 pl-2 text-right font-semibold whitespace-nowrap">{tenge(by === 'profit' ? p.profit ?? 0 : p.amount)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </Card>
  )
}

function CustomersCard({ o, onAll }: { o: Overview | undefined; onAll: () => void }) {
  const t = useT()
  const c = o?.customers
  const rate = c?.returningRate ?? null
  const R = 34, C = 2 * Math.PI * R
  const mini = (label: string, value: number, prev: number | null | undefined, icon: React.ReactNode, tone: string) => (
    <div className="flex flex-1 items-center gap-2.5 rounded-xl bg-field px-3 py-2.5">
      <span className={`flex size-9 items-center justify-center rounded-lg ${tone}`}>{icon}</span>
      <div><div className="text-[11px] text-ink-2">{label}</div><div className="text-[18px] font-extrabold">{num(value)}</div>
        {prev !== null && prev !== undefined && prev > 0 && (
          <div className={`text-[11px] font-semibold ${value >= prev ? 'text-success' : 'text-danger'}`}>{value >= prev ? '+' : ''}{num(Math.round((value - prev) / prev * 1000) / 10)}%</div>
        )}</div>
    </div>
  )
  return (
    <Card title={t('stats.customersBlock')} icon={<UserCheck size={18} />} info={t('stats.info.returning')}
      action={<button type="button" onClick={onAll} className="flex items-center gap-1 text-[12px] font-semibold text-brand">{t('stats.allCustomers')}<ArrowRight size={14} /></button>}>
      {!c ? <Skeleton className="h-56" /> : (
        <div className="flex flex-col gap-3">
          <div className="flex gap-2">
            {mini(t('stats.newCustomers'), c.new, c.newPrevious, <Users size={18} />, 'bg-success-soft text-success')}
            {mini(t('stats.repeatCustomers'), c.repeat, c.repeatPrevious, <Repeat size={18} />, 'bg-brand-soft text-brand')}
          </div>
          <div className="flex items-center gap-3">
            <svg viewBox="0 0 80 80" className="size-20 shrink-0 -rotate-90">
              <circle cx="40" cy="40" r={R} fill="none" stroke="var(--color-field)" strokeWidth="9" />
              {rate !== null && <circle cx="40" cy="40" r={R} fill="none" stroke="#1261d8" strokeWidth="9" strokeLinecap="round" strokeDasharray={`${(rate / 100) * C} ${C}`} />}
            </svg>
            <div>
              <div className="text-[22px] font-extrabold">{rate === null ? '—' : `${num(rate)}%`}</div>
              <div className="text-[12px] font-semibold">{t('stats.returningRate')}</div>
              <div className="text-[11px] text-ink-3">{rate === null ? t('stats.noDenominator') : t('stats.returningN', { a: num(c.repeat), b: num(c.buyers) })}</div>
            </div>
          </div>
          <div>
            <div className="mb-1 text-[12px] font-semibold">{t('stats.frequency')} <span className="font-normal text-ink-3">· {t('stats.frequencyBasis')}</span></div>
            {c.frequency.map((f) => (
              <div key={f.key} className="flex items-center gap-2 py-0.5 text-[12px]">
                <span className="w-16 text-ink-2">{t(`stats.freq.${f.key}` as TranslationKey)}</span>
                <span className="h-2 flex-1 overflow-hidden rounded-full bg-field"><span className="block h-full rounded-full bg-brand" style={{ width: `${f.percent}%` }} /></span>
                <span className="w-8 text-right">{num(f.customers)}</span>
                <span className="w-12 text-right text-ink-3">{num(f.percent)}%</span>
              </div>
            ))}
          </div>
          {c.unidentifiedReceipts > 0 && <p className="text-[11px] text-ink-3">{t('stats.unidentified', { n: c.unidentifiedReceipts })}</p>}
        </div>
      )}
    </Card>
  )
}

function ActivityCard({ o, inactiveDays, onInactiveDays, onMore }: { o: Overview | undefined; inactiveDays: number; onInactiveDays: (n: number) => void; onMore: () => void }) {
  const t = useT()
  const [tab, setTab] = useState<'new' | 'regular' | 'inactive'>('regular')
  const c = o?.customers
  const list: ActivityCustomer[] = !c ? [] : tab === 'new' ? c.newList : tab === 'regular' ? c.regularList : c.inactiveList
  return (
    <Card title={t('stats.activity')} icon={<Coins size={18} />}
      action={<button type="button" onClick={onMore} className="flex items-center gap-1 text-[12px] font-semibold text-brand">{t('stats.more')}<ArrowRight size={14} /></button>}>
      <div className="flex flex-wrap items-center gap-2">
        <Segmented value={tab} onChange={setTab} options={[
          { key: 'new', label: t('stats.act.new') }, { key: 'regular', label: t('stats.act.regular') }, { key: 'inactive', label: t('stats.act.inactive') },
        ]} />
        {tab === 'inactive' && (
          <select value={inactiveDays} onChange={(e) => onInactiveDays(Number(e.target.value))} aria-label={t('stats.act.inactive')}
            className="h-8 rounded-lg border border-line bg-surface px-2 text-[12px]">
            {[30, 60, 90].map((d) => <option key={d} value={d}>{t('stats.daysN', { n: d })}</option>)}
          </select>
        )}
      </div>
      {tab === 'regular' && c && <p className="mt-1.5 text-[11px] text-ink-3">{t('stats.regularRule', { n: c.regularThreshold })}</p>}
      {!c ? <Skeleton className="mt-3 h-44" /> : list.length === 0 ? <Empty text={t('stats.noCustomersHere')} /> : (
        <table className="mt-2 w-full text-[12px]">
          <thead className="text-[11px] text-ink-3">
            <tr><th className="pb-1 text-left font-medium">{t('stats.col.customer')}</th><th className="pb-1 text-left font-medium">{t('stats.col.phone')}</th>
              <th className="pb-1 text-right font-medium">{t('stats.col.purchases')}</th><th className="pb-1 text-right font-medium">{t('stats.col.sum')}</th></tr>
          </thead>
          <tbody>
            {list.map((x) => (
              <tr key={x.id} className="border-t border-line" title={t('stats.lastPurchase', { date: dateTime(x.lastAt) })}>
                <td className="max-w-28 truncate py-2 pr-2 font-medium">{x.name}</td>
                <td className="py-2 pr-2 whitespace-nowrap text-ink-2">{x.phone ?? '—'}</td>
                <td className="py-2 text-right">{num(x.purchases)}</td>
                <td className="py-2 pl-2 text-right font-semibold whitespace-nowrap">{tenge(x.amount)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </Card>
  )
}

function SummaryCard({ title, icon, items, onMore, hint }: {
  title: string; icon: React.ReactNode; onMore?: () => void; hint?: string
  items: { label: string; value: string; tone?: string; onClick?: () => void }[] | null
}) {
  const t = useT()
  return (
    <Card title={title} icon={icon}
      action={onMore && <button type="button" onClick={onMore} className="flex items-center gap-1 text-[12px] font-semibold text-brand">{t('stats.more')}<ArrowRight size={14} /></button>}>
      {!items ? <Skeleton className="h-20" /> : (
        <div className="grid grid-cols-2 gap-2">
          {items.map((it) => {
            const inner = <><div className={`truncate text-[16px] font-extrabold ${it.tone ?? ''}`}>{it.value}</div><div className="text-[11px] text-ink-3">{it.label}</div></>
            return it.onClick
              ? <button key={it.label} type="button" onClick={it.onClick} className="rounded-xl bg-field px-3 py-2 text-left hover:bg-brand-soft">{inner}</button>
              : <div key={it.label} className="rounded-xl bg-field px-3 py-2">{inner}</div>
          })}
        </div>
      )}
      {hint && <p className="mt-2 text-[11px] text-ink-3">{hint}</p>}
    </Card>
  )
}

export type { Kpi }
