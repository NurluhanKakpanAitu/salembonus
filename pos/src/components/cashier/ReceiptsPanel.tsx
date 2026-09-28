import { useEffect, useState } from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { ChevronLeft, ChevronRight, Eye, Printer, Search, Undo2, X } from 'lucide-react'
import { RowMenu } from '../ui/RowMenu'
import { cashierApi } from '../../lib/cashierApi'
import type { PaymentMethod, SaleStatus } from '../../lib/cashierTypes'
import { addDays, dateTime, isoDate } from '../../lib/dates'
import { tenge } from '../../lib/money'
import { useT, type TranslationKey } from '../../lib/i18n'

type Period = 'today' | 'yesterday' | 'week' | 'month' | 'all'
const PERIODS: Period[] = ['today', 'yesterday', 'week', 'month', 'all']

function range(p: Period): { from: string | null; to: string | null } {
  const today = new Date()
  switch (p) {
    case 'today': return { from: isoDate(today), to: isoDate(today) }
    case 'yesterday': return { from: isoDate(addDays(today, -1)), to: isoDate(addDays(today, -1)) }
    case 'week': return { from: isoDate(addDays(today, -6)), to: isoDate(today) }
    case 'month': return { from: isoDate(addDays(today, -29)), to: isoDate(today) }
    default: return { from: null, to: null }
  }
}

export const METHOD_STYLE: Record<PaymentMethod, string> = {
  Cash: 'bg-success-soft text-success', Card: 'bg-brand-soft text-brand', Qr: 'bg-violet-100 text-violet-700',
  Transfer: 'bg-sky-100 text-sky-700', Debt: 'bg-amber-100 text-amber-800',
}

export function StatusPill({ status }: { status: SaleStatus }) {
  const t = useT()
  if (status === 'Completed') return null
  return (
    <span className={`rounded-md px-1.5 py-0.5 text-[11px] font-medium ${status === 'Returned' ? 'bg-danger-soft text-danger' : 'bg-amber-100 text-amber-800'}`}>
      {t(`receipts.status.${status}` as TranslationKey)}
    </span>
  )
}

/**
 * Чектер тарихы (ТЗ «Касса» §12): «Чеки» басылғанда себеттің орнында. Тізімде тек нөмір мен қысқа
 * мәлімет (§12.2), толығы — чекті ашқанда.
 */
export function ReceiptsPanel({ canReturn, onClose, onOpen, onReturn, onPrint }: {
  canReturn: boolean
  onClose: () => void
  onOpen: (id: string) => void
  onReturn: (id: string) => void
  onPrint: (id: string) => void
}) {
  const t = useT()
  const [period, setPeriod] = useState<Period>('today')
  const [q, setQ] = useState('')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const pageSize = 15

  useEffect(() => {
    const id = setTimeout(() => { setSearch(q.trim()); setPage(1) }, 300)
    return () => clearTimeout(id)
  }, [q])

  const { from, to } = range(period)
  const query = useQuery({
    queryKey: ['cashier', 'sales', period, search, page],
    queryFn: () => cashierApi.sales({ from: search ? null : from, to: search ? null : to, search, page, pageSize }),
    placeholderData: keepPreviousData,
  })
  const items = query.data?.items ?? []
  const pages = Math.max(1, Math.ceil((query.data?.total ?? 0) / pageSize))

  return (
    <aside className="flex min-h-0 w-full shrink-0 flex-col rounded-2xl bg-surface lg:w-[520px] xl:w-[600px]">
      <div className="flex items-center justify-between border-b border-line px-4 py-3">
        <h2 className="text-[18px] font-bold">{t('receipts.title')}</h2>
        <button type="button" aria-label={t('common.close')} onClick={onClose}
          className="flex size-9 items-center justify-center rounded-lg text-ink-2 hover:bg-field"><X size={19} /></button>
      </div>

      <div className="flex flex-col gap-2 border-b border-line p-3">
        <div className="grid grid-cols-5 gap-1 rounded-xl bg-field p-1">
          {PERIODS.map((p) => (
            <button key={p} type="button" onClick={() => { setPeriod(p); setPage(1) }}
              className={`rounded-lg py-1.5 text-[13px] font-medium ${period === p && !search ? 'bg-brand text-white' : 'text-ink-2 hover:text-ink'}`}>
              {t(`receipts.period.${p}` as TranslationKey)}
            </button>
          ))}
        </div>
        <div className="relative">
          <Search size={17} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-ink-3" />
          <input value={q} onChange={(e) => setQ(e.target.value)} placeholder={t('receipts.search')}
            className="h-10 w-full rounded-xl border border-line bg-field pl-9 pr-3 text-[14px] outline-none focus:border-brand focus:bg-surface" />
        </div>
      </div>

      <div className={`min-h-0 flex-1 overflow-y-auto transition-opacity ${query.isFetching && !query.isLoading ? 'opacity-70' : ''}`}>
        <table className="w-full text-[13px]">
          <thead className="sticky top-0 bg-surface text-left text-[12px] text-ink-3">
            <tr className="border-b border-line">
              <th className="px-3 py-2 font-medium">№</th>
              <th className="px-2 py-2 font-medium">{t('receipts.col.date')}</th>
              <th className="px-2 py-2 font-medium">{t('receipts.col.customer')}</th>
              <th className="px-2 py-2 text-right font-medium">{t('receipts.col.sum')}</th>
              <th className="px-2 py-2 font-medium">{t('receipts.col.payment')}</th>
              <th className="w-10" />
            </tr>
          </thead>
          <tbody>
            {query.isLoading && <tr><td colSpan={6} className="py-10 text-center text-ink-3">{t('common.loading')}</td></tr>}
            {!query.isLoading && items.length === 0 && <tr><td colSpan={6} className="py-10 text-center text-ink-3">{t('receipts.empty')}</td></tr>}
            {items.map((s) => (
              <tr key={s.id} onClick={() => onOpen(s.id)} className="cursor-pointer border-b border-line hover:bg-field/60">
                <td className="px-3 py-2.5 font-bold">{s.number}</td>
                <td className="px-2 py-2.5 whitespace-nowrap text-ink-2">{dateTime(s.createdAt)}</td>
                <td className="max-w-28 truncate px-2 py-2.5">{s.customerName ?? '—'}</td>
                <td className="px-2 py-2.5 text-right font-semibold whitespace-nowrap">
                  {tenge(s.total)}
                  <div><StatusPill status={s.status} /></div>
                </td>
                <td className="px-2 py-2.5">
                  <div className="flex flex-wrap gap-1">
                    {s.methods.map((m) => <span key={m} className={`rounded-md px-1.5 py-0.5 text-[11px] font-medium ${METHOD_STYLE[m]}`}>{t(`pay.${m}`)}</span>)}
                  </div>
                </td>
                <td className="pr-2" onClick={(e) => e.stopPropagation()}>
                  <RowMenu items={[
                    { label: t('receipts.open'), icon: <Eye size={16} />, onClick: () => onOpen(s.id) },
                    ...(canReturn && s.status !== 'Returned'
                      ? [{ label: t('receipts.return'), icon: <Undo2 size={16} />, danger: true, onClick: () => onReturn(s.id) }] : []),
                    { label: t('receipts.print'), icon: <Printer size={16} />, onClick: () => onPrint(s.id) },
                  ]} />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {pages > 1 && (
        <div className="flex items-center justify-center gap-2 border-t border-line p-3 text-[14px]">
          <button type="button" disabled={page <= 1} onClick={() => setPage(page - 1)} aria-label="Previous"
            className="flex size-9 items-center justify-center rounded-lg border border-line disabled:opacity-40"><ChevronLeft size={17} /></button>
          <span className="min-w-16 text-center">{page} / {pages}</span>
          <button type="button" disabled={page >= pages} onClick={() => setPage(page + 1)} aria-label="Next"
            className="flex size-9 items-center justify-center rounded-lg border border-line disabled:opacity-40"><ChevronRight size={17} /></button>
        </div>
      )}
    </aside>
  )
}
