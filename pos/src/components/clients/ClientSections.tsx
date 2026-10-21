import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Link, useNavigate } from 'react-router-dom'
import { AlarmClock, Cake, Gift, HandCoins, Megaphone, MessageSquare, Moon, PieChart, PlusCircle, Repeat, TrendingUp, Trophy, UserPlus, Users, Zap } from 'lucide-react'
import { Button } from '../ui/Button'
import { AccrueModal } from '../bonus/CustomerPanel'
import { Pill, Points, StatusBadge } from '../bonus/bonusUi'
import { Donut, Legend, type Slice } from '../stats/Charts'
import { ComboChart } from '../stats/ComboChart'
import { BlockError, Card, Empty, Segmented, Skeleton } from '../stats/statsUi'
import { EmptyRow, td, th } from '../inventory/inventoryUi'
import { clientsApi, handOffAudience } from '../../lib/clientsApi'
import type { BonusCustomer } from '../../lib/bonusTypes'
import type { ClientActivity, ClientBirthday, ClientKpis, ClientRow, QuickFilter } from '../../lib/clientsTypes'
import { dateOnly } from '../../lib/dates'
import { num, tenge } from '../../lib/money'
import { useT, type TranslationKey } from '../../lib/i18n'
import { lastPurchaseText } from './ClientModals'
import { ACTIVITY_COLOR, ActivityPill, Avatar, KpiTile, clientNo, useClientMeta, useOpenClient, useParamSetter } from './clientsUi'

const storesQs = (storeIds?: string) => (storeIds ? `&stores=${storeIds}` : '')

function ClientLine({ r, onOpen, right }: { r: ClientRow; onOpen: (id: string) => void; right?: React.ReactNode }) {
  return (
    <li>
      <button type="button" onClick={() => onOpen(r.id)} className="flex w-full items-center gap-3 px-1 py-2 text-left text-[13px] hover:bg-field">
        <Avatar name={r.name} size={30} />
        <span className="min-w-0 flex-1"><span className="block truncate font-medium">{r.name}</span><span className="block text-[11px] text-ink-3">ID {clientNo(r.number)}</span></span>
        {right}
      </button>
    </li>
  )
}

const OVERVIEW_KPIS: { key: keyof Omit<ClientKpis, 'generatedAt'>; quick: QuickFilter | null; icon: React.ReactNode; tone: 'blue' | 'green' | 'violet' | 'sky' | 'gray' | 'amber' | 'red' | 'pink' }[] = [
  { key: 'total', quick: null, icon: <Users size={17} />, tone: 'blue' },
  { key: 'active', quick: 'active', icon: <Zap size={17} />, tone: 'green' },
  { key: 'new', quick: 'new', icon: <UserPlus size={17} />, tone: 'violet' },
  { key: 'repeat', quick: 'repeat', icon: <Repeat size={17} />, tone: 'sky' },
  { key: 'passive', quick: 'passive', icon: <Moon size={17} />, tone: 'gray' },
  { key: 'withDebt', quick: 'debt', icon: <HandCoins size={17} />, tone: 'amber' },
  { key: 'overdue', quick: 'overdue', icon: <AlarmClock size={17} />, tone: 'red' },
  { key: 'withBonus', quick: 'bonus', icon: <Gift size={17} />, tone: 'pink' },
]

/** «Обзор» (ТЗ «Клиенты» §3): KPI, белсенділік бөлінісі, жаңа клиенттер (12 ай), үздік клиенттер, кету қаупі, туған күндер, қарыздар. */
export function OverviewSection({ storeIds }: { storeIds?: string }) {
  const t = useT()
  const nav = useNavigate()
  const openClient = useOpenClient()
  const q = useQuery({ queryKey: ['clients', 'overview', storeIds], queryFn: () => clientsApi.overview(storeIds) })
  const d = q.data
  if (q.isError) return <BlockError message={(q.error as Error).message} onRetry={() => void q.refetch()} />
  const slices: Slice[] = (d?.activity ?? []).map((a) => ({ key: a.key, label: t(`cli.activity.${a.key}` as TranslationKey), value: a.count, color: ACTIVITY_COLOR[a.key] }))
  return (
    <div className="flex flex-col gap-4">
      <div className="grid grid-cols-2 gap-2.5 sm:grid-cols-4 2xl:grid-cols-8">
        {OVERVIEW_KPIS.map((k) => (
          <KpiTile key={k.key} label={t(`cli.kpi.${k.key}` as TranslationKey)} value={d ? num(d.kpis[k.key]) : '…'} icon={k.icon} tone={k.tone}
            onClick={() => nav(`/clients?${k.quick ? `quick=${k.quick}` : ''}${storesQs(storeIds)}`)} />
        ))}
      </div>
      <div className="grid grid-cols-1 gap-4 xl:grid-cols-3">
        <Card title={t('cli.ov.newByMonth')} icon={<TrendingUp size={18} />} className="xl:col-span-2">
          {!d ? <Skeleton className="h-64" /> : (
            <ComboChart granularity="month" height={240}
              buckets={d.newByMonth.map((m) => { const [y, mo] = m.month.split('-').map(Number); return { start: `${m.month}-01`, end: `${m.month}-${String(new Date(y, mo, 0).getDate()).padStart(2, '0')}` } })}
              series={[{ key: 'new', label: t('cli.kpi.new'), kind: 'count', style: 'bar', color: '#1769FF', values: d.newByMonth.map((m) => m.count) }]} />
          )}
        </Card>
        <Card title={t('cli.ov.activity')} icon={<PieChart size={18} />} action={<Link to={`/clients/activity${storeIds ? `?stores=${storeIds}` : ''}`} className="text-[13px] font-semibold text-brand hover:underline">{t('cli.all')}</Link>}>
          {!d ? <Skeleton className="h-48" /> : slices.every((s) => s.value === 0) ? <Empty text={t('cli.empty')} /> : (
            <div className="flex flex-col items-center gap-3">
              <Donut slices={slices} center={num(d.kpis.total)} sub={t('cli.ov.clients')} />
              <Legend slices={slices} format={(s) => num(s.value)} />
            </div>
          )}
        </Card>
      </div>
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2 xl:grid-cols-4">
        <Card title={t('cli.ov.top')} icon={<Trophy size={18} />}>
          {!d ? <Skeleton className="h-40" /> : d.top.length === 0 ? <Empty text={t('cli.prof.noPurchases')} /> : (
            <ul className="divide-y divide-line">{d.top.map((r) => <ClientLine key={r.id} r={r} onOpen={openClient} right={<span className="font-semibold">{tenge(r.amount)}</span>} />)}</ul>
          )}
        </Card>
        <Card title={t('cli.ov.atRisk')} icon={<AlarmClock size={18} />} info={t('cli.ov.atRiskInfo')}>
          {!d ? <Skeleton className="h-40" /> : d.atRisk.length === 0 ? <Empty text={t('cli.ov.noRisk')} /> : (
            <ul className="divide-y divide-line">{d.atRisk.map((r) => <ClientLine key={r.id} r={r} onOpen={openClient} right={<span className="text-[12px] text-ink-2">{t('cli.daysAgo', { n: r.daysSinceLastPurchase ?? 0 })}</span>} />)}</ul>
          )}
        </Card>
        <Card title={t('cli.ov.birthdays')} icon={<Cake size={18} />} action={<Link to={`/clients/birthdays${storeIds ? `?stores=${storeIds}` : ''}`} className="text-[13px] font-semibold text-brand hover:underline">{t('cli.all')}</Link>}>
          {!d ? <Skeleton className="h-40" /> : d.birthdays.length === 0 ? <Empty text={t('cli.bday.none')} /> : (
            <ul className="divide-y divide-line">{d.birthdays.map((b) => <ClientLine key={b.client.id} r={b.client} onOpen={openClient} right={<span className="text-[12px] text-ink-2">{birthdayWhen(t, b)}</span>} />)}</ul>
          )}
        </Card>
        <Card title={t('cli.ov.debts')} icon={<HandCoins size={18} />} action={<Link to={`/clients/debts${storeIds ? `?stores=${storeIds}` : ''}`} className="text-[13px] font-semibold text-brand hover:underline">{t('cli.all')}</Link>}>
          {!d ? <Skeleton className="h-40" /> : (
            <div className="flex flex-col gap-3">
              <div><div className="text-[12px] text-ink-2">{t('cli.prof.debtTotal')}</div><div className="text-[22px] font-extrabold">{tenge(d.debtTotal)}</div></div>
              <div><div className="text-[12px] text-ink-2">{t('cli.prof.debtOverdue')}</div><div className={`text-[18px] font-bold ${d.debtOverdue > 0 ? 'text-danger' : ''}`}>{tenge(d.debtOverdue)}</div></div>
              <div className="text-[12px] text-ink-3">{t('cli.ov.debtClients', { n: d.kpis.withDebt, overdue: d.kpis.overdue })}</div>
            </div>
          )}
        </Card>
      </div>
    </div>
  )
}

const SEGMENTS: ClientActivity[] = ['active', 'cooling', 'passive', 'noPurchases']

/** «Активность» (ТЗ «Клиенты» §11): белсенділік сегменттері (шектер — баптаулардан), сегмент тізімі, «суып бара жатқан» тұрақты клиенттер. */
export function ActivitySection({ storeIds }: { storeIds?: string }) {
  const t = useT()
  const nav = useNavigate()
  const meta = useClientMeta()
  const s = meta.data?.settings
  const openClient = useOpenClient()
  const [seg, setSeg] = useState<ClientActivity>('cooling')
  const overview = useQuery({ queryKey: ['clients', 'overview', storeIds], queryFn: () => clientsApi.overview(storeIds) })
  const list = useQuery({ queryKey: ['clients', 'activity-list', storeIds, seg], queryFn: () => clientsApi.search({ storeIds, filter: { quick: seg }, sort: seg === 'noPurchases' ? 'registered' : 'lastPurchase', dir: 'desc', pageSize: 50 }) })
  const risk = useQuery({ queryKey: ['clients', 'activity-risk', storeIds], queryFn: () => clientsApi.search({ storeIds, filter: { quick: 'cooling', minPurchases: 2 }, sort: 'amount', dir: 'desc', pageSize: 200 }) })
  const counts = Object.fromEntries((overview.data?.activity ?? []).map((a) => [a.key, a.count])) as Record<ClientActivity, number>
  const hint = (k: ClientActivity) => s ? t(`cli.act.hint.${k}` as TranslationKey, { active: s.activeDays, passive: s.passiveDays }) : ''
  const campaign = (rows: ClientRow[]) => { handOffAudience(rows.map((r) => r.customerId)); nav('/bonus/campaigns?new=1&audience=clients') }
  return (
    <div className="flex flex-col gap-4">
      <div className="grid grid-cols-2 gap-2.5 lg:grid-cols-4">
        {SEGMENTS.map((k) => (
          <button key={k} type="button" onClick={() => setSeg(k)}
            className={`rounded-2xl border bg-surface p-4 text-left ${seg === k ? 'border-brand ring-2 ring-brand/15' : 'border-line hover:border-brand/50'}`}>
            <span className="flex items-center gap-2"><span className="size-2.5 rounded-full" style={{ background: ACTIVITY_COLOR[k] }} /><span className="text-[13px] font-semibold">{t(`cli.activity.${k}` as TranslationKey)}</span></span>
            <span className="mt-1 block text-[24px] font-extrabold">{overview.data ? num(counts[k] ?? 0) : '…'}</span>
            <span className="block text-[12px] text-ink-3">{hint(k)}</span>
          </button>
        ))}
      </div>
      <div className="grid grid-cols-1 gap-4 xl:grid-cols-3">
        <Card title={t(`cli.activity.${seg}` as TranslationKey)} className="xl:col-span-2"
          action={<Link to={`/clients?quick=${seg}${storesQs(storeIds)}`} className="text-[13px] font-semibold text-brand hover:underline">{t('cli.act.openList')}</Link>}>
          <div className="overflow-x-auto">
            <table className="w-full min-w-[560px]">
              <thead className="border-b border-line"><tr>
                <th className={th}>{t('cli.col.client')}</th><th className={`${th} text-right`}>{t('cli.col.purchases')}</th>
                <th className={`${th} text-right`}>{t('cli.col.amount')}</th><th className={th}>{t('cli.col.lastPurchase')}</th>
              </tr></thead>
              <tbody>
                {!list.data && <EmptyRow colSpan={4} text={t('cli.loading')} />}
                {list.data?.rows.length === 0 && <EmptyRow colSpan={4} text={t('cli.emptyFiltered')} />}
                {list.data?.rows.map((r) => (
                  <tr key={r.id} className="cursor-pointer border-b border-line last:border-0 hover:bg-field/60" onClick={() => openClient(r.id)}>
                    <td className={td}><span className="font-medium">{r.name}</span> <span className="text-[12px] text-ink-3">ID {clientNo(r.number)}</span></td>
                    <td className={`${td} text-right`}>{num(r.purchases)}</td><td className={`${td} text-right`}>{tenge(r.amount)}</td>
                    <td className={td}>{lastPurchaseText(t, r.lastPurchaseAt, r.daysSinceLastPurchase)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {list.data && list.data.total > list.data.rows.length && <p className="mt-2 text-[12px] text-ink-3">{t('cli.act.shown', { n: list.data.rows.length, total: list.data.total })}</p>}
        </Card>
        <Card title={t('cli.ov.atRisk')} icon={<AlarmClock size={18} />} info={t('cli.ov.atRiskInfo')}
          action={meta.data?.can.bonusCampaigns && (risk.data?.rows.length ?? 0) > 0 ? <Button variant="secondary" className="h-8! px-2.5! text-[12px]!" icon={<Megaphone size={14} />} onClick={() => campaign(risk.data!.rows)}>{t('cli.bulk.campaign')}</Button> : undefined}>
          {!risk.data ? <Skeleton className="h-40" /> : risk.data.rows.length === 0 ? <Empty text={t('cli.ov.noRisk')} /> : (
            <ul className="divide-y divide-line">
              {risk.data.rows.slice(0, 15).map((r) => <ClientLine key={r.id} r={r} onOpen={openClient} right={<span className="text-right text-[12px] text-ink-2">{tenge(r.amount)}<br />{t('cli.daysAgo', { n: r.daysSinceLastPurchase ?? 0 })}</span>} />)}
            </ul>
          )}
          <p className="mt-3 text-[12px] text-ink-3">{t('cli.prof.riskHint')}</p>
        </Card>
      </div>
    </div>
  )
}

function birthdayWhen(t: ReturnType<typeof useT>, b: ClientBirthday) {
  const date = dateOnly(b.nextBirthday).slice(0, 5)
  return b.daysLeft === 0 ? `${date} · ${t('cli.today')}` : b.daysLeft === 1 ? `${date} · ${t('cli.tomorrow')}` : `${date} · ${t('cli.inDays', { n: b.daysLeft })}`
}

function rowToBonus(r: ClientRow): BonusCustomer {
  return {
    customerId: r.customerId, name: r.name, phone: r.phone, cardCode: null, statusId: r.statusId, statusName: r.statusName, statusColor: r.statusColor, statusManual: false,
    balance: r.bonus, totalSpent: r.amount, birthDate: r.birthDate, registeredAt: r.registeredAt, lastOperationAt: null, optOut: false,
  }
}

/** «Дни рождения» (ТЗ «Клиенты» §14): жақын туған күндер; құттықтау — хабармен (келісіммен), сыйлық — «Бонусы» ережесімен не қолмен. */
export function BirthdaysSection({ storeIds }: { storeIds?: string }) {
  const t = useT()
  const meta = useClientMeta()
  const can = meta.data?.can
  const { set } = useParamSetter()
  const [days, setDays] = useState<'7' | '14' | '30'>('7')
  const [accrue, setAccrue] = useState<ClientRow | null>(null)
  const q = useQuery({ queryKey: ['clients', 'birthdays', storeIds, days], queryFn: () => clientsApi.birthdays(storeIds, Number(days)) })
  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center gap-3">
        <Segmented value={days} onChange={setDays} options={[{ key: '7', label: t('cli.bday.days', { n: 7 }) }, { key: '14', label: t('cli.bday.days', { n: 14 }) }, { key: '30', label: t('cli.bday.days', { n: 30 }) }]} />
        {can?.bonusManage && <Link to="/bonus/birthday" className="ml-auto text-[13px] font-semibold text-brand hover:underline">{t('cli.bday.rules')}</Link>}
      </div>
      <div className="overflow-x-auto rounded-2xl border border-line bg-surface">
        <table className="w-full min-w-[760px]">
          <thead className="border-b border-line bg-field/60"><tr>
            <th className={th}>{t('cli.bday.date')}</th><th className={th}>{t('cli.col.client')}</th><th className={th}>{t('cli.bday.age')}</th>
            <th className={th}>{t('cli.col.status')}</th><th className={`${th} text-right`}>{t('cli.col.bonus')}</th><th className={th}>{t('cli.col.activity')}</th><th className={th} />
          </tr></thead>
          <tbody>
            {!q.data && <EmptyRow colSpan={7} text={t('cli.loading')} />}
            {q.data?.length === 0 && <EmptyRow colSpan={7} text={t('cli.bday.none')} />}
            {q.data?.map((b) => (
              <tr key={b.client.id} className="cursor-pointer border-b border-line last:border-0 hover:bg-field/60" onClick={() => set({ client: b.client.id }, true)}>
                <td className={td}>{b.daysLeft === 0 ? <Pill tone="green">{birthdayWhen(t, b)}</Pill> : birthdayWhen(t, b)}</td>
                <td className={td}><span className="font-medium">{b.client.name}</span> <span className="text-[12px] text-ink-3">ID {clientNo(b.client.number)}</span></td>
                <td className={td}>{b.age ?? '—'}</td>
                <td className={td}>{b.client.hasCard ? <StatusBadge name={b.client.statusName} color={b.client.statusColor} /> : '—'}</td>
                <td className={`${td} text-right`}><Points value={b.client.bonus} /></td>
                <td className={td}><ActivityPill activity={b.client.activity} /></td>
                <td className="px-2 text-right" onClick={(e) => e.stopPropagation()}>
                  <span className="inline-flex gap-1">
                    {can?.edit && <Button variant="secondary" className="h-8! px-2.5! text-[12px]!" icon={<MessageSquare size={14} />} onClick={() => set({ client: b.client.id, message: '1', tpl: 'birthday' }, true)}>{t('cli.bday.greet')}</Button>}
                    {can?.bonusAccrue && b.client.hasCard && <Button variant="secondary" className="h-8! px-2.5! text-[12px]!" icon={<PlusCircle size={14} />} onClick={() => setAccrue(b.client)}>{t('cli.prof.accrue')}</Button>}
                  </span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {accrue && <AccrueModal customer={rowToBonus(accrue)} onClose={() => setAccrue(null)} onDone={() => { setAccrue(null); void q.refetch() }} />}
    </div>
  )
}
