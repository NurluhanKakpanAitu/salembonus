import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Ban, CircleCheck, Monitor, Pencil, Plus, Unplug } from 'lucide-react'
import { Button } from '../ui/Button'
import { confirmDialog } from '../ui/Confirm'
import { Field, Input, Select } from '../ui/Field'
import { Modal } from '../ui/Modal'
import { RowMenu } from '../ui/RowMenu'
import { toast } from '../ui/Toast'
import { EmptyRow, td, th } from '../inventory/inventoryUi'
import { BlockError, Skeleton } from '../stats/statsUi'
import { ApiError } from '../../lib/api'
import { dateOnly, dateTime } from '../../lib/dates'
import { useT } from '../../lib/i18n'
import { settingsApi, type RegisterRow } from '../../lib/staffAdminApi'

const LOCKS = [5, 10, 15, 30, 60]

/** «Настройка → Кассы»: ағымдағы дүкеннің кассалары — қосу, атау мен автобұғат, құрылғыны ажырату, өшіру/қосу. */
export function RegistersSection() {
  const t = useT()
  const qc = useQueryClient()
  const q = useQuery({ queryKey: ['settings', 'registers'], queryFn: settingsApi.registers })
  const [editing, setEditing] = useState<RegisterRow | 'new' | null>(null)
  if (q.isError) return <BlockError message={(q.error as Error).message} onRetry={() => void q.refetch()} />
  const refresh = () => qc.invalidateQueries({ queryKey: ['settings', 'registers'] })
  const fail = (err: unknown) => toast(err instanceof ApiError ? err.message : t('login.failed'), 'error')
  const unbind = async (r: RegisterRow) => {
    if (!(await confirmDialog({ title: t('set.reg.unbind'), message: t('set.reg.unbindConfirm', { name: r.name }), confirmLabel: t('set.reg.unbind'), danger: true }))) return
    try { await settingsApi.unbindRegister(r.id); await refresh() } catch (err) { fail(err) }
  }
  const toggle = async (r: RegisterRow) => {
    if (r.isActive && !(await confirmDialog({ title: t('set.reg.disable'), message: t('set.reg.disableConfirm', { name: r.name }), confirmLabel: t('set.reg.disable'), danger: true }))) return
    try { await settingsApi.setRegisterActive(r.id, !r.isActive); await refresh() } catch (err) { fail(err) }
  }
  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center gap-3">
        <p className="max-w-2xl text-[13px] text-ink-2">{t('set.reg.howTo')}</p>
        <Button className="ml-auto" icon={<Plus size={16} />} onClick={() => setEditing('new')}>{t('set.reg.add')}</Button>
      </div>
      {!q.data ? <Skeleton className="h-48" /> : (
        <div className="overflow-x-auto rounded-2xl border border-line bg-surface">
          <table className="w-full min-w-[760px]">
            <thead className="border-b border-line bg-field/60"><tr>
              <th className={th}>{t('set.reg.name')}</th><th className={th}>{t('set.reg.device')}</th><th className={th}>{t('set.reg.autoLock')}</th><th className={th}>{t('set.reg.lastSale')}</th><th className="w-12" />
            </tr></thead>
            <tbody>
              {q.data.length === 0 && <EmptyRow colSpan={5} text={t('set.reg.empty')} />}
              {q.data.map((r) => (
                <tr key={r.id} className={`border-b border-line last:border-0 ${r.isActive ? '' : 'opacity-60'}`}>
                  <td className={td}>
                    <span className="flex items-center gap-2.5">
                      <span className={`flex size-9 shrink-0 items-center justify-center rounded-xl ${r.isActive ? 'bg-brand-soft text-brand' : 'bg-field text-ink-3'}`}><Monitor size={17} /></span>
                      <span><span className="block font-semibold">{r.name}</span>{!r.isActive && <span className="block text-[12px] text-danger">{t('set.reg.inactive')}</span>}</span>
                    </span>
                  </td>
                  <td className={td}>
                    {r.isBound ? (
                      <span><span className="block font-medium text-success">{t('set.reg.bound')}{r.isThisDevice && <span className="ml-1 text-[12px] font-normal text-ink-3">· {t('set.reg.thisDevice')}</span>}</span>
                        {r.activatedAt && <span className="block text-[12px] text-ink-3">{t('set.reg.boundAt', { date: dateTime(r.activatedAt) })}</span>}</span>
                    ) : <span className="text-ink-3">{t('set.reg.notBound')}</span>}
                  </td>
                  <td className={td}>{r.autoLockMinutes ? t('settings.minutes', { n: r.autoLockMinutes }) : t('set.reg.autoLockOff')}</td>
                  <td className={td}><span className="block">{r.lastSaleAt ? dateOnly(r.lastSaleAt) : '—'}</span><span className="block text-[12px] text-ink-3">{t('set.reg.sales', { n: r.sales30 })}</span></td>
                  <td className="px-2 py-2.5 text-right">
                    <RowMenu items={[
                      { label: t('set.reg.edit'), icon: <Pencil size={16} />, onClick: () => setEditing(r) },
                      ...(r.isBound ? [{ label: t('set.reg.unbind'), icon: <Unplug size={16} />, onClick: () => void unbind(r) }] : []),
                      { label: r.isActive ? t('set.reg.disable') : t('set.reg.enable'), icon: r.isActive ? <Ban size={16} /> : <CircleCheck size={16} />, danger: r.isActive, onClick: () => void toggle(r) },
                    ]} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {editing && <RegisterModal register={editing === 'new' ? null : editing} onClose={() => setEditing(null)} onSaved={async () => { setEditing(null); await refresh(); toast(t('set.reg.saved')) }} />}
    </div>
  )
}

function RegisterModal({ register, onClose, onSaved }: { register: RegisterRow | null; onClose: () => void; onSaved: () => Promise<void> }) {
  const t = useT()
  const [name, setName] = useState(register?.name ?? '')
  const [lock, setLock] = useState<number | null>(register ? register.autoLockMinutes : 10)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const submit = async () => {
    setBusy(true)
    try { if (register) await settingsApi.updateRegister(register.id, { name: name.trim(), autoLockMinutes: lock }); else await settingsApi.createRegister({ name: name.trim(), autoLockMinutes: lock }); await onSaved() }
    catch (err) { setError(err instanceof ApiError ? err.message : t('login.failed')) } finally { setBusy(false) }
  }
  return (
    <Modal title={register ? t('set.reg.edit') : t('set.reg.new')} onClose={onClose} width={440}>
      <div className="flex flex-col gap-3">
        <Field label={t('set.reg.name')} required><Input value={name} maxLength={60} autoFocus onChange={(e) => setName(e.target.value)} /></Field>
        <Field label={t('set.reg.autoLock')} hint={t('settings.autoLockHint')}>
          <Select value={lock ?? ''} onChange={(e) => setLock(e.target.value ? Number(e.target.value) : null)}>
            <option value="">{t('set.reg.autoLockOff')}</option>
            {[...new Set([...LOCKS, ...(lock ? [lock] : [])])].sort((a, b) => a - b).map((m) => <option key={m} value={m}>{t('settings.minutes', { n: m })}</option>)}
          </Select>
        </Field>
        {error && <p className="text-[13px] text-danger">{error}</p>}
        <div className="flex justify-end gap-2"><Button variant="secondary" onClick={onClose}>{t('common.cancel')}</Button><Button loading={busy} disabled={name.trim().length === 0} onClick={() => void submit()}>{t('common.save')}</Button></div>
      </div>
    </Modal>
  )
}
