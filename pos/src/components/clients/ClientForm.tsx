import { useEffect, useMemo, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { AlertTriangle, ExternalLink, Info, Loader2 } from 'lucide-react'
import { Button } from '../ui/Button'
import { confirmDialog } from '../ui/Confirm'
import { Drawer } from '../ui/Drawer'
import { Field, Input, Select, Textarea, Toggle } from '../ui/Field'
import { toast } from '../ui/Toast'
import { ApiError } from '../../lib/api'
import { clientKeys, clientsApi } from '../../lib/clientsApi'
import type { ClientCard, ClientGender, ClientLookup, ClientSave } from '../../lib/clientsTypes'
import { useT, type TranslationKey } from '../../lib/i18n'
import { KatoPicker } from './KatoPicker'
import { clientNo, Section, useClientMeta } from './clientsUi'

interface FormState {
  phone: string; lastName: string; firstName: string; middleName: string; birthDate: string; gender: ClientGender; kato: string[]; address: string; email: string
  comment: string; consentApp: boolean; consentSms: boolean; consentWhatsApp: boolean; consentEmail: boolean; customFields: Record<string, string>
}

const EMPTY: FormState = {
  phone: '+7', lastName: '', firstName: '', middleName: '', birthDate: '', gender: 'Unknown', kato: [], address: '', email: '', comment: '',
  consentApp: false, consentSms: false, consentWhatsApp: false, consentEmail: false, customFields: {},
}

function fromCard(c: ClientCard): FormState {
  return {
    phone: c.client.phone ?? '', lastName: c.client.lastName, firstName: c.client.firstName, middleName: c.client.middleName ?? '', birthDate: c.client.birthDate ?? '',
    gender: c.client.gender, kato: c.kato.map((k) => k.code), address: c.address ?? '', email: c.client.email ?? '', comment: c.comment ?? '',
    consentApp: c.client.consentApp, consentSms: c.client.consentSms, consentWhatsApp: c.client.consentWhatsApp, consentEmail: c.client.consentEmail,
    customFields: { ...c.client.customFields },
  }
}

/**
 * «Добавить клиента» / «Редактировать» (ТЗ «Клиенты» §5, §6): міндетті өрістер баптаулардан, телефон бойынша телік бірден көрсетіледі
 * (бар карточканы ашуға болады), басқа бизнестің клиенті — деректерін ашпай байланыстырылады. Сақталмаған өзгеріс — растаумен жабылады.
 */
export function ClientForm({ id, onClose, onSaved, onOpen }: { id: string | null; onClose: () => void; onSaved: (card: ClientCard) => void; onOpen: (id: string) => void }) {
  const t = useT()
  const qc = useQueryClient()
  const meta = useClientMeta()
  const settings = meta.data?.settings
  const card = useQuery({ queryKey: ['clients', 'card', id, 'form'], queryFn: () => clientsApi.card(id!), enabled: !!id, gcTime: 0 })
  const [f, setF] = useState<FormState | null>(id ? null : EMPTY)
  const [initial, setInitial] = useState<FormState | null>(id ? null : EMPTY)
  const [phoneTouched, setPhoneTouched] = useState(false)
  const [lookup, setLookup] = useState<ClientLookup | null>(null)
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [busy, setBusy] = useState(false)
  useEffect(() => {
    if (card.data && !f) { const s = fromCard(card.data); setF(s); setInitial(s) }
  }, [card.data, f])
  const dirty = useMemo(() => !!f && !!initial && JSON.stringify(f) !== JSON.stringify(initial), [f, initial])
  const canEditPhone = !id || card.data?.canEditPhone === true
  const patch = (p: Partial<FormState>) => setF((x) => (x ? { ...x, ...p } : x))

  const close = async () => {
    if (dirty && !(await confirmDialog({ message: t('cli.form.unsaved'), confirmLabel: t('cli.form.discard'), danger: true }))) return
    onClose()
  }

  const checkPhone = async () => {
    if (id || !f) return
    const digits = f.phone.replace(/\D/g, '')
    if (digits.length < 11) { setLookup(null); return }
    try { setLookup(await clientsApi.lookup(f.phone)) } catch { setLookup(null) }
  }

  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!f) return
    const err: Record<string, string> = {}
    if (!id && f.phone.replace(/\D/g, '').length < 11) err.phone = t('cli.form.phoneRequired')
    if (!f.firstName.trim()) err.firstName = t('cli.form.required')
    if (settings?.requireLastName && !f.lastName.trim()) err.lastName = t('cli.form.required')
    if (settings?.requireBirthDate && !f.birthDate) err.birthDate = t('cli.form.required')
    setErrors(err)
    if (Object.keys(err).length > 0) return
    const body: ClientSave = {
      phone: id ? (phoneTouched && canEditPhone ? f.phone : null) : f.phone,
      firstName: f.firstName.trim(), lastName: f.lastName.trim(), middleName: f.middleName.trim() || null, birthDate: f.birthDate || null, gender: f.gender,
      katoCode: f.kato[f.kato.length - 1] ?? null, address: f.address.trim() || null, email: f.email.trim() || null, comment: f.comment.trim() || null,
      consentApp: f.consentApp, consentSms: f.consentSms, consentWhatsApp: f.consentWhatsApp, consentEmail: f.consentEmail, customFields: f.customFields,
    }
    setBusy(true)
    try {
      const saved = id ? await clientsApi.update(id, body) : await clientsApi.create(body)
      toast(id ? t('cli.form.saved') : t('cli.form.created', { id: clientNo(saved.client.number) }))
      void qc.invalidateQueries({ queryKey: clientKeys.all })
      setInitial(f)
      onSaved(saved)
    } catch (ex) {
      if (ex instanceof ApiError && ex.field) setErrors({ [ex.field]: ex.message })
      else toast(ex instanceof ApiError ? ex.message : t('cli.failed'), 'error')
    } finally { setBusy(false) }
  }

  return (
    <Drawer title={id ? t('cli.form.editTitle') : t('cli.form.newTitle')} subtitle={id && card.data ? `${card.data.client.name} · ID ${clientNo(card.data.client.number)}` : undefined}
      onClose={() => void close()} width={560}
      footer={<>
        <Button variant="secondary" onClick={() => void close()}>{t('common.cancel')}</Button>
        <Button type="submit" form="client-form" loading={busy} disabled={!f}>{id ? t('cli.form.save') : t('cli.form.create')}</Button>
      </>}>
      {!f ? <div className="py-16 text-center text-ink-3"><Loader2 className="mx-auto animate-spin" size={24} /></div> : (
        <form id="client-form" onSubmit={submit} className="flex flex-col gap-5">
          <Section title={t('cli.form.main')}>
            <Field label={t('cli.col.phone')} required={!id} error={errors.phone}
              hint={id && !canEditPhone ? t('cli.form.phoneLocked') : undefined}>
              <Input type="tel" inputMode="tel" value={f.phone} disabled={!canEditPhone} error={!!errors.phone} autoFocus={!id}
                onChange={(e) => { patch({ phone: e.target.value }); setPhoneTouched(true); setLookup(null) }} onBlur={() => void checkPhone()} placeholder="+7 700 000 00 00" />
            </Field>
            {lookup?.exists && lookup.id && (
              <div className="flex items-start gap-2 rounded-xl border border-amber-200 bg-amber-50 px-3 py-2 text-[13px] text-amber-800">
                <AlertTriangle size={16} className="mt-0.5 shrink-0" />
                <span className="min-w-0 flex-1">{t('cli.form.duplicate', { name: lookup.name ?? '—', id: clientNo(lookup.number ?? 0) })}{lookup.archived ? ` · ${t('cli.archived')}` : ''}</span>
                <button type="button" className="inline-flex shrink-0 items-center gap-1 font-semibold text-brand hover:underline" onClick={() => onOpen(lookup.id!)}>
                  {t('cli.form.openExisting')}<ExternalLink size={13} />
                </button>
              </div>
            )}
            {lookup && !lookup.exists && lookup.global && (
              <div className="flex items-start gap-2 rounded-xl bg-brand-soft px-3 py-2 text-[13px] text-brand"><Info size={16} className="mt-0.5 shrink-0" />{t('cli.form.globalCustomer')}</div>
            )}
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <Field label={t('cli.col.lastName')} required={settings?.requireLastName} error={errors.lastName}>
                <Input value={f.lastName} maxLength={100} error={!!errors.lastName} onChange={(e) => patch({ lastName: e.target.value })} />
              </Field>
              <Field label={t('cli.col.firstName')} required error={errors.firstName}>
                <Input value={f.firstName} maxLength={100} error={!!errors.firstName} onChange={(e) => patch({ firstName: e.target.value })} />
              </Field>
              <Field label={t('cli.col.middleName')}><Input value={f.middleName} maxLength={100} onChange={(e) => patch({ middleName: e.target.value })} /></Field>
              <Field label={t('cli.col.birthDate')} required={settings?.requireBirthDate} error={errors.birthDate}>
                <Input type="date" value={f.birthDate} error={!!errors.birthDate} onChange={(e) => patch({ birthDate: e.target.value })} />
              </Field>
              <Field label={t('cli.col.gender')}>
                <Select value={f.gender} onChange={(e) => patch({ gender: e.target.value as ClientGender })}>
                  {(['Unknown', 'Female', 'Male'] as ClientGender[]).map((g) => <option key={g} value={g}>{t(`cli.gender.${g}` as TranslationKey)}</option>)}
                </Select>
              </Field>
              <Field label={t('cli.col.email')} error={errors.email}>
                <Input type="email" value={f.email} maxLength={200} error={!!errors.email} onChange={(e) => patch({ email: e.target.value })} />
              </Field>
            </div>
          </Section>

          <Section title={t('cli.form.address')}>
            <Field label={t('cli.form.settlement')} error={errors.katoCode}><KatoPicker path={f.kato} onChange={(kato) => patch({ kato })} /></Field>
            <Field label={t('cli.form.street')}><Input value={f.address} maxLength={300} onChange={(e) => patch({ address: e.target.value })} /></Field>
          </Section>

          <Section title={t('cli.form.consents')}>
            <p className="text-[12px] text-ink-3">{t('cli.form.consentsHint')}</p>
            <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
              <Toggle checked={f.consentApp} onChange={(v) => patch({ consentApp: v })} label={t('cli.channel.app')} />
              <Toggle checked={f.consentSms} onChange={(v) => patch({ consentSms: v })} label={t('cli.channel.sms')} />
              <Toggle checked={f.consentWhatsApp} onChange={(v) => patch({ consentWhatsApp: v })} label={t('cli.channel.whatsapp')} />
              <Toggle checked={f.consentEmail} onChange={(v) => patch({ consentEmail: v })} label={t('cli.channel.email')} />
            </div>
          </Section>

          {(settings?.customFields.length ?? 0) > 0 && (
            <Section title={t('cli.form.custom')}>
              <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                {settings!.customFields.map((d) => {
                  const v = f.customFields[d.key] ?? ''
                  const set = (value: string) => patch({ customFields: { ...f.customFields, [d.key]: value } })
                  const err = errors[`customFields.${d.key}`]
                  return (
                    <Field key={d.key} label={d.label} error={err}>
                      {d.type === 'select' ? (
                        <Select value={v} error={!!err} onChange={(e) => set(e.target.value)}>
                          <option value="">—</option>
                          {d.options.map((o) => <option key={o} value={o}>{o}</option>)}
                        </Select>
                      ) : <Input type={d.type === 'date' ? 'date' : 'text'} inputMode={d.type === 'number' ? 'decimal' : undefined} value={v} error={!!err} onChange={(e) => set(e.target.value)} />}
                    </Field>
                  )
                })}
              </div>
            </Section>
          )}

          <Field label={t('cli.form.comment')}><Textarea value={f.comment} maxLength={1000} onChange={(e) => patch({ comment: e.target.value })} /></Field>
        </form>
      )}
    </Drawer>
  )
}
