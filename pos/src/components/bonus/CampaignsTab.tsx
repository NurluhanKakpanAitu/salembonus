import { useEffect, useMemo, useRef, useState } from 'react'
import { keepPreviousData, useQuery, useQueryClient } from '@tanstack/react-query'
import { Ban, Check, Copy, Eye, FileText, Megaphone, Pencil, Plus, RotateCcw, Send } from 'lucide-react'
import { Button } from '../ui/Button'
import { Field, Input, Select, Textarea, Toggle } from '../ui/Field'
import { Modal } from '../ui/Modal'
import { RowMenu } from '../ui/RowMenu'
import { SearchInput } from '../ui/SearchInput'
import { toast } from '../ui/Toast'
import { confirmDialog } from '../ui/Confirm'
import { EmptyRow, Pager, td, th, useDebounced } from '../inventory/inventoryUi'
import { ApiError } from '../../lib/api'
import { bonusApi, bonusKeys } from '../../lib/bonusApi'
import {
  CAMPAIGN_TYPES, EMPTY_FILTER, type AudiencePreview, type Campaign, type CampaignStatus, type CampaignType, type SaveCampaign,
} from '../../lib/bonusTypes'
import { dateTime } from '../../lib/dates'
import { num } from '../../lib/money'
import { useT, type TranslationKey } from '../../lib/i18n'
import { Segmented } from '../stats/statsUi'
import { CustomerPanel } from './CustomerPanel'
import { OpTypeBadge, Pill, Points, reportError, StatusBadge, useBonusMeta, useBonusPermissions, useParamSetter } from './bonusUi'

const VARIABLES = ['{{Имя}}', '{{ФИО}}', '{{Бонусы}}', '{{ДатаСгорания}}', '{{ДатаРождения}}', '{{НазваниеМагазина}}']
const STATUS_TONE: Record<CampaignStatus, 'green' | 'amber' | 'red' | 'blue' | 'gray' | 'violet'> = {
  Draft: 'gray', Scheduled: 'blue', Queued: 'blue', Sending: 'violet', Completed: 'green', CompletedWithErrors: 'amber', Cancelled: 'gray',
}

/**
 * «Рассылка бонусов» (ТЗ «Бонусы» §6): тізім (атауы, түрі, аудитория, арна, уақыты, алушылар, жеткізілді, қате, мәртебе, автор),
 * бес қадамды шебер, тарату карточкасы мен есебі. Арна — тек қосылған (SalemBonus қосымшасы).
 */
export function CampaignsTab() {
  const t = useT()
  const qc = useQueryClient()
  const perms = useBonusPermissions()
  const { get, set } = useParamSetter()
  const [editing, setEditing] = useState<Campaign | 'new' | null>(get('new') ? 'new' : null)
  const search = useDebounced(get('q'), 300)
  const page = Number(get('page')) || 1
  const status = get('cstatus') || undefined
  const list = useQuery({ queryKey: ['bonus', 'campaigns', search, status, page], queryFn: () => bonusApi.campaigns({ search, status, page, pageSize: 25 }), placeholderData: keepPreviousData })
  const openId = get('campaign')
  const invalidate = () => { void list.refetch(); void qc.invalidateQueries({ queryKey: bonusKeys.all }) }
  useEffect(() => { if (get('new')) set({ new: null }, true) }, []) // eslint-disable-line react-hooks/exhaustive-deps

  const act = async (c: Campaign, kind: 'duplicate' | 'cancel' | 'retry') => {
    try {
      if (kind === 'duplicate') { await bonusApi.duplicateCampaign(c.id); toast(t('bon.cmp.duplicated')) }
      if (kind === 'cancel') {
        if (!(await confirmDialog({ message: t('bon.cmp.cancelConfirm', { name: c.name }), confirmLabel: t('bon.cmp.cancel'), danger: true }))) return
        await bonusApi.cancelCampaign(c.id); toast(t('bon.cmp.cancelled'))
      }
      if (kind === 'retry') { await bonusApi.retryCampaign(c.id); toast(t('bon.cmp.retried')) }
      invalidate()
    } catch (err) { reportError(err, t('bon.failed')) }
  }

  return (
    <div className="flex flex-col gap-3">
      <div className="flex flex-wrap items-center gap-2">
        <SearchInput value={get('q')} onChange={(v) => set({ q: v })} placeholder={t('bon.cmp.search')} className="h-10! w-64" />
        <Segmented value={status ?? 'all'} options={[{ key: 'all', label: t('bon.cmp.all') }, { key: 'Draft', label: t('bon.cmp.st.Draft') }, { key: 'Scheduled', label: t('bon.cmp.st.Scheduled') }, { key: 'Completed', label: t('bon.cmp.history') }]}
          onChange={(v) => set({ cstatus: v === 'all' ? null : v })} />
        <span className="ml-auto" />
        {perms.campaigns && <Button className="h-10! px-3!" icon={<Plus size={16} />} onClick={() => setEditing('new')}>{t('bon.cmp.create')}</Button>}
      </div>
      <div className="flex items-center gap-2 rounded-xl bg-brand-soft/50 px-4 py-2 text-[13px] text-brand"><Megaphone size={15} />{t('bon.cmp.channelNote')}</div>
      <div className="overflow-x-auto rounded-2xl border border-line bg-surface">
        <table className="w-full min-w-[1000px]">
          <thead className="bg-field/60"><tr>
            <th className={th}>{t('bon.cmp.name')}</th><th className={th}>{t('bon.cmp.type')}</th><th className={th}>{t('bon.cmp.audience')}</th><th className={th}>{t('bon.cmp.when')}</th>
            <th className={`${th} text-right`}>{t('bon.cmp.recipients')}</th><th className={`${th} text-right`}>{t('bon.cmp.delivered')}</th><th className={`${th} text-right`}>{t('bon.cmp.readCol')}</th>
            <th className={`${th} text-right`}>{t('bon.cmp.errors')}</th><th className={th}>{t('bon.cmp.status')}</th><th className={th}>{t('bon.cmp.author')}</th><th className={`${th} w-12`}></th>
          </tr></thead>
          <tbody>
            {!list.data ? null : list.data.items.length === 0 ? <EmptyRow colSpan={11} text={t('bon.cmp.empty')} /> : list.data.items.map((c) => (
              <tr key={c.id} className="border-t border-line hover:bg-field/40">
                <td className={td}><button type="button" className="font-medium hover:text-brand" onClick={() => set({ campaign: c.id }, true)}>{c.name}</button>
                  {c.bonusAmount ? <span className="block text-[12px] text-ink-3">+{num(c.bonusAmount)} {t('bon.pts')}</span> : null}</td>
                <td className={`${td} text-ink-2`}>{t(`bon.cmp.typeOpt.${c.type}` as TranslationKey)}</td>
                <td className={`${td} text-[13px] text-ink-2`}>{c.audienceMode === 'Manual' ? t('bon.cmp.manualN', { n: c.customerIds.length }) : t('bon.cmp.allMatching')}</td>
                <td className={`${td} text-[13px] text-ink-2`}>{c.startedAt ? dateTime(c.startedAt) : c.scheduledAt ? dateTime(c.scheduledAt) : '—'}</td>
                <td className={`${td} text-right tabular-nums`}>{num(c.recipients)}</td>
                <td className={`${td} text-right tabular-nums`}>{num(c.delivered)}</td>
                <td className={`${td} text-right tabular-nums text-ink-2`}>{num(c.read)}</td>
                <td className={`${td} text-right tabular-nums ${c.failed ? 'text-danger' : 'text-ink-3'}`}>{num(c.failed)}</td>
                <td className={td}><Pill tone={STATUS_TONE[c.status]}>{t(`bon.cmp.st.${c.status}` as TranslationKey)}</Pill></td>
                <td className={`${td} text-[13px] text-ink-2`}>{c.author}</td>
                <td className={td}><RowMenu items={[
                  { label: t('bon.open'), icon: <Eye size={16} />, onClick: () => set({ campaign: c.id }, true) },
                  ...(perms.campaigns && c.canEdit ? [{ label: t('bon.edit'), icon: <Pencil size={16} />, onClick: () => setEditing(c) }] : []),
                  ...(perms.campaigns ? [{ label: t('bon.st.duplicate'), icon: <Copy size={16} />, onClick: () => void act(c, 'duplicate') }] : []),
                  ...(perms.campaigns && c.canRetry ? [{ label: t('bon.cmp.retry'), icon: <RotateCcw size={16} />, onClick: () => void act(c, 'retry') }] : []),
                  ...(perms.campaigns && c.canCancel ? [{ label: t('bon.cmp.cancel'), icon: <Ban size={16} />, danger: true, onClick: () => void act(c, 'cancel') }] : []),
                ]} /></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {list.data && <Pager page={page} pageSize={25} total={list.data.total} onPage={(p) => set({ page: String(p) }, true)} onPageSize={() => undefined} />}
      {editing && <CampaignWizard initial={editing === 'new' ? null : editing} onClose={() => setEditing(null)} onDone={(id) => { setEditing(null); invalidate(); if (id) set({ campaign: id }, true) }} />}
      {openId && <CampaignReport id={openId} onClose={() => set({ campaign: null }, true)} onChanged={invalidate} />}
    </div>
  )
}

const TEMPLATES: Partial<Record<CampaignType, string>> = {
  Gift: '{{Имя}}, дарим вам {{Бонусы}} бонусов! Ждём вас в {{НазваниеМагазина}}.',
  Promo: '{{Имя}}, в {{НазваниеМагазина}} акция! Оплатите покупку бонусами — на счету {{Бонусы}}.',
  Expiring: '{{Имя}}, у вас {{Бонусы}} бонусов, часть сгорит {{ДатаСгорания}}. Успейте потратить!',
  Birthday: '{{Имя}}, с днём рождения! Дарим {{Бонусы}} бонусов.',
  Winback: '{{Имя}}, мы соскучились! Возвращайтесь — на вашем счету {{Бонусы}} бонусов.',
}

/** Шебер (§6.2–6.6): 1 атауы мен түрі → 2 аудитория → 3 арна мен хабарлама → 4 уақыт → 5 тексеру мен іске қосу. */
function CampaignWizard({ initial, onClose, onDone }: { initial: Campaign | null; onClose: () => void; onDone: (id?: string) => void }) {
  const t = useT()
  const meta = useBonusMeta()
  const [step, setStep] = useState(1)
  const [id, setId] = useState(initial?.id ?? null)
  const [f, setF] = useState<SaveCampaign>(initial ? {
    name: initial.name, type: initial.type, channel: 'app', audienceMode: initial.audienceMode, filter: { ...EMPTY_FILTER, ...initial.filter }, customerIds: initial.customerIds,
    message: initial.message, bonusAmount: initial.bonusAmount, bonusValidityDays: initial.bonusValidityDays,
  } : { name: '', type: 'Gift', channel: 'app', audienceMode: 'All', filter: EMPTY_FILTER, customerIds: [], message: TEMPLATES.Gift!, bonusAmount: null, bonusValidityDays: 30 })
  const [when, setWhen] = useState<'now' | 'later'>(initial?.scheduledAt ? 'later' : 'now')
  const [at, setAt] = useState(() => {
    const d = initial?.scheduledAt ? new Date(initial.scheduledAt) : new Date(Date.now() + 3600_000)
    const p = (n: number) => String(n).padStart(2, '0')
    return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}T${p(d.getHours())}:${p(d.getMinutes())}`
  })
  const [audience, setAudience] = useState<AudiencePreview | null>(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [testPhone, setTestPhone] = useState('')
  const messageRef = useRef<HTMLTextAreaElement>(null)
  const dirty = useRef(false)
  const patch = (p: Partial<SaveCampaign>) => { dirty.current = true; setF((x) => ({ ...x, ...p })) }
  const patchFilter = (p: Partial<SaveCampaign['filter']>) => patch({ filter: { ...f.filter, ...p } })
  const past = when === 'later' && new Date(at).getTime() <= Date.now() + 60_000
  const channels = meta.data?.channels ?? []

  useEffect(() => {
    if (step !== 2 && step !== 5) return
    const h = setTimeout(() => { void bonusApi.audience(f).then(setAudience).catch(() => setAudience(null)) }, 300)
    return () => clearTimeout(h)
  }, [f, step])

  const persist = async () => {
    const saved = id ? await bonusApi.updateCampaign(id, f) : await bonusApi.createCampaign(f)
    setId(saved.id)
    dirty.current = false
    return saved
  }
  const saveDraft = async () => {
    setBusy(true); setError(null)
    try { await persist(); toast(t('bon.cmp.draftSaved')); onDone() } catch (err) { setError(err instanceof ApiError ? err.message : t('bon.failed')) } finally { setBusy(false) }
  }
  const close = async () => {
    if (dirty.current) {
      const keep = await confirmDialog({ title: t('bon.unsavedTitle'), message: t('bon.cmp.saveDraftQuestion'), confirmLabel: t('bon.cmp.saveDraft'), cancelLabel: t('bon.closeWithoutSaving') })
      if (keep) { await saveDraft(); return }
    }
    onClose()
  }
  const launch = async () => {
    if (when === 'now') {
      const ok = await confirmDialog({ title: t('bon.cmp.confirmTitle'), message: t('bon.cmp.confirmSend', { n: audience?.eligible ?? 0 }) + (f.bonusAmount ? `\n${t('bon.cmp.confirmBonus', { n: audience?.eligible ?? 0, total: num(audience?.bonusTotal ?? 0) })}` : ''), confirmLabel: t('bon.cmp.sendNow') })
      if (!ok) return
    }
    setBusy(true); setError(null)
    try {
      const saved = await persist()
      if (when === 'now') { await bonusApi.sendCampaign(saved.id); toast(t('bon.cmp.sent')) }
      else { await bonusApi.scheduleCampaign(saved.id, new Date(at).toISOString()); toast(t('bon.cmp.scheduled')) }
      onDone(saved.id)
    } catch (err) { setError(err instanceof ApiError ? err.message : t('bon.failed')) } finally { setBusy(false) }
  }
  const test = async () => {
    try {
      const saved = await persist()
      const r = await bonusApi.testCampaign(saved.id, { phone: testPhone, message: f.message })
      toast(t('bon.cmp.testSent', { name: r.customer }))
    } catch (err) { reportError(err, t('bon.failed')) }
  }
  const insert = (v: string) => {
    const el = messageRef.current
    const s = el?.selectionStart ?? f.message.length
    const e = el?.selectionEnd ?? f.message.length
    patch({ message: f.message.slice(0, s) + v + f.message.slice(e) })
    requestAnimationFrame(() => { el?.focus(); el?.setSelectionRange(s + v.length, s + v.length) })
  }
  const preview = useMemo(() => f.message.replaceAll('{{Имя}}', 'Айгерим').replaceAll('{{ФИО}}', 'Айгерим Садыкова').replaceAll('{{Бонусы}}', num(f.bonusAmount ?? 1250))
    .replaceAll('{{ДатаСгорания}}', '31.12').replaceAll('{{ДатаРождения}}', '15.03').replaceAll('{{НазваниеМагазина}}', meta.data?.storeName ?? ''), [f.message, f.bonusAmount, meta.data])
  const canNext = step === 1 ? f.name.trim().length >= 2 : step === 2 ? (f.audienceMode === 'All' || f.customerIds.length > 0) : step === 3 ? f.message.trim().length > 0 : step === 4 ? !past : true
  const steps = ['bon.cmp.step1', 'bon.cmp.step2', 'bon.cmp.step3', 'bon.cmp.step4', 'bon.cmp.step5']

  return (
    <Modal title={initial ? t('bon.cmp.editTitle') : t('bon.cmp.newTitle')} onClose={() => void close()} width={900}>
      <ol className="mb-5 flex flex-wrap gap-2">
        {steps.map((s, i) => (
          <li key={s} className={`flex items-center gap-2 rounded-xl px-3 py-1.5 text-[13px] font-semibold ${step === i + 1 ? 'bg-brand text-white' : step > i + 1 ? 'bg-success-soft text-success' : 'bg-field text-ink-3'}`}>
            {step > i + 1 ? <Check size={14} /> : <span>{i + 1}</span>}{t(s as TranslationKey)}
          </li>
        ))}
      </ol>
      <div className="min-h-[340px]">
        {step === 1 && (
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label={t('bon.cmp.name')} required><Input autoFocus value={f.name} maxLength={120} onChange={(e) => patch({ name: e.target.value })} placeholder={t('bon.cmp.namePlaceholder')} /></Field>
            <Field label={t('bon.cmp.type')} required>
              <Select value={f.type} onChange={(e) => { const type = e.target.value as CampaignType; patch({ type, message: f.message && dirty.current ? f.message : TEMPLATES[type] ?? f.message }) }}>
                {CAMPAIGN_TYPES.map((x) => <option key={x} value={x}>{t(`bon.cmp.typeOpt.${x}` as TranslationKey)}</option>)}
              </Select>
            </Field>
            <p className="text-[13px] text-ink-2 sm:col-span-2">{t(`bon.cmp.typeHint.${f.type}` as TranslationKey)}</p>
          </div>
        )}
        {step === 2 && <AudienceStep f={f} patch={patch} patchFilter={patchFilter} audience={audience} />}
        {step === 3 && (
          <div className="grid gap-4 lg:grid-cols-2">
            <div className="flex flex-col gap-4">
              <Field label={t('bon.cmp.channel')}>
                <div className="flex flex-col gap-1.5">
                  {channels.map((c) => (
                    <label key={c.key} className={`flex items-center gap-2 text-[14px] ${c.available ? '' : 'text-ink-3'}`} title={c.note ?? undefined}>
                      <input type="radio" name="channel" className="accent-brand" disabled={!c.available} checked={f.channel === c.key} onChange={() => patch({ channel: c.key })} />
                      {t(`bon.channel.${c.key}` as TranslationKey)}{c.note && <span className="text-[12px]">— {c.note}</span>}
                    </label>
                  ))}
                </div>
              </Field>
              <Field label={t('bon.cmp.message')} required hint={t('bon.cmp.chars', { n: f.message.length })}>
                <Textarea ref={messageRef} value={f.message} maxLength={1000} onChange={(e) => patch({ message: e.target.value })} className="min-h-32!" />
                <div className="mt-1 flex flex-wrap gap-1">{VARIABLES.map((v) => <button key={v} type="button" onClick={() => insert(v)} className="rounded-md bg-field px-1.5 py-0.5 text-[11px] font-semibold text-ink-2 hover:bg-brand-soft hover:text-brand">{v}</button>)}</div>
              </Field>
              <Toggle checked={f.bonusAmount !== null} onChange={(v) => patch({ bonusAmount: v ? 500 : null, bonusValidityDays: v ? 30 : null })} label={t('bon.cmp.withBonus')} />
              {f.bonusAmount !== null && (
                <div className="grid grid-cols-2 gap-3">
                  <Field label={t('bon.cmp.bonusAmount')}><Input inputMode="numeric" value={f.bonusAmount} onChange={(e) => patch({ bonusAmount: Number(e.target.value.replace(/\D/g, '')) || 0 })} /></Field>
                  <Field label={t('bon.cmp.bonusDays')}><Input inputMode="numeric" value={f.bonusValidityDays ?? ''} onChange={(e) => patch({ bonusValidityDays: e.target.value ? Number(e.target.value.replace(/\D/g, '')) : null })} placeholder={t('bon.noExpiry')} /></Field>
                </div>
              )}
            </div>
            <div className="flex flex-col gap-3">
              <div className="rounded-2xl border border-line bg-field/50 p-4">
                <div className="mb-2 text-[12px] font-semibold text-ink-2">{t('bon.cmp.preview')}</div>
                <div className="rounded-xl bg-surface p-3 text-[14px] shadow-sm"><b className="block">{f.name || '—'}</b><span className="whitespace-pre-line">{preview}</span></div>
              </div>
              <Field label={t('bon.cmp.testContact')} hint={t('bon.cmp.testHint')}>
                <div className="flex gap-2"><Input value={testPhone} onChange={(e) => setTestPhone(e.target.value)} placeholder="+7 7__ ___ __ __" /><Button variant="secondary" disabled={testPhone.replace(/\D/g, '').length < 10} onClick={() => void test()}>{t('bon.cmp.sendTest')}</Button></div>
              </Field>
            </div>
          </div>
        )}
        {step === 4 && (
          <div className="flex max-w-md flex-col gap-4">
            <Segmented value={when} options={[{ key: 'now', label: t('bon.cmp.sendNow') }, { key: 'later', label: t('bon.cmp.schedule') }]} onChange={setWhen} />
            {when === 'later' && (
              <Field label={t('bon.cmp.dateTime')} error={past ? t('bon.cmp.pastError') : null} hint={t('bon.cmp.tzHint', { tz: meta.data?.timeZone ?? '' })}>
                <Input type="datetime-local" value={at} onChange={(e) => setAt(e.target.value)} error={past} />
              </Field>
            )}
            <p className="text-[13px] text-ink-2">{t('bon.cmp.cancelHint')}</p>
          </div>
        )}
        {step === 5 && (
          <div className="grid gap-3 text-[14px] sm:grid-cols-2">
            {[
              [t('bon.cmp.name'), f.name], [t('bon.cmp.type'), t(`bon.cmp.typeOpt.${f.type}` as TranslationKey)], [t('bon.cmp.channel'), t(`bon.channel.${f.channel}` as TranslationKey)],
              [t('bon.cmp.audience'), f.audienceMode === 'Manual' ? t('bon.cmp.manualN', { n: f.customerIds.length }) : t('bon.cmp.allMatching')],
              [t('bon.cmp.recipients'), audience ? `${num(audience.eligible)} (${t('bon.cmp.excludedN', { n: audience.excluded })})` : '…'],
              [t('bon.cmp.when'), when === 'now' ? t('bon.cmp.sendNow') : dateTime(new Date(at).toISOString())],
              [t('bon.cmp.bonusAction'), f.bonusAmount ? t('bon.cmp.bonusLine', { amount: num(f.bonusAmount), days: f.bonusValidityDays ?? '∞', total: num(audience?.bonusTotal ?? 0) }) : t('bon.cmp.noBonus')],
            ].map(([k, v]) => <div key={k} className="rounded-xl border border-line px-3 py-2"><div className="text-[12px] text-ink-2">{k}</div><div className="font-semibold">{v}</div></div>)}
            <div className="rounded-xl border border-line px-3 py-2 sm:col-span-2"><div className="text-[12px] text-ink-2">{t('bon.cmp.message')}</div><div className="whitespace-pre-line">{f.message}</div></div>
            {f.bonusAmount && <p className="rounded-xl bg-amber-50 px-3 py-2 text-[13px] text-amber-800 sm:col-span-2">{t('bon.cmp.bonusWarning', { n: audience?.eligible ?? 0 })}</p>}
          </div>
        )}
      </div>
      {error && <p className="mt-3 text-[13px] text-danger">{error}</p>}
      <div className="mt-5 flex flex-wrap gap-2 border-t border-line pt-4">
        <Button variant="ghost" onClick={() => void close()}>{t('common.cancel')}</Button>
        <Button variant="secondary" loading={busy} disabled={f.name.trim().length < 2} onClick={() => void saveDraft()}>{t('bon.cmp.saveDraft')}</Button>
        <span className="ml-auto" />
        {step > 1 && <Button variant="secondary" onClick={() => setStep(step - 1)}>{t('common.back')}</Button>}
        {step < 5 ? <Button disabled={!canNext} onClick={() => setStep(step + 1)}>{t('bon.next')}</Button>
          : <Button loading={busy} disabled={past || !audience || audience.eligible === 0} icon={<Send size={16} />} onClick={() => void launch()}>{when === 'now' ? t('bon.cmp.confirmAndSend') : t('bon.cmp.scheduleBtn')}</Button>}
      </div>
    </Modal>
  )
}

/** Аудитория (§6.3): «Все подходящие» — сүзгілер мен санақ; «Выбрать вручную» — клиенттер кестесі белгілеумен. */
function AudienceStep({ f, patch, patchFilter, audience }: {
  f: SaveCampaign; patch: (p: Partial<SaveCampaign>) => void; patchFilter: (p: Partial<SaveCampaign['filter']>) => void; audience: AudiencePreview | null
}) {
  const t = useT()
  const meta = useBonusMeta()
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const q = useQuery({ queryKey: ['bonus', 'cmp-customers', search, page], queryFn: () => bonusApi.customers({ search, page, pageSize: 10 }), placeholderData: keepPreviousData, enabled: f.audienceMode === 'Manual' })
  const n = (v: string) => (v === '' ? null : Number(v.replace(/\D/g, '')))
  return (
    <div className="flex flex-col gap-4">
      <Segmented value={f.audienceMode} options={[{ key: 'All', label: t('bon.cmp.allMatching') }, { key: 'Manual', label: t('bon.cmp.manual') }]} onChange={(v) => patch({ audienceMode: v })} />
      {f.audienceMode === 'All' ? (
        <div className="grid gap-3 sm:grid-cols-3">
          <Field label={t('bon.exp.statuses')}>
            <div className="flex flex-col gap-1">{(meta.data?.statuses ?? []).filter((s) => s.isActive).map((s) => (
              <label key={s.id} className="flex items-center gap-2 text-[13px]"><input type="checkbox" className="accent-brand" checked={f.filter.statusIds.includes(s.id)}
                onChange={(e) => patchFilter({ statusIds: e.target.checked ? [...f.filter.statusIds, s.id] : f.filter.statusIds.filter((x) => x !== s.id) })} />{s.name}</label>
            ))}</div>
          </Field>
          <div className="flex flex-col gap-3">
            <Toggle checked={f.filter.positiveBalance} onChange={(v) => patchFilter({ positiveBalance: v })} label={t('bon.cmp.f.positive')} />
            <Field label={t('bon.cmp.f.expiring')}><Input inputMode="numeric" value={f.filter.expiringWithinDays ?? ''} onChange={(e) => patchFilter({ expiringWithinDays: n(e.target.value) })} placeholder={t('bon.cmp.f.days')} /></Field>
            <Field label={t('bon.cmp.f.inactive')}><Input inputMode="numeric" value={f.filter.inactiveDays ?? ''} onChange={(e) => patchFilter({ inactiveDays: n(e.target.value) })} placeholder={t('bon.cmp.f.days')} /></Field>
          </div>
          <div className="flex flex-col gap-3">
            <Field label={t('bon.cmp.f.minAmount')}><Input inputMode="numeric" value={f.filter.minPurchaseAmount ?? ''} onChange={(e) => patchFilter({ minPurchaseAmount: n(e.target.value) })} placeholder="₸" /></Field>
            <Field label={t('bon.cmp.f.minCount')}><Input inputMode="numeric" value={f.filter.minPurchaseCount ?? ''} onChange={(e) => patchFilter({ minPurchaseCount: n(e.target.value) })} /></Field>
            <Field label={t('bon.cmp.f.birthMonth')}>
              <Select value={f.filter.birthdayMonth ?? ''} onChange={(e) => patchFilter({ birthdayMonth: e.target.value ? Number(e.target.value) : null })}>
                <option value="">—</option>{Array.from({ length: 12 }, (_, i) => <option key={i + 1} value={i + 1}>{new Date(2000, i, 1).toLocaleString('ru', { month: 'long' })}</option>)}
              </Select>
            </Field>
          </div>
        </div>
      ) : (
        <div className="flex flex-col gap-2">
          <SearchInput value={search} onChange={(v) => { setSearch(v); setPage(1) }} placeholder={t('bon.cust.searchPlaceholder')} className="h-10!" />
          <div className="max-h-72 overflow-auto rounded-xl border border-line">
            <table className="w-full">
              <tbody>{(q.data?.items ?? []).map((c) => (
                <tr key={c.customerId} className="border-t border-line first:border-0">
                  <td className="w-10 px-3 py-2"><input type="checkbox" className="accent-brand" checked={f.customerIds.includes(c.customerId)}
                    onChange={(e) => patch({ customerIds: e.target.checked ? [...f.customerIds, c.customerId] : f.customerIds.filter((x) => x !== c.customerId) })} /></td>
                  <td className="py-2 text-[14px]">{c.name}<span className="ml-2 text-[12px] text-ink-3">{c.phone}</span></td>
                  <td className="py-2"><StatusBadge name={c.statusName} color={c.statusColor} /></td>
                  <td className="px-3 py-2 text-right text-[13px]">{c.optOut ? <Pill tone="gray">{t('bon.cust.optOut')}</Pill> : <Points value={c.balance} />}</td>
                </tr>
              ))}</tbody>
            </table>
          </div>
          {q.data && <Pager page={page} pageSize={10} total={q.data.total} onPage={setPage} onPageSize={() => undefined} />}
          <p className="text-[13px] text-ink-2">{t('bon.cmp.selectedN', { n: f.customerIds.length })}</p>
        </div>
      )}
      <div className="flex flex-wrap gap-x-6 gap-y-1 rounded-xl bg-field/60 px-4 py-2 text-[13px]">
        {audience ? (
          <>
            <span>{t('bon.cmp.found')}: <b>{num(audience.found)}</b></span>
            <span>{t('bon.cmp.eligible')}: <b className="text-success">{num(audience.eligible)}</b></span>
            <span>{t('bon.cmp.excluded')}: <b>{num(audience.excluded)}</b>{audience.reasons.length > 0 && ` (${audience.reasons.map((r) => `${t(`bon.reason.${r.reason}` as TranslationKey)}: ${r.count}`).join(', ')})`}</span>
          </>
        ) : <span className="text-ink-3">{t('bon.cmp.counting')}</span>}
      </div>
    </div>
  )
}

/** Карточка мен есеп (§6.7): «Обзор», «Получатели», «Ошибки», «Сообщение», «Бонусные операции». */
function CampaignReport({ id, onClose, onChanged }: { id: string; onClose: () => void; onChanged: () => void }) {
  const t = useT()
  const perms = useBonusPermissions()
  const [tab, setTab] = useState<'overview' | 'recipients' | 'errors' | 'message' | 'bonus'>('overview')
  const [page, setPage] = useState(1)
  const [customer, setCustomer] = useState<string | null>(null)
  const c = useQuery({ queryKey: ['bonus', 'campaign', id], queryFn: () => bonusApi.campaign(id) })
  const rec = useQuery({ queryKey: ['bonus', 'campaign', id, 'recipients', tab, page], queryFn: () => bonusApi.recipients(id, { status: tab === 'errors' ? 'Failed' : undefined, page, pageSize: 25 }), enabled: tab === 'recipients' || tab === 'errors', placeholderData: keepPreviousData })
  const ops = useQuery({ queryKey: ['bonus', 'campaign', id, 'ops'], queryFn: () => bonusApi.operations({ campaignId: id, pageSize: 100 }), enabled: tab === 'bonus' })
  const d = c.data
  const exportRecipients = async () => {
    try { await bonusApi.exportFile('recipients', 'xlsx', { id }); toast(t('stats.exportReady')) } catch (err) { reportError(err, t('stats.exportFailed')) }
  }
  const send = async () => {
    if (!d || !(await confirmDialog({ message: t('bon.cmp.confirmSend', { n: '…' }), confirmLabel: t('bon.cmp.sendNow') }))) return
    try { await bonusApi.sendCampaign(id); toast(t('bon.cmp.sent')); void c.refetch(); onChanged() } catch (err) { reportError(err, t('bon.failed')) }
  }
  return (
    <Modal title={d?.name ?? t('bon.cmp.report')} onClose={onClose} width={900}>
      {!d ? <p className="py-10 text-center text-ink-3">…</p> : (
        <div className="flex flex-col gap-4">
          <div className="flex flex-wrap items-center gap-2 text-[13px] text-ink-2">
            <Pill tone={STATUS_TONE[d.status]}>{t(`bon.cmp.st.${d.status}` as TranslationKey)}</Pill>
            <span>{t(`bon.cmp.typeOpt.${d.type}` as TranslationKey)}</span>·<span>{t('bon.channel.app')}</span>·<span>{d.author}, {dateTime(d.createdAt)}</span>
            <span className="ml-auto" />
            {perms.campaigns && d.canSend && <Button className="h-9! px-3! text-[13px]!" icon={<Send size={15} />} onClick={() => void send()}>{t('bon.cmp.sendNow')}</Button>}
            {perms.export && <Button variant="secondary" className="h-9! px-3! text-[13px]!" icon={<FileText size={15} />} onClick={() => void exportRecipients()}>{t('bon.cmp.exportRecipients')}</Button>}
          </div>
          <Segmented value={tab} options={(['overview', 'recipients', 'errors', 'message', ...(d.bonusAmount ? ['bonus' as const] : [])] as const).map((k) => ({ key: k, label: t(`bon.cmp.tab.${k}` as TranslationKey) }))} onChange={(v) => { setTab(v); setPage(1) }} />
          {tab === 'overview' && (
            <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
              {[['recipients', d.recipients], ['delivered', d.delivered], ['read', d.read], ['errors', d.failed], ['excluded', d.excluded]].map(([k, v]) => (
                <div key={k as string} className="rounded-xl border border-line px-3 py-2"><div className="text-[12px] text-ink-2">{t(`bon.cmp.m.${k}` as TranslationKey)}</div><div className="text-[20px] font-extrabold">{num(v as number)}</div></div>
              ))}
              <div className="rounded-xl border border-line px-3 py-2"><div className="text-[12px] text-ink-2">{t('bon.cmp.when')}</div><div className="font-semibold">{d.startedAt ? dateTime(d.startedAt) : d.scheduledAt ? dateTime(d.scheduledAt) : '—'}</div></div>
              {d.bonusAmount && <div className="rounded-xl border border-line px-3 py-2"><div className="text-[12px] text-ink-2">{t('bon.cmp.bonusAction')}</div><div className="font-semibold">+{num(d.bonusAmount)} × {num(d.delivered)}</div></div>}
              <p className="text-[12px] text-ink-3 sm:col-span-4">{t('bon.cmp.readNote')}</p>
            </div>
          )}
          {(tab === 'recipients' || tab === 'errors') && (
            <>
              <div className="max-h-[50vh] overflow-auto rounded-xl border border-line">
                <table className="w-full">
                  <thead className="sticky top-0 bg-field/90"><tr><th className={th}>{t('bon.customer')}</th><th className={th}>{t('bon.cmp.contact')}</th><th className={th}>{t('bon.cmp.delivery')}</th><th className={th}>{t('bon.cmp.time')}</th><th className={th}>{t('bon.reason')}</th></tr></thead>
                  <tbody>{(rec.data?.items ?? []).length === 0 ? <EmptyRow colSpan={5} text={t('bon.empty')} /> : rec.data!.items.map((r) => (
                    <tr key={r.id} className="border-t border-line">
                      <td className={td}><button type="button" className="hover:text-brand" onClick={() => setCustomer(r.customerId)}>{r.customerName}</button></td>
                      <td className={`${td} text-ink-2`}>{r.contact ?? '—'}</td>
                      <td className={td}><Pill tone={r.status === 'Sent' ? 'green' : r.status === 'Failed' ? 'red' : 'gray'}>{t(`bon.rec.${r.status}` as TranslationKey)}{r.read ? ` · ${t('bon.cmp.m.read')}` : ''}</Pill></td>
                      <td className={`${td} text-[13px] text-ink-2`}>{r.sentAt ? dateTime(r.sentAt) : '—'}</td>
                      <td className={`${td} text-[13px] text-ink-2`}>{r.reason ? t(`bon.reason.${r.reason}` as TranslationKey) : '—'}</td>
                    </tr>
                  ))}</tbody>
                </table>
              </div>
              {rec.data && <Pager page={page} pageSize={25} total={rec.data.total} onPage={setPage} onPageSize={() => undefined} />}
            </>
          )}
          {tab === 'message' && <div className="whitespace-pre-line rounded-xl border border-line p-4 text-[14px]">{d.message}</div>}
          {tab === 'bonus' && (
            <ul className="flex max-h-[50vh] flex-col divide-y divide-line overflow-auto rounded-xl border border-line">
              {(ops.data?.items ?? []).map((o) => (
                <li key={o.id} className="flex items-center gap-3 px-3 py-2 text-[13px]"><OpTypeBadge type={o.type} amount={o.amount} /><span className="flex-1">{o.customerName}</span><span className="text-ink-3">{dateTime(o.createdAt)}</span><Points value={o.amount} signed muted={o.status === 'reversed'} /></li>
              ))}
              {ops.data?.items.length === 0 && <li className="px-3 py-6 text-center text-ink-3">{t('bon.empty')}</li>}
            </ul>
          )}
        </div>
      )}
      {customer && <CustomerPanel customerId={customer} onClose={() => setCustomer(null)} />}
    </Modal>
  )
}
