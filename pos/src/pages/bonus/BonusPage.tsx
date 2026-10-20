import { lazy, Suspense, useMemo } from 'react'
import { NavLink, Navigate, Route, Routes, useLocation } from 'react-router-dom'
import { Loader2 } from 'lucide-react'
import { PeriodPicker, StorePicker } from '../../components/stats/statsUi'
import { BONUS_SECTIONS } from '../../layout/nav'
import { useBonusFilters } from '../../components/bonus/bonusUi'
import { activeStore, can, useAuth } from '../../lib/auth'
import { Permission } from '../../lib/types'
import { useT, type TranslationKey } from '../../lib/i18n'

const OverviewTab = lazy(() => import('../../components/bonus/OverviewTab').then((m) => ({ default: m.OverviewTab })))
const StatusesTab = lazy(() => import('../../components/bonus/StatusesTab').then((m) => ({ default: m.StatusesTab })))
const ExpiryTab = lazy(() => import('../../components/bonus/ExpiryTab').then((m) => ({ default: m.ExpiryTab })))
const BirthdayTab = lazy(() => import('../../components/bonus/BirthdayTab').then((m) => ({ default: m.BirthdayTab })))
const CampaignsTab = lazy(() => import('../../components/bonus/CampaignsTab').then((m) => ({ default: m.CampaignsTab })))
const StatisticsTab = lazy(() => import('../../components/bonus/StatisticsTab').then((m) => ({ default: m.StatisticsTab })))
const HistoryTab = lazy(() => import('../../components/bonus/HistoryTab').then((m) => ({ default: m.HistoryTab })))
const RulesTab = lazy(() => import('../../components/bonus/RulesTab').then((m) => ({ default: m.RulesTab })))

/** Кезең қажет бөлімдер: шолу, статистика, журнал (§1.3). Қалғаны — ағымдағы баптаулар мен тізімдер. */
const WITH_PERIOD = new Set(['', 'statistics', 'history'])

/**
 * «Бонусы» (ТЗ «Бонусы» v1.0): сегіз бөлім, бірінші — «Обзор» (§18). Бағдарлама — ағымдағы дүкеннің SalemBonus бағдарламасы;
 * кезең мен дүкендер URL-да, бөлім ауысқанда сақталады.
 */
export function BonusPage() {
  const t = useT()
  const me = useAuth((s) => s.me)
  const current = useAuth(activeStore)
  const location = useLocation()
  const { filters, update, storeIds } = useBonusFilters()
  const section = BONUS_SECTIONS.find((s) => s.path !== '' && location.pathname.startsWith(`/bonus/${s.path}`))?.path ?? ''
  const search = location.search
  const stores = useMemo(() => (me?.stores ?? []).filter((s) => can(s, Permission.BonusView)).map((s) => ({ id: s.id, name: s.name })), [me])
  if (!can(current, Permission.BonusView)) return <Navigate to="/" replace />
  const key = section || 'overview'

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-end gap-3">
        <div className="mr-auto">
          <div className="text-[13px] text-[#66758f]">{t('nav.bonus')}{section ? ` › ${t(`bon.section.${key}` as TranslationKey)}` : ''}</div>
          <h1 className="text-[28px] font-extrabold tracking-tight text-[#0b1f44]">{t(`bon.section.${key}` as TranslationKey)}</h1>
          <p className="text-[14px] text-[#66758f]">{t(`bon.sectionHint.${key}` as TranslationKey)}</p>
        </div>
        {WITH_PERIOD.has(section) && <PeriodPicker from={filters.from} to={filters.to} onChange={(r) => update({ from: r.from, to: r.to })} />}
        {WITH_PERIOD.has(section) && stores.length > 1 && <StorePicker stores={stores} value={filters.storeIds} onChange={(ids) => update({ stores: ids.join(',') })} />}
      </div>

      <nav aria-label={t('nav.bonus')} className="-mx-1 flex gap-1 overflow-x-auto rounded-2xl border border-[#e4ecf7] bg-surface p-1.5 lg:hidden">
        {BONUS_SECTIONS.map((s) => (
          <NavLink key={s.path} to={`/bonus/${s.path}${search}`} end={s.path === ''}
            className={() => `flex shrink-0 items-center gap-2 rounded-xl px-3.5 py-2 text-[13px] font-semibold whitespace-nowrap ${section === s.path ? 'bg-brand-soft text-brand' : 'text-ink-2 hover:bg-field hover:text-ink'}`}>
            <s.icon size={16} />{t(s.label)}
          </NavLink>
        ))}
      </nav>

      <Suspense fallback={<div className="flex justify-center py-20 text-ink-3"><Loader2 className="animate-spin" size={28} /></div>}>
        <Routes>
          <Route index element={<OverviewTab filters={filters} storeIds={storeIds} />} />
          <Route path="statuses" element={<StatusesTab />} />
          <Route path="expiry" element={<ExpiryTab filters={filters} storeIds={storeIds} />} />
          <Route path="birthday" element={<BirthdayTab />} />
          <Route path="campaigns" element={<CampaignsTab />} />
          <Route path="statistics" element={<StatisticsTab filters={filters} storeIds={storeIds} />} />
          <Route path="history" element={<HistoryTab filters={filters} storeIds={storeIds} />} />
          <Route path="rules" element={<RulesTab />} />
          <Route path="*" element={<Navigate to={`/bonus${search}`} replace />} />
        </Routes>
      </Suspense>
    </div>
  )
}
