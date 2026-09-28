import { useState, type ReactNode } from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import {
  AlertTriangle, ArrowDownRight, ArrowRight, ArrowUpRight, Banknote, CalendarRange, Coins, Crown, Gift, HandCoins, ImageOff,
  PackageX, RefreshCw, Repeat, ShoppingCart, UserPlus, Users, Wallet,
} from 'lucide-react'
import { activeStore, useAuth } from '../lib/auth'
import { dashboardApi, type Dashboard, type Kpi } from '../lib/dashboardApi'
import { addDays, dateTime, isoDate } from '../lib/dates'
import { num, tenge } from '../lib/money'
import { useT, type TranslationKey } from '../lib/i18n'
import { Donut, Legend, SalesChart, type Slice } from '../components/stats/Charts'

type Period = 'today' | 'yesterday' | 'week' | 'month' | 'custom'

/** ТЗ §5: апта — ағымдағы күнтізбелік апта (дүйсенбіден), ай — айдың 1-інен бүгінге дейін. */
function range(p: Period, custom: { from: string; to: string }) {
  const today = new Date()
  switch (p) {
    case 'yesterday': return { from: isoDate(addDays(today, -1)), to: isoDate(addDays(today, -1)) }
    case 'week': return { from: isoDate(addDays(today, -((today.getDay() + 6) % 7))), to: isoDate(today) }
    case 'month': return { from: isoDate(new Date(today.getFullYear(), today.getMonth(), 1)), to: isoDate(today) }
    case 'custom': return custom
    default: return { from: isoDate(today), to: isoDate(today) }
  }
}

const PAY_COLORS: Record<string, string> = { Cash: '#1570ff', Card: '#22c55e', Qr: '#f59e0b', Transfer: '#8b5cf6', Debt: '#94a3b8' }
const CAT_COLORS = ['#1570ff', '#22c55e', '#f59e0b', '#8b5cf6', '#ec4899', '#94a3b8']

const KPI_META: Record<Kpi['key'], { icon: typeof Wallet; tone: string; bg: string }> = {
  revenue: { icon: Wallet, tone: 'text-success', bg: 'bg-success-soft' },
  sales: { icon: ShoppingCart, tone: 'text-brand', bg: 'bg-brand-soft' },
  average: { icon: Coins, tone: 'text-amber-600', bg: 'bg-amber-50' },
  customers: { icon: Users, tone: 'text-violet-600', bg: 'bg-violet-50' },
  newCustomers: { icon: UserPlus, tone: 'text-emerald-600', bg: 'bg-emerald-50' },
  bonusAccrued: { icon: Gift, tone: 'text-danger', bg: 'bg-danger-soft' },
}

/**
 * «Статистика» — иесінің бас экраны (ТЗ «Статистика»). Бір snapshot: кезең не дүкен ауысса, барлық блок
 * бірге жаңарады (§5, AC-17/18). Қате болса, бұрынғы деректер өшірілмейді, «Повторить» шығады (§20).
 */
export function StatisticsPage() {
  const t = useT()
  const me = useAuth((s) => s.me)
  const store = useAuth(activeStore)
  const [period, setPeriod] = useState<Period>('today')
  const [custom, setCustom] = useState(() => ({ from: isoDate(addDays(new Date(), -6)), to: isoDate(new Date()) }))
  const { from, to } = range(period, custom)
  const query = useQuery({
    queryKey: ['dashboard', store?.id, from, to],
    queryFn: () => dashboardApi.get(from, to),
    placeholderData: keepPreviousData,
  })
  const d = query.data
  const compare = t(`stats.compare.${period}` as TranslationKey)
  const hour = new Date().getHours()
  const greeting = t(hour < 12 ? 'stats.morning' : hour < 18 ? 'stats.day' : 'stats.evening', { name: me?.firstName ?? '' })

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-end gap-3">
        <div className="mr-auto">
          <h2 className="text-[20px] font-bold">{greeting}</h2>
          <p className="text-[14px] text-ink-2">{t('stats.subtitle')}</p>
        </div>
        <div className="flex w-full flex-wrap items-center gap-2 sm:w-auto">
          {/* Телефонда бес батырма бір жолға тең бөлінеді — экраннан шықпайды. */}
          <div className="grid w-full grid-cols-5 gap-1 rounded-xl bg-surface p-1 shadow-sm sm:flex sm:w-auto">
            {(['today', 'yesterday', 'week', 'month', 'custom'] as const).map((p) => (
              <button key={p} type="button" onClick={() => setPeriod(p)}
                className={`truncate rounded-lg px-1 py-2 text-[13px] font-medium sm:px-3.5 sm:text-[14px] ${period === p ? 'bg-brand text-white' : 'text-ink-2 hover:text-ink'}`}>
                {t(`stats.period.${p}` as TranslationKey)}
              </button>
            ))}
          </div>
          {period === 'custom' && (
            <div className="flex items-center gap-1.5 rounded-xl bg-surface px-2 py-1 shadow-sm">
              <CalendarRange size={16} className="text-ink-3" />
              <input type="date" value={custom.from} max={custom.to} onChange={(e) => setCustom((c) => ({ ...c, from: e.target.value }))}
                className="h-9 rounded-lg bg-field px-2 text-[13px] outline-none" />
              <span className="text-ink-3">—</span>
              <input type="date" value={custom.to} min={custom.from} max={isoDate(new Date())} onChange={(e) => setCustom((c) => ({ ...c, to: e.target.value }))}
                className="h-9 rounded-lg bg-field px-2 text-[13px] outline-none" />
            </div>
          )}
          <button type="button" onClick={() => void query.refetch()}
            className="flex h-11 items-center gap-2 rounded-xl bg-surface px-3.5 text-[14px] font-medium shadow-sm hover:bg-field">
            <RefreshCw size={16} className={query.isFetching ? 'animate-spin' : ''} /> {t('stats.refresh')}
          </button>
          {d && <span className="text-[12px] text-ink-3">{t('stats.updated', { time: dateTime(d.meta.generatedAt).slice(11) })}</span>}
        </div>
      </div>

      {query.isError && (
        <div className="flex items-center justify-between gap-3 rounded-xl bg-danger-soft px-4 py-3 text-[14px] text-danger">
          <span>{query.error.message}</span>
          <button type="button" onClick={() => void query.refetch()} className="font-semibold underline">{t('common.retry')}</button>
        </div>
      )}

      {!d ? <Skeleton /> : (
        <div className={`flex flex-col gap-4 transition-opacity ${query.isFetching ? 'opacity-70' : ''}`}>
          <div className="grid grid-cols-2 gap-3 md:grid-cols-3 xl:grid-cols-6">
            {d.kpi.map((k) => <KpiCard key={k.key} k={k} compare={compare} />)}
          </div>

          {/* ТЗ §22: desktop — 3 баған, планшет — 2, телефон — 1. */}
          <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-[1.35fr_1fr_1fr]">
            <Card className="md:col-span-2 xl:col-span-1" title={t('stats.salesByTime')} hint={t(d.meta.granularity === 'hour' ? 'stats.byHour' : 'stats.byDay')}>
              {d.chart.every((p) => p.count === 0 && p.revenue === 0)
                ? <Empty text={t('stats.noSales')} />
                : <SalesChart points={d.chart} revenueLabel={t('stats.revenueTg')} countLabel={t('stats.receiptsCount')}
                    receiptsText={(n) => t('finance.receipts', { n })} />}
            </Card>
            <PaymentsCard d={d} />
            <CategoriesCard d={d} />
          </div>

          <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
            <FinancialCard d={d} />
            <CashCard d={d} />
            <TopProductsCard d={d} />
          </div>

          <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
            <CustomersCard d={d} compare={compare} />
            <BonusCard d={d} />
            <AttentionCard d={d} />
          </div>
        </div>
      )}
    </div>
  )
}

function Card({ title, hint, action, className = '', children }: {
  title: string; hint?: string; action?: ReactNode; className?: string; children: ReactNode
}) {
  return (
    <section className={`flex min-w-0 flex-col rounded-2xl bg-surface p-4 shadow-sm ${className}`}>
      <div className="mb-3 flex items-center gap-2">
        <h3 className="text-[16px] font-bold">{title}</h3>
        {hint && <span className="rounded-md bg-field px-2 py-0.5 text-[12px] text-ink-2">{hint}</span>}
        {action && <div className="ml-auto">{action}</div>}
      </div>
      {children}
    </section>
  )
}

function Change({ value, compare }: { value: number | null; compare?: string }) {
  if (value == null) return <span className="text-[12px] text-ink-3">— {compare}</span>
  const up = value >= 0
  const Icon = up ? ArrowUpRight : ArrowDownRight
  return (
    <span className="flex items-center gap-1 text-[12px]">
      <span className={`flex items-center font-semibold ${up ? 'text-success' : 'text-danger'}`}><Icon size={14} />{up ? '+' : ''}{num(value)}%</span>
      {compare && <span className="text-ink-3">{compare}</span>}
    </span>
  )
}

function KpiCard({ k, compare }: { k: Kpi; compare: string }) {
  const t = useT()
  const { icon: Icon, tone, bg } = KPI_META[k.key]
  const value = k.key === 'revenue' || k.key === 'average' ? tenge(k.value)
    : k.key === 'sales' ? t('finance.receipts', { n: k.value })
    : k.key === 'bonusAccrued' ? `${num(k.value)} Б` : num(k.value)
  return (
    <div className="rounded-2xl bg-surface p-4 shadow-sm">
      <div className="flex items-center gap-2.5">
        <span className={`flex size-10 items-center justify-center rounded-xl ${bg} ${tone}`}><Icon size={20} /></span>
        <span className="text-[13px] font-medium text-ink-2">{t(`stats.kpi.${k.key}` as TranslationKey)}</span>
      </div>
      <div className="mt-2 truncate text-[22px] font-extrabold tracking-tight">{value}</div>
      <Change value={k.changePercent} compare={compare} />
    </div>
  )
}

function PaymentsCard({ d }: { d: Dashboard }) {
  const t = useT()
  const slices: Slice[] = d.paymentMethods.filter((m) => m.amount > 0 || m.key !== 'Debt').map((m) => ({
    key: m.key, label: t(`pay.${m.key}` as TranslationKey), value: m.amount, color: PAY_COLORS[m.key] ?? '#94a3b8', extra: `${num(m.percent)}%`,
  }))
  const total = d.paymentMethods.reduce((s, m) => s + m.amount, 0)
  return (
    <Card title={t('stats.payments')}>
      {total === 0 ? <Empty text={t('stats.noSales')} /> : (
        <div className="flex items-center gap-4">
          <Donut slices={slices} center={tenge(total)} sub={t('stats.total')} size={128} />
          <Legend slices={slices} format={(s) => tenge(s.value)} />
        </div>
      )}
    </Card>
  )
}

function CategoriesCard({ d }: { d: Dashboard }) {
  const t = useT()
  const slices: Slice[] = d.categories.map((c, i) => ({
    key: c.key, label: c.key === 'other' ? t('stats.otherCategories') : c.label === '—' ? t('product.noClassification') : c.label,
    value: c.amount, color: CAT_COLORS[i % CAT_COLORS.length], extra: `${num(c.percent)}%`,
  }))
  const total = slices.reduce((s, x) => s + x.value, 0)
  return (
    <Card title={t('stats.categories')}>
      {total === 0 ? <Empty text={t('stats.noSales')} /> : (
        <div className="flex items-center gap-4">
          <Donut slices={slices} center={tenge(total)} sub={t('stats.total')} size={128} />
          <Legend slices={slices} format={(s) => tenge(s.value)} />
        </div>
      )}
    </Card>
  )
}

function Row({ label, value, strong, tone }: { label: string; value: string; strong?: boolean; tone?: string }) {
  return (
    <div className={`flex justify-between py-1.5 text-[14px] ${strong ? 'font-bold' : ''}`}>
      <span className={strong ? '' : 'text-ink-2'}>{label}</span><span className={tone}>{value}</span>
    </div>
  )
}

function FinancialCard({ d }: { d: Dashboard }) {
  const t = useT()
  const f = d.financial
  return (
    <Card title={t('stats.financial')}>
      <Row label={t('stats.kpi.revenue')} value={tenge(f.revenue)} />
      <Row label={t('stats.cost')} value={`− ${tenge(f.cost)}`} />
      <Row label={t('stats.expenses')} value={f.expenses == null ? t('stats.noData') : `− ${tenge(f.expenses)}`} tone={f.expenses == null ? 'text-ink-3' : ''} />
      <div className={`mt-1 flex justify-between rounded-xl px-3 py-2.5 text-[15px] font-bold ${f.profit >= 0 ? 'bg-success-soft text-success' : 'bg-danger-soft text-danger'}`}>
        <span>{t('stats.profit')}</span><span>{tenge(f.profit)}</span>
      </div>
      {!f.costComplete && <p className="mt-2 text-[12px] text-amber-700">{t('stats.costIncomplete')}</p>}
      {f.expenses == null && <p className="mt-2 text-[12px] text-ink-3">{t('stats.expensesLater')}</p>}
    </Card>
  )
}

function CashCard({ d }: { d: Dashboard }) {
  const t = useT()
  const c = d.cash
  return (
    <Card title={t('stats.cash')} action={<Banknote size={18} className="text-success" />}>
      <Row label={t('stats.cashActual')} value={c.actualBalance == null ? t('stats.noData') : tenge(c.actualBalance)} tone={c.actualBalance == null ? 'text-ink-3' : ''} />
      <Row label={t('stats.cashIn')} value={tenge(c.received)} />
      <Row label={t('stats.cashOut')} value={`− ${tenge(c.paid)}`} />
      <Row label={t('stats.cashExpected')} value={tenge(c.expected)} strong />
      <p className="mt-2 text-[12px] text-ink-3">{t('stats.cashHint')}</p>
    </Card>
  )
}

function TopProductsCard({ d }: { d: Dashboard }) {
  const t = useT()
  return (
    <Card className="md:col-span-2 xl:col-span-1" title={t('stats.topProducts')} action={<Link to="/products" className="flex items-center gap-1 text-[13px] font-medium text-brand">{t('stats.allProducts')} <ArrowRight size={14} /></Link>}>
      {d.topProducts.length === 0 ? <Empty text={t('stats.noSales')} /> : (
        <ol className="flex flex-col gap-2">
          {d.topProducts.map((p, i) => (
            <li key={p.productId} className="flex items-center gap-3">
              <span className="flex size-6 shrink-0 items-center justify-center rounded-full bg-field text-[12px] font-bold text-ink-2">{i + 1}</span>
              {p.imageUrl
                ? <img src={p.imageUrl} alt="" className="size-9 shrink-0 rounded-lg border border-line bg-white object-contain" />
                : <span className="flex size-9 shrink-0 items-center justify-center rounded-lg bg-field text-ink-3"><ImageOff size={15} /></span>}
              <span className="min-w-0 flex-1">
                <span className="block truncate text-[14px]">{p.name}</span>
                <span className="text-[12px] text-ink-3">{num(p.quantity)} {p.unit ?? ''}</span>
              </span>
              <span className="shrink-0 text-right text-[14px] font-semibold">{tenge(p.revenue)}</span>
            </li>
          ))}
        </ol>
      )}
    </Card>
  )
}

function CustomersCard({ d, compare }: { d: Dashboard; compare: string }) {
  const t = useT()
  const c = d.customers
  const others = Math.max(0, c.total - c.regular - c.vip)
  const slices: Slice[] = [
    { key: 'vip', label: t('stats.vip'), value: c.vip, color: '#f59e0b' },
    { key: 'regular', label: t('stats.regular'), value: c.regular, color: '#1570ff' },
    { key: 'other', label: t('stats.otherCustomers'), value: others, color: '#bfdbfe' },
  ]
  return (
    <Card title={t('stats.customers')}>
      <div className="flex items-center gap-4">
        <Donut slices={slices} center={num(c.total)} sub={t('stats.customersTotal')} size={130} />
        <ul className="flex flex-1 flex-col gap-2.5 text-[14px]">
          <Stat icon={<UserPlus size={16} />} value={num(c.new)} label={t('stats.kpi.newCustomers')} change={<Change value={c.newChange} />} />
          <Stat icon={<Repeat size={16} />} value={num(c.regular)} label={t('stats.regular')} />
          <Stat icon={<Crown size={16} />} value={num(c.vip)} label={t('stats.vip')} />
        </ul>
      </div>
      {c.total === 0 && <p className="mt-2 text-[12px] text-ink-3">{t('stats.noCustomers', { compare })}</p>}
    </Card>
  )
}

function BonusCard({ d }: { d: Dashboard }) {
  const t = useT()
  const b = d.bonuses
  const slices: Slice[] = [
    { key: 'accrued', label: '', value: b.accrued, color: '#f43f5e' },
    { key: 'redeemed', label: '', value: b.redeemed, color: '#fda4af' },
  ]
  return (
    <Card title={t('stats.bonus')}>
      <div className="flex items-center gap-4">
        <Donut slices={slices} center={`${num(b.accrued)} Б`} sub={t('stats.accrued')} size={130} />
        <ul className="flex flex-1 flex-col gap-2.5 text-[14px]">
          <Stat icon={<Gift size={16} />} value={`${num(b.accrued)} Б`} label={t('stats.accrued')} change={<Change value={b.accruedChange} />} />
          <Stat icon={<Gift size={16} />} value={`${num(b.redeemed)} Б`} label={t('stats.redeemed')} change={<Change value={b.redeemedChange} />} />
          <Stat icon={<Coins size={16} />} value={`${num(b.balance)} Б`} label={t('stats.balances')} />
        </ul>
      </div>
      <p className="mt-2 text-[12px] text-ink-3">{t('stats.bonusNotMoney')}</p>
    </Card>
  )
}

function Stat({ icon, value, label, change }: { icon: ReactNode; value: string; label: string; change?: ReactNode }) {
  return (
    <li className="flex items-center gap-2.5">
      <span className="flex size-8 items-center justify-center rounded-lg bg-field text-ink-2">{icon}</span>
      <div className="min-w-0 flex-1">
        <div className="font-bold leading-tight">{value}</div>
        <div className="text-[12px] text-ink-3">{label}</div>
      </div>
      {change}
    </li>
  )
}

function AttentionCard({ d }: { d: Dashboard }) {
  const t = useT()
  const META = {
    lowStock: { icon: AlertTriangle, cls: 'bg-amber-50 text-amber-700', to: '/products' },
    outOfStock: { icon: PackageX, cls: 'bg-danger-soft text-danger', to: '/products' },
    debts: { icon: HandCoins, cls: 'bg-sky-50 text-sky-700', to: '/cashier' },
    overdueDebts: { icon: HandCoins, cls: 'bg-danger-soft text-danger', to: '/cashier' },
  } as const
  return (
    <Card className="md:col-span-2 xl:col-span-1" title={t('stats.attention')}>
      {d.attention.length === 0 ? (
        <div className="flex flex-1 items-center justify-center rounded-xl bg-success-soft px-4 py-6 text-[14px] font-medium text-success">{t('stats.allGood')}</div>
      ) : (
        <div className="grid grid-cols-2 gap-2">
          {d.attention.map((a) => {
            const m = META[a.type]
            return (
              <Link key={a.type} to={m.to} className={`flex flex-col gap-1 rounded-xl px-3 py-3 ${m.cls} hover:opacity-90`}>
                <m.icon size={20} />
                <span className="text-[14px] font-bold">{t(`stats.attn.${a.type}` as TranslationKey, { n: a.count })}</span>
                {a.amount != null && <span className="text-[12px]">{t('stats.attnSum', { sum: tenge(a.amount) })}</span>}
              </Link>
            )
          })}
        </div>
      )}
    </Card>
  )
}

function Empty({ text }: { text: string }) {
  return <div className="flex min-h-40 flex-1 items-center justify-center rounded-xl bg-field text-[14px] text-ink-3">{text}</div>
}

/** ТЗ §20: жүктелу кезінде интерфейс секірмейді — блоктардың орнында сұр қаңқа. */
function Skeleton() {
  const box = 'animate-pulse rounded-2xl bg-surface'
  return (
    <div className="flex flex-col gap-4">
      <div className="grid grid-cols-2 gap-3 md:grid-cols-3 xl:grid-cols-6">{Array.from({ length: 6 }, (_, i) => <div key={i} className={`${box} h-28`} />)}</div>
      {/* Бағандар нақты бетпен бірдей — деректер келгенде бет «секірмейді» (§20). */}
      {['h-72', 'h-56'].map((h, row) => (
        <div key={h} className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          {Array.from({ length: 3 }, (_, i) => (
            <div key={i} className={`${box} ${h} ${(row === 0 ? i === 0 : i === 2) ? 'md:col-span-2 xl:col-span-1' : ''}`} />
          ))}
        </div>
      ))}
    </div>
  )
}
