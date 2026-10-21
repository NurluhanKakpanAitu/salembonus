import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowRight, Ban, CircleCheck, Plus, Store } from 'lucide-react'
import { Button } from '../ui/Button'
import { confirmDialog } from '../ui/Confirm'
import { Field, Input, Select } from '../ui/Field'
import { Modal } from '../ui/Modal'
import { RowMenu } from '../ui/RowMenu'
import { toast } from '../ui/Toast'
import { BlockError, Skeleton } from '../stats/statsUi'
import { ApiError } from '../../lib/api'
import { activeStore, refreshSession, setActiveStore, useAuth } from '../../lib/auth'
import { useT } from '../../lib/i18n'
import { settingsApi, type StoreRow } from '../../lib/staffAdminApi'

const TIME_ZONES = ['Asia/Almaty', 'Asia/Qostanay', 'Asia/Qyzylorda', 'Asia/Aqtobe', 'Asia/Aqtau', 'Asia/Atyrau', 'Asia/Oral', 'Asia/Bishkek', 'Asia/Tashkent', 'Europe/Moscow']

/** «Настройка → Магазины»: бизнестің барлық дүкені; қосу/өшіру — тек иесіне; «Перейти» — ағымдағы дүкенді ауыстыру. */
export function StoresSection() {
  const t = useT()
  const qc = useQueryClient()
  const current = useAuth(activeStore)
  const isOwner = current?.role === 'Owner'
  const q = useQuery({ queryKey: ['settings', 'stores'], queryFn: settingsApi.stores })
  const [adding, setAdding] = useState(false)
  if (q.isError) return <BlockError message={(q.error as Error).message} onRetry={() => void q.refetch()} />
  const refresh = async () => { await qc.invalidateQueries({ queryKey: ['settings', 'stores'] }); await refreshSession() }
  const toggle = async (s: StoreRow) => {
    if (s.isActive && !(await confirmDialog({ title: t('set.stores.disable'), message: t('set.stores.disableConfirm', { name: s.name }), confirmLabel: t('set.stores.disable'), danger: true }))) return
    try { await settingsApi.setStoreActive(s.id, !s.isActive); await refresh() } catch (err) { toast(err instanceof ApiError ? err.message : t('login.failed'), 'error') }
  }
  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center gap-2">
        {!isOwner && <p className="text-[13px] text-ink-3">{t('set.stores.ownerOnly')}</p>}
        {isOwner && <Button className="ml-auto" icon={<Plus size={16} />} onClick={() => setAdding(true)}>{t('set.stores.add')}</Button>}
      </div>
      {!q.data ? <Skeleton className="h-48" /> : (
        <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
          {q.data.map((s) => (
            <div key={s.id} className={`flex flex-col gap-3 rounded-2xl border bg-surface p-4 ${s.isActive ? 'border-line' : 'border-dashed border-line opacity-70'}`}>
              <div className="flex items-start gap-3">
                <span className={`flex size-10 shrink-0 items-center justify-center rounded-xl ${s.isActive ? 'bg-brand-soft text-brand' : 'bg-field text-ink-3'}`}><Store size={18} /></span>
                <div className="min-w-0 flex-1">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="break-words text-[16px] font-bold">{s.name}</span>
                    {s.isCurrent && <span className="rounded-full bg-brand-soft px-2 py-0.5 text-[11px] font-bold uppercase text-brand">{t('set.stores.current')}</span>}
                    {!s.isActive && <span className="rounded-full bg-danger-soft px-2 py-0.5 text-[11px] font-bold uppercase text-danger">{t('set.stores.inactive')}</span>}
                  </div>
                  <div className="text-[13px] text-ink-2">{[s.category, s.address].filter(Boolean).join(' · ') || '—'}</div>
                </div>
                {isOwner && !s.isCurrent && (
                  <RowMenu items={[{ label: s.isActive ? t('set.stores.disable') : t('set.stores.enable'), icon: s.isActive ? <Ban size={16} /> : <CircleCheck size={16} />, danger: s.isActive, onClick: () => void toggle(s) }]} />
                )}
              </div>
              <div className="flex flex-wrap gap-x-4 gap-y-1 text-[13px] text-ink-2">
                <span>{t('set.stores.registers', { n: s.registers })}</span><span>{t('set.stores.staff', { n: s.staff })}</span><span>{s.timeZone}</span>
              </div>
              <div className="mt-auto flex items-center justify-between gap-2">
                <span className="text-[12px] text-ink-3">{t('set.store.joinCode')}: <b className="font-mono tracking-wider text-ink">{s.joinCode}</b></span>
                {!s.isCurrent && s.isActive && <Button variant="ghost" className="h-9! px-3! text-[13px]!" iconRight={<ArrowRight size={15} />} onClick={() => setActiveStore(s.id)}>{t('set.stores.switch')}</Button>}
              </div>
            </div>
          ))}
        </div>
      )}
      {adding && <NewStoreModal onClose={() => setAdding(false)} onCreated={async () => { setAdding(false); await refresh(); toast(t('set.stores.created')) }} />}
    </div>
  )
}

function NewStoreModal({ onClose, onCreated }: { onClose: () => void; onCreated: () => Promise<void> }) {
  const t = useT()
  const [f, setF] = useState({ name: '', category: '', address: '', phone: '', timeZone: 'Asia/Almaty' })
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [busy, setBusy] = useState(false)
  const submit = async () => {
    setBusy(true)
    try { await settingsApi.createStore({ name: f.name.trim(), category: f.category.trim() || null, address: f.address.trim() || null, phone: f.phone.trim() || null, timeZone: f.timeZone }); await onCreated() }
    catch (err) { if (err instanceof ApiError && err.field) setErrors({ [err.field]: err.message }); else toast(err instanceof ApiError ? err.message : t('login.failed'), 'error') }
    finally { setBusy(false) }
  }
  return (
    <Modal title={t('set.stores.new')} onClose={onClose} width={520}>
      <div className="flex flex-col gap-3">
        <p className="text-[13px] text-ink-2">{t('set.stores.newHint')}</p>
        <Field label={t('set.store.name')} required error={errors.name}><Input value={f.name} maxLength={200} autoFocus onChange={(e) => setF({ ...f, name: e.target.value })} /></Field>
        <div className="grid gap-3 sm:grid-cols-2">
          <Field label={t('set.store.category')}><Input value={f.category} maxLength={100} onChange={(e) => setF({ ...f, category: e.target.value })} /></Field>
          <Field label={t('set.store.phone')}><Input inputMode="tel" value={f.phone} maxLength={30} onChange={(e) => setF({ ...f, phone: e.target.value })} /></Field>
        </div>
        <Field label={t('set.store.address')}><Input value={f.address} maxLength={300} onChange={(e) => setF({ ...f, address: e.target.value })} /></Field>
        <Field label={t('set.store.timeZone')} error={errors.timeZone}><Select value={f.timeZone} onChange={(e) => setF({ ...f, timeZone: e.target.value })}>{TIME_ZONES.map((z) => <option key={z} value={z}>{z}</option>)}</Select></Field>
        <div className="flex justify-end gap-2"><Button variant="secondary" onClick={onClose}>{t('common.cancel')}</Button><Button loading={busy} disabled={f.name.trim().length < 2} onClick={() => void submit()}>{t('set.stores.add')}</Button></div>
      </div>
    </Modal>
  )
}
