import { lazy, Suspense } from 'react'
import { NavLink, Navigate, Route, Routes, useLocation } from 'react-router-dom'
import { Loader2 } from 'lucide-react'
import { SETTINGS_SECTIONS } from '../../layout/nav'
import { activeStore, can, useAuth } from '../../lib/auth'
import { Permission } from '../../lib/types'
import { useT, type TranslationKey } from '../../lib/i18n'

const StoreSection = lazy(() => import('../../components/settings/StoreSection').then((m) => ({ default: m.StoreSection })))
const StoresSection = lazy(() => import('../../components/settings/StoresSection').then((m) => ({ default: m.StoresSection })))
const RegistersSection = lazy(() => import('../../components/settings/RegistersSection').then((m) => ({ default: m.RegistersSection })))
const PaymentsSection = lazy(() => import('../../components/settings/PaymentsSection').then((m) => ({ default: m.PaymentsSection })))
const NotificationsSection = lazy(() => import('../../components/settings/NotificationsSection').then((m) => ({ default: m.NotificationsSection })))
const SecuritySection = lazy(() => import('../../components/settings/SecuritySection').then((m) => ({ default: m.SecuritySection })))
const PlanSection = lazy(() => import('../../components/settings/PlanSection').then((m) => ({ default: m.PlanSection })))
const AuditTable = lazy(() => import('../../components/staff/AuditTable').then((m) => ({ default: m.AuditTable })))

/**
 * «Настройка»: дүкен профилі, дүкендер, кассалар, төлем мен чек, хабарламалар, қауіпсіздік, әрекет журналы, тариф.
 * Бөлімдер — сол жақ мәзірде (SETTINGS_SECTIONS), тар экранда — жолақ.
 */
export function SettingsPage() {
  const t = useT()
  const current = useAuth(activeStore)
  const location = useLocation()
  const section = SETTINGS_SECTIONS.find((s) => s.path !== '' && location.pathname.startsWith(`/settings/${s.path}`))?.path ?? ''
  if (!can(current, Permission.SettingsManage)) return <Navigate to="/" replace />
  const key = section || 'store'

  return (
    <div className="flex flex-col gap-4">
      <div>
        <div className="text-[13px] text-[#66758f]">{t('nav.settings')}{section ? ` › ${t(`set.section.${key}` as TranslationKey)}` : ''}</div>
        <h1 className="text-[28px] font-extrabold tracking-tight text-[#0b1f44]">{t(`set.section.${key}` as TranslationKey)}</h1>
        <p className="text-[14px] text-[#66758f]">{t(`set.sectionHint.${key}` as TranslationKey)}</p>
      </div>

      <nav aria-label={t('nav.settings')} className="-mx-1 flex gap-1 overflow-x-auto rounded-2xl border border-[#e4ecf7] bg-surface p-1.5 lg:hidden">
        {SETTINGS_SECTIONS.map((s) => (
          <NavLink key={s.path} to={`/settings/${s.path}`} end={s.path === ''}
            className={() => `flex shrink-0 items-center gap-2 rounded-xl px-3.5 py-2 text-[13px] font-semibold whitespace-nowrap ${section === s.path ? 'bg-brand-soft text-brand' : 'text-ink-2 hover:bg-field hover:text-ink'}`}>
            <s.icon size={16} />{t(s.label)}
          </NavLink>
        ))}
      </nav>

      <Suspense fallback={<div className="flex justify-center py-20 text-ink-3"><Loader2 className="animate-spin" size={28} /></div>}>
        <Routes>
          <Route index element={<StoreSection />} />
          <Route path="stores" element={<StoresSection />} />
          <Route path="registers" element={<RegistersSection />} />
          <Route path="payments" element={<PaymentsSection />} />
          <Route path="notifications" element={<NotificationsSection />} />
          <Route path="security" element={<SecuritySection />} />
          <Route path="audit" element={<AuditTable />} />
          <Route path="plan" element={<PlanSection />} />
          <Route path="*" element={<Navigate to="/settings" replace />} />
        </Routes>
      </Suspense>
    </div>
  )
}
