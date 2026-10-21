import { useQuery } from '@tanstack/react-query'
import { Link, useNavigate } from 'react-router-dom'
import { AlarmClock, Ban, Building2, CalendarClock, CircleDollarSign, FlaskConical, Inbox, TrendingUp, Wallet } from 'lucide-react'
import { ComboChart } from '../../components/stats/ComboChart'
import { BlockError, Skeleton } from '../../components/stats/statsUi'
import { num, tenge } from '../../lib/money'
import { adminApi } from '../adminApi'
import { METHODS, MODULES, PageHeader, Panel, StatusPill, Tile, date } from '../ui'

/** Шолу: бизнестер күй бойынша, MRR, айдағы түсім, айлар бойынша түсім, мерзімі өткендер, жақын төлемдер, соңғы төлемдер, модульдер. */
export function DashboardPage() {
  const nav = useNavigate()
  const q = useQuery({ queryKey: ['admin', 'overview'], queryFn: adminApi.overview })
  const d = q.data
  if (q.isError) return <BlockError message={(q.error as Error).message} onRetry={() => void q.refetch()} />
  const st = (k: string) => d?.byStatus[k] ?? 0
  const growth = d && d.paidLastMonth > 0 ? Math.round(((d.paidThisMonth - d.paidLastMonth) / d.paidLastMonth) * 100) : null
  return (
    <div className="flex flex-col gap-4">
      <PageHeader title="Обзор" subtitle="Бизнесы на SalemPos, оплаты и подключённые модули" />
      {d && d.newLeads > 0 && (
        <Link to="/leads" className="flex items-center gap-3 rounded-2xl border border-brand/30 bg-brand-soft px-4 py-3 text-[14px] text-brand hover:border-brand">
          <Inbox size={18} /><span className="font-semibold">Новых заявок с сайта: {d.newLeads}</span><span className="ml-auto font-semibold">Открыть →</span>
        </Link>
      )}
      <div className="grid grid-cols-2 gap-3 md:grid-cols-4">
        <Tile label="Бизнесов" value={d ? num(d.businesses) : '…'} hint={d ? `+${d.newThisMonth} в этом месяце` : undefined} icon={<Building2 size={18} />} onClick={() => nav('/businesses')} />
        <Tile label="MRR" value={d ? tenge(d.mrr) : '…'} hint="ежемесячно от платящих" icon={<TrendingUp size={18} />} tone="green" />
        <Tile label="Оплачено в этом месяце" value={d ? tenge(d.paidThisMonth) : '…'} tone="violet" icon={<Wallet size={18} />}
          hint={d ? `${d.paymentsThisMonth} платежей${growth !== null ? ` · ${growth >= 0 ? '+' : ''}${growth}% к прошлому` : ''}` : undefined} onClick={() => nav('/payments')} />
        <Tile label="Просрочено / не оплачено" value={d ? num(st('overdue') + st('unpaid')) : '…'} tone="red" icon={<AlarmClock size={18} />}
          onClick={() => nav('/businesses?status=overdue')} />
        <Tile label="Оплачено" value={d ? num(st('active') + st('dueSoon')) : '…'} tone="green" icon={<CircleDollarSign size={18} />} hint={d ? `${st('dueSoon')} — скоро оплата` : undefined}
          onClick={() => nav('/businesses?status=active')} />
        <Tile label="Пробный период" value={d ? num(st('trial')) : '…'} tone="violet" icon={<FlaskConical size={18} />} onClick={() => nav('/businesses?status=trial')} />
        <Tile label="Скоро оплата" value={d ? num(st('dueSoon')) : '…'} tone="amber" icon={<CalendarClock size={18} />} onClick={() => nav('/businesses?status=dueSoon')} />
        <Tile label="Приостановлены" value={d ? num(st('suspended')) : '…'} tone="gray" icon={<Ban size={18} />} onClick={() => nav('/businesses?status=suspended')} />
      </div>

      <div className="grid grid-cols-1 gap-4 xl:grid-cols-3">
        <Panel title="Поступления по месяцам" className="xl:col-span-2">
          {!d ? <Skeleton className="h-64" /> : (
            <ComboChart granularity="month" height={240}
              buckets={d.revenue.map((m) => { const [y, mo] = m.month.split('-').map(Number); return { start: `${m.month}-01`, end: `${m.month}-${String(new Date(y, mo, 0).getDate()).padStart(2, '0')}` } })}
              series={[
                { key: 'amount', label: 'Сумма', kind: 'money', style: 'bar', color: '#1570ff', values: d.revenue.map((m) => m.amount) },
                { key: 'count', label: 'Платежей', kind: 'count', style: 'line', color: '#14b8a6', values: d.revenue.map((m) => m.count) },
              ]} />
          )}
        </Panel>
        <Panel title="Модули у активных бизнесов">
          {!d ? <Skeleton className="h-48" /> : (
            <ul className="flex flex-col gap-2.5">
              {MODULES.map((m) => {
                const count = d.modules.find((x) => x.module === m.key)?.businesses ?? 0
                const total = Math.max(1, d.businesses - st('suspended'))
                return (
                  <li key={m.key} className="text-[13px]">
                    <div className="flex items-center gap-2"><span className="text-ink-2">{m.icon}</span><span className="flex-1">{m.label}</span><b>{count}</b></div>
                    <div className="mt-1 h-1.5 rounded-full bg-field"><div className="h-1.5 rounded-full bg-brand" style={{ width: `${(count / total) * 100}%` }} /></div>
                  </li>
                )
              })}
            </ul>
          )}
        </Panel>
      </div>

      <div className="grid grid-cols-1 gap-4 xl:grid-cols-3">
        <Panel title="Просрочка и неоплаченные" action={<Link to="/businesses?status=overdue" className="text-[13px] font-semibold text-brand hover:underline">Все</Link>}>
          {!d ? <Skeleton className="h-40" /> : d.overdue.length === 0 ? <p className="py-6 text-center text-[13px] text-ink-3">Должников нет</p> : (
            <ul className="divide-y divide-line">
              {d.overdue.map((b) => (
                <li key={b.id}>
                  <Link to={`/businesses/${b.id}`} className="flex items-center gap-2 py-2 text-[13px] hover:text-brand">
                    <span className="min-w-0 flex-1 truncate font-medium">{b.name}</span>
                    <span className="text-danger">{b.daysOverdue ? `${b.daysOverdue} дн.` : '—'}</span>
                    <span className="w-24 text-right">{tenge(b.fee)}</span>
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </Panel>
        <Panel title="Ближайшие оплаты и конец пробного">
          {!d ? <Skeleton className="h-40" /> : d.renewals.length === 0 ? <p className="py-6 text-center text-[13px] text-ink-3">В ближайшую неделю — нет</p> : (
            <ul className="divide-y divide-line">
              {d.renewals.map((b) => (
                <li key={b.id}>
                  <Link to={`/businesses/${b.id}`} className="flex items-center gap-2 py-2 text-[13px] hover:text-brand">
                    <span className="min-w-0 flex-1 truncate font-medium">{b.name}</span>
                    <StatusPill status={b.status} />
                    <span className="w-20 text-right text-ink-2">{date(b.paidUntil ?? b.trialUntil)}</span>
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </Panel>
        <Panel title="Последние оплаты" action={<Link to="/payments" className="text-[13px] font-semibold text-brand hover:underline">Все</Link>}>
          {!d ? <Skeleton className="h-40" /> : d.recentPayments.length === 0 ? <p className="py-6 text-center text-[13px] text-ink-3">Оплат пока нет</p> : (
            <ul className="divide-y divide-line">
              {d.recentPayments.map((p) => (
                <li key={p.id}>
                  <Link to={`/businesses/${p.organizationId}`} className="flex items-center gap-2 py-2 text-[13px] hover:text-brand">
                    <span className="min-w-0 flex-1"><span className="block truncate font-medium">{p.organizationName}</span><span className="block text-[11px] text-ink-3">{date(p.paidOn)} · {METHODS[p.method] ?? p.method}</span></span>
                    <span className="font-semibold">{tenge(p.amount)}</span>
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </Panel>
      </div>
    </div>
  )
}
