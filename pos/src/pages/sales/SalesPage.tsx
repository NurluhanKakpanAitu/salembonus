import { useEffect, useState } from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useSearchParams } from 'react-router-dom'
import {
  ArrowDown, ArrowUp, Calculator, Coins, Download, Filter, Gift, HandCoins, Package, Percent, Printer, ReceiptText, RotateCcw, ShoppingCart, Undo2,
} from 'lucide-react'
import { Button } from '../../components/ui/Button'
import { Input, Select } from '../../components/ui/Field'
import { RowMenu } from '../../components/ui/RowMenu'
import { SearchInput } from '../../components/ui/SearchInput'
import { toast } from '../../components/ui/Toast'
import { EmptyRow, Pager, StatCard, td, th, useDebounced } from '../../components/inventory/inventoryUi'
import { printReceipt } from '../../components/cashier/ReceiptView'
import { ReturnWizard } from '../../components/sales/ReturnWizard'
import { SaleCard, SaleStatusBadge } from '../../components/sales/SaleCard'
import { ApiError } from '../../lib/api'
import { activeStore, can, useAuth } from '../../lib/auth'
import { cashierApi } from '../../lib/cashierApi'
import type { CashierCustomer, PaymentMethod } from '../../lib/cashierTypes'
import { addDays, dateTime, isoDate } from '../../lib/dates'
import { num, tenge } from '../../lib/money'
import { salesApi, salesKeys } from '../../lib/salesApi'
import type { SaleHistoryStatus, SalesQuery, SalesSort } from '../../lib/salesTypes'
import { Permission } from '../../lib/types'
import { useT, type TranslationKey } from '../../lib/i18n'

type Period = 'today' | 'yesterday' | 'week' | 'month' | 'thisMonth' | 'lastMonth' | 'custom' | 'all'
const PERIODS: Period[] = ['today', 'yesterday', 'week', 'month', 'thisMonth', 'lastMonth', 'custom', 'all']
const METHODS: PaymentMethod[] = ['Cash', 'Card', 'Qr', 'Transfer', 'Debt']
const STATUSES: SaleHistoryStatus[] = ['Completed', 'PartiallyReturned', 'Returned', 'Debt']
const METHOD_TONE: Record<PaymentMethod, string> = {
  Cash: 'bg-success-soft text-success', Card: 'bg-brand-soft text-brand', Qr: 'bg-violet-100 text-violet-700',
  Transfer: 'bg-sky-100 text-sky-700', Debt: 'bg-amber-100 text-amber-800',
}

/** ТЗ §4: әдепкі — ағымдағы ай. «Прошлый месяц» — күнтізбелік. */
function periodRange(p: Period, custom: { from: string; to: string }): { from?: string; to?: string } {
  const today = new Date()
  const first = (y: number, m: number) => new Date(y, m, 1)
  switch (p) {
    case 'today': return { from: isoDate(today), to: isoDate(today) }
    case 'yesterday': return { from: isoDate(addDays(today, -1)), to: isoDate(addDays(today, -1)) }
    case 'week': return { from: isoDate(addDays(today, -6)), to: isoDate(today) }
    case 'month': return { from: isoDate(addDays(today, -29)), to: isoDate(today) }
    case 'thisMonth': return { from: isoDate(first(today.getFullYear(), today.getMonth())), to: isoDate(today) }
    case 'lastMonth': return { from: isoDate(first(today.getFullYear(), today.getMonth() - 1)), to: isoDate(addDays(first(today.getFullYear(), today.getMonth()), -1)) }
    case 'custom': return custom
    default: return {}
  }
}

/**
 * «Продажи» (ТЗ «Продажи» v1.0): KPI, іздеу мен сүзгілер, серверлік кесте, оң жақта таңдалған чектің карточкасы.
 * Барлық күй URL-да: карточканы жабу сүзгі мен бетті тастамайды (§3). Жаңа сатылым мұнда жасалмайды — тек кассада.
 */
export function SalesPage() {
  const t = useT()
  const store = useAuth(activeStore)
  const canReturn = can(store, Permission.SalesReturn)
  const canExport = can(store, Permission.SalesExport)
  const canSell = can(store, Permission.SalesCreate)
  const [params, setParams] = useSearchParams()
  const [search, setSearch] = useState(params.get('search') ?? '')
  const debounced = useDebounced(search.trim(), 350)
  const [filtersOpen, setFiltersOpen] = useState(() => ['registerId', 'staffUserId', 'method', 'status', 'hasReturn', 'customerId', 'noCustomer'].some((k) => params.get(k)))
  const [returning, setReturning] = useState<string | null>(null)

  const period = (params.get('period') as Period) || 'thisMonth'
  const custom = { from: params.get('from') ?? isoDate(addDays(new Date(), -6)), to: params.get('to') ?? isoDate(new Date()) }
  const selected = params.get('sale')
  const sort = (params.get('sort') as SalesSort) || 'date'
  const dir = params.get('dir') === 'asc' ? 'asc' : 'desc'
  const page = Math.max(1, Number(params.get('page')) || 1)
  const pageSize = Number(params.get('pageSize')) || 20
  const get = (k: string) => params.get(k) ?? ''

  const update = (patch: Record<string, string>, resetPage = true) =>
    setParams((prev) => {
      const next = new URLSearchParams(prev)
      for (const [k, v] of Object.entries(patch)) { if (v) next.set(k, v); else next.delete(k) }
      if (resetPage) next.delete('page')
      return next
    }, { replace: true })
  useEffect(() => { if ((params.get('search') ?? '') !== debounced) update({ search: debounced }) }, [debounced])

  const query: SalesQuery = {
    ...periodRange(period, custom), search: debounced, registerId: get('registerId'), staffUserId: get('staffUserId'), method: get('method'),
    status: get('status'), hasReturn: get('hasReturn'), customerId: get('customerId'), noCustomer: get('noCustomer') === '1', sort, dir, page, pageSize,
  }
  const { page: _p, pageSize: _s, sort: _o, dir: _d, ...kpiQuery } = query
  void _p; void _s; void _o; void _d
  const list = useQuery({ queryKey: [...salesKeys.history, store?.id, query], queryFn: () => salesApi.history(query), placeholderData: keepPreviousData })
  const summary = useQuery({ queryKey: [...salesKeys.summary, store?.id, kpiQuery], queryFn: () => salesApi.summary(kpiQuery), placeholderData: keepPreviousData })
  const filters = useQuery({ queryKey: [...salesKeys.filters, store?.id], queryFn: salesApi.filters })
  const items = list.data?.items ?? []
  const s = summary.data
  const filtered = !!(debounced || get('registerId') || get('staffUserId') || get('method') || get('status') || get('hasReturn') || get('customerId') || get('noCustomer'))

  const resetAll = () => { setSearch(''); setParams(selected ? { sale: selected } : {}, { replace: true }) }
  const toggleSort = (key: SalesSort) => update({ sort: key, dir: sort === key && dir === 'desc' ? 'asc' : 'desc' }, false)
  const exportXlsx = async () => {
    try { await salesApi.exportXlsx(query) } catch (err) { toast(err instanceof ApiError ? err.message : String(err), 'error') }
  }
  const sortIcon = (key: SalesSort) => sort === key ? (dir === 'asc' ? <ArrowUp size={13} className="inline" /> : <ArrowDown size={13} className="inline" />) : null

  return (
    <div className="flex min-h-0 gap-4">
      <div className="min-w-0 flex-1">
        <div className="mb-4 flex flex-wrap items-start justify-between gap-3">
          <div>
            <h1 className="text-[26px] font-extrabold tracking-tight">{t('sales.title')}</h1>
            <p className="text-[14px] text-ink-2">{t('sales.subtitle')}</p>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <Select className="w-44!" value={period} onChange={(e) => update({ period: e.target.value })}>
              {PERIODS.map((p) => <option key={p} value={p}>{t(`sales.period.${p}` as TranslationKey)}</option>)}
            </Select>
            {period === 'custom' && (
              <>
                <Input type="date" className="w-40!" value={custom.from} onChange={(e) => update({ from: e.target.value })} />
                <Input type="date" className="w-40!" value={custom.to} onChange={(e) => update({ to: e.target.value })} />
              </>
            )}
            {canSell && (
              <a href="/cashier" target="_blank" rel="noopener"
                className="inline-flex h-11 items-center gap-2 rounded-xl bg-brand px-5 text-[15px] font-semibold text-white hover:bg-brand-hover">
                <Calculator size={18} />{t('sales.newSale')}
              </a>
            )}
          </div>
        </div>

        <div className="mb-3 grid grid-cols-2 gap-3 xl:grid-cols-4">
          <button type="button" className="text-left" onClick={() => update({ status: '', hasReturn: '' })}>
            <StatCard icon={<ShoppingCart size={20} />} label={t('sales.kpi.total')} value={s ? <>{num(s.salesCount)} <span className="text-[15px] font-semibold text-ink-2">/ {tenge(s.salesTotal)}</span></> : '…'}
              hint={s?.salesChangePercent != null ? t('sales.kpi.change', { p: (s.salesChangePercent > 0 ? '+' : '') + num(s.salesChangePercent) }) : s ? t('sales.kpi.totalHint', { n: s.salesCount }) : undefined} />
          </button>
          <button type="button" className="text-left" onClick={() => update({ hasReturn: 'yes', status: '' })}>
            <StatCard icon={<Undo2 size={20} />} label={t('sales.kpi.returns')} value={s ? <>{num(s.returnsCount)} <span className="text-[15px] font-semibold text-ink-2">/ {tenge(s.returnsTotal)}</span></> : '…'}
              hint={s ? t('sales.kpi.returnsHint', { n: s.returnsCount, p: num(s.returnsShare) }) : undefined} tone="danger" />
          </button>
          <button type="button" className="text-left" onClick={() => update({ status: 'Debt', hasReturn: '' })}>
            <StatCard icon={<HandCoins size={20} />} label={t('sales.kpi.debt')} value={s ? <>{num(s.debtCount)} <span className="text-[15px] font-semibold text-ink-2">/ {tenge(s.debtTotal)}</span></> : '…'}
              hint={s ? t('sales.kpi.debtHint', { n: s.debtCount, sum: tenge(s.debtOpen) }) : undefined} tone="amber" />
          </button>
          <StatCard icon={<Coins size={20} />} label={t('sales.kpi.average')} value={s ? tenge(s.averageCheck) : '…'} hint={s ? t('sales.kpi.averageHint', { n: s.salesCount }) : undefined} tone="violet" />
        </div>
        <div className="mb-4 grid grid-cols-2 gap-3 xl:grid-cols-4">
          <StatCard icon={<Package size={20} />} label={t('sales.kpi.units')} value={s ? num(s.units) : '…'} hint={t('sales.kpi.unitsHint')} tone="success" />
          <StatCard icon={<Percent size={20} />} label={t('sales.kpi.discounts')} value={s ? tenge(s.discounts) : '…'} hint={s ? t('sales.kpi.discountsHint', { n: s.discountCount, p: num(s.discountsShare) }) : undefined} tone="amber" />
          <StatCard icon={<Gift size={20} />} label={t('sales.kpi.bonus')} value={s ? tenge(s.bonusRedeemed) : '…'} hint={s ? t('sales.kpi.bonusHint', { n: s.bonusCount, p: num(s.bonusShare) }) : undefined} tone="violet" />
          <button type="button" className="text-left" onClick={() => update({ hasReturn: 'yes' })}>
            <StatCard icon={<ReceiptText size={20} />} label={t('sales.kpi.withReturn')} value={s ? num(s.withReturn) : '…'} hint={s ? t('sales.kpi.withReturnHint', { p: num(s.withReturnShare) }) : undefined} />
          </button>
        </div>

        <div className="mb-3 flex flex-wrap items-center gap-2">
          <SearchInput className="w-full sm:w-96" value={search} onChange={setSearch} placeholder={t('sales.search')} />
          <Button variant="secondary" className={filtersOpen ? 'border-brand! text-brand!' : ''} icon={<Filter size={17} />} onClick={() => setFiltersOpen((v) => !v)}>
            {t('sales.filters')}
          </Button>
          {canExport && <Button variant="secondary" icon={<Download size={17} />} onClick={() => void exportXlsx()}>{t('sales.export')}</Button>}
          {filtered && (
            <Button variant="secondary" className="ml-auto" icon={<RotateCcw size={16} />} onClick={resetAll}>{t('sales.resetAll')}</Button>
          )}
        </div>
        {filtersOpen && (
          <div className="mb-4 flex flex-wrap items-center gap-2 rounded-2xl border border-line bg-surface p-3">
            <Select className="w-44!" value={get('registerId')} onChange={(e) => update({ registerId: e.target.value })}>
              <option value="">{t('sales.filter.allRegisters')}</option>
              {filters.data?.registers.map((r) => <option key={r.id} value={r.id}>{r.name}</option>)}
            </Select>
            <Select className="w-48!" value={get('staffUserId')} onChange={(e) => update({ staffUserId: e.target.value })}>
              <option value="">{t('sales.filter.allCashiers')}</option>
              {filters.data?.cashiers.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
            </Select>
            <Select className="w-48!" value={get('method')} onChange={(e) => update({ method: e.target.value })}>
              <option value="">{t('sales.filter.allMethods')}</option>
              {METHODS.map((m) => <option key={m} value={m}>{t(`pay.${m}`)}</option>)}
            </Select>
            <Select className="w-44!" value={get('status')} onChange={(e) => update({ status: e.target.value })}>
              <option value="">{t('sales.filter.allStatuses')}</option>
              {STATUSES.map((x) => <option key={x} value={x}>{t(`sales.status.${x}`)}</option>)}
            </Select>
            <Select className="w-44!" value={get('hasReturn')} onChange={(e) => update({ hasReturn: e.target.value })}>
              <option value="">{t('sales.filter.anyReturn')}</option>
              <option value="yes">{t('sales.filter.withReturn')}</option>
              <option value="no">{t('sales.filter.withoutReturn')}</option>
            </Select>
            <CustomerFilter customerId={get('customerId')} noCustomer={get('noCustomer') === '1'}
              onChange={(c) => update({ customerId: c.customerId, noCustomer: c.noCustomer ? '1' : '' })} />
          </div>
        )}

        <div className={`overflow-x-auto rounded-2xl border border-line bg-surface transition-opacity ${list.isFetching && !list.isLoading ? 'opacity-70' : ''}`}>
          <table className="w-full min-w-[920px] border-collapse">
            <thead className="bg-field">
              <tr>
                <th className={`${th} cursor-pointer`} onClick={() => toggleSort('number')}>{t('sales.col.number')} {sortIcon('number')}</th>
                <th className={`${th} cursor-pointer`} onClick={() => toggleSort('date')}>{t('sales.col.date')} {sortIcon('date')}</th>
                <th className={th}>{t('sales.col.customer')}</th>
                <th className={`${th} text-right`}>{t('sales.col.items')}</th>
                <th className={`${th} cursor-pointer text-right`} onClick={() => toggleSort('total')}>{t('sales.col.total')} {sortIcon('total')}</th>
                <th className={`${th} cursor-pointer text-right`} onClick={() => toggleSort('returned')}>{t('sales.col.returned')} {sortIcon('returned')}</th>
                <th className={`${th} cursor-pointer text-right`} onClick={() => toggleSort('net')}>{t('sales.col.net')} {sortIcon('net')}</th>
                <th className={`${th} cursor-pointer`} onClick={() => toggleSort('status')}>{t('sales.col.status')} {sortIcon('status')}</th>
                <th className={`${th} text-right`}>{t('catalog.col.actions')}</th>
              </tr>
            </thead>
            <tbody>
              {list.isLoading && <EmptyRow colSpan={9} text={t('common.loading')} />}
              {list.error && (
                <tr><td colSpan={9} className="px-4 py-8 text-center text-danger">{list.error.message}
                  <button type="button" className="ml-2 font-semibold underline" onClick={() => void list.refetch()}>{t('common.retry')}</button></td></tr>
              )}
              {!list.isLoading && !list.error && items.length === 0 && (
                <tr><td colSpan={9} className="px-4 py-10 text-center text-ink-3">
                  <div className="font-semibold text-ink-2">{t('sales.empty')}</div><div className="text-[13px]">{t('sales.emptyHint')}</div>
                  {filtered && <button type="button" className="mt-2 text-[13px] font-semibold text-brand" onClick={resetAll}>{t('sales.resetAll')}</button>}
                </td></tr>
              )}
              {items.map((r) => (
                <tr key={r.id} onClick={() => update({ sale: r.id }, false)}
                  className={`cursor-pointer border-t border-line ${selected === r.id ? 'bg-brand-soft/60' : 'hover:bg-field/40'}`}>
                  <td className={`${td} font-semibold text-brand`}>{t('pos.receiptNo', { n: r.number })}</td>
                  <td className={`${td} whitespace-nowrap text-[13px]`}>{dateTime(r.createdAt)}</td>
                  <td className={td}>{r.customerName ?? <span className="text-ink-3">—</span>}</td>
                  <td className={`${td} text-right`}>{num(r.units)}</td>
                  <td className={`${td} text-right font-semibold`}>{tenge(r.total)}</td>
                  <td className={`${td} text-right ${r.returned > 0 ? 'text-danger' : 'text-ink-3'}`}>{tenge(r.returned)}</td>
                  <td className={`${td} text-right font-bold`}>{tenge(r.net)}</td>
                  <td className={td}>
                    <div className="flex flex-wrap items-center gap-1">
                      <SaleStatusBadge status={r.status} />
                      {r.methods.map((m) => <span key={m} className={`rounded-md px-1.5 py-0.5 text-[11px] font-medium ${METHOD_TONE[m]}`}>{t(`pay.${m}`)}</span>)}
                    </div>
                  </td>
                  <td className={`${td} text-right`} onClick={(e) => e.stopPropagation()}>
                    <RowMenu items={[
                      { label: t('sales.open'), icon: <ReceiptText size={16} />, onClick: () => update({ sale: r.id }, false) },
                      { label: t('sales.print'), icon: <Printer size={16} />, onClick: () => void salesApi.receipt(r.id).then((x) => printReceipt(x, t)) },
                      ...(canReturn && r.status !== 'Returned' ? [{ label: t('sales.return'), icon: <Undo2 size={16} />, danger: true, onClick: () => setReturning(r.id) }] : []),
                    ]} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <Pager page={page} pageSize={pageSize} total={list.data?.total ?? 0}
          onPage={(p) => update({ page: String(p) }, false)} onPageSize={(x) => update({ pageSize: String(x) })} />
      </div>

      {selected && (
        <div className="fixed inset-0 z-30 bg-black/40 p-3 xl:static xl:z-auto xl:w-[420px] xl:shrink-0 xl:bg-transparent xl:p-0" onMouseDown={(e) => { if (e.target === e.currentTarget) update({ sale: '' }, false) }}>
          <div className="h-full xl:sticky xl:top-4 xl:h-[calc(100dvh-6rem)]">
            <SaleCard key={selected} saleId={selected} canReturn={canReturn} onClose={() => update({ sale: '' }, false)} onReturn={() => setReturning(selected)} />
          </div>
        </div>
      )}

      {returning && (
        <ReturnWizard saleId={returning} source="office" onClose={() => setReturning(null)}
          onDone={(r) => { setReturning(null); update({ sale: r.id }, false) }} />
      )}
    </div>
  )
}

/** «Клиент» сүзгісі (ТЗ §6.2): бәрі / нақты клиент (аты не телефоны бойынша іздеу) / клиентсіз. */
function CustomerFilter({ customerId, noCustomer, onChange }: {
  customerId: string
  noCustomer: boolean
  onChange: (v: { customerId: string; noCustomer: boolean }) => void
}) {
  const t = useT()
  const [q, setQ] = useState('')
  const term = useDebounced(q.trim(), 300)
  const results = useQuery({ queryKey: ['sales', 'customers', term], queryFn: () => cashierApi.searchCustomers(term), enabled: term.length >= 2 })
  const current = useQuery({ queryKey: ['sales', 'customer', customerId], queryFn: () => cashierApi.customer(customerId), enabled: !!customerId })
  const pick = (c: CashierCustomer) => { onChange({ customerId: c.id, noCustomer: false }); setQ('') }

  if (customerId) {
    return (
      <span className="inline-flex h-11 items-center gap-2 rounded-xl bg-brand-soft px-3 text-[13px] font-medium text-brand">
        {current.data ? current.data.fullName || current.data.phone : '…'}
        <button type="button" aria-label={t('common.close')} className="text-brand/70 hover:text-brand" onClick={() => onChange({ customerId: '', noCustomer: false })}>×</button>
      </span>
    )
  }
  return (
    <div className="relative flex items-center gap-2">
      <Select className="w-40!" value={noCustomer ? 'none' : ''} onChange={(e) => onChange({ customerId: '', noCustomer: e.target.value === 'none' })}>
        <option value="">{t('sales.filter.allCustomers')}</option>
        <option value="none">{t('sales.filter.noCustomer')}</option>
      </Select>
      {!noCustomer && (
        <div className="relative">
          <Input className="w-56!" value={q} onChange={(e) => setQ(e.target.value)} placeholder={t('sales.filter.customerSearch')} />
          {term.length >= 2 && results.data && results.data.length > 0 && (
            <ul className="absolute left-0 top-12 z-20 max-h-64 w-72 overflow-y-auto rounded-xl border border-line bg-surface py-1 shadow-lg">
              {results.data.map((c) => (
                <li key={c.id}>
                  <button type="button" className="flex w-full flex-col px-3 py-2 text-left hover:bg-field" onClick={() => pick(c)}>
                    <span className="text-[14px] font-medium">{c.fullName || c.phone}</span>
                    <span className="text-[12px] text-ink-3">{c.phone}</span>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
    </div>
  )
}
