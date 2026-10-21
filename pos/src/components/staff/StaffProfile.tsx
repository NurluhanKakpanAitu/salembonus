import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Ban, CircleCheck, KeyRound, LogOut, Pencil, RotateCcw } from 'lucide-react'
import { Button } from '../ui/Button'
import { confirmDialog } from '../ui/Confirm'
import { Drawer } from '../ui/Drawer'
import { RowMenu } from '../ui/RowMenu'
import { toast } from '../ui/Toast'
import { BlockError, Skeleton } from '../stats/statsUi'
import { ApiError } from '../../lib/api'
import { dateOnly, dateTime } from '../../lib/dates'
import { useT, type TranslationKey } from '../../lib/i18n'
import { num, tenge } from '../../lib/money'
import { permKey } from '../../lib/permissions'
import { formatPhoneInput } from '../../lib/phone'
import { staffAdminApi, type StaffDetail } from '../../lib/staffAdminApi'
import { AuditTable } from './AuditTable'
import { SetPinModal, TempPasswordModal } from './StaffForm'
import { deviceName, InfoRow, RoleBadge } from './staffUi'

type Tab = 'overview' | 'access' | 'activity'

/** Қызметкер карточкасы: шолу (байланыс, кіру, сатылым, сеанстар), дүкендер бойынша қолжетімділік, әрекет журналы; әрекеттер төменде. */
export function StaffProfile({ id, onClose, onEdit }: { id: string; onClose: () => void; onEdit: () => void }) {
  const t = useT()
  const qc = useQueryClient()
  const q = useQuery({ queryKey: ['staff', id], queryFn: () => staffAdminApi.get(id) })
  const [tab, setTab] = useState<Tab>('overview')
  const [pinOpen, setPinOpen] = useState(false)
  const [temp, setTemp] = useState<string | null>(null)
  const d = q.data
  const name = d ? `${d.staff.firstName} ${d.staff.lastName}`.trim() : ''
  const apply = (next: StaffDetail) => { qc.setQueryData(['staff', id], next); void qc.invalidateQueries({ queryKey: ['staff', 'list'] }) }
  const run = async (fn: () => Promise<StaffDetail>, message: string) => {
    try { apply(await fn()); toast(message) } catch (err) { toast(err instanceof ApiError ? err.message : t('login.failed'), 'error') }
  }
  const deactivate = async () => {
    if (!d) return
    if (d.staff.isActive) {
      if (!(await confirmDialog({ title: t('stf.action.deactivate'), message: t('stf.deactivateConfirm', { name }), confirmLabel: t('stf.action.deactivate'), danger: true }))) return
      await run(() => staffAdminApi.setActive(id, false), t('stf.deactivated'))
    } else await run(() => staffAdminApi.setActive(id, true), t('stf.activated'))
  }
  const resetPassword = async () => {
    if (!(await confirmDialog({ title: t('stf.action.resetPassword'), message: t('stf.resetPasswordConfirm', { name }), confirmLabel: t('stf.action.resetPassword'), danger: true }))) return
    try { const saved = await staffAdminApi.resetPassword(id); apply(saved.staff); setTemp(saved.tempPassword) } catch (err) { toast(err instanceof ApiError ? err.message : t('login.failed'), 'error') }
  }
  const logoutAll = async () => {
    if (!(await confirmDialog({ title: t('stf.action.logoutAll'), message: t('stf.logoutAllConfirm', { name }), confirmLabel: t('stf.action.logoutAll'), danger: true }))) return
    await run(() => staffAdminApi.revokeSessions(id), t('stf.sessionsRevoked'))
  }

  const tabs: Tab[] = ['overview', 'access', 'activity']
  const footer = d && d.staff.canEdit ? (
    <>
      <RowMenu items={[
        { label: t('stf.action.setPin'), icon: <KeyRound size={16} />, onClick: () => setPinOpen(true) },
        ...(d.staff.role !== 'Cashier' ? [{ label: t('stf.action.resetPassword'), icon: <RotateCcw size={16} />, onClick: () => void resetPassword() }] : []),
        { label: t('stf.action.logoutAll'), icon: <LogOut size={16} />, onClick: () => void logoutAll() },
        ...(d.staff.isSelf ? [] : [{ label: d.staff.isActive ? t('stf.action.deactivate') : t('stf.action.activate'), icon: d.staff.isActive ? <Ban size={16} /> : <CircleCheck size={16} />, danger: d.staff.isActive, onClick: () => void deactivate() }]),
      ]} />
      <Button className="ml-auto" icon={<Pencil size={16} />} onClick={onEdit}>{t('stf.action.edit')}</Button>
    </>
  ) : undefined

  return (
    <Drawer title={d ? name : '…'} subtitle={d ? formatPhoneInput(d.staff.phone) : undefined} onClose={onClose} width={620} footer={footer}>
      {q.isError ? <BlockError message={(q.error as Error).message} onRetry={() => void q.refetch()} /> : !d ? <Skeleton className="h-64" /> : (
        <div className="flex flex-col gap-4">
          <div className="flex flex-wrap items-center gap-2">
            <RoleBadge role={d.staff.role} />
            <span className={`rounded-full px-2.5 py-1 text-[13px] font-semibold ${d.staff.isActive ? 'bg-success-soft text-success' : 'bg-danger-soft text-danger'}`}>
              {d.staff.isActive ? t('stf.status.active') : t('stf.status.inactive')}
            </span>
            {d.staff.isLocked && <span className="rounded-full bg-amber-50 px-2.5 py-1 text-[13px] font-semibold text-amber-700">{t('stf.status.locked')}</span>}
            {d.staff.isSelf && <span className="text-[13px] text-ink-3">{t('stf.you')}</span>}
          </div>
          <nav className="flex gap-1 border-b border-line">
            {tabs.map((k) => (
              <button key={k} type="button" onClick={() => setTab(k)}
                className={`-mb-px border-b-2 px-3 py-2 text-[14px] font-semibold ${tab === k ? 'border-brand text-brand' : 'border-transparent text-ink-2 hover:text-ink'}`}>{t(`stf.tab.${k}` as TranslationKey)}</button>
            ))}
          </nav>

          {tab === 'overview' && (
            <div className="flex flex-col gap-4">
              <div className="divide-y divide-line rounded-2xl border border-line px-4">
                <InfoRow label={t('stf.ov.contact')}>{formatPhoneInput(d.staff.phone)}</InfoRow>
                <InfoRow label={t('stf.ov.language')}>{d.staff.language === 'kk' ? 'Қазақша' : 'Русский'}</InfoRow>
                <InfoRow label={t('stf.col.access')}>
                  <span className="flex flex-wrap justify-end gap-1.5">
                    <span className={`rounded-md px-2 py-0.5 text-[12px] ${d.staff.hasPin ? 'bg-success-soft text-success' : 'bg-field text-ink-3'}`}>{d.staff.hasPin ? t('stf.pin') : t('stf.noPin')}</span>
                    <span className={`rounded-md px-2 py-0.5 text-[12px] ${d.staff.hasPassword ? 'bg-success-soft text-success' : 'bg-field text-ink-3'}`}>{d.staff.hasPassword ? t('stf.password') : t('stf.noPassword')}</span>
                  </span>
                </InfoRow>
                <InfoRow label={t('stf.ov.joined')}>{dateOnly(d.staff.createdAt)}</InfoRow>
                <InfoRow label={t('stf.ov.lastLogin')}>{d.staff.lastLoginAt ? dateTime(d.staff.lastLoginAt) : t('stf.never')}</InfoRow>
              </div>
              <div className="grid grid-cols-3 gap-3">
                {[[t('stf.ov.sales30'), num(d.staff.sales30)], [t('stf.ov.revenue30'), tenge(d.staff.revenue30)], [t('stf.ov.lastSale'), d.staff.lastSaleAt ? dateOnly(d.staff.lastSaleAt) : '—']].map(([label, value]) => (
                  <div key={label} className="rounded-2xl border border-line p-3"><div className="text-[12px] text-ink-3">{label}</div><div className="mt-1 text-[17px] font-bold">{value}</div></div>
                ))}
              </div>
              <div>
                <h3 className="mb-2 text-[14px] font-bold">{t('stf.ov.sessions')}</h3>
                {d.sessions.length === 0 ? <p className="text-[13px] text-ink-3">{t('stf.ov.noSessions')}</p> : (
                  <ul className="divide-y divide-line rounded-2xl border border-line">
                    {d.sessions.map((s) => (
                      <li key={s.id} className="flex items-center justify-between gap-3 px-4 py-2.5 text-[13px]">
                        <span className="min-w-0 break-words font-medium">{deviceName(s.device)}</span>
                        <span className="shrink-0 text-right text-ink-3">{t('stf.ov.since')} {dateTime(s.createdAt)}<br />{t('stf.ov.until')} {dateOnly(s.expiresAt)}</span>
                      </li>
                    ))}
                  </ul>
                )}
              </div>
            </div>
          )}

          {tab === 'access' && (
            <ul className="flex flex-col gap-3">
              {d.access.map((a) => (
                <li key={a.storeId} className={`rounded-2xl border border-line p-4 ${a.isActive ? '' : 'opacity-60'}`}>
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="font-bold">{a.storeName}</span>
                    {!a.storeActive && <span className="text-[12px] text-ink-3">· {t('stf.acc.inactiveStore')}</span>}
                    <span className="ml-auto">{a.isActive ? <RoleBadge role={a.role} size="sm" /> : <span className="text-[12px] text-ink-3">{t('stf.acc.noAccess')}</span>}</span>
                  </div>
                  {a.isActive && (
                    <div className="mt-2 text-[13px]">
                      <div className="text-ink-2">{t('stf.acc.discount')}: <b>{a.maxDiscountPercent}%</b></div>
                      <div className="mt-1 text-ink-2">{t('stf.acc.perms')}: {a.role === 'Owner' ? <b>{t('stf.acc.allPerms')}</b> : (
                        <span className="mt-1 flex flex-wrap gap-1">{a.permissions.map((p) => <span key={p} className="rounded-md bg-field px-1.5 py-0.5 text-[12px]">{t(permKey(p))}</span>)}</span>
                      )}</div>
                    </div>
                  )}
                </li>
              ))}
            </ul>
          )}

          {tab === 'activity' && <AuditTable staffId={id} compact />}
        </div>
      )}
      {pinOpen && d && <SetPinModal id={id} name={name} onClose={() => setPinOpen(false)} onSaved={(next) => { apply(next); setPinOpen(false) }} />}
      {temp && d && <TempPasswordModal phone={d.staff.phone} password={temp} onClose={() => setTemp(null)} />}
    </Drawer>
  )
}
