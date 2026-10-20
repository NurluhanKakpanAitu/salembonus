import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useSearchParams } from 'react-router-dom'
import { ArrowLeftRight, Plus } from 'lucide-react'
import { Button } from '../ui/Button'
import { Select } from '../ui/Field'
import { financeApi, financeKeys } from '../../lib/financeApi'
import { tenge } from '../../lib/money'
import { useT, type TranslationKey } from '../../lib/i18n'
import { Donut, Legend, type Slice } from '../stats/Charts'
import { ComboChart } from '../stats/ComboChart'
import { BlockError, Card, Skeleton } from '../stats/statsUi'
import { daysToChart, SHARE_COLORS, useFinanceMeta, useFinancePermissions, type FinanceFilters } from './financeUi'
import { OperationForm } from './OperationForm'
import { OperationsJournal } from './OperationsJournal'
import { TransferModal } from './TransferModal'

/**
 * «Движение денег» (ТЗ «Финансы» §6): басындағы қалдық, сыртқы түсім/төлем, аударым тобы (кіріс те, шығыс та емес), соңындағы қалдық;
 * шот бойынша сүзгі; журнал; «Добавить операцию» — кіріс/шығыс/аударым (жеткізушіге төлем, клиент төлемі, инкассация — өз процесінен).
 */
export function CashFlowTab({ filters, storeIds }: { filters: FinanceFilters; storeIds?: string }) {
  const t = useT()
  const qc = useQueryClient()
  const [params, setParams] = useSearchParams()
  const { canOperate } = useFinancePermissions()
  const meta = useFinanceMeta(storeIds)
  const accountId = params.get('account') || undefined
  const [form, setForm] = useState<'Income' | 'Expense' | 'transfer' | null>(null)
  const q = useQuery({ queryKey: ['finance', 'cash-flow', storeIds, filters.from, filters.to, accountId], queryFn: () => financeApi.cashFlow(storeIds, filters.from, filters.to, accountId) })
  const d = q.data
  const invalidate = () => void qc.invalidateQueries({ queryKey: financeKeys.all })
  const chart = d ? daysToChart(d.days, t) : null
  const slices = (xs: { key: string; amount: number; percent: number }[]): Slice[] =>
    xs.map((s, i) => ({ key: s.key, label: t(`fin.type.${s.key}` as TranslationKey), value: s.amount, color: SHARE_COLORS[i % SHARE_COLORS.length], extra: `${s.percent}%` }))
  const Stat = ({ label, value, tone }: { label: string; value: number; tone?: string }) => (
    <div className="rounded-2xl border border-line bg-surface p-3">
      <div className="text-[12px] font-medium text-ink-2">{label}</div>
      <div className={`mt-0.5 text-[20px] font-extrabold tracking-tight tabular-nums ${tone ?? ''}`}>{tenge(value)}</div>
    </div>
  )

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center gap-2">
        <Select className="h-10! w-auto! min-w-48" value={accountId ?? ''} onChange={(e) => setParams((p) => { const n = new URLSearchParams(p); if (e.target.value) n.set('account', e.target.value); else n.delete('account'); n.delete('page'); return n }, { replace: true })} aria-label={t('fin.account')}>
          <option value="">{t('fin.filter.allAccounts')}</option>
          {(meta.data?.accounts ?? []).map((a) => <option key={a.id} value={a.id}>{a.name}</option>)}
        </Select>
        <span className="ml-auto" />
        {canOperate && (
          <>
            <Button variant="secondary" className="h-10! px-3!" icon={<ArrowLeftRight size={16} />} onClick={() => setForm('transfer')}>{t('fin.transfer')}</Button>
            <Button className="h-10! px-3!" icon={<Plus size={16} />} onClick={() => setForm('Expense')}>{t('fin.addOperation')}</Button>
          </>
        )}
      </div>
      {q.isError ? <BlockError message={(q.error as Error).message} onRetry={() => void q.refetch()} /> : !d ? <Skeleton className="h-24" /> : (
        <div className="grid grid-cols-2 gap-3 md:grid-cols-3 xl:grid-cols-6">
          <Stat label={t('fin.cf.opening')} value={d.opening} />
          <Stat label={t('fin.cf.inflow')} value={d.inflow} tone="text-success" />
          <Stat label={t('fin.cf.outflow')} value={d.outflow} tone="text-danger" />
          <Stat label={t('fin.cf.transfers')} value={d.transfers} tone="text-brand" />
          <Stat label={t('fin.cf.adjustments')} value={d.adjustments} tone="text-amber-700" />
          <Stat label={t('fin.cf.closing')} value={d.closing} />
        </div>
      )}
      <div className="grid grid-cols-1 gap-4 xl:grid-cols-3">
        <Card title={t('fin.chart.title')} className="xl:col-span-2" info={t('fin.cf.info')}>
          {!chart ? <Skeleton className="h-56" /> : <ComboChart buckets={chart.buckets} series={chart.series} granularity="day" height={220} />}
        </Card>
        <Card title={t('fin.cf.structure')}>
          {!d ? <Skeleton className="h-56" /> : (
            <div className="flex flex-col gap-3">
              {[{ title: t('fin.cf.inflow'), xs: d.inflowByType }, { title: t('fin.cf.outflow'), xs: d.outflowByType }].map((g) => (
                <div key={g.title} className="flex items-center gap-3">
                  {g.xs.length === 0 ? <span className="text-[13px] text-ink-3">{g.title}: —</span> : (
                    <>
                      <Donut slices={slices(g.xs)} center={tenge(g.xs.reduce((s, x) => s + x.amount, 0))} sub={g.title} size={110} />
                      <Legend slices={slices(g.xs)} format={(s) => tenge(s.value)} />
                    </>
                  )}
                </div>
              ))}
            </div>
          )}
        </Card>
      </div>
      <OperationsJournal filters={filters} storeIds={storeIds} show={['type', 'category', 'source', 'counterparty', 'status', 'search']} />
      {form === 'transfer' && <TransferModal accounts={meta.data?.accounts ?? []} onClose={() => setForm(null)} onDone={() => { setForm(null); invalidate() }} />}
      {(form === 'Income' || form === 'Expense') && (
        <OperationForm accounts={meta.data?.accounts ?? []} categories={meta.data?.categories ?? []} kind={form} onClose={() => setForm(null)}
          onSaved={() => { setForm(null); invalidate() }} onCategoriesChanged={() => void meta.refetch()} />
      )}
    </div>
  )
}
