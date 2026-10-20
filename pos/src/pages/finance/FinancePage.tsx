import { lazy, Suspense, useMemo } from 'react'
import { NavLink, Navigate, Route, Routes, useLocation } from 'react-router-dom'
import { Loader2 } from 'lucide-react'
import { PeriodPicker, StorePicker } from '../../components/stats/statsUi'
import { FINANCE_SECTIONS } from '../../layout/nav'
import { useFinanceFilters } from '../../components/finance/financeUi'
import { activeStore, can, useAuth } from '../../lib/auth'
import { Permission } from '../../lib/types'
import { useT, type TranslationKey } from '../../lib/i18n'

const OverviewTab = lazy(() => import('../../components/finance/OverviewTab').then((m) => ({ default: m.OverviewTab })))
const PnlTab = lazy(() => import('../../components/finance/PnlTab').then((m) => ({ default: m.PnlTab })))
const CashFlowTab = lazy(() => import('../../components/finance/CashFlowTab').then((m) => ({ default: m.CashFlowTab })))
const IncomeExpenseTab = lazy(() => import('../../components/finance/IncomeExpenseTab').then((m) => ({ default: m.IncomeExpenseTab })))
const TransactionsTab = lazy(() => import('../../components/finance/TransactionsTab').then((m) => ({ default: m.TransactionsTab })))
const AccountsTab = lazy(() => import('../../components/finance/AccountsTab').then((m) => ({ default: m.AccountsTab })))
const ProductProfitTab = lazy(() => import('../../components/finance/ProductProfitTab').then((m) => ({ default: m.ProductProfitTab })))
const CustomerDebtsTab = lazy(() => import('../../components/finance/CustomerDebtsTab').then((m) => ({ default: m.CustomerDebtsTab })))
const SupplierDebtsTab = lazy(() => import('../../components/finance/SupplierDebtsTab').then((m) => ({ default: m.SupplierDebtsTab })))
const DocumentsTab = lazy(() => import('../../components/finance/DocumentsTab').then((m) => ({ default: m.DocumentsTab })))

/** Кезең сүзгісі керек емес подразделдер: қарыздар — ағымдағы жағдай, шоттар — ағымдағы қалдық (кезең тек выпискаға). */
const NO_PERIOD = new Set(['customer-debts'])

/**
 * «Финансы» (ТЗ «Финансы» v1.0): он подраздел, әрқайсысының өз міндеті мен құрылымы (§1.1). Кезең мен дүкен — URL-да, подраздел ауысқанда сақталады.
 * Сомалар серверде есептеледі; әр сома бастапқы құжатқа сілтемемен.
 */
export function FinancePage() {
  const t = useT()
  const me = useAuth((s) => s.me)
  const current = useAuth(activeStore)
  const location = useLocation()
  const { filters, update, storeIds } = useFinanceFilters()
  const section = FINANCE_SECTIONS.find((s) => s.path !== '' && location.pathname.startsWith(`/finance/${s.path}`))?.path ?? ''
  const search = location.search
  const stores = useMemo(() => (me?.stores ?? []).filter((s) => can(s, Permission.FinanceView)).map((s) => ({ id: s.id, name: s.name })), [me])
  if (!can(current, Permission.FinanceView)) return <Navigate to="/" replace />

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-end gap-3">
        <div className="mr-auto">
          <h1 className="text-[28px] font-extrabold tracking-tight text-[#0b1f44]">{t(`fin.section.${section || 'overview'}` as TranslationKey)}</h1>
          <p className="text-[14px] text-[#66758f]">{t(`fin.sectionHint.${section || 'overview'}` as TranslationKey)}</p>
        </div>
        {!NO_PERIOD.has(section) && <PeriodPicker from={filters.from} to={filters.to} onChange={(r) => update({ from: r.from, to: r.to })} />}
        {stores.length > 1 && <StorePicker stores={stores} value={filters.storeIds} onChange={(ids) => update({ stores: ids.join(',') })} />}
      </div>

      <nav aria-label={t('nav.finance')} className="-mx-1 flex gap-1 overflow-x-auto rounded-2xl border border-[#e4ecf7] bg-surface p-1.5 lg:hidden">
        {FINANCE_SECTIONS.map((s) => (
          <NavLink key={s.path} to={`/finance/${s.path}${search}`} end={s.path === ''}
            className={() => `flex shrink-0 items-center gap-2 rounded-xl px-3.5 py-2 text-[13px] font-semibold whitespace-nowrap ${section === s.path ? 'bg-brand-soft text-brand' : 'text-ink-2 hover:bg-field hover:text-ink'}`}>
            <s.icon size={16} />{t(s.label)}
          </NavLink>
        ))}
      </nav>

      <Suspense fallback={<div className="flex justify-center py-20 text-ink-3"><Loader2 className="animate-spin" size={28} /></div>}>
        <Routes>
          <Route index element={<OverviewTab filters={filters} storeIds={storeIds} />} />
          <Route path="pnl" element={<PnlTab filters={filters} storeIds={storeIds} />} />
          <Route path="cash-flow" element={<CashFlowTab filters={filters} storeIds={storeIds} />} />
          <Route path="income-expense" element={<IncomeExpenseTab filters={filters} storeIds={storeIds} />} />
          <Route path="transactions" element={<TransactionsTab filters={filters} storeIds={storeIds} />} />
          <Route path="accounts" element={<AccountsTab filters={filters} storeIds={storeIds} />} />
          <Route path="product-profit" element={<ProductProfitTab filters={filters} storeIds={storeIds} />} />
          <Route path="customer-debts" element={<CustomerDebtsTab filters={filters} storeIds={storeIds} />} />
          <Route path="supplier-debts" element={<SupplierDebtsTab filters={filters} storeIds={storeIds} />} />
          <Route path="documents" element={<DocumentsTab filters={filters} storeIds={storeIds} />} />
          <Route path="*" element={<Navigate to={`/finance${search}`} replace />} />
        </Routes>
      </Suspense>
    </div>
  )
}
