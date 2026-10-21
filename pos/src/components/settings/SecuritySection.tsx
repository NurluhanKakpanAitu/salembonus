import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { KeyRound, Lock, LogOut, Monitor } from 'lucide-react'
import { Button } from '../ui/Button'
import { confirmDialog } from '../ui/Confirm'
import { Select } from '../ui/Field'
import { toast } from '../ui/Toast'
import { BlockError, Skeleton } from '../stats/statsUi'
import { PasswordModal, PinModal } from '../CredentialModals'
import { deviceName, InfoRow, RoleBadge, Section } from '../staff/staffUi'
import { ApiError } from '../../lib/api'
import { activeStore, applyMe, useAuth } from '../../lib/auth'
import { dateOnly, dateTime } from '../../lib/dates'
import { useT } from '../../lib/i18n'
import { useLanguageStore, type Language } from '../../lib/language'
import { formatPhoneInput } from '../../lib/phone'
import { staffAdminApi } from '../../lib/staffAdminApi'
import { staffApi } from '../../lib/staffApi'

/** «Настройка → Безопасность»: өз аккаунтым (құпиясөз, PIN, тіл) және кірген құрылғылар (сеанстарды жабу). */
export function SecuritySection() {
  const t = useT()
  const qc = useQueryClient()
  const me = useAuth((s) => s.me)
  const current = useAuth(activeStore)
  const setLang = useLanguageStore((s) => s.set)
  const sessions = useQuery({ queryKey: ['settings', 'sessions'], queryFn: staffAdminApi.mySessions })
  const [modal, setModal] = useState<'pin' | 'password' | null>(null)
  const fail = (err: unknown) => toast(err instanceof ApiError ? err.message : t('login.failed'), 'error')
  const refresh = () => qc.invalidateQueries({ queryKey: ['settings', 'sessions'] })
  const changeLanguage = async (next: Language) => {
    setLang(next)
    try { applyMe(await staffApi.setLanguage(next)) } catch (err) { fail(err) }
  }
  const revoke = async (id: string) => { try { await staffAdminApi.revokeMySession(id); await refresh(); toast(t('set.sec.revoked')) } catch (err) { fail(err) } }
  const revokeOthers = async () => {
    if (!(await confirmDialog({ title: t('set.sec.signOutOthers'), message: t('set.sec.signOutOthersConfirm'), confirmLabel: t('set.sec.signOutOthers'), danger: true }))) return
    try { await staffAdminApi.revokeOtherSessions(); await refresh(); toast(t('set.sec.revokedAll')) } catch (err) { fail(err) }
  }
  if (!me) return null
  const others = (sessions.data ?? []).filter((s) => !s.isCurrent)

  return (
    <div className="flex flex-col gap-4">
      <Section title={t('set.sec.me')}>
        <div className="divide-y divide-line">
          <InfoRow label={t('stf.form.phone')}>{formatPhoneInput(me.phone)}</InfoRow>
          <InfoRow label={t('stf.col.name')}>{me.firstName} {me.lastName}</InfoRow>
          <InfoRow label={t('stf.col.role')}>{current ? <RoleBadge role={current.role} size="sm" /> : '—'}</InfoRow>
          <InfoRow label={t('set.sec.language')}>
            <Select className="h-9! w-auto min-w-32 text-[13px]" value={me.language} onChange={(e) => void changeLanguage(e.target.value as Language)} aria-label={t('set.sec.language')}>
              <option value="ru">Русский</option><option value="kk">Қазақша</option>
            </Select>
          </InfoRow>
        </div>
        <div className="mt-4 grid gap-3 sm:grid-cols-2">
          {me.hasPassword && (
            <div className="flex items-center gap-3 rounded-xl border border-line p-3">
              <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-brand-soft text-brand"><Lock size={18} /></span>
              <span className="min-w-0 flex-1"><span className="block font-semibold">{t('set.sec.password')}</span><span className="block text-[12px] text-ink-3">{t('set.sec.passwordHint')}</span></span>
              <Button variant="secondary" className="h-9! px-3! text-[13px]!" onClick={() => setModal('password')}>{t('set.sec.change')}</Button>
            </div>
          )}
          <div className="flex items-center gap-3 rounded-xl border border-line p-3">
            <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-brand-soft text-brand"><KeyRound size={18} /></span>
            <span className="min-w-0 flex-1"><span className="block font-semibold">{t('set.sec.pin')}</span><span className="block text-[12px] text-ink-3">{t('set.sec.pinHint')}</span></span>
            <Button variant="secondary" className="h-9! px-3! text-[13px]!" onClick={() => setModal('pin')}>{me.hasPin ? t('set.sec.change') : t('set.sec.set')}</Button>
          </div>
        </div>
      </Section>

      <Section title={t('set.sec.sessions')} hint={t('set.sec.sessionsHint')}
        action={others.length > 0 ? <Button variant="secondary" className="h-9! px-3! text-[13px]!" icon={<LogOut size={15} />} onClick={() => void revokeOthers()}>{t('set.sec.signOutOthers')}</Button> : undefined}>
        {sessions.isError ? <BlockError message={(sessions.error as Error).message} onRetry={() => void sessions.refetch()} /> : !sessions.data ? <Skeleton className="h-24" /> : sessions.data.length === 0 ? (
          <p className="text-[13px] text-ink-3">{t('set.sec.noSessions')}</p>
        ) : (
          <ul className="divide-y divide-line">
            {sessions.data.map((s) => (
              <li key={s.id} className="flex items-center gap-3 py-2.5 text-[14px]">
                <span className={`flex size-9 shrink-0 items-center justify-center rounded-xl ${s.isCurrent ? 'bg-success-soft text-success' : 'bg-field text-ink-2'}`}><Monitor size={16} /></span>
                <span className="min-w-0 flex-1">
                  <span className="block break-words font-medium">{deviceName(s.device)}{s.isCurrent && <span className="ml-1.5 text-[12px] font-normal text-success">· {t('set.sec.thisSession')}</span>}</span>
                  <span className="block text-[12px] text-ink-3">{t('stf.ov.since')} {dateTime(s.createdAt)} · {t('stf.ov.until')} {dateOnly(s.expiresAt)}</span>
                </span>
                {!s.isCurrent && <Button variant="ghost" className="h-9! px-3! text-[13px]!" onClick={() => void revoke(s.id)}>{t('set.sec.signOut')}</Button>}
              </li>
            ))}
          </ul>
        )}
      </Section>
      {modal === 'pin' && <PinModal hasPin={me.hasPin} hasPassword={me.hasPassword} onClose={() => setModal(null)} />}
      {modal === 'password' && <PasswordModal onClose={() => setModal(null)} />}
    </div>
  )
}
