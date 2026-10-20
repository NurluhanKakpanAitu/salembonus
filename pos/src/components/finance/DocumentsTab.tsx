import { useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { ArrowDownLeft, ArrowLeftRight, ArrowUpRight, ChevronRight, FileText, HandCoins, Truck } from 'lucide-react'
import { financeKeys } from '../../lib/financeApi'
import { useT, type TranslationKey } from '../../lib/i18n'
import { Segmented } from '../stats/statsUi'
import { useFinanceMeta, useFinancePermissions, type FinanceFilters } from './financeUi'
import { OperationForm } from './OperationForm'
import { OperationsJournal } from './OperationsJournal'
import { TransferModal } from './TransferModal'

/** Есептер каталогы (§13.3): әрқайсысы — дайын бет не экспорт параметрлері. */
const REPORTS: { key: string; to: string }[] = [
  { key: 'pnl', to: '/finance/pnl' },
  { key: 'cashflow', to: '/finance/cash-flow' },
  { key: 'statement', to: '/finance/accounts' },
  { key: 'categories', to: '/finance/income-expense' },
  { key: 'productProfit', to: '/finance/product-profit' },
  { key: 'customerDebts', to: '/finance/customer-debts' },
  { key: 'supplierDebts', to: '/finance/supplier-debts' },
  { key: 'supplierPayments', to: '/finance/transactions?type=SupplierPayment' },
  { key: 'customerPayments', to: '/finance/transactions?type=CustomerPayment' },
  { key: 'collections', to: '/finance/transactions?type=Collection' },
  { key: 'reconciliation', to: '/finance/supplier-debts' },
  { key: 'journal', to: '/finance/transactions' },
]

/**
 * «Документы и отчёты» (ТЗ «Финансы» §13): «Документы» — жылдам батырмалар мен құжаттар кестесі (черновиктерді қоса);
 * «Отчёты» — каталог, әр есеп өз параметрлерімен ашылады, экспорт — сүзгінің барлық жолы.
 */
export function DocumentsTab({ filters, storeIds }: { filters: FinanceFilters; storeIds?: string }) {
  const t = useT()
  const qc = useQueryClient()
  const nav = useNavigate()
  const [params, setParams] = useSearchParams()
  const { canOperate } = useFinancePermissions()
  const meta = useFinanceMeta(storeIds)
  const view = (params.get('view') || 'documents') as 'documents' | 'reports'
  const [form, setForm] = useState<'Income' | 'Expense' | 'transfer' | null>(null)
  const invalidate = () => void qc.invalidateQueries({ queryKey: financeKeys.all })
  const search = `?from=${filters.from}&to=${filters.to}${storeIds ? `&stores=${storeIds}` : ''}`
  const quick = [
    { key: 'income', icon: <ArrowDownLeft size={18} />, tone: 'bg-success-soft text-success', onClick: () => setForm('Income') },
    { key: 'expense', icon: <ArrowUpRight size={18} />, tone: 'bg-danger-soft text-danger', onClick: () => setForm('Expense') },
    { key: 'transfer', icon: <ArrowLeftRight size={18} />, tone: 'bg-brand-soft text-brand', onClick: () => setForm('transfer') },
    { key: 'supplierPayment', icon: <Truck size={18} />, tone: 'bg-violet-50 text-violet-600', onClick: () => nav(`/finance/supplier-debts${search}`) },
    { key: 'customerPayment', icon: <HandCoins size={18} />, tone: 'bg-amber-50 text-amber-700', onClick: () => nav(`/finance/customer-debts${search}`) },
  ]
  return (
    <div className="flex flex-col gap-4">
      <Segmented value={view} options={[{ key: 'documents', label: t('fin.docs.documents') }, { key: 'reports', label: t('fin.docs.reports') }]}
        onChange={(v) => setParams((p) => { const n = new URLSearchParams(p); if (v === 'documents') n.delete('view'); else n.set('view', v); return n }, { replace: true })} />
      {view === 'documents' ? (
        <>
          {canOperate && (
            <div className="grid grid-cols-2 gap-3 md:grid-cols-3 xl:grid-cols-5">
              {quick.map((b) => (
                <button key={b.key} type="button" onClick={b.onClick} className="flex items-center gap-3 rounded-2xl border border-line bg-surface p-3 text-left hover:border-brand">
                  <span className={`flex size-10 shrink-0 items-center justify-center rounded-xl ${b.tone}`}>{b.icon}</span>
                  <span><span className="block text-[14px] font-semibold">{t(`fin.docs.quick.${b.key}` as TranslationKey)}</span><span className="block text-[12px] text-ink-3">{t(`fin.docs.quickHint.${b.key}` as TranslationKey)}</span></span>
                </button>
              ))}
            </div>
          )}
          <OperationsJournal filters={filters} storeIds={storeIds} show={['type', 'status', 'counterparty', 'account', 'search']} />
        </>
      ) : (
        <ul className="grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-3">
          {REPORTS.map((r) => (
            <li key={r.key}>
              <Link to={r.to.includes('?') ? `${r.to}&${search.slice(1)}` : `${r.to}${search}`} className="flex items-center gap-3 rounded-2xl border border-line bg-surface p-4 hover:border-brand">
                <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-brand-soft text-brand"><FileText size={18} /></span>
                <span className="min-w-0 flex-1"><span className="block text-[14px] font-semibold">{t(`fin.docs.report.${r.key}` as TranslationKey)}</span><span className="block text-[12px] text-ink-3">{t(`fin.docs.reportHint.${r.key}` as TranslationKey)}</span></span>
                <ChevronRight size={18} className="text-ink-3" />
              </Link>
            </li>
          ))}
        </ul>
      )}
      {form === 'transfer' && <TransferModal accounts={meta.data?.accounts ?? []} onClose={() => setForm(null)} onDone={() => { setForm(null); invalidate() }} />}
      {(form === 'Income' || form === 'Expense') && (
        <OperationForm accounts={meta.data?.accounts ?? []} categories={meta.data?.categories ?? []} kind={form} onClose={() => setForm(null)}
          onSaved={() => { setForm(null); invalidate() }} onCategoriesChanged={() => void meta.refetch()} />
      )}
    </div>
  )
}
