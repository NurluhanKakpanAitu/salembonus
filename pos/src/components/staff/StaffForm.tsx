import { useEffect, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Check, Copy, KeyRound } from 'lucide-react'
import { Button } from '../ui/Button'
import { confirmDialog } from '../ui/Confirm'
import { Drawer } from '../ui/Drawer'
import { Field, Input, Select, Toggle } from '../ui/Field'
import { Modal } from '../ui/Modal'
import { toast } from '../ui/Toast'
import { Skeleton } from '../stats/statsUi'
import { ApiError } from '../../lib/api'
import { useT, type TranslationKey } from '../../lib/i18n'
import { defaultPermissions } from '../../lib/permissions'
import { formatPhoneInput, isCompletePhone } from '../../lib/phone'
import { staffAdminApi, type SaveStaff, type StaffDetail, type StaffList, type StaffSaved } from '../../lib/staffAdminApi'
import type { StaffRole } from '../../lib/types'
import { PermissionsEditor } from './PermissionsEditor'
import { ROLES, RoleBadge } from './staffUi'

type Form = { firstName: string; lastName: string; phone: string; language: 'ru' | 'kk'; role: StaffRole; storeIds: string[]; permissions: string[]; maxDiscount: string; pin: string; generatePassword: boolean }
type Errors = Partial<Record<keyof Form | 'form', string>>

const empty = (storeId: string | null): Form => ({
  firstName: '', lastName: '', phone: '+7', language: 'ru', role: 'Cashier', storeIds: storeId ? [storeId] : [], permissions: defaultPermissions('Cashier'),
  maxDiscount: '0', pin: '', generatePassword: true,
})

const fromDetail = (d: StaffDetail): Form => ({
  firstName: d.staff.firstName, lastName: d.staff.lastName, phone: formatPhoneInput(d.staff.phone), language: d.staff.language, role: d.staff.role,
  storeIds: d.staff.storeIds, permissions: d.permissions, maxDiscount: String(d.maxDiscountPercent), pin: '', generatePassword: false,
})

/**
 * Қызметкерді қосу/өзгерту шторкасы: деректер → рөл (карточкалар) → дүкендер → рұқсаттар → жеңілдік шегі → кіру (PIN, уақытша құпиясөз).
 * Иесі — барлық дүкенде, барлық рұқсатпен (өрістер жасырылады); өзін өзгерткенде қолжетімділік өрістері жабық.
 */
export function StaffForm({ id, list, onClose, onSaved }: { id: string | null; list: StaffList; onClose: () => void; onSaved: (saved: StaffSaved) => void }) {
  const t = useT()
  const detail = useQuery({ queryKey: ['staff', id], queryFn: () => staffAdminApi.get(id!), enabled: !!id })
  const currentStore = list.stores.find((s) => s.isActive)?.id ?? null
  const [f, setF] = useState<Form>(() => empty(currentStore))
  const [initial, setInitial] = useState<string>(() => JSON.stringify(empty(currentStore)))
  const [errors, setErrors] = useState<Errors>({})
  const [busy, setBusy] = useState(false)
  useEffect(() => {
    if (detail.data) { const v = fromDetail(detail.data); setF(v); setInitial(JSON.stringify(v)) }
  }, [detail.data])
  const self = detail.data?.staff.isSelf ?? false
  const dirty = JSON.stringify(f) !== initial
  const patch = (p: Partial<Form>) => setF((x) => ({ ...x, ...p }))
  const setRole = (role: StaffRole) => patch({ role, permissions: defaultPermissions(role), maxDiscount: role === 'Owner' ? '100' : f.maxDiscount })
  const close = async () => { if (!dirty || await confirmDialog({ title: t('stf.form.unsaved'), message: t('stf.form.unsavedText'), danger: true })) onClose() }
  const activeStores = list.stores.filter((s) => s.isActive)

  const submit = async () => {
    const next: Errors = {}
    if (!f.firstName.trim()) next.firstName = t('common.required')
    if (!isCompletePhone(f.phone)) next.phone = t('login.phoneInvalid')
    if (f.role !== 'Owner' && f.storeIds.length === 0) next.storeIds = t('stf.form.storesHint')
    const discount = Number(f.maxDiscount.replace(',', '.'))
    if (f.role !== 'Owner' && (Number.isNaN(discount) || discount < 0 || discount > 100)) next.maxDiscount = '0–100'
    if (f.pin && !/^\d{4}$/.test(f.pin)) next.pin = t('pin.format')
    if (!id && f.role === 'Cashier' && !f.pin) next.pin = t('stf.form.pinHint')
    setErrors(next)
    if (Object.keys(next).length) return
    const body: SaveStaff = {
      firstName: f.firstName.trim(), lastName: f.lastName.trim() || null, phone: f.phone, language: f.language, role: f.role,
      storeIds: f.storeIds, permissions: f.permissions, maxDiscountPercent: f.role === 'Owner' ? 100 : discount, pin: f.pin || null, generatePassword: f.generatePassword,
    }
    setBusy(true)
    try {
      const saved = id ? await staffAdminApi.update(id, body) : await staffAdminApi.create(body)
      toast(id ? t('stf.saved') : t('stf.created'))
      onSaved(saved)
    } catch (err) {
      if (err instanceof ApiError && err.field) setErrors({ [err.field as keyof Form]: err.message })
      else setErrors({ form: err instanceof ApiError ? err.message : t('login.failed') })
    } finally { setBusy(false) }
  }

  return (
    <Drawer title={id ? t('stf.form.edit') : t('stf.form.new')} onClose={() => void close()} width={660}
      footer={<><Button variant="secondary" onClick={() => void close()}>{t('common.cancel')}</Button><Button loading={busy} onClick={() => void submit()}>{id ? t('stf.form.save') : t('stf.form.create')}</Button></>}>
      {id && !detail.data ? <Skeleton className="h-64" /> : (
        <div className="flex flex-col gap-5">
          <section className="flex flex-col gap-3">
            <h3 className="text-[14px] font-bold text-ink-2">{t('stf.form.person')}</h3>
            <div className="grid gap-3 sm:grid-cols-2">
              <Field label={t('stf.form.firstName')} required error={errors.firstName}><Input value={f.firstName} maxLength={60} autoFocus onChange={(e) => patch({ firstName: e.target.value })} /></Field>
              <Field label={t('stf.form.lastName')}><Input value={f.lastName} maxLength={60} onChange={(e) => patch({ lastName: e.target.value })} /></Field>
              <Field label={t('stf.form.phone')} required error={errors.phone}><Input inputMode="tel" value={f.phone} onChange={(e) => patch({ phone: formatPhoneInput(e.target.value) })} /></Field>
              <Field label={t('stf.form.language')}>
                <Select value={f.language} onChange={(e) => patch({ language: e.target.value as 'ru' | 'kk' })}><option value="ru">Русский</option><option value="kk">Қазақша</option></Select>
              </Field>
            </div>
          </section>

          <section className="flex flex-col gap-3">
            <h3 className="text-[14px] font-bold text-ink-2">{t('stf.form.role')}</h3>
            {self && <p className="rounded-xl bg-amber-50 px-3 py-2 text-[13px] text-amber-800">{t('stf.form.selfHint')}</p>}
            <div className="grid gap-2 sm:grid-cols-3">
              {ROLES.filter((r) => r !== 'Owner' || list.isOwner || f.role === 'Owner').map((r) => (
                <button key={r} type="button" disabled={self} onClick={() => setRole(r)} aria-pressed={f.role === r}
                  className={`flex flex-col items-start gap-1.5 rounded-xl border p-3 text-left transition-colors disabled:opacity-60 ${f.role === r ? 'border-brand bg-brand-soft/60' : 'border-line hover:border-brand/40'}`}>
                  <RoleBadge role={r} /><span className="text-[12px] leading-snug text-ink-2">{t(`stf.form.roleHint.${r}` as TranslationKey)}</span>
                </button>
              ))}
            </div>
          </section>

          {f.role === 'Owner' ? <p className="rounded-xl bg-field px-3 py-2 text-[13px] text-ink-2">{t('stf.form.allStores')}</p> : (
            <>
              <section className="flex flex-col gap-2">
                <Field label={t('stf.form.stores')} hint={t('stf.form.storesHint')} error={errors.storeIds}>
                  <div className="flex flex-wrap gap-2">
                    {activeStores.map((s) => {
                      const on = f.storeIds.includes(s.id)
                      return (
                        <label key={s.id} className={`flex cursor-pointer items-center gap-2 rounded-xl border px-3 py-2 text-[14px] ${on ? 'border-brand bg-brand-soft/60' : 'border-line'} ${self ? 'opacity-60' : ''}`}>
                          <input type="checkbox" className="accent-brand" disabled={self} checked={on} onChange={(e) => patch({ storeIds: e.target.checked ? [...f.storeIds, s.id] : f.storeIds.filter((x) => x !== s.id) })} />{s.name}
                        </label>
                      )
                    })}
                  </div>
                </Field>
              </section>
              <section className="flex flex-col gap-3">
                <div><h3 className="text-[14px] font-bold text-ink-2">{t('stf.form.permissions')}</h3><p className="text-[12px] text-ink-3">{t('stf.form.permissionsHint')}</p></div>
                <PermissionsEditor role={f.role} value={f.permissions} onChange={(permissions) => patch({ permissions })} disabled={self} />
              </section>
              <Field label={t('stf.form.maxDiscount')} hint={t('stf.form.maxDiscountHint')} error={errors.maxDiscount}>
                <Input className="w-32" inputMode="decimal" disabled={self} value={f.maxDiscount} onChange={(e) => patch({ maxDiscount: e.target.value.replace(/[^\d.,]/g, '') })} />
              </Field>
            </>
          )}

          <section className="flex flex-col gap-3">
            <h3 className="text-[14px] font-bold text-ink-2">{t('stf.form.credentials')}</h3>
            <Field label={t('stf.form.pin')} required={!id && f.role === 'Cashier'} hint={id ? t('stf.form.pinKeep') : t('stf.form.pinHint')} error={errors.pin}>
              <Input className="w-40 tracking-[0.3em]" inputMode="numeric" maxLength={4} value={f.pin} placeholder="••••" onChange={(e) => patch({ pin: e.target.value.replace(/\D/g, '').slice(0, 4) })} />
            </Field>
            {!id && f.role !== 'Cashier' && (
              <div className="flex flex-col gap-1">
                <Toggle checked={f.generatePassword} onChange={(v) => patch({ generatePassword: v })} label={t('stf.form.generatePassword')} />
                <p className="pl-14 text-[12px] text-ink-3">{t('stf.form.generatePasswordHint')}</p>
              </div>
            )}
          </section>
          {errors.form && <p className="rounded-xl bg-danger-soft px-4 py-3 text-[14px] text-danger">{errors.form}</p>}
        </div>
      )}
    </Drawer>
  )
}

/** Уақытша құпиясөз — бір рет көрсетіледі (қосқанда немесе қалпына келтіргенде). */
export function TempPasswordModal({ phone, password, onClose }: { phone: string; password: string; onClose: () => void }) {
  const t = useT()
  const [copied, setCopied] = useState(false)
  const copy = async () => {
    try { await navigator.clipboard.writeText(`${formatPhoneInput(phone)}\n${password}`); setCopied(true); setTimeout(() => setCopied(false), 2000) } catch { /* буфер жабық */ }
  }
  return (
    <Modal title={t('stf.temp.title')} onClose={onClose} width={460}>
      <div className="flex flex-col gap-4">
        <p className="text-[14px] text-ink-2">{t('stf.temp.text')}</p>
        <div className="rounded-2xl bg-field p-4">
          <div className="text-[12px] font-semibold uppercase tracking-wider text-ink-3">{t('stf.temp.phone')}</div>
          <div className="text-[18px] font-bold">{formatPhoneInput(phone)}</div>
          <div className="mt-3 text-[12px] font-semibold uppercase tracking-wider text-ink-3">{t('stf.temp.password')}</div>
          <div className="flex items-center gap-2 text-[22px] font-extrabold tracking-wide"><KeyRound size={20} className="text-brand" />{password}</div>
        </div>
        <div className="flex justify-end gap-2">
          <Button variant="secondary" icon={copied ? <Check size={16} /> : <Copy size={16} />} onClick={() => void copy()}>{copied ? t('stf.temp.copied') : t('stf.temp.copy')}</Button>
          <Button onClick={onClose}>{t('stf.temp.done')}</Button>
        </div>
      </div>
    </Modal>
  )
}

/** Иесі/әкімші қызметкерге PIN қояды (кассир өзінің PIN-ін білмесе). */
export function SetPinModal({ id, name, onClose, onSaved }: { id: string; name: string; onClose: () => void; onSaved: (d: StaffDetail) => void }) {
  const t = useT()
  const [pin, setPin] = useState('')
  const [confirm, setConfirm] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const submit = async () => {
    if (!/^\d{4}$/.test(pin)) return setError(t('pin.format'))
    if (pin !== confirm) return setError(t('pin.mismatch'))
    setBusy(true)
    try { onSaved(await staffAdminApi.setPin(id, pin)); toast(t('stf.pinSaved')) } catch (err) { setError(err instanceof ApiError ? err.message : t('login.failed')) } finally { setBusy(false) }
  }
  return (
    <Modal title={`${t('stf.pinModal')} · ${name}`} onClose={onClose} width={420}>
      <div className="flex flex-col gap-3">
        <Field label={t('stf.form.pin')} hint={t('stf.form.pinHint')}><Input className="w-40 tracking-[0.3em]" inputMode="numeric" maxLength={4} autoFocus value={pin} onChange={(e) => setPin(e.target.value.replace(/\D/g, '').slice(0, 4))} /></Field>
        <Field label={t('pin.repeat')}><Input className="w-40 tracking-[0.3em]" inputMode="numeric" maxLength={4} value={confirm} onChange={(e) => setConfirm(e.target.value.replace(/\D/g, '').slice(0, 4))} /></Field>
        {error && <p className="text-[13px] text-danger">{error}</p>}
        <div className="flex justify-end gap-2"><Button variant="secondary" onClick={onClose}>{t('common.cancel')}</Button><Button loading={busy} onClick={() => void submit()}>{t('common.save')}</Button></div>
      </div>
    </Modal>
  )
}
