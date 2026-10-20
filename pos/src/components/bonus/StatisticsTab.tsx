import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import { BarChart3, Download, Flame, Megaphone, PieChart, Printer, RotateCcw, Trophy } from 'lucide-react'
import { Button } from '../ui/Button'
import { Select } from '../ui/Field'
import { toast } from '../ui/Toast'
import { td, th } from '../inventory/inventoryUi'
import { bonusApi } from '../../lib/bonusApi'
import { dateTime } from '../../lib/dates'
import { num } from '../../lib/money'
import { useT, type TranslationKey } from '../../lib/i18n'
import { Donut, Legend, type Slice } from '../stats/Charts'
import { ComboChart, type ChartSeries } from '../stats/ComboChart'
import { BlockError, Card, Empty, KpiCard, Skeleton } from '../stats/statsUi'
import { CustomerPanel } from './CustomerPanel'
import { BONUS_CHART, toKpi } from './OverviewTab'
import { reportError, StatusBadge, useBonusMeta, useBonusPermissions, useParamSetter, type BonusFilters } from './bonusUi'

const KPIS = ['accrued', 'used', 'returned', 'expired', 'balance', 'withBonus', 'averageBalance', 'usageRate', 'participation']

/**
 * «Статистика» (ТЗ «Бонусы» §7): тек оқиды. Бір кезең барлық ағын көрсеткішіне; қалдықтар — «на текущий момент». Әр көрсеткіштің анықтамасы ⓘ-да,
 * баған/сегментті басу — сол сүзгімен журнал. Экспорт — Excel/CSV, басып шығару — браузер.
 */
export function StatisticsTab({ filters, storeIds }: { filters: BonusFilters; storeIds?: string }) {
  const t = useT()
  const nav = useNavigate()
  const meta = useBonusMeta()
  const perms = useBonusPermissions()
  const { get, set } = useParamSetter()
  const [customer, setCustomer] = useState<string | null>(null)
  const statusId = get('status') || undefined
  const type = get('type') || undefined
  const q = useQuery({ queryKey: ['bonus', 'statistics', storeIds, filters.from, filters.to, statusId, type], queryFn: () => bonusApi.statistics({ storeIds, from: filters.from, to: filters.to, statusId, type }) })
  const d = q.data
  const search = `from=${filters.from}&to=${filters.to}${storeIds ? `&stores=${storeIds}` : ''}`
  const exportFile = async (format: 'xlsx' | 'csv') => {
    try { await bonusApi.exportFile('statistics', format, { storeIds, from: filters.from, to: filters.to, type }); toast(t('stats.exportReady')) } catch (err) { reportError(err, t('stats.exportFailed')) }
  }
  if (q.isError) return <BlockError message={(q.error as Error).message} onRetry={() => void q.refetch()} />
  const series: ChartSeries[] = d ? [
    { key: 'accrued', label: t('bon.kpi.accrued'), kind: 'points', style: 'bar', color: BONUS_CHART.accrued, values: d.days.map((x) => x.accrued) },
    { key: 'used', label: t('bon.kpi.used'), kind: 'points', style: 'bar', color: BONUS_CHART.used, values: d.days.map((x) => x.used) },
    { key: 'returned', label: t('bon.kpi.returned'), kind: 'points', style: 'line', color: BONUS_CHART.returned, values: d.days.map((x) => x.returned) },
    { key: 'expired', label: t('bon.kpi.expired'), kind: 'points', style: 'line', color: BONUS_CHART.expired, values: d.days.map((x) => x.expired) },
  ] : []
  const slices: Slice[] = (d?.statuses ?? []).map((s) => ({ key: s.statusId ?? 'none', label: s.name, value: s.customers, color: s.color, extra: `${s.percent}%` }))
  const maxType = Math.max(1, ...(d?.types ?? []).map((x) => Math.abs(x.amount)))

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center gap-2 print:hidden">
        <Select className="h-10! w-auto! min-w-40 text-[13px]" value={statusId ?? ''} onChange={(e) => set({ status: e.target.value })}>
          <option value="">{t('bon.allStatuses')}</option>
          {(meta.data?.statuses ?? []).map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
        </Select>
        <Select className="h-10! w-auto! min-w-40 text-[13px]" value={type ?? ''} onChange={(e) => set({ type: e.target.value })}>
          <option value="">{t('bon.allTypes')}</option>
          {['accrual', 'redemption', 'return', 'expiration', 'adjustment', 'reversal'].map((x) => <option key={x} value={x}>{t(`bon.typeGroup.${x}` as TranslationKey)}</option>)}
        </Select>
        {(statusId || type) && <Button variant="ghost" className="h-10! px-3!" icon={<RotateCcw size={15} />} onClick={() => set({ status: null, type: null })}>{t('bon.reset')}</Button>}
        <span className="ml-auto text-[12px] text-ink-3">{d ? t('bon.asOf', { time: dateTime(d.generatedAt) }) : ''}</span>
        {perms.export && <><Button variant="secondary" className="h-10! px-3!" icon={<Download size={16} />} onClick={() => void exportFile('xlsx')}>Excel</Button><Button variant="secondary" className="h-10! px-3!" onClick={() => void exportFile('csv')}>CSV</Button></>}
        <Button variant="secondary" className="h-10! px-3!" icon={<Printer size={16} />} onClick={() => window.print()}>{t('bon.print')}</Button>
      </div>

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3 2xl:grid-cols-5">
        {KPIS.map((key) => {
          const k = d?.kpis.find((x) => x.key === key)
          return (
            <KpiCard key={key} small k={toKpi(k)} label={t(`bon.kpi.${key}` as TranslationKey)} icon={<BarChart3 size={18} />} tone={key === 'expired' ? 'pink' : key === 'used' ? 'coral' : key === 'accrued' ? 'green' : 'blue'}
              kind={key === 'usageRate' || key === 'participation' ? 'percent' : key === 'withBonus' ? 'count' : 'points'} compare={k?.asOfNow ? 'none' : 'prev'} invert={key === 'expired'}
              info={t(`bon.kpi.${key}Info` as TranslationKey)} sub={k?.asOfNow ? <span className="text-[11px] text-ink-3">{t('bon.asOfNow')}</span> : k?.note === 'noAccrual' ? <span className="text-[11px] text-ink-3">—</span> : undefined} />
          )
        })}
      </div>

      <div className="grid grid-cols-1 gap-4 xl:grid-cols-3">
        <Card title={t('bon.chart.byDays')} icon={<BarChart3 size={18} />} className="xl:col-span-2" info={t('bon.chart.info')}>
          {!d ? <Skeleton className="h-64" /> : <ComboChart buckets={d.days.map((x) => ({ start: x.date, end: x.date }))} series={series} granularity="day"
            onBucketClick={(i) => nav(`/bonus/history?from=${d.days[i].date}&to=${d.days[i].date}${storeIds ? `&stores=${storeIds}` : ''}`)} />}
        </Card>
        <Card title={t('bon.statusesChart')} icon={<PieChart size={18} />} info={t('bon.statusesInfo')}>
          {!d ? <Skeleton className="h-48" /> : slices.every((s) => s.value === 0) ? <Empty text={t('bon.noCustomers')} /> : (
            <div className="flex flex-col items-center gap-3"><Donut slices={slices} center={num(slices.reduce((s, x) => s + x.value, 0))} sub={t('bon.customersWord')} /><Legend slices={slices} format={(s) => `${num(s.value)} · ${s.extra}`} /></div>
          )}
        </Card>
      </div>

      <div className="grid grid-cols-1 gap-4 xl:grid-cols-3">
        <Card title={t('bon.byTypes')} icon={<BarChart3 size={18} />} info={t('bon.byTypesInfo')}>
          {!d ? <Skeleton className="h-40" /> : d.types.length === 0 ? <Empty text={t('bon.empty')} /> : (
            <ul className="flex flex-col gap-2">
              {d.types.map((x) => (
                <li key={x.type}>
                  <button type="button" className="w-full text-left" onClick={() => nav(`/bonus/history?${search}&types=${x.type.startsWith('Adjustment') ? 'Adjustment' : x.type}`)}>
                    <div className="flex justify-between text-[13px]"><span>{t(`bon.type.${x.type}` as TranslationKey)} <span className="text-ink-3">· {x.count}</span></span><b className={x.amount < 0 ? 'text-danger' : 'text-success'}>{x.amount > 0 ? '+' : ''}{num(x.amount)}</b></div>
                    <div className="mt-1 h-2 rounded-full bg-field"><div className={`h-2 rounded-full ${x.amount < 0 ? 'bg-danger' : 'bg-brand'}`} style={{ width: `${Math.abs(x.amount) / maxType * 100}%` }} /></div>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </Card>
        <Card title={t('bon.expiryBlock')} icon={<Flame size={18} />} info={t('bon.expiryBlockInfo')}>
          {!d ? <Skeleton className="h-40" /> : (
            <div className="grid grid-cols-2 gap-3">
              {[['expiredInPeriod', d.expiredInPeriod], ['expiring30', d.expiring30], ['expiring60', d.expiring60], ['expiring90', d.expiring90]].map(([k, v]) => (
                <button key={k as string} type="button" onClick={() => nav(k === 'expiredInPeriod' ? `/bonus/history?${search}&type=expiration` : `/bonus/expiry?days=${(k as string).slice(8)}`)}
                  className="rounded-xl border border-line px-3 py-2 text-left hover:border-brand"><div className="text-[12px] text-ink-2">{t(`bon.stat.${k}` as TranslationKey)}</div><div className="text-[18px] font-extrabold">{num(v as number)}</div></button>
              ))}
            </div>
          )}
        </Card>
        <Card title={t('bon.campaignEffect')} icon={<Megaphone size={18} />} info={t('bon.campaignEffectInfo')}>
          {!d ? <Skeleton className="h-40" /> : d.campaigns.length === 0 ? <Empty text={t('bon.cmp.empty')} /> : (
            <ul className="flex flex-col divide-y divide-line">{d.campaigns.map((c) => (
              <li key={c.id} className="py-2 text-[13px]"><button type="button" className="font-medium hover:text-brand" onClick={() => nav(`/bonus/campaigns?campaign=${c.id}`)}>{c.name}</button>
                <span className="block text-[12px] text-ink-3">{t('bon.cmp.effLine', { sent: num(c.sent), read: num(c.read), p: c.readPercent ?? '—', bonus: num(c.bonusTotal) })}</span></li>
            ))}</ul>
          )}
        </Card>
      </div>

      <div className="grid grid-cols-1 gap-4 xl:grid-cols-2">
        {([['topBalance', d?.topBalance], ['topUsage', d?.topUsage]] as const).map(([key, rows]) => (
          <Card key={key} title={t(`bon.${key}` as TranslationKey)} icon={<Trophy size={18} />} info={t(`bon.${key}Info` as TranslationKey)}>
            {!rows ? <Skeleton className="h-48" /> : rows.length === 0 ? <Empty text={t('bon.empty')} /> : (
              <table className="w-full">
                <thead><tr><th className={`${th} w-8`}>#</th><th className={th}>{t('bon.customer')}</th><th className={th}>{t('bon.st.name')}</th><th className={`${th} text-right`}>{t(key === 'topBalance' ? 'bon.cust.balance' : 'bon.kpi.used')}</th>{key === 'topUsage' && <th className={`${th} text-right`}>{t('bon.kpi.returned')}</th>}<th className={th}>{t('bon.cust.lastOperation')}</th></tr></thead>
                <tbody>{rows.map((r) => (
                  <tr key={r.customerId + r.place} className="border-t border-line">
                    <td className={`${td} text-ink-3`}>{r.place}</td>
                    <td className={td}><button type="button" className="font-medium hover:text-brand" onClick={() => setCustomer(r.customerId)}>{r.name}</button></td>
                    <td className={td}><StatusBadge name={r.statusName} color={r.statusColor} /></td>
                    <td className={`${td} text-right font-semibold tabular-nums`}>{num(r.value)}</td>
                    {key === 'topUsage' && <td className={`${td} text-right tabular-nums text-ink-2`}>{r.returned ? num(r.returned) : '—'}</td>}
                    <td className={`${td} text-[13px] text-ink-2`}>{r.lastOperationAt ? dateTime(r.lastOperationAt) : '—'}</td>
                  </tr>
                ))}</tbody>
              </table>
            )}
          </Card>
        ))}
      </div>
      {customer && <CustomerPanel customerId={customer} onClose={() => setCustomer(null)} />}
    </div>
  )
}
