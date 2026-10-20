import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Link, useSearchParams } from 'react-router-dom'
import { ChevronDown, ChevronRight, Printer } from 'lucide-react'
import { Button } from '../ui/Button'
import { Toggle } from '../ui/Field'
import { financeApi } from '../../lib/financeApi'
import type { PnlRow } from '../../lib/financeTypes'
import { num, tenge } from '../../lib/money'
import { useT, type TranslationKey } from '../../lib/i18n'
import { BlockError, Skeleton } from '../stats/statsUi'
import { periodText } from '../stats/statsFormat'
import { Warn, type FinanceFilters } from './financeUi'

const PARENTS = new Set(['operatingExpenses', 'otherIncome', 'otherExpenses', 'taxes'])

/**
 * «Прибыль и убытки» (ТЗ «Финансы» §5): бір иерархиялық кесте — карточкалар мен диаграммаларсыз.
 * Бағандар: көрсеткіш, сома, % түсімнен, өткен кезең, өзгеріс. Жолды басу — балаларын ашады; соманы басу — бастапқы модульге өтеді.
 */
export function PnlTab({ filters, storeIds }: { filters: FinanceFilters; storeIds?: string }) {
  const t = useT()
  const [params, setParams] = useSearchParams()
  const compare = params.get('compare') !== '0'
  const [open, setOpen] = useState<Record<string, boolean>>({ operatingExpenses: true })
  const q = useQuery({ queryKey: ['finance', 'pnl', storeIds, filters.from, filters.to, compare], queryFn: () => financeApi.pnl(storeIds, filters.from, filters.to, compare) })
  if (q.isError) return <BlockError message={(q.error as Error).message} onRetry={() => void q.refetch()} />
  const d = q.data
  const search = `?from=${filters.from}&to=${filters.to}${storeIds ? `&stores=${storeIds}` : ''}`
  const label = (r: PnlRow) => r.label ?? t(`fin.pnl.${r.key}` as TranslationKey)
  const change = (r: PnlRow) => {
    if (r.change === null) return <span className="text-ink-3">—</span>
    const good = r.key.startsWith('operatingExpenses') || r.key.startsWith('otherExpenses') || r.key === 'cogs' || r.key === 'taxes' || r.key === 'discounts' || r.key === 'returns' ? r.change >= 0 : r.change >= 0
    return <span className={`tabular-nums ${r.change === 0 ? 'text-ink-3' : good ? 'text-success' : 'text-danger'}`}>{r.change > 0 ? '+' : ''}{num(r.change)}{r.changePercent !== null ? ` (${r.changePercent > 0 ? '+' : ''}${r.changePercent}%)` : ''}</span>
  }
  const visible = (d?.rows ?? []).filter((r) => r.level === 0 || open[r.key.split('.')[0]])

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center gap-3">
        <Toggle checked={compare} onChange={(v) => setParams((p) => { const n = new URLSearchParams(p); if (v) n.delete('compare'); else n.set('compare', '0'); return n }, { replace: true })} label={t('fin.comparePrev')} />
        <span className="ml-auto text-[13px] text-ink-3">{d ? periodText(d.from, d.to) : ''}{d?.prevFrom && d.prevTo ? ` · ${t('fin.prevPeriod')}: ${periodText(d.prevFrom, d.prevTo)}` : ''}</span>
        <Button variant="secondary" icon={<Printer size={16} />} onClick={() => window.print()}>{t('fin.print')}</Button>
      </div>
      {d && !d.costComplete && <Warn text={t('fin.pnl.costIncomplete')} />}
      {d && !d.taxesTracked && <Warn tone="blue" text={t('fin.pnl.taxesNotTracked')} />}
      {!d ? <Skeleton className="h-96" /> : (
        <div className="overflow-x-auto rounded-2xl border border-line bg-surface print:border-0">
          <table className="w-full min-w-[720px]">
            <thead className="bg-field/60 text-[13px] font-semibold text-ink-2">
              <tr>
                <th className="px-4 py-3 text-left">{t('fin.pnl.metric')}</th>
                <th className="px-4 py-3 text-right">{t('fin.pnl.amount')}</th>
                <th className="px-4 py-3 text-right">{t('fin.pnl.percentOfRevenue')}</th>
                {compare && <th className="px-4 py-3 text-right">{t('fin.prevPeriod')}</th>}
                {compare && <th className="px-4 py-3 text-right">{t('fin.pnl.change')}</th>}
                <th className="w-28 px-4 py-3 text-right">{t('fin.pnl.detail')}</th>
              </tr>
            </thead>
            <tbody>
              {visible.map((r) => {
                const parent = PARENTS.has(r.key)
                const hasChildren = parent && (d.rows.some((x) => x.key.startsWith(r.key + '.')))
                return (
                  <tr key={r.key} className={`border-t border-line ${r.isTotal ? 'bg-brand-soft/40 font-bold' : r.level > 0 ? 'text-[13px] text-ink-2' : ''}`}>
                    <td className="px-4 py-2.5">
                      <div className={`flex items-center gap-2 ${r.level > 0 ? 'pl-7' : ''}`}>
                        {hasChildren ? (
                          <button type="button" onClick={() => setOpen((o) => ({ ...o, [r.key]: !o[r.key] }))} className="flex items-center gap-1 text-left hover:text-brand" aria-expanded={!!open[r.key]}>
                            {open[r.key] ? <ChevronDown size={16} /> : <ChevronRight size={16} />}{label(r)}
                          </button>
                        ) : <span className={r.isTotal ? 'text-[15px]' : ''}>{label(r)}</span>}
                      </div>
                    </td>
                    <td className={`px-4 py-2.5 text-right tabular-nums ${r.amount < 0 && !r.isTotal ? 'text-danger' : r.isTotal && r.amount < 0 ? 'text-danger' : ''}`}>
                      {r.link ? <Link to={r.link.includes('?') ? `${r.link}&from=${filters.from}&to=${filters.to}` : `${r.link}${search}`} className="hover:text-brand hover:underline">{tenge(r.amount)}</Link> : tenge(r.amount)}
                    </td>
                    <td className="px-4 py-2.5 text-right tabular-nums text-ink-2">{r.percentOfRevenue === null ? '—' : `${r.percentOfRevenue}%`}</td>
                    {compare && <td className="px-4 py-2.5 text-right tabular-nums text-ink-2">{r.previous === null ? '—' : tenge(r.previous)}</td>}
                    {compare && <td className="px-4 py-2.5 text-right">{change(r)}</td>}
                    <td className="px-4 py-2.5 text-right text-[12px]">
                      {r.link && <Link to={r.link.includes('?') ? `${r.link}&from=${filters.from}&to=${filters.to}` : `${r.link}${search}`} className="font-semibold text-brand hover:underline">{t('fin.pnl.open')}</Link>}
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      )}
      {d && (
        <ul className="flex flex-col gap-1 text-[12px] text-ink-3">
          {d.notes.map((n) => <li key={n}>· {t(`fin.pnl.note.${n}` as TranslationKey)}</li>)}
        </ul>
      )}
    </div>
  )
}
