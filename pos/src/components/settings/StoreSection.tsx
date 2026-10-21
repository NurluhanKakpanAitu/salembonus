import { useEffect, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Check, Copy } from 'lucide-react'
import { Button } from '../ui/Button'
import { Field, Input, Select, Textarea } from '../ui/Field'
import { toast } from '../ui/Toast'
import { KatoPicker } from '../clients/KatoPicker'
import { BlockError, Skeleton } from '../stats/statsUi'
import { Section } from '../staff/staffUi'
import { ApiError } from '../../lib/api'
import { refreshSession } from '../../lib/auth'
import { useT } from '../../lib/i18n'
import { settingsApi, type StoreProfile } from '../../lib/staffAdminApi'

type Form = { name: string; category: string; description: string; address: string; phone: string; kato: string[]; timeZone: string; organizationName: string; organizationBin: string }
const toForm = (p: StoreProfile): Form => ({
  name: p.name, category: p.category, description: p.description, address: p.address ?? '', phone: p.phone ?? '', kato: p.katoPath, timeZone: p.timeZone,
  organizationName: p.organizationName, organizationBin: p.organizationBin ?? '',
})

/** «Настройка → Магазин»: ағымдағы дүкеннің профилі, клиент коды, бизнес реквизиттері (тек иесіне). */
export function StoreSection() {
  const t = useT()
  const qc = useQueryClient()
  const q = useQuery({ queryKey: ['settings', 'store'], queryFn: settingsApi.store })
  const [f, setF] = useState<Form | null>(null)
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [busy, setBusy] = useState(false)
  const [copied, setCopied] = useState(false)
  useEffect(() => { if (q.data) setF(toForm(q.data)) }, [q.data])
  if (q.isError) return <BlockError message={(q.error as Error).message} onRetry={() => void q.refetch()} />
  if (!q.data || !f) return <Skeleton className="h-96" />
  const p = q.data
  const patch = (x: Partial<Form>) => setF((v) => ({ ...v!, ...x }))
  const dirty = JSON.stringify(f) !== JSON.stringify(toForm(p))
  const save = async () => {
    setBusy(true)
    setErrors({})
    try {
      const saved = await settingsApi.updateStore({
        name: f.name.trim(), category: f.category.trim() || null, description: f.description.trim() || null, address: f.address.trim() || null, phone: f.phone.trim() || null,
        katoCode: f.kato[f.kato.length - 1] ?? null, timeZone: f.timeZone,
        organizationName: p.canEditOrganization ? f.organizationName.trim() : null, organizationBin: p.canEditOrganization ? f.organizationBin.trim() || null : null,
      })
      qc.setQueryData(['settings', 'store'], saved)
      void qc.invalidateQueries({ queryKey: ['settings'] })
      await refreshSession() // дүкен атауы жоғарғы панельде де жаңарады
      toast(t('set.saved'))
    } catch (err) {
      if (err instanceof ApiError && err.field) setErrors({ [err.field]: err.message })
      else toast(err instanceof ApiError ? err.message : t('login.failed'), 'error')
    } finally { setBusy(false) }
  }
  const copy = async () => { try { await navigator.clipboard.writeText(p.joinCode); setCopied(true); setTimeout(() => setCopied(false), 2000) } catch { /* буфер жабық */ } }

  return (
    <div className="flex flex-col gap-4">
      <Section title={t('set.section.store')}>
        <div className="grid gap-4 md:grid-cols-2">
          <Field label={t('set.store.name')} required error={errors.name}><Input value={f.name} maxLength={200} onChange={(e) => patch({ name: e.target.value })} /></Field>
          <Field label={t('set.store.category')}><Input value={f.category} maxLength={100} onChange={(e) => patch({ category: e.target.value })} /></Field>
          <Field label={t('set.store.address')}><Input value={f.address} maxLength={300} onChange={(e) => patch({ address: e.target.value })} /></Field>
          <Field label={t('set.store.phone')}><Input inputMode="tel" value={f.phone} maxLength={30} onChange={(e) => patch({ phone: e.target.value })} /></Field>
          <div className="md:col-span-2"><Field label={t('set.store.location')} error={errors.katoCode}><KatoPicker path={f.kato} onChange={(kato) => patch({ kato })} /></Field></div>
          <Field label={t('set.store.timeZone')} hint={t('set.store.timeZoneHint')} error={errors.timeZone}>
            <Select value={f.timeZone} onChange={(e) => patch({ timeZone: e.target.value })}>
              {[...new Set([...p.timeZones, f.timeZone])].map((z) => <option key={z} value={z}>{z}</option>)}
            </Select>
          </Field>
          <div className="md:col-span-2"><Field label={t('set.store.description')} hint={t('set.store.descriptionHint')}><Textarea value={f.description} maxLength={500} onChange={(e) => patch({ description: e.target.value })} className="min-h-20" /></Field></div>
        </div>
      </Section>

      <Section title={t('set.store.joinCode')} hint={t('set.store.joinCodeHint')}>
        <div className="flex flex-wrap items-center gap-3">
          <span className="rounded-xl bg-field px-4 py-2 font-mono text-[22px] font-extrabold tracking-[0.2em]">{p.joinCode}</span>
          <Button variant="secondary" icon={copied ? <Check size={16} /> : <Copy size={16} />} onClick={() => void copy()}>{copied ? t('stf.temp.copied') : t('stf.temp.copy')}</Button>
        </div>
      </Section>

      <Section title={t('set.store.org')} hint={p.canEditOrganization ? undefined : t('set.store.orgOwnerOnly')}>
        <div className="grid gap-4 md:grid-cols-2">
          <Field label={t('set.store.orgName')} error={errors.organizationName}><Input value={f.organizationName} maxLength={200} disabled={!p.canEditOrganization} onChange={(e) => patch({ organizationName: e.target.value })} /></Field>
          <Field label={t('set.store.bin')} hint={t('set.store.binHint')} error={errors.organizationBin}><Input inputMode="numeric" value={f.organizationBin} maxLength={12} disabled={!p.canEditOrganization} onChange={(e) => patch({ organizationBin: e.target.value.replace(/\D/g, '').slice(0, 12) })} /></Field>
        </div>
      </Section>

      <div className="sticky bottom-0 flex justify-end gap-2 rounded-2xl border border-line bg-surface/95 p-3 backdrop-blur">
        <Button variant="secondary" disabled={!dirty} onClick={() => setF(toForm(p))}>{t('set.cancel')}</Button>
        <Button loading={busy} disabled={!dirty || !f.name.trim()} onClick={() => void save()}>{t('set.save')}</Button>
      </div>
    </div>
  )
}
