import { useMemo, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Navigate, useSearchParams } from 'react-router-dom'
import { Crown, UserPlus, UserRound, Users } from 'lucide-react'
import { Button } from '../../components/ui/Button'
import { Select } from '../../components/ui/Field'
import { SearchInput } from '../../components/ui/SearchInput'
import { EmptyRow, StatCard, td, th, useDebounced } from '../../components/inventory/inventoryUi'
import { BlockError, Skeleton } from '../../components/stats/statsUi'
import { StaffForm, TempPasswordModal } from '../../components/staff/StaffForm'
import { StaffProfile } from '../../components/staff/StaffProfile'
import { RoleBadge } from '../../components/staff/staffUi'
import { activeStore, can, useAuth } from '../../lib/auth'
import { dateTime } from '../../lib/dates'
import { useT, type TranslationKey } from '../../lib/i18n'
import { num, tenge } from '../../lib/money'
import { formatPhoneInput, matchesPhone } from '../../lib/phone'
import { staffAdminApi, type StaffRow } from '../../lib/staffAdminApi'
import { Permission, type StaffRole } from '../../lib/types'

/**
 * «Сотрудники»: тізім (іздеу, рөл/күй/дүкен сүзгілері), жолды басу — карточка (?staff=), қосу/өзгерту — шторка (?new=1, ?edit=).
 * Көрсеткіштер: барлығы, белсенді, кассирлер, иелер мен әкімшілер.
 */
export function StaffPage() {
  const t = useT()
  const qc = useQueryClient()
  const current = useAuth(activeStore)
  const [params, setParams] = useSearchParams()
  const [search, setSearch] = useState('')
  const q = useDebounced(search.trim().toLowerCase(), 200)
  const [role, setRole] = useState('')
  const [status, setStatus] = useState<'all' | 'active' | 'inactive'>('active')
  const [store, setStore] = useState('')
  const [temp, setTemp] = useState<{ phone: string; password: string } | null>(null)
  const list = useQuery({ queryKey: ['staff', 'list'], queryFn: staffAdminApi.list })
  const set = (patch: Record<string, string | null>) => setParams((p) => {
    const n = new URLSearchParams(p)
    for (const [k, v] of Object.entries(patch)) { if (v) n.set(k, v); else n.delete(k) }
    return n
  }, { replace: true })
  const rows = useMemo(() => (list.data?.rows ?? []).filter((r) =>
    (!q || `${r.firstName} ${r.lastName} ${r.phone}`.toLowerCase().includes(q) || matchesPhone(r.phone, q))
    && (!role || r.role === role) && (status === 'all' || (status === 'active') === r.isActive) && (!store || r.storeIds.includes(store))), [list.data, q, role, status, store])
  if (!can(current, Permission.StaffManage)) return <Navigate to="/" replace />
  const all = list.data?.rows ?? []
  const kpis: [string, number, 'brand' | 'success' | 'violet' | 'amber', React.ReactNode][] = [
    [t('stf.kpi.total'), all.length, 'brand', <Users size={18} key="t" />],
    [t('stf.kpi.active'), all.filter((r) => r.isActive).length, 'success', <UserRound size={18} key="a" />],
    [t('stf.kpi.cashiers'), all.filter((r) => r.isActive && r.role === 'Cashier').length, 'violet', <UserRound size={18} key="c" />],
    [t('stf.kpi.managers'), all.filter((r) => r.isActive && r.role !== 'Cashier').length, 'amber', <Crown size={18} key="m" />],
  ]
  const staffId = params.get('staff')
  const editId = params.get('edit')
  const creating = params.get('new') === '1'

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-end gap-3">
        <div className="mr-auto">
          <h1 className="text-[28px] font-extrabold tracking-tight text-[#0b1f44]">{t('stf.title')}</h1>
          <p className="text-[14px] text-[#66758f]">{t('stf.hint')}</p>
        </div>
        <Button icon={<UserPlus size={16} />} onClick={() => set({ new: '1' })}>{t('stf.add')}</Button>
      </div>

      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        {kpis.map(([label, value, tone, icon]) => <StatCard key={label} icon={icon} label={label} value={num(value)} tone={tone} />)}
      </div>

      <div className="flex flex-wrap items-center gap-2">
        <SearchInput value={search} onChange={setSearch} placeholder={t('stf.search')} className="w-full sm:w-72" />
        <Select className="h-11! w-auto min-w-40" value={role} onChange={(e) => setRole(e.target.value)} aria-label={t('stf.col.role')}>
          <option value="">{t('stf.filter.role')}</option>
          {(['Owner', 'Admin', 'Cashier'] as StaffRole[]).map((r) => <option key={r} value={r}>{t(`role.${r}` as TranslationKey)}</option>)}
        </Select>
        <Select className="h-11! w-auto min-w-36" value={status} onChange={(e) => setStatus(e.target.value as typeof status)} aria-label={t('stf.col.status')}>
          {(['active', 'inactive', 'all'] as const).map((s) => <option key={s} value={s}>{t(`stf.filter.${s}` as TranslationKey)}</option>)}
        </Select>
        {(list.data?.stores.length ?? 0) > 1 && (
          <Select className="h-11! w-auto min-w-44" value={store} onChange={(e) => setStore(e.target.value)} aria-label={t('stf.col.stores')}>
            <option value="">{t('stf.filter.store')}</option>
            {list.data!.stores.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
          </Select>
        )}
      </div>

      {list.isError ? <BlockError message={(list.error as Error).message} onRetry={() => void list.refetch()} /> : !list.data ? <Skeleton className="h-64" /> : (
        <div className="overflow-x-auto rounded-2xl border border-line bg-surface">
          <table className="w-full min-w-[920px]">
            <thead className="border-b border-line bg-field/60"><tr>
              <th className={th}>{t('stf.col.name')}</th><th className={th}>{t('stf.col.role')}</th><th className={th}>{t('stf.col.stores')}</th><th className={th}>{t('stf.col.access')}</th>
              <th className={th}>{t('stf.col.lastLogin')}</th><th className={`${th} text-right`}>{t('stf.col.sales')}</th><th className={th}>{t('stf.col.status')}</th>
            </tr></thead>
            <tbody>
              {rows.length === 0 && <EmptyRow colSpan={7} text={all.length === 0 ? t('stf.empty') : t('stf.notFound')} />}
              {rows.map((r) => <Row key={r.id} r={r} onOpen={() => set({ staff: r.id })} />)}
            </tbody>
          </table>
        </div>
      )}

      {staffId && !editId && <StaffProfile key={staffId} id={staffId} onClose={() => set({ staff: null })} onEdit={() => set({ edit: staffId })} />}
      {(creating || editId) && list.data && (
        <StaffForm key={editId ?? 'new'} id={editId} list={list.data} onClose={() => set({ new: null, edit: null })}
          onSaved={(saved) => {
            qc.setQueryData(['staff', saved.staff.staff.id], saved.staff)
            void qc.invalidateQueries({ queryKey: ['staff', 'list'] })
            set({ new: null, edit: null, staff: saved.staff.staff.id })
            if (saved.tempPassword) setTemp({ phone: saved.staff.staff.phone, password: saved.tempPassword })
          }} />
      )}
      {temp && <TempPasswordModal phone={temp.phone} password={temp.password} onClose={() => setTemp(null)} />}
    </div>
  )
}

function Row({ r, onOpen }: { r: StaffRow; onOpen: () => void }) {
  const t = useT()
  return (
    <tr className="cursor-pointer border-b border-line last:border-0 hover:bg-field/60" onClick={onOpen}>
      <td className={td}>
        <span className="flex items-center gap-2.5">
          <span className={`flex size-9 shrink-0 items-center justify-center rounded-full text-[13px] font-bold ${r.isActive ? 'bg-brand-soft text-brand' : 'bg-field text-ink-3'}`}>{(r.firstName[0] ?? '') + (r.lastName[0] ?? '')}</span>
          <span className="min-w-0">
            <span className="block font-semibold">{r.firstName} {r.lastName}{r.isSelf && <span className="ml-1.5 text-[12px] font-normal text-ink-3">· {t('stf.you')}</span>}</span>
            <span className="block text-[12px] text-ink-3">{formatPhoneInput(r.phone)}</span>
          </span>
        </span>
      </td>
      <td className={td}><RoleBadge role={r.role} size="sm" /></td>
      <td className={`${td} max-w-56`}><span className="block break-words text-[13px]">{r.storeNames.join(', ') || '—'}</span></td>
      <td className={td}>
        <span className="flex flex-wrap gap-1">
          <span className={`rounded-md px-1.5 py-0.5 text-[12px] ${r.hasPin ? 'bg-success-soft text-success' : 'bg-field text-ink-3'}`}>{r.hasPin ? t('stf.pin') : t('stf.noPin')}</span>
          {r.role !== 'Cashier' && <span className={`rounded-md px-1.5 py-0.5 text-[12px] ${r.hasPassword ? 'bg-success-soft text-success' : 'bg-field text-ink-3'}`}>{t('stf.password')}</span>}
        </span>
      </td>
      <td className={`${td} whitespace-nowrap text-[13px] text-ink-2`}>{r.lastLoginAt ? dateTime(r.lastLoginAt) : t('stf.never')}</td>
      <td className={`${td} whitespace-nowrap text-right`}><span className="block font-semibold">{num(r.sales30)}</span><span className="block text-[12px] text-ink-3">{tenge(r.revenue30)}</span></td>
      <td className={td}>
        <span className={`rounded-full px-2 py-0.5 text-[12px] font-semibold ${!r.isActive ? 'bg-danger-soft text-danger' : r.isLocked ? 'bg-amber-50 text-amber-700' : 'bg-success-soft text-success'}`}>
          {!r.isActive ? t('stf.status.inactive') : r.isLocked ? t('stf.status.locked') : t('stf.status.active')}
        </span>
      </td>
    </tr>
  )
}
