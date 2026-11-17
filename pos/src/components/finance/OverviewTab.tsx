import { useQuery } from '@tanstack/react-query'
import { Link, useNavigate } from 'react-router-dom'
import { ArrowDown, ArrowUp, CalendarClock, Coins, HandCoins, Landmark, ListOrdered, PieChart, Wallet } from 'lucide-react'
import { financeApi } from '../../lib/financeApi'
import { dateOnly } from '../../lib/dates'
import { tenge } from '../../lib/money'
import { useT, type TranslationKey } from '../../lib/i18n'
import { Donut, Legend, type Slice } from '../stats/Charts'
import { ComboChart } from '../stats/ComboChart'
import { BlockError, Card, Empty, KpiCard, Skeleton } from '../stats/statsUi'
import { ACCOUNT_ICON, ACCOUNT_TONE, Amount, daysToChart, OpStatusBadge, SHARE_COLORS, toKpi, TypeBadge, type FinanceFilters } from './financeUi'

/**
 * «Обзор» (ТЗ «Финансы» §4): жалғыз жалпы көрініс — KPI, график, шоттар, шығын құрылымы, соңғы транзакциялар, жақын төлемдер, қарыздар.
 * Басқа подразделдердің толық кестелері мұнда қайталанбайды; әр виджет — сол подразделге сілтеме.
 */
export function OverviewTab({ filters, storeIds }: { filters: FinanceFilters; storeIds?: string }) {
  const t = useT()
  const nav = useNavigate()
  const q = useQuery({ queryKey: ['finance', 'overview', storeIds, filters.from, filters.to], queryFn: () => financeApi.overview(storeIds, filters.from, filters.to) })
  const search = `?from=${filters.from}&to=${filters.to}${storeIds ? `&stores=${storeIds}` : ''}`
  if (q.isError) return <BlockError message={(q.error as Error).message} onRetry={() => void q.refetch()} />
  const d = q.data
  const kpi = (key: string) => toKpi(d?.kpis.find((k) => k.key === key))
  const empty = d && d.days.every((x) => x.inflow === 0 && x.outflow === 0) && d.recent.length === 0
  const chart = d ? daysToChart(d.days, t) : null
  // Санат үлесі — серверден атымен; жеткізушіге төлем мен қайтарым — операция түрінің атауымен (§4).
  const shareLabel = (s: { key: string; label: string }) => s.key === 'supplierPayments' ? t('fin.type.SupplierPayment') : s.key === 'refunds' ? t('fin.type.Refund') : s.label
  const slices: Slice[] = (d?.expenses ?? []).map((s, i) => ({ key: s.key, label: shareLabel(s), value: s.amount, color: SHARE_COLORS[i % SHARE_COLORS.length], extra: `${s.percent}%` }))

  return (
    <div className="flex flex-col gap-4">
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-5">
        <KpiCard k={kpi('cash')} label={t('fin.kpi.cash')} icon={<Wallet size={20} />} tone="blue" compare="none" info={t('fin.kpi.cashInfo')} onClick={() => nav(`/finance/accounts${search}`)} />
        <KpiCard k={kpi('inflow')} label={t('fin.kpi.inflow')} icon={<ArrowDown size={20} />} tone="green" compare="prev" info={t('fin.kpi.inflowInfo')} onClick={() => nav(`/finance/cash-flow${search}&flow=in`)} />
        <KpiCard k={kpi('outflow')} label={t('fin.kpi.outflow')} icon={<ArrowUp size={20} />} tone="coral" compare="prev" invert info={t('fin.kpi.outflowInfo')} onClick={() => nav(`/finance/cash-flow${search}&flow=out`)} />
        <KpiCard k={kpi('netFlow')} label={t('fin.kpi.netFlow')} icon={<Coins size={20} />} tone="violet" compare="prev" info={t('fin.kpi.netFlowInfo')} onClick={() => nav(`/finance/cash-flow${search}`)} />
        <KpiCard k={kpi('profit')} label={t('fin.kpi.profit')} icon={<PieChart size={20} />} tone="amber" compare="prev" info={t('fin.kpi.profitInfo')} onClick={() => nav(`/finance/pnl${search}`)} />
      </div>

      {empty && <Empty text={t('fin.emptyPeriod')} action={<span className="text-[12px]">{t('fin.emptyPeriodHint')}</span>} />}

      <div className="grid grid-cols-1 gap-4 xl:grid-cols-3">
        <Card title={t('fin.chart.title')} icon={<Landmark size={18} />} className="xl:col-span-2" info={t('fin.chart.info')}>
          {!chart ? <Skeleton className="h-64" /> : <ComboChart buckets={chart.buckets} series={chart.series} granularity="day" leftTitle="₸" />}
        </Card>
        <Card title={t('fin.accounts')} icon={<Wallet size={18} />} action={<Link to={`/finance/accounts${search}`} className="text-[13px] font-semibold text-brand hover:underline">{t('fin.allAccounts')}</Link>}>
          {!d ? <Skeleton className="h-48" /> : d.accounts.length === 0 ? <Empty text={t('fin.noAccounts')} /> : (
            <ul className="flex flex-col gap-2">
              {d.accounts.filter((a) => a.isActive).slice(0, 6).map((a) => (
                <li key={a.id} className="flex items-center gap-3 rounded-xl border border-line px-3 py-2">
                  <span className={`flex size-9 shrink-0 items-center justify-center rounded-lg ${ACCOUNT_TONE[a.type]}`}>{ACCOUNT_ICON[a.type]}</span>
                  <span className="min-w-0 flex-1">
                    <span className="block break-words text-[14px] font-medium">{a.name}</span>
                    <span className="block text-[12px] text-ink-3">{t(`fin.accountType.${a.type}` as TranslationKey)}{d.accounts.some((x) => x.storeId !== a.storeId) ? ` · ${a.storeName}` : ''}</span>
                  </span>
                  <span className={`text-[15px] font-bold tabular-nums ${a.balance < 0 ? 'text-danger' : ''}`}>{tenge(a.balance)}</span>
                </li>
              ))}
            </ul>
          )}
        </Card>
      </div>

      <div className="grid grid-cols-1 gap-4 xl:grid-cols-3">
        <Card title={t('fin.expenseStructure')} icon={<PieChart size={18} />} action={<Link to={`/finance/income-expense${search}&type=Expense`} className="text-[13px] font-semibold text-brand hover:underline">{t('fin.allExpenses')}</Link>}>
          {!d ? <Skeleton className="h-48" /> : slices.length === 0 ? <Empty text={t('fin.noExpenses')} /> : (
            <div className="flex flex-col items-center gap-3 sm:flex-row">
              <Donut slices={slices} center={tenge(slices.reduce((s, x) => s + x.value, 0))} sub={t('fin.kpi.outflow')} />
              <Legend slices={slices} format={(s) => tenge(s.value)} />
            </div>
          )}
        </Card>
        <Card title={t('fin.recent')} icon={<ListOrdered size={18} />} action={<Link to={`/finance/transactions${search}`} className="text-[13px] font-semibold text-brand hover:underline">{t('fin.allTransactions')}</Link>}>
          {!d ? <Skeleton className="h-48" /> : d.recent.length === 0 ? <Empty text={t('fin.emptyPeriod')} /> : (
            <ul className="flex flex-col divide-y divide-line">
              {d.recent.map((o) => (
                <li key={o.id} className="flex items-center gap-3 py-2">
                  <TypeBadge type={o.type} />
                  <Link to={`/finance/transactions${search}&op=${o.id}`} className="min-w-0 flex-1 break-words text-[13px] hover:text-brand">{o.description}<span className="block text-[11px] text-ink-3">{dateOnly(o.date)} · {o.counterpartyName ?? o.number}</span></Link>
                  <Amount value={o.amount} type={o.type} entries={o.entries} muted={o.status === 'Reversed'} />
                  {o.status !== 'Posted' && <OpStatusBadge status={o.status} />}
                </li>
              ))}
            </ul>
          )}
        </Card>
        <div className="flex flex-col gap-4">
          <Card title={t('fin.upcoming')} icon={<CalendarClock size={18} />} info={t('fin.upcomingInfo')}>
            {!d ? <Skeleton className="h-28" /> : d.upcoming.length === 0 ? <Empty text={t('fin.noUpcoming')} /> : (
              <ul className="flex flex-col divide-y divide-line">
                {d.upcoming.slice(0, 6).map((u) => (
                  <li key={`${u.kind}-${u.id}`} className="flex items-center gap-3 py-2 text-[13px]">
                    <span className={`rounded-lg px-2 py-0.5 text-[11px] font-semibold ${u.status === 'Overdue' ? 'bg-danger-soft text-danger' : u.status === 'DueSoon' ? 'bg-amber-50 text-amber-700' : 'bg-brand-soft text-brand'}`}>
                      {u.dueDate ? dateOnly(u.dueDate) : '—'}
                    </span>
                    <Link to={u.kind === 'customer' ? `/finance/customer-debts${search}&customer=${u.id}` : `/finance/supplier-debts${search}&supplier=${u.id}`} className="min-w-0 flex-1 break-words hover:text-brand">
                      {u.counterparty}{' '}<span className="ml-1 text-[11px] text-ink-3">{t(u.kind === 'customer' ? 'fin.upcomingCustomer' : 'fin.upcomingSupplier')}{u.daysLeft !== null ? ` · ${t('fin.daysLeft', { n: u.daysLeft })}` : ''}</span>
                    </Link>
                    <span className="font-semibold tabular-nums">{tenge(u.amount)}</span>
                  </li>
                ))}
              </ul>
            )}
          </Card>
          <Card title={t('fin.debts')} icon={<HandCoins size={18} />}>
            {!d ? <Skeleton className="h-20" /> : (
              <div className="grid grid-cols-2 gap-3 text-[13px]">
                <Link to={`/finance/customer-debts${search}`} className="rounded-xl border border-line p-3 hover:border-brand">
                  <span className="block text-ink-2">{t('fin.customerDebt')}</span>
                  <span className="block text-[18px] font-bold tabular-nums">{tenge(d.debts.customerDebt)}</span>
                  <span className={`block text-[12px] ${d.debts.customerOverdue > 0 ? 'text-danger' : 'text-ink-3'}`}>{t('fin.overdue')}: {tenge(d.debts.customerOverdue)} · {t('fin.customersN', { n: d.debts.customerCount })}</span>
                </Link>
                <Link to={`/finance/supplier-debts${search}`} className="rounded-xl border border-line p-3 hover:border-brand">
                  <span className="block text-ink-2">{t('fin.supplierDebt')}</span>
                  <span className="block text-[18px] font-bold tabular-nums">{tenge(d.debts.supplierDebt)}</span>
                  <span className={`block text-[12px] ${d.debts.supplierOverdue > 0 ? 'text-danger' : 'text-ink-3'}`}>{t('fin.overdue')}: {tenge(d.debts.supplierOverdue)} · {t('fin.documentsN', { n: d.debts.supplierDocuments })}</span>
                </Link>
              </div>
            )}
          </Card>
        </div>
      </div>
    </div>
  )
}
