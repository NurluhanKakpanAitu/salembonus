import { useMemo, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowRight, Download, GitMerge, Loader2, Send } from 'lucide-react'
import { Button } from '../ui/Button'
import { confirmDialog } from '../ui/Confirm'
import { Drawer } from '../ui/Drawer'
import { Field, Input, Select, Textarea, Toggle } from '../ui/Field'
import { Modal } from '../ui/Modal'
import { SearchInput } from '../ui/SearchInput'
import { toast } from '../ui/Toast'
import { useDebounced } from '../inventory/inventoryUi'
import { Segmented } from '../stats/statsUi'
import { ApiError } from '../../lib/api'
import { clientKeys, clientsApi } from '../../lib/clientsApi'
import { DEFAULT_EXPORT_FIELDS, EXPORT_FIELDS, type Channel, type ClientCard, type ClientFilter, type ClientSegment } from '../../lib/clientsTypes'
import { dateOnly, dateTime } from '../../lib/dates'
import { useT, type TranslationKey } from '../../lib/i18n'
import { FiltersForm } from './ClientFilters'
import { clientNo, compact, useClientMeta } from './clientsUi'

const errText = (err: unknown, fallback: string) => (err instanceof ApiError ? err.message : fallback)

/** Мұрағат (§18): себеп міндетті; ашық қарыз — болмайды; бонус қалдығы — растаумен. Физикалық жою жоқ. */
export function ArchiveModal({ card, onClose, onDone }: { card: ClientCard; onClose: () => void; onDone: (c: ClientCard) => void }) {
  const t = useT()
  const [reason, setReason] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (reason.trim().length < 3) return
    setBusy(true)
    setError(null)
    try {
      onDone(await clientsApi.archive(card.client.id, reason.trim()))
    } catch (err) {
      if (err instanceof ApiError && err.field === 'bonus') {
        if (await confirmDialog({ message: err.message, confirmLabel: t('cli.archive.confirm'), danger: true })) {
          try { onDone(await clientsApi.archive(card.client.id, reason.trim(), true)) } catch (e2) { setError(errText(e2, t('cli.failed'))) }
        }
      } else setError(errText(err, t('cli.failed')))
    } finally { setBusy(false) }
  }
  return (
    <Modal title={t('cli.archive.title')} onClose={onClose} width={480}>
      <form onSubmit={submit} className="flex flex-col gap-4">
        <p className="text-[14px] text-ink-2">{t('cli.archive.hint', { name: card.client.name })}</p>
        {card.client.debt > 0 && <p className="rounded-xl bg-danger-soft px-3 py-2 text-[13px] text-danger">{t('cli.archive.hasDebt')}</p>}
        <Field label={t('cli.reason')} required><Textarea value={reason} maxLength={500} autoFocus onChange={(e) => setReason(e.target.value)} /></Field>
        {error && <p className="text-[13px] text-danger">{error}</p>}
        <div className="flex justify-end gap-2">
          <Button type="button" variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" variant="danger" loading={busy} disabled={reason.trim().length < 3}>{t('cli.archive.action')}</Button>
        </div>
      </form>
    </Modal>
  )
}

/** Біріктіру (§20): ұсынылған телік не іздеу; қайсысы негізгі — таңдалады; қарыз бен бонус екі еселенбейді, телік мұрағатқа түседі. */
export function MergeModal({ card, storeIds, onClose, onDone }: { card: ClientCard; storeIds?: string; onClose: () => void; onDone: (c: ClientCard) => void }) {
  const t = useT()
  const [search, setSearch] = useState('')
  const q = useDebounced(search, 300)
  const [other, setOther] = useState<{ id: string; name: string; number: number } | null>(null)
  const [keepCurrent, setKeepCurrent] = useState(true)
  const [busy, setBusy] = useState(false)
  const dupes = useQuery({ queryKey: ['clients', 'duplicates', card.client.id], queryFn: () => clientsApi.duplicates(card.client.id) })
  const found = useQuery({
    queryKey: ['clients', 'merge-search', q, storeIds], enabled: q.trim().length >= 2,
    queryFn: () => clientsApi.search({ storeIds, filter: { search: q }, pageSize: 10 }),
  })
  const candidates = [
    ...(dupes.data ?? []).map((d) => ({ id: d.id, name: d.name, number: d.number, hint: t(`cli.merge.reason.${d.reason}` as TranslationKey) })),
    ...(found.data?.rows ?? []).filter((r) => r.id !== card.client.id && !(dupes.data ?? []).some((d) => d.id === r.id))
      .map((r) => ({ id: r.id, name: r.name, number: r.number, hint: r.phone ?? '' })),
  ]
  const primary = keepCurrent ? { id: card.client.id, name: card.client.name, number: card.client.number } : other
  const duplicate = keepCurrent ? other : { id: card.client.id, name: card.client.name, number: card.client.number }
  const submit = async () => {
    if (!primary || !duplicate) return
    if (!(await confirmDialog({ message: t('cli.merge.confirm', { dup: `${duplicate.name} (ID ${clientNo(duplicate.number)})`, main: `${primary.name} (ID ${clientNo(primary.number)})` }), confirmLabel: t('cli.merge.action'), danger: true }))) return
    setBusy(true)
    try { onDone(await clientsApi.merge(primary.id, duplicate.id)); toast(t('cli.merge.done')) } catch (err) { toast(errText(err, t('cli.failed')), 'error') } finally { setBusy(false) }
  }
  return (
    <Modal title={t('cli.merge.title')} onClose={onClose} width={620}>
      <div className="flex flex-col gap-4">
        <p className="text-[13px] text-ink-2">{t('cli.merge.hint')}</p>
        <SearchInput value={search} onChange={setSearch} placeholder={t('cli.searchPlaceholder')} />
        <ul className="flex max-h-64 flex-col divide-y divide-line overflow-y-auto rounded-xl border border-line">
          {(dupes.isLoading || found.isFetching) && <li className="py-4 text-center text-ink-3"><Loader2 className="mx-auto animate-spin" size={18} /></li>}
          {!dupes.isLoading && candidates.length === 0 && <li className="py-6 text-center text-[13px] text-ink-3">{t('cli.merge.none')}</li>}
          {candidates.map((c) => (
            <li key={c.id}>
              <label className="flex cursor-pointer items-center gap-3 px-3 py-2 text-[14px] hover:bg-field">
                <input type="radio" name="merge-other" className="accent-brand" checked={other?.id === c.id} onChange={() => setOther(c)} />
                <span className="min-w-0 flex-1"><span className="block truncate font-medium">{c.name}</span><span className="block text-[12px] text-ink-3">ID {clientNo(c.number)} · {c.hint}</span></span>
              </label>
            </li>
          ))}
        </ul>
        {other && (
          <div className="flex flex-col gap-2 rounded-xl bg-field p-3 text-[13px]">
            <span className="font-semibold">{t('cli.merge.whichMain')}</span>
            <Segmented value={keepCurrent ? 'current' : 'other'} onChange={(v) => setKeepCurrent(v === 'current')}
              options={[{ key: 'current', label: `${card.client.name} · ${clientNo(card.client.number)}` }, { key: 'other', label: `${other.name} · ${clientNo(other.number)}` }]} />
            <span className="flex items-center gap-2 text-ink-2">{duplicate?.name} <ArrowRight size={14} /> {primary?.name}</span>
            <span className="text-ink-3">{t('cli.merge.what')}</span>
          </div>
        )}
        <div className="flex justify-end gap-2">
          <Button variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
          <Button icon={<GitMerge size={16} />} loading={busy} disabled={!other} onClick={() => void submit()}>{t('cli.merge.action')}</Button>
        </div>
      </div>
    </Modal>
  )
}

/** Хабар (§15): тек қосылған арна (SalemBonus қосымшасы), келісіммен; басқа арналар — «интеграция не подключена». */
export function MessageModal({ card, template, onClose, onSent }: { card: ClientCard; template?: string; onClose: () => void; onSent: () => void }) {
  const t = useT()
  const [channel, setChannel] = useState<Channel>('app')
  const [text, setText] = useState(template ?? '')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const c = card.client
  const blocker = !c.consentApp ? t('cli.msg.noConsent') : !c.hasApp ? t('cli.msg.noApp') : !card.bonus.hasCard ? t('cli.msg.noCard') : card.bonus.optOut ? t('cli.msg.optOut') : null
  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!text.trim() || blocker) return
    setBusy(true)
    setError(null)
    try { await clientsApi.message(c.id, channel, text.trim()); toast(t('cli.msg.sent')); onSent() } catch (err) { setError(errText(err, t('cli.failed'))) } finally { setBusy(false) }
  }
  const off = t('cli.msg.notConnected')
  return (
    <Modal title={t('cli.msg.title', { name: c.name })} onClose={onClose} width={520}>
      <form onSubmit={submit} className="flex flex-col gap-4">
        <Segmented value={channel} onChange={setChannel} disabled={{ sms: off, whatsapp: off, email: off }}
          options={(['app', 'sms', 'whatsapp', 'email'] as Channel[]).map((k) => ({ key: k, label: t(`cli.channel.${k}` as TranslationKey) }))} />
        <p className="text-[12px] text-ink-3">{t('cli.msg.channelsHint')}</p>
        {blocker && <p className="rounded-xl bg-amber-50 px-3 py-2 text-[13px] text-amber-800">{blocker}</p>}
        <Field label={t('cli.msg.text')} required hint={`${text.length} / 1000`}>
          <Textarea value={text} maxLength={1000} autoFocus onChange={(e) => setText(e.target.value)} className="min-h-32" />
        </Field>
        {error && <p className="text-[13px] text-danger">{error}</p>}
        <div className="flex justify-end gap-2">
          <Button type="button" variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" icon={<Send size={16} />} loading={busy} disabled={!text.trim() || !!blocker}>{t('cli.msg.send')}</Button>
        </div>
      </form>
    </Modal>
  )
}

const ACTION_KEYS = ['client.create', 'client.update', 'client.archive', 'client.restore', 'client.merge', 'client.merged', 'client.message', 'client.note.add', 'client.note.edit', 'client.note.delete']

/** Өзгерістер тарихы (§24): кім, қашан, не өзгертті — «было → стало». */
export function HistoryDrawer({ clientId, onClose }: { clientId: string; onClose: () => void }) {
  const t = useT()
  const q = useQuery({ queryKey: ['clients', 'history', clientId], queryFn: () => clientsApi.history(clientId) })
  const label = (f: string) => (t(`cli.field.${f}` as TranslationKey) as string | undefined) || f
  return (
    <Drawer title={t('cli.history.title')} onClose={onClose} width={520}>
      {!q.data ? <div className="py-16 text-center text-ink-3"><Loader2 className="mx-auto animate-spin" size={24} /></div> : q.data.length === 0 ? (
        <p className="py-10 text-center text-[13px] text-ink-3">{t('cli.history.empty')}</p>
      ) : (
        <ol className="flex flex-col gap-3">
          {q.data.map((h) => (
            <li key={h.id} className="rounded-xl border border-line p-3">
              <div className="flex items-center gap-2 text-[13px]">
                <span className="font-semibold">{ACTION_KEYS.includes(h.action) ? t(`cli.action.${h.action}` as TranslationKey) : h.action}</span>
                <span className="ml-auto text-ink-3">{dateTime(h.createdAt)}</span>
              </div>
              <div className="text-[12px] text-ink-3">{h.staff || '—'}</div>
              {h.changes.length > 0 && (
                <ul className="mt-2 flex flex-col gap-1 text-[13px]">
                  {h.changes.map((c) => (
                    <li key={c.field} className="flex flex-wrap items-baseline gap-1.5">
                      <span className="text-ink-2">{label(c.field)}:</span>
                      {c.old !== null && <span className="text-ink-3 line-through">{c.old}</span>}
                      {c.old !== null && <ArrowRight size={12} className="text-ink-3" />}
                      <span className="font-medium">{c.new ?? '—'}</span>
                    </li>
                  ))}
                </ul>
              )}
            </li>
          ))}
        </ol>
      )}
    </Drawer>
  )
}

/** Сегмент (§16): атауы, сипаттамасы, шарттары (динамикалық), ортақ не жеке. */
export function SegmentModal({ segment, initialFilter, onClose, onSaved }: {
  segment: ClientSegment | null; initialFilter?: ClientFilter; onClose: () => void; onSaved: (s: ClientSegment) => void
}) {
  const t = useT()
  const meta = useClientMeta()
  const [name, setName] = useState(segment?.name ?? '')
  const [description, setDescription] = useState(segment?.description ?? '')
  const [shared, setShared] = useState(segment?.isShared ?? false)
  const [filter, setFilter] = useState<ClientFilter>(segment?.filter ?? initialFilter ?? {})
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const submit = async () => {
    if (name.trim().length < 2) { setError(t('cli.seg.nameRequired')); return }
    setBusy(true)
    try {
      const body = { name: name.trim(), description: description.trim() || null, filter: compact(filter), isShared: shared }
      const saved = segment ? await clientsApi.updateSegment(segment.id, body) : await clientsApi.createSegment(body)
      toast(t('cli.seg.saved'))
      onSaved(saved)
    } catch (err) { setError(errText(err, t('cli.failed'))) } finally { setBusy(false) }
  }
  return (
    <Drawer title={segment ? t('cli.seg.edit') : t('cli.seg.new')} onClose={onClose} width={560}
      footer={<>
        <Button variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
        <Button loading={busy} onClick={() => void submit()}>{t('cli.form.save')}</Button>
      </>}>
      <div className="flex flex-col gap-4">
        <Field label={t('cli.seg.name')} required><Input value={name} maxLength={120} autoFocus onChange={(e) => setName(e.target.value)} /></Field>
        <Field label={t('cli.seg.description')}><Textarea value={description} maxLength={500} onChange={(e) => setDescription(e.target.value)} className="min-h-16" /></Field>
        <Toggle checked={shared} onChange={setShared} label={t('cli.seg.shared')} />
        <p className="rounded-xl bg-field px-3 py-2 text-[12px] text-ink-2">{t('cli.seg.dynamicHint')}</p>
        <FiltersForm value={filter} onChange={setFilter} statuses={meta.data?.statuses} withQuick withSearch />
        {error && <p className="text-[13px] text-danger">{error}</p>}
      </div>
    </Drawer>
  )
}

/** Топтық: сегментке қосу (қолмен қосылғандар тізіміне). */
export function BulkSegmentModal({ ids, storeIds, onClose, onDone }: { ids: string[]; storeIds?: string; onClose: () => void; onDone: () => void }) {
  const t = useT()
  const qc = useQueryClient()
  const segments = useQuery({ queryKey: ['clients', 'segments', storeIds], queryFn: () => clientsApi.segments(storeIds) })
  const [segmentId, setSegmentId] = useState('')
  const [newName, setNewName] = useState('')
  const [busy, setBusy] = useState(false)
  const submit = async () => {
    setBusy(true)
    try {
      let id = segmentId
      if (!id) id = (await clientsApi.createSegment({ name: newName.trim(), filter: {}, isShared: false, includeIds: ids })).id
      else await clientsApi.addToSegment(ids, id)
      toast(t('cli.bulk.addedToSegment', { n: ids.length }))
      void qc.invalidateQueries({ queryKey: clientKeys.all })
      onDone()
    } catch (err) { toast(errText(err, t('cli.failed')), 'error') } finally { setBusy(false) }
  }
  return (
    <Modal title={t('cli.bulk.toSegment')} onClose={onClose} width={460}>
      <div className="flex flex-col gap-4">
        <p className="text-[13px] text-ink-2">{t('cli.bulk.selected', { n: ids.length })}</p>
        <Field label={t('cli.seg.segment')}>
          <Select value={segmentId} onChange={(e) => setSegmentId(e.target.value)}>
            <option value="">{t('cli.seg.createNew')}</option>
            {(segments.data ?? []).map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
          </Select>
        </Field>
        {!segmentId && <Field label={t('cli.seg.name')} required><Input value={newName} maxLength={120} onChange={(e) => setNewName(e.target.value)} /></Field>}
        <div className="flex justify-end gap-2">
          <Button variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
          <Button loading={busy} disabled={!segmentId && newName.trim().length < 2} onClick={() => void submit()}>{t('cli.bulk.add')}</Button>
        </div>
      </div>
    </Modal>
  )
}

/** Топтық мәртебе: «Бонусы» ережесімен (bonus.manage, себебімен), ағымдағы дүкен бағдарламасында. */
export function BulkStatusModal({ ids, onClose, onDone }: { ids: string[]; onClose: () => void; onDone: () => void }) {
  const t = useT()
  const qc = useQueryClient()
  const meta = useClientMeta()
  const [statusId, setStatusId] = useState('')
  const [reason, setReason] = useState('')
  const [busy, setBusy] = useState(false)
  const submit = async () => {
    setBusy(true)
    try {
      const r = await clientsApi.assignStatus(ids, statusId, reason.trim())
      toast(t('cli.bulk.statusDone', { n: r.processed, skipped: r.skipped }))
      void qc.invalidateQueries({ queryKey: clientKeys.all })
      void qc.invalidateQueries({ queryKey: ['bonus'] })
      onDone()
    } catch (err) { toast(errText(err, t('cli.failed')), 'error') } finally { setBusy(false) }
  }
  return (
    <Modal title={t('cli.bulk.status')} onClose={onClose} width={460}>
      <div className="flex flex-col gap-4">
        <p className="text-[13px] text-ink-2">{t('cli.bulk.selected', { n: ids.length })}</p>
        <Field label={t('cli.col.status')} required>
          <Select value={statusId} onChange={(e) => setStatusId(e.target.value)}>
            <option value="">—</option>
            {(meta.data?.statuses ?? []).map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
          </Select>
        </Field>
        <Field label={t('cli.reason')} required><Textarea value={reason} maxLength={500} onChange={(e) => setReason(e.target.value)} className="min-h-16" /></Field>
        <div className="flex justify-end gap-2">
          <Button variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
          <Button loading={busy} disabled={!statusId || reason.trim().length < 3} onClick={() => void submit()}>{t('cli.bulk.apply')}</Button>
        </div>
      </div>
    </Modal>
  )
}

const PERSONAL = new Set(['email', 'birthDate', 'address'])

/** Экспорт (§19.2): ағымдағы сүзгі не таңдалғандар; бағандар таңдалады; жеке деректер — тек құқықпен. */
export function ExportModal({ total, selected, request, onClose }: {
  total: number | null
  selected: string[]
  request: { storeIds?: string; filter?: ClientFilter; segmentId?: string | null }
  onClose: () => void
}) {
  const t = useT()
  const meta = useClientMeta()
  const contacts = meta.data?.can.contacts ?? false
  const custom = meta.data?.settings.customFields ?? []
  const [format, setFormat] = useState<'xlsx' | 'csv'>('xlsx')
  const [scope, setScope] = useState<'filter' | 'selected'>(selected.length > 0 ? 'selected' : 'filter')
  const [fields, setFields] = useState<string[]>(DEFAULT_EXPORT_FIELDS)
  const [busy, setBusy] = useState(false)
  const all = useMemo(() => [...EXPORT_FIELDS, ...custom.map((c) => `custom.${c.key}`)], [custom])
  const label = (f: string) => (f.startsWith('custom.') ? custom.find((c) => `custom.${c.key}` === f)?.label ?? f : t(`cli.field.${f}` as TranslationKey))
  const submit = async () => {
    setBusy(true)
    try {
      await clientsApi.exportFile({ ...request, ids: scope === 'selected' ? selected : undefined, format, fields: all.filter((f) => fields.includes(f)) })
      onClose()
    } catch (err) { toast(errText(err, t('cli.failed')), 'error') } finally { setBusy(false) }
  }
  return (
    <Modal title={t('cli.export.title')} onClose={onClose} width={620}>
      <div className="flex flex-col gap-4">
        <div className="flex flex-wrap items-center gap-3">
          <Segmented value={scope} onChange={setScope} disabled={selected.length === 0 ? { selected: t('cli.export.noSelection') } : undefined}
            options={[{ key: 'filter', label: t('cli.export.byFilter', { n: total ?? '…' }) }, { key: 'selected', label: t('cli.export.selected', { n: selected.length }) }]} />
          <Segmented value={format} onChange={setFormat} options={[{ key: 'xlsx', label: 'Excel' }, { key: 'csv', label: 'CSV' }]} />
        </div>
        <div className="grid grid-cols-2 gap-x-4 gap-y-2 sm:grid-cols-3">
          {all.map((f) => {
            const locked = !contacts && PERSONAL.has(f)
            return (
              <label key={f} className={`flex items-center gap-2 text-[13px] ${locked ? 'opacity-50' : ''}`} title={locked ? t('cli.export.needContacts') : undefined}>
                <input type="checkbox" className="accent-brand" disabled={locked} checked={!locked && fields.includes(f)}
                  onChange={(e) => setFields((x) => (e.target.checked ? [...x, f] : x.filter((y) => y !== f)))} />
                {label(f)}
              </label>
            )
          })}
        </div>
        {!contacts && <p className="text-[12px] text-ink-3">{t('cli.export.maskedHint')}</p>}
        <div className="flex justify-end gap-2">
          <Button variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
          <Button icon={<Download size={16} />} loading={busy} disabled={fields.length === 0} onClick={() => void submit()}>{t('cli.export.action')}</Button>
        </div>
      </div>
    </Modal>
  )
}

export function lastPurchaseText(t: ReturnType<typeof useT>, iso: string | null, days: number | null) {
  if (!iso) return '—'
  return `${dateOnly(iso)}${days !== null ? ` · ${days === 0 ? t('cli.today') : t('cli.daysAgo', { n: days })}` : ''}`
}
