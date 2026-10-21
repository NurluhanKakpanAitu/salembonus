import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Link, useNavigate } from 'react-router-dom'
import { Cake, CircleDollarSign, Flame, Gift, Megaphone, PieChart, RefreshCw, ShieldCheck, ShoppingBag, Timer, Undo2, Users, Wallet, History } from 'lucide-react'
import { Button } from '../ui/Button'
import { bonusApi } from '../../lib/bonusApi'
import type { BonusKpi } from '../../lib/bonusTypes'
import type { Kpi } from '../../lib/analyticsTypes'
import { dateOnly, dateTime } from '../../lib/dates'
import { num } from '../../lib/money'
import { useT, type TranslationKey } from '../../lib/i18n'
import { Donut, Legend, type Slice } from '../stats/Charts'
import { ComboChart, type ChartSeries } from '../stats/ComboChart'
import { BlockError, Card, Empty, KpiCard, KPI_TONES, Skeleton } from '../stats/statsUi'
import { CustomerPanel } from './CustomerPanel'
import { BIRTHDAY_TONE, OpTypeBadge, Pill, Points, StatusBadge, type BonusFilters } from './bonusUi'

export const BONUS_CHART = { accrued: '#1769FF', used: '#14B8A6', returned: '#8B5CF6', expired: '#E11D48' }

export function toKpi(k: BonusKpi | undefined): Kpi | undefined {
  if (!k) return undefined
  return {
    key: k.key, value: k.value, previous: k.previous, changeAbs: k.previous === null || k.value === null ? null : k.value - k.previous, changePercent: k.changePercent,
    amount: null, previousAmount: null, numerator: null, denominator: null, available: k.value !== null, note: null,
  }
}

const KPI_META: Record<string, { icon: React.ReactNode; tone: keyof typeof KPI_TONES; invert?: boolean }> = {
  customers: { icon: <Users size={20} />, tone: 'blue' },
  withBonus: { icon: <Wallet size={20} />, tone: 'violet' },
  accrued: { icon: <Gift size={20} />, tone: 'green' },
  used: { icon: <ShoppingBag size={20} />, tone: 'coral' },
  returned: { icon: <Undo2 size={20} />, tone: 'sky' },
  expired: { icon: <Flame size={20} />, tone: 'pink', invert: true },
  balance: { icon: <CircleDollarSign size={20} />, tone: 'amber' },
}

/**
 * «Обзор» (ТЗ «Бонусы» §2): KPI (ағын мен қалдық бөлек; қалдық — «на момент обновления»), есептеу/жұмсау графигі, мәртебелер бойынша
 * клиенттер, жақын жану мен туған күндер, соңғы операциялар, жылдам өтулер. Әр виджет өз бөліміне апарады.
 */
export function OverviewTab({ filters, storeIds }: { filters: BonusFilters; storeIds?: string }) {
  const t = useT()
  const nav = useNavigate()
  const [customer, setCustomer] = useState<string | null>(null)
  const q = useQuery({ queryKey: ['bonus', 'overview', storeIds, filters.from, filters.to], queryFn: () => bonusApi.overview({ storeIds, from: filters.from, to: filters.to }) })
  const d = q.data
  const search = `?from=${filters.from}&to=${filters.to}${storeIds ? `&stores=${storeIds}` : ''}`
  if (q.isError) return <BlockError message={(q.error as Error).message} onRetry={() => void q.refetch()} />
  const series: ChartSeries[] = d ? [
    { key: 'accrued', label: t('bon.kpi.accrued'), kind: 'points', style: 'bar', color: BONUS_CHART.accrued, values: d.days.map((x) => x.accrued) },
    { key: 'used', label: t('bon.kpi.used'), kind: 'points', style: 'bar', color: BONUS_CHART.used, values: d.days.map((x) => x.used) },
    { key: 'returned', label: t('bon.kpi.returned'), kind: 'points', style: 'line', color: BONUS_CHART.returned, values: d.days.map((x) => x.returned) },
    { key: 'expired', label: t('bon.kpi.expired'), kind: 'points', style: 'line', color: BONUS_CHART.expired, values: d.days.map((x) => x.expired) },
  ] : []
  const slices: Slice[] = (d?.statuses ?? []).map((s) => ({ key: s.statusId ?? 'none', label: s.name, value: s.customers, color: s.color, extra: `${s.percent}%` }))
  const quick = [
    { to: '/bonus/statuses', label: 'bon.section.statuses', icon: <ShieldCheck size={16} /> },
    { to: '/bonus/expiry', label: 'bon.section.expiry', icon: <Timer size={16} /> },
    { to: '/bonus/birthday', label: 'bon.section.birthday', icon: <Cake size={16} /> },
    { to: '/bonus/campaigns?new=1', label: 'bon.quick.newCampaign', icon: <Megaphone size={16} /> },
    { to: '/bonus/history', label: 'bon.section.history', icon: <History size={16} /> },
  ]

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center gap-2">
        {quick.map((x) => (
          <Link key={x.to} to={`${x.to}${x.to.includes('?') ? '&' : '?'}${search.slice(1)}`} className="inline-flex h-9 items-center gap-2 rounded-xl border border-line bg-surface px-3 text-[13px] font-semibold text-ink-2 hover:border-brand hover:text-brand">
            {x.icon}{t(x.label as TranslationKey)}
          </Link>
        ))}
        <span className="ml-auto text-[12px] text-ink-3">{d ? t('bon.asOf', { time: dateTime(d.generatedAt) }) : ''}</span>
        <Button variant="secondary" className="h-9! px-3!" icon={<RefreshCw size={15} className={q.isFetching ? 'animate-spin' : ''} />} onClick={() => void q.refetch()}>{t('bon.refresh')}</Button>
      </div>

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4 2xl:grid-cols-7">
        {['customers', 'withBonus', 'accrued', 'used', 'returned', 'expired', 'balance'].map((key) => {
          const k = d?.kpis.find((x) => x.key === key)
          const m = KPI_META[key]
          return (
            <KpiCard key={key} small k={toKpi(k)} label={t(`bon.kpi.${key}` as TranslationKey)} icon={m.icon} tone={m.tone} kind={key === 'customers' || key === 'withBonus' ? 'count' : 'points'} compare={k?.asOfNow ? 'none' : 'prev'}
              invert={m.invert} info={t(`bon.kpi.${key}Info` as TranslationKey)}
              sub={k?.asOfNow ? <span className="text-[11px] text-ink-3">{t('bon.asOfNow')}</span> : undefined}
              onClick={key === 'balance' || key === 'withBonus' || key === 'customers' ? () => nav(`/bonus/statuses${search}&customers=1`) : () => nav(`/bonus/history${search}&type=${key === 'accrued' ? 'accrual' : key === 'used' ? 'redemption' : key === 'returned' ? 'ReturnRestore' : 'expiration'}`)} />
          )
        })}
      </div>

      <div className="grid grid-cols-1 gap-4 xl:grid-cols-3">
        <Card title={t('bon.chart.title')} icon={<Gift size={18} />} className="xl:col-span-2" info={t('bon.chart.info')}>
          {!d ? <Skeleton className="h-64" /> : (
            <ComboChart buckets={d.days.map((x) => ({ start: x.date, end: x.date }))} series={series} granularity="day"
              onBucketClick={(i) => nav(`/bonus/history?from=${d.days[i].date}&to=${d.days[i].date}${storeIds ? `&stores=${storeIds}` : ''}`)} />
          )}
        </Card>
        <Card title={t('bon.statusesChart')} icon={<PieChart size={18} />} action={<Link to={`/bonus/statuses${search}`} className="text-[13px] font-semibold text-brand hover:underline">{t('bon.all')}</Link>}>
          {!d ? <Skeleton className="h-48" /> : slices.every((s) => s.value === 0) ? <Empty text={t('bon.noCustomers')} /> : (
            <div className="flex flex-col items-center gap-3">
              <Donut slices={slices} center={num(slices.reduce((s, x) => s + x.value, 0))} sub={t('bon.customersWord')} />
              <Legend slices={slices} format={(s) => `${num(s.value)} · ${s.extra}`} />
            </div>
          )}
        </Card>
      </div>

      <div className="grid grid-cols-1 gap-4 xl:grid-cols-3">
        <Card title={t('bon.upcomingExpiry')} icon={<Timer size={18} />} action={<Link to={`/bonus/expiry${search}`} className="text-[13px] font-semibold text-brand hover:underline">{t('bon.all')}</Link>}>
          {!d ? <Skeleton className="h-40" /> : !d.expiryEnabled && d.expiring.length === 0 ? (
            <Empty text={t('bon.expiryDisabled')} action={<Link to="/bonus/expiry" className="text-[13px] font-semibold text-brand">{t('bon.configure')}</Link>} />
          ) : d.expiring.length === 0 ? <Empty text={t('bon.noExpiring')} /> : (
            <ul className="flex flex-col divide-y divide-line">
              {d.expiring.map((x) => (
                <li key={x.lotId} className="flex items-center gap-3 py-2 text-[13px]">
                  <span className="w-20 shrink-0 text-ink-2">{dateOnly(x.expiresAt)}</span>
                  <button type="button" onClick={() => setCustomer(x.customerId)} className="min-w-0 flex-1 break-words text-left hover:text-brand">{x.customerName}</button>
                  <Points value={x.amount} />
                  <Pill tone={x.notification === 'sent' ? 'green' : x.notification === 'scheduled' ? 'blue' : 'gray'}>{t(`bon.notify.${x.notification}` as TranslationKey)}</Pill>
                </li>
              ))}
            </ul>
          )}
        </Card>
        <Card title={t('bon.upcomingBirthdays')} icon={<Cake size={18} />} action={<Link to={`/bonus/birthday${search}`} className="text-[13px] font-semibold text-brand hover:underline">{t('bon.all')}</Link>}>
          {!d ? <Skeleton className="h-40" /> : d.birthdays.length === 0 ? <Empty text={t('bon.noBirthdays')} /> : (
            <ul className="flex flex-col divide-y divide-line">
              {d.birthdays.map((b) => (
                <li key={b.customerId} className="flex items-center gap-2 py-2 text-[13px]">
                  <span className="w-12 shrink-0 text-ink-2">{dateOnly(b.birthday).slice(0, 5)}</span>
                  <button type="button" onClick={() => setCustomer(b.customerId)} className="min-w-0 flex-1 break-words text-left hover:text-brand">{b.customerName}</button>
                  <StatusBadge name={b.statusName} color={b.statusColor} />
                  <span className="w-16 text-right tabular-nums">{b.amount ? num(b.amount) : '—'}</span>
                  <Pill tone={BIRTHDAY_TONE[b.state] ?? 'gray'}>{t(`bon.bday.${b.state}` as TranslationKey)}</Pill>
                </li>
              ))}
            </ul>
          )}
          {d && !d.birthdayEnabled && <p className="mt-2 text-[12px] text-ink-3">{t('bon.birthdayOff')}</p>}
        </Card>
        <Card title={t('bon.recent')} icon={<History size={18} />} action={<Link to={`/bonus/history${search}`} className="text-[13px] font-semibold text-brand hover:underline">{t('bon.allOperations')}</Link>}>
          {!d ? <Skeleton className="h-40" /> : d.recent.length === 0 ? <Empty text={t('bon.empty')} /> : (
            <ul className="flex flex-col divide-y divide-line">
              {d.recent.map((o) => (
                <li key={o.id} className="flex items-center gap-2 py-2 text-[13px]">
                  <OpTypeBadge type={o.type} amount={o.amount} />
                  <span className="min-w-0 flex-1"><button type="button" onClick={() => setCustomer(o.customerId)} className="block max-w-full break-words text-left hover:text-brand">{o.customerName}</button>
                    <span className="block text-[11px] text-ink-3">{dateTime(o.createdAt)} · {o.basis} · {o.author}</span></span>
                  <Points value={o.amount} signed muted={o.status === 'reversed'} />
                </li>
              ))}
            </ul>
          )}
        </Card>
      </div>
      {customer && <CustomerPanel customerId={customer} onClose={() => setCustomer(null)} />}
    </div>
  )
}
