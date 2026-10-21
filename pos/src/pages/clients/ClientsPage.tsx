import { lazy, Suspense } from 'react'
import { NavLink, Navigate, Route, Routes, useLocation, useNavigate } from 'react-router-dom'
import { Download, Loader2, Upload, UserPlus } from 'lucide-react'
import { Button } from '../../components/ui/Button'
import { StorePicker } from '../../components/stats/statsUi'
import { ClientForm } from '../../components/clients/ClientForm'
import { ClientProfile } from '../../components/clients/ClientProfile'
import { useClientMeta, useClientScope, useParamSetter } from '../../components/clients/clientsUi'
import { CLIENT_SECTIONS } from '../../layout/nav'
import { activeStore, can, useAuth } from '../../lib/auth'
import { Permission } from '../../lib/types'
import { useT, type TranslationKey } from '../../lib/i18n'

const ClientsList = lazy(() => import('../../components/clients/ClientsList').then((m) => ({ default: m.ClientsList })))
const OverviewSection = lazy(() => import('../../components/clients/ClientSections').then((m) => ({ default: m.OverviewSection })))
const ActivitySection = lazy(() => import('../../components/clients/ClientSections').then((m) => ({ default: m.ActivitySection })))
const BirthdaysSection = lazy(() => import('../../components/clients/ClientSections').then((m) => ({ default: m.BirthdaysSection })))
const SegmentsSection = lazy(() => import('../../components/clients/ManageSections').then((m) => ({ default: m.SegmentsSection })))
const DebtsSection = lazy(() => import('../../components/clients/ManageSections').then((m) => ({ default: m.DebtsSection })))
const ExchangeSection = lazy(() => import('../../components/clients/ManageSections').then((m) => ({ default: m.ExchangeSection })))
const SettingsSection = lazy(() => import('../../components/clients/ManageSections').then((m) => ({ default: m.SettingsSection })))

type ProfileTab = 'overview' | 'purchases' | 'receipts' | 'debts' | 'bonus' | 'activity' | 'documents' | 'notes'
const PROFILE_TABS = new Set<string>(['overview', 'purchases', 'receipts', 'debts', 'bonus', 'activity', 'documents', 'notes'])

/**
 * «Клиенты» (ТЗ «Клиенты» v1.0): клиенттер базасы — тізім, шолу, сегменттер, қарыздар, белсенділік, туған күндер, импорт/экспорт, баптаулар.
 * Профиль (?client=), қосу/өзгерту (?new=1, ?edit=) кез келген бөлімнен ашылады; дүкендер таңдауы URL-да сақталады.
 */
export function ClientsPage() {
  const t = useT()
  const nav = useNavigate()
  const location = useLocation()
  const current = useAuth(activeStore)
  const meta = useClientMeta()
  const scope = useClientScope()
  const { get, set } = useParamSetter()
  const section = CLIENT_SECTIONS.find((s) => s.path !== '' && location.pathname.startsWith(`/clients/${s.path}`))?.path ?? ''
  const search = location.search
  if (!can(current, Permission.ClientsView)) return <Navigate to="/" replace />
  const key = section || 'list'
  const storeIds = scope.storeIds
  const canEdit = meta.data?.can.edit ?? false
  const tabParam = get('tab')
  const closeProfile = () => set({ client: null, tab: null, message: null, archive: null, tpl: null }, true)

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-end gap-3">
        <div className="mr-auto">
          <div className="text-[13px] text-[#66758f]">{t('nav.clients')}{section ? ` › ${t(`cli.section.${key}` as TranslationKey)}` : ''}</div>
          <h1 className="text-[28px] font-extrabold tracking-tight text-[#0b1f44]">{t(`cli.section.${key}` as TranslationKey)}</h1>
          <p className="text-[14px] text-[#66758f]">{t(`cli.sectionHint.${key}` as TranslationKey)}</p>
        </div>
        {scope.stores.length > 1 && section !== 'settings' && section !== 'exchange' && <StorePicker stores={scope.stores} value={scope.selected} onChange={scope.setStores} />}
        {section === '' && (
          <div className="flex flex-wrap gap-2">
            {canEdit && <Button variant="secondary" icon={<Upload size={16} />} onClick={() => nav(`/clients/exchange${search}`)}>{t('cli.import')}</Button>}
            {meta.data?.can.export && <Button variant="secondary" icon={<Download size={16} />} onClick={() => set({ export: '1' }, true)}>{t('cli.exportBtn')}</Button>}
            {canEdit && <Button icon={<UserPlus size={16} />} onClick={() => set({ new: '1' }, true)}>{t('cli.add')}</Button>}
          </div>
        )}
      </div>

      <nav aria-label={t('nav.clients')} className="-mx-1 flex gap-1 overflow-x-auto rounded-2xl border border-[#e4ecf7] bg-surface p-1.5 lg:hidden">
        {CLIENT_SECTIONS.map((s) => (
          <NavLink key={s.path} to={`/clients/${s.path}${search}`} end={s.path === ''}
            className={() => `flex shrink-0 items-center gap-2 rounded-xl px-3.5 py-2 text-[13px] font-semibold whitespace-nowrap ${section === s.path ? 'bg-brand-soft text-brand' : 'text-ink-2 hover:bg-field hover:text-ink'}`}>
            <s.icon size={16} />{t(s.label)}
          </NavLink>
        ))}
      </nav>

      <Suspense fallback={<div className="flex justify-center py-20 text-ink-3"><Loader2 className="animate-spin" size={28} /></div>}>
        <Routes>
          <Route index element={<ClientsList storeIds={storeIds} />} />
          <Route path="overview" element={<OverviewSection storeIds={storeIds} />} />
          <Route path="segments" element={<SegmentsSection storeIds={storeIds} />} />
          <Route path="debts" element={meta.data && !meta.data.can.debts ? <Navigate to={`/clients${search}`} replace /> : <DebtsSection storeIds={storeIds} />} />
          <Route path="activity" element={<ActivitySection storeIds={storeIds} />} />
          <Route path="birthdays" element={<BirthdaysSection storeIds={storeIds} />} />
          <Route path="exchange" element={<ExchangeSection storeIds={storeIds} />} />
          <Route path="settings" element={<SettingsSection />} />
          <Route path="*" element={<Navigate to={`/clients${search}`} replace />} />
        </Routes>
      </Suspense>

      {get('client') && (
        <ClientProfile key={get('client')} id={get('client')} storeIds={storeIds} onClose={closeProfile}
          initialTab={PROFILE_TABS.has(tabParam) ? (tabParam as ProfileTab) : 'overview'}
          initialModal={get('message') ? 'message' : get('archive') ? 'archive' : null} messageTemplate={get('tpl') || undefined}
          onEdit={(id) => set({ edit: id }, true)} onOpen={(id) => set({ client: id, tab: null, message: null, archive: null, tpl: null }, true)} />
      )}
      {(get('new') || get('edit')) && canEdit && (
        <ClientForm key={get('edit') || 'new'} id={get('edit') || null} onClose={() => set({ new: null, edit: null }, true)}
          onSaved={(card) => set({ new: null, edit: null, client: card.client.id }, true)}
          onOpen={(id) => set({ new: null, edit: null, client: id }, true)} />
      )}
    </div>
  )
}
