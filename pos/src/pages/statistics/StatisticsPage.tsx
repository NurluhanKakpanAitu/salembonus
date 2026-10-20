import { lazy, Suspense, useMemo, useState } from 'react'
import { NavLink, Navigate, Route, Routes, useLocation, useSearchParams } from 'react-router-dom'
import {
  Banknote, Boxes, ChartColumn, Gift, LayoutDashboard, Loader2, Monitor, Package, ShoppingCart, Truck, UserCog, Users,
} from 'lucide-react'
import { Select } from '../../components/ui/Field'
import { toast } from '../../components/ui/Toast'
import { ExportMenu, PeriodPicker, presetRange, StorePicker } from '../../components/stats/statsUi'
import { PdfReportModal } from '../../components/stats/PdfReport'
import { ApiError } from '../../lib/api'
import { analyticsApi } from '../../lib/analyticsApi'
import type { AnalyticsFilters, AnalyticsTab, Compare } from '../../lib/analyticsTypes'
import { activeStore, can, useAuth } from '../../lib/auth'
import { Permission } from '../../lib/types'
import { useT, type TranslationKey } from '../../lib/i18n'

const OverviewTab = lazy(() => import('../../components/stats/OverviewTab').then((m) => ({ default: m.OverviewTab })))
const DetailTab = lazy(() => import('../../components/stats/DetailTab').then((m) => ({ default: m.DetailTab })))

const TAB_ICONS: Record<AnalyticsTab, typeof ChartColumn> = {
  overview: LayoutDashboard, sales: ShoppingCart, products: Package, inventory: Boxes, suppliers: Truck,
  finance: Banknote, bonus: Gift, customers: Users, employees: UserCog, registers: Monitor,
}
const ORDER: AnalyticsTab[] = ['overview', 'sales', 'products', 'inventory', 'suppliers', 'finance', 'bonus', 'customers', 'employees', 'registers']

/** Жалпы сүзгілер URL-да: вкладка ауысқанда сақталады (§24), бетті жаңартса да қалады. */
export function useAnalyticsFilters() {
  const [params, setParams] = useSearchParams()
  const store = useAuth(activeStore)
  const def = presetRange('thisMonth')
  const filters: AnalyticsFilters = {
    from: params.get('from') ?? def.from,
    to: params.get('to') ?? def.to,
    storeIds: params.get('stores')?.split(',').filter(Boolean) ?? (store ? [store.id] : []),
    compare: (params.get('compare') as Compare) || 'prev',
  }
  const update = (patch: Record<string, string | null>) =>
    setParams((prev) => {
      const next = new URLSearchParams(prev)
      for (const [k, v] of Object.entries(patch)) { if (v) next.set(k, v); else next.delete(k) }
      next.delete('page')
      return next
    }, { replace: true })
  return { filters, params, update }
}

/**
 * «Статистика» (ТЗ «Статистика» v1.1): тақырып, кезең, дүкендер, салыстыру, экспорт; он вкладка.
 * Тек оқиды — әр блоктан бастапқы модульге өтуге болады, операция жасалмайды (§1).
 */
export function StatisticsPage() {
  const t = useT()
  const me = useAuth((s) => s.me)
  const current = useAuth(activeStore)
  const location = useLocation()
  const { filters, params, update } = useAnalyticsFilters()
  const [exporting, setExporting] = useState(false)
  const [pdfOpen, setPdfOpen] = useState(false)
  const tab = (ORDER.find((x) => location.pathname.endsWith(`/statistics/${x}`)) ?? 'overview') as AnalyticsTab
  const search = location.search

  // Тек статистика құқығы бар дүкендер (§28); «Все магазины» — бәріне құқық болса ғана.
  const stores = useMemo(() => (me?.stores ?? []).filter((s) => can(s, Permission.StatisticsView)).map((s) => ({ id: s.id, name: s.name })), [me])
  const canExport = can(current, Permission.StatisticsExport)

  const local = () => {
    const extra: Record<string, string> = {}
    params.forEach((v, k) => { if (!['from', 'to', 'stores', 'compare'].includes(k)) extra[k] = v })
    return extra
  }
  const exportFile = async (format: 'xlsx' | 'csv') => {
    setExporting(true)
    try {
      await analyticsApi.exportFile(tab, format, filters, local())
      toast(t('stats.exportReady'))
    } catch (err) {
      toast(err instanceof ApiError ? err.message : t('stats.exportFailed'), 'error')
    } finally {
      setExporting(false)
    }
  }

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-end gap-3">
        <div className="mr-auto">
          <h1 className="text-[28px] font-extrabold tracking-tight text-[#0b1f44]">{t('stats.title')}</h1>
          <p className="text-[14px] text-[#66758f]">{t('stats.subtitleFull')}</p>
        </div>
        <PeriodPicker from={filters.from} to={filters.to} onChange={(r) => update({ from: r.from, to: r.to })} />
        {stores.length > 1 && <StorePicker stores={stores} value={filters.storeIds} onChange={(ids) => update({ stores: ids.join(',') })} />}
        <Select className="w-auto! min-w-48" aria-label={t('stats.compare')} value={filters.compare} onChange={(e) => update({ compare: e.target.value })}>
          {(['prev', 'year', 'none'] as Compare[]).map((c) => <option key={c} value={c}>{t(`stats.compareOpt.${c}` as TranslationKey)}</option>)}
        </Select>
        {canExport && <ExportMenu busy={exporting} onExcel={() => void exportFile('xlsx')} onCsv={() => void exportFile('csv')} onPdf={() => setPdfOpen(true)} />}
      </div>

      <nav aria-label={t('stats.title')} className="-mx-1 flex gap-1 overflow-x-auto rounded-2xl border border-[#e4ecf7] bg-surface p-1.5">
        {ORDER.map((x) => {
          const Icon = TAB_ICONS[x]
          return (
            <NavLink key={x} to={`/statistics/${x === 'overview' ? '' : x}${search}`} end={x === 'overview'}
              className={() => `flex shrink-0 items-center gap-2 rounded-xl px-3.5 py-2 text-[13px] font-semibold whitespace-nowrap ${tab === x ? 'bg-brand-soft text-brand shadow-[inset_0_-2px_0_var(--color-brand)]' : 'text-ink-2 hover:bg-field hover:text-ink'}`}>
              <Icon size={16} />{t(`stats.tab.${x}` as TranslationKey)}
            </NavLink>
          )
        })}
      </nav>

      <Suspense fallback={<div className="flex justify-center py-20 text-ink-3"><Loader2 className="animate-spin" size={28} /></div>}>
        <Routes>
          <Route index element={<OverviewTab filters={filters} />} />
          {ORDER.filter((x) => x !== 'overview').map((x) => (
            <Route key={x} path={x} element={<DetailTab key={x} tab={x as Exclude<AnalyticsTab, 'overview'>} filters={filters} />} />
          ))}
          <Route path="*" element={<Navigate to={`/statistics${search}`} replace />} />
        </Routes>
      </Suspense>

      {pdfOpen && <PdfReportModal filters={filters} tab={tab} extra={local()} stores={stores} onClose={() => setPdfOpen(false)} />}
    </div>
  )
}
