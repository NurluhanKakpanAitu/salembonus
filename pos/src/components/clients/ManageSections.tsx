import { useEffect, useRef, useState } from 'react'
import { keepPreviousData, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, useNavigate } from 'react-router-dom'
import { CheckCircle2, Download, Eye, FileSpreadsheet, HandCoins, Layers, Lock, Pencil, Plus, Trash2, Upload, Users } from 'lucide-react'
import { Button } from '../ui/Button'
import { confirmDialog } from '../ui/Confirm'
import { Field, Input, Select, Toggle } from '../ui/Field'
import { RowMenu } from '../ui/RowMenu'
import { SearchInput } from '../ui/SearchInput'
import { toast } from '../ui/Toast'
import { EmptyRow, Pager, StatCard, td, th, useDebounced } from '../inventory/inventoryUi'
import { DebtBadge } from '../finance/financeUi'
import { BlockError, Card, Segmented, Skeleton } from '../stats/statsUi'
import { Pill } from '../bonus/bonusUi'
import { ApiError } from '../../lib/api'
import { clientKeys, clientsApi } from '../../lib/clientsApi'
import { DEFAULT_EXPORT_FIELDS, IMPORT_FIELDS, type ClientSegment, type ClientSettings, type CustomFieldDefinition, type ImportPreview, type ImportResult } from '../../lib/clientsTypes'
import { dateOnly, dateTime } from '../../lib/dates'
import { num, tenge } from '../../lib/money'
import { useT, type TranslationKey } from '../../lib/i18n'
import { ExportModal, SegmentModal } from './ClientModals'
import { downloadCsv, useClientMeta, useParamSetter } from './clientsUi'

const errText = (err: unknown, fallback: string) => (err instanceof ApiError ? err.message : fallback)

// ---------- Сегменттер ----------

/** «Сегменты» (ТЗ «Клиенты» §16): динамикалық (шарттар әр ашылғанда қайта есептеледі) + қолмен қосылғандар; жеке не ортақ. */
export function SegmentsSection({ storeIds }: { storeIds?: string }) {
  const t = useT()
  const nav = useNavigate()
  const meta = useClientMeta()
  const canEdit = meta.data?.can.edit ?? false
  const q = useQuery({ queryKey: ['clients', 'segments', storeIds], queryFn: () => clientsApi.segments(storeIds) })
  const [editing, setEditing] = useState<ClientSegment | 'new' | null>(null)
  const open = (s: ClientSegment) => `/clients?segment=${s.id}${storeIds ? `&stores=${storeIds}` : ''}`
  const remove = async (s: ClientSegment) => {
    if (!(await confirmDialog({ message: t('cli.seg.deleteConfirm', { name: s.name }), confirmLabel: t('cli.seg.delete'), danger: true }))) return
    try { await clientsApi.deleteSegment(s.id); toast(t('cli.seg.deleted')); void q.refetch() } catch (err) { toast(errText(err, t('cli.failed')), 'error') }
  }
  if (q.isError) return <BlockError message={(q.error as Error).message} onRetry={() => void q.refetch()} />
  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center gap-3">
        <p className="text-[13px] text-ink-2">{t('cli.seg.hint')}</p>
        {canEdit && <Button className="ml-auto" icon={<Plus size={16} />} onClick={() => setEditing('new')}>{t('cli.seg.new')}</Button>}
      </div>
      {!q.data ? <Skeleton className="h-40" /> : q.data.length === 0 ? (
        <div className="rounded-2xl border border-dashed border-line bg-surface py-14 text-center text-[13px] text-ink-3">
          <Layers className="mx-auto mb-2 text-ink-3" size={28} />{t('cli.seg.empty')}
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-3">
          {q.data.map((s) => (
            <div key={s.id} className="flex flex-col gap-2 rounded-2xl border border-line bg-surface p-4">
              <div className="flex items-start gap-2">
                <span className="flex size-9 shrink-0 items-center justify-center rounded-xl bg-brand-soft text-brand"><Layers size={18} /></span>
                <div className="min-w-0 flex-1">
                  <Link to={open(s)} className="block truncate text-[15px] font-bold hover:text-brand">{s.name}</Link>
                  <div className="text-[12px] text-ink-3">{s.isShared ? t('cli.seg.sharedBy', { author: s.author }) : t('cli.seg.private')} · {dateOnly(s.updatedAt)}</div>
                </div>
                <RowMenu items={[
                  { label: t('cli.seg.open'), icon: <Eye size={16} />, onClick: () => nav(open(s)) },
                  ...(canEdit ? [
                    { label: t('cli.menu.edit'), icon: <Pencil size={16} />, onClick: () => setEditing(s) },
                    { label: t('cli.seg.delete'), icon: <Trash2 size={16} />, danger: true, onClick: () => void remove(s) },
                  ] : []),
                ]} />
              </div>
              {s.description && <p className="line-clamp-2 text-[13px] text-ink-2">{s.description}</p>}
              <div className="mt-auto flex items-end justify-between">
                <span><span className="text-[24px] font-extrabold">{num(s.count)}</span> <span className="text-[13px] text-ink-2">{t('cli.ov.clients')}</span></span>
                {s.included > 0 && <Pill tone="violet">{t('cli.seg.included', { n: s.included })}</Pill>}
              </div>
            </div>
          ))}
        </div>
      )}
      {editing && <SegmentModal segment={editing === 'new' ? null : editing} onClose={() => setEditing(null)} onSaved={() => { setEditing(null); void q.refetch() }} />}
    </div>
  )
}

// ---------- Клиенттер қарызы ----------

type DebtSegment = 'all' | 'overdue' | 'due' | 'paid'

/** «Долги клиентов» (ТЗ «Клиенты» §10): қарызы бар клиенттер; карточкада — құжаттар, төлемдер, «Принять оплату» (finance.operations). */
export function DebtsSection({ storeIds }: { storeIds?: string }) {
  const t = useT()
  const { set } = useParamSetter()
  const [segment, setSegment] = useState<DebtSegment>('all')
  const [search, setSearch] = useState('')
  const q = useDebounced(search, 300)
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(25)
  const list = useQuery({
    queryKey: ['clients', 'debt-registry', storeIds, segment, q, page, pageSize], placeholderData: keepPreviousData,
    queryFn: () => clientsApi.debts({ storeIds, segment: segment === 'all' ? undefined : segment, search: q || undefined, page, pageSize }),
  })
  const open = async (customerId: string) => {
    try { const card = await clientsApi.byCustomer(customerId); set({ client: card.client.id, tab: 'debts' }, true) } catch (err) { toast(errText(err, t('cli.failed')), 'error') }
  }
  if (list.isError) return <BlockError message={(list.error as Error).message} onRetry={() => void list.refetch()} />
  const d = list.data
  return (
    <div className="flex flex-col gap-4">
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <StatCard icon={<Users size={18} />} label={t('cli.debts.clients')} value={d ? num(d.customers) : '…'} />
        <StatCard icon={<HandCoins size={18} />} label={t('cli.prof.debtTotal')} value={d ? tenge(d.total) : '…'} tone="amber" />
        <StatCard icon={<HandCoins size={18} />} label={t('cli.prof.debtOverdue')} value={d ? tenge(d.overdue) : '…'} tone="danger" />
        <StatCard icon={<HandCoins size={18} />} label={t('cli.debts.dueSoon')} value={d ? tenge(d.dueSoon) : '…'} tone="violet" />
      </div>
      <div className="flex flex-wrap items-center gap-3">
        <Segmented value={segment} onChange={(v) => { setSegment(v); setPage(1) }}
          options={(['all', 'overdue', 'due', 'paid'] as DebtSegment[]).map((k) => ({ key: k, label: t(`cli.debts.seg.${k}` as TranslationKey) }))} />
        <SearchInput value={search} onChange={(v) => { setSearch(v); setPage(1) }} placeholder={t('cli.searchPlaceholder')} className="min-w-60 flex-1" />
      </div>
      <div className="overflow-x-auto rounded-2xl border border-line bg-surface">
        <table className="w-full min-w-[760px]">
          <thead className="border-b border-line bg-field/60"><tr>
            <th className={th}>{t('cli.col.client')}</th><th className={th}>{t('cli.col.phone')}</th><th className={`${th} text-right`}>{t('cli.col.debt')}</th>
            <th className={`${th} text-right`}>{t('cli.col.overdue')}</th><th className={th}>{t('cli.prof.nextDue')}</th><th className={`${th} text-right`}>{t('cli.debts.docs')}</th><th className={th}>{t('cli.debts.status')}</th>
          </tr></thead>
          <tbody>
            {!d && <EmptyRow colSpan={7} text={t('cli.loading')} />}
            {d?.rows.length === 0 && <EmptyRow colSpan={7} text={t('cli.debts.empty')} />}
            {d?.rows.map((r) => (
              <tr key={r.customerId} className="cursor-pointer border-b border-line last:border-0 hover:bg-field/60" onClick={() => void open(r.customerId)}>
                <td className={`${td} font-medium`}>{r.name}</td><td className={td}>{r.phone ?? '—'}</td>
                <td className={`${td} text-right font-semibold`}>{tenge(r.total)}</td>
                <td className={`${td} text-right ${r.overdue > 0 ? 'font-semibold text-danger' : 'text-ink-3'}`}>{r.overdue > 0 ? tenge(r.overdue) : '—'}</td>
                <td className={td}>{r.nextDue ? dateOnly(r.nextDue) : '—'}</td><td className={`${td} text-right`}>{r.documents}</td>
                <td className={td}><DebtBadge status={r.status} /></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {d && <Pager page={page} pageSize={pageSize} total={d.rowsTotal} onPage={setPage} onPageSize={(s) => { setPageSize(s); setPage(1) }} />}
    </div>
  )
}

// ---------- Импорт пен экспорт ----------

/** «Импорт / экспорт» (ТЗ «Клиенты» §19): файл → бағандар → алдын ала тексеру (растауға дейін ештеңе сақталмайды) → импорт; кілт — қайталанбау. */
export function ExchangeSection({ storeIds }: { storeIds?: string }) {
  const t = useT()
  const qc = useQueryClient()
  const meta = useClientMeta()
  const can = meta.data?.can
  const fileRef = useRef<HTMLInputElement>(null)
  const [file, setFile] = useState<File | null>(null)
  const [strategy, setStrategy] = useState<'skip' | 'update'>('skip')
  const [preview, setPreview] = useState<ImportPreview | null>(null)
  const [mapping, setMapping] = useState<Record<string, number> | null>(null)
  const [result, setResult] = useState<ImportResult | null>(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [exporting, setExporting] = useState(false)
  const key = useRef(crypto.randomUUID())

  const runPreview = async (f: File, m: Record<string, number> | null, s: string) => {
    setBusy(true)
    setError(null)
    try { const p = await clientsApi.importPreview(f, m, s); setPreview(p); setMapping(p.mapping) } catch (err) { setPreview(null); setError(errText(err, t('cli.failed'))) } finally { setBusy(false) }
  }
  useEffect(() => { if (file) void runPreview(file, mapping, strategy) }, [strategy]) // eslint-disable-line react-hooks/exhaustive-deps

  const pick = (f: File | null) => {
    setFile(f); setPreview(null); setMapping(null); setResult(null); key.current = crypto.randomUUID()
    if (f) void runPreview(f, null, strategy)
  }
  const remap = (field: string, index: number) => {
    const next = { ...mapping }
    if (index < 0) delete next[field]; else next[field] = index
    setMapping(next)
    if (file) void runPreview(file, next, strategy)
  }
  const commit = async () => {
    if (!file || !mapping) return
    if (!(await confirmDialog({ message: t('cli.imp.confirm', { n: preview?.new ?? 0, u: strategy === 'update' ? preview?.update ?? 0 : 0 }), confirmLabel: t('cli.imp.run') }))) return
    setBusy(true)
    try {
      setResult(await clientsApi.importCommit(file, mapping, strategy, key.current))
      void qc.invalidateQueries({ queryKey: clientKeys.all })
    } catch (err) { setError(errText(err, t('cli.failed'))) } finally { setBusy(false) }
  }
  const errorsCsv = (items: { row: number; phone: string | null; name: string | null; message: string | null; status: string }[]) =>
    downloadCsv('clients-import-errors.csv', [t('cli.imp.row'), t('cli.col.phone'), t('cli.col.client'), t('cli.imp.message')],
      items.filter((x) => x.status === 'error').map((x) => [x.row, x.phone, x.name, x.message]))

  return (
    <div className="grid grid-cols-1 gap-4 xl:grid-cols-3">
      <Card title={t('cli.imp.title')} icon={<Upload size={18} />} className="xl:col-span-2"
        action={<Button variant="ghost" className="h-9! px-3!" icon={<FileSpreadsheet size={15} />} onClick={() => void clientsApi.template()}>{t('cli.imp.template')}</Button>}>
        {!can?.edit ? <p className="flex items-center gap-2 text-[13px] text-ink-3"><Lock size={14} />{t('cli.noRight')}</p> : (
          <div className="flex flex-col gap-4">
            <div className="flex flex-wrap items-center gap-3">
              <input ref={fileRef} type="file" accept=".xlsx,.csv,text/csv" className="hidden" onChange={(e) => pick(e.target.files?.[0] ?? null)} />
              <Button variant="secondary" icon={<Upload size={16} />} onClick={() => fileRef.current?.click()}>{file ? file.name : t('cli.imp.choose')}</Button>
              <Segmented value={strategy} onChange={setStrategy} options={[{ key: 'skip', label: t('cli.imp.skip') }, { key: 'update', label: t('cli.imp.update') }]} />
            </div>
            <p className="text-[12px] text-ink-3">{t('cli.imp.hint')}</p>
            {error && <p className="rounded-xl bg-danger-soft px-3 py-2 text-[13px] text-danger">{error}</p>}
            {busy && !preview && <Skeleton className="h-32" />}
            {preview && !result && (
              <>
                <div className="grid grid-cols-1 gap-2 sm:grid-cols-2 lg:grid-cols-3">
                  {IMPORT_FIELDS.map((f) => (
                    <Field key={f} label={t(`cli.imp.field.${f}` as TranslationKey)} required={f === 'phone'}>
                      <Select value={mapping?.[f] ?? -1} onChange={(e) => remap(f, Number(e.target.value))}>
                        <option value={-1}>{t('cli.imp.notMapped')}</option>
                        {preview.headers.map((h, i) => <option key={`${h}-${i}`} value={i}>{h || `#${i + 1}`}</option>)}
                      </Select>
                    </Field>
                  ))}
                </div>
                <div className="grid grid-cols-2 gap-2 sm:grid-cols-5">
                  {([['rows', preview.rows], ['new', preview.new], ['update', preview.update], ['skip', preview.skip], ['errors', preview.errors]] as const).map(([k, v]) => (
                    <div key={k} className={`rounded-xl border px-3 py-2 ${k === 'errors' && v > 0 ? 'border-danger/40 bg-danger-soft' : 'border-line'}`}>
                      <div className="text-[12px] text-ink-2">{t(`cli.imp.count.${k}` as TranslationKey)}</div><div className="text-[18px] font-bold">{num(v)}</div>
                    </div>
                  ))}
                </div>
                <ImportTable items={preview.items} />
                <div className="flex flex-wrap items-center gap-2">
                  {preview.errors > 0 && <Button variant="secondary" icon={<Download size={15} />} onClick={() => errorsCsv(preview.items)}>{t('cli.imp.errorsCsv')}</Button>}
                  <Button className="ml-auto" loading={busy} disabled={preview.new + (strategy === 'update' ? preview.update : 0) === 0} onClick={() => void commit()}>{t('cli.imp.run')}</Button>
                </div>
              </>
            )}
            {result && (
              <div className="flex flex-col gap-3">
                <p className="flex items-center gap-2 text-[14px] font-semibold text-success"><CheckCircle2 size={18} />{t('cli.imp.done', { created: result.created, updated: result.updated, skipped: result.skipped, errors: result.errors })}</p>
                <ImportTable items={result.items.filter((x) => x.status === 'error' || x.status === 'new')} />
                <div className="flex gap-2">
                  {result.errors > 0 && <Button variant="secondary" icon={<Download size={15} />} onClick={() => errorsCsv(result.items)}>{t('cli.imp.errorsCsv')}</Button>}
                  <Button variant="secondary" onClick={() => pick(null)}>{t('cli.imp.again')}</Button>
                  <Link to="/clients?quick=new" className="ml-auto self-center text-[13px] font-semibold text-brand hover:underline">{t('cli.imp.openList')}</Link>
                </div>
              </div>
            )}
          </div>
        )}
      </Card>
      <Card title={t('cli.export.title')} icon={<Download size={18} />}>
        <div className="flex flex-col gap-3 text-[13px] text-ink-2">
          <p>{t('cli.export.hint')}</p>
          <p className="text-[12px] text-ink-3">{t('cli.export.defaultFields', { n: DEFAULT_EXPORT_FIELDS.length })}</p>
          {can?.export ? <Button icon={<Download size={16} />} onClick={() => setExporting(true)}>{t('cli.export.action')}</Button>
            : <p className="flex items-center gap-2 text-ink-3"><Lock size={14} />{t('cli.noRight')}</p>}
        </div>
      </Card>
      {exporting && <ExportModal total={null} selected={[]} request={{ storeIds, filter: {} }} onClose={() => setExporting(false)} />}
    </div>
  )
}

const IMPORT_TONE = { new: 'green', update: 'blue', skip: 'gray', error: 'red' } as const

function ImportTable({ items }: { items: ImportResult['items'] }) {
  const t = useT()
  if (items.length === 0) return null
  return (
    <div className="max-h-80 overflow-auto rounded-xl border border-line">
      <table className="w-full text-[13px]">
        <thead className="sticky top-0 border-b border-line bg-field"><tr>
          <th className={th}>{t('cli.imp.row')}</th><th className={th}>{t('cli.debts.status')}</th><th className={th}>{t('cli.col.phone')}</th><th className={th}>{t('cli.col.client')}</th><th className={th}>{t('cli.imp.message')}</th>
        </tr></thead>
        <tbody>
          {items.map((x) => (
            <tr key={`${x.row}-${x.status}`} className="border-b border-line last:border-0">
              <td className="px-3 py-1.5">{x.row}</td>
              <td className="px-3 py-1.5"><Pill tone={IMPORT_TONE[x.status]}>{t(`cli.imp.st.${x.status}` as TranslationKey)}</Pill></td>
              <td className="px-3 py-1.5 whitespace-nowrap">{x.phone ?? '—'}</td><td className="px-3 py-1.5">{x.name ?? '—'}</td>
              <td className="px-3 py-1.5 text-ink-2">{x.message ?? ''}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

// ---------- Баптаулар ----------

const COLUMN_OPTIONS = ['phone', 'status', 'purchases', 'amount', 'average', 'lastPurchase', 'bonus', 'debt', 'registered', 'activity', 'birthDate', 'source', 'email']

/** «Настройки» (ТЗ «Клиенты» §21): міндетті өрістер, белсенділік шектері, «скоро срок», әдепкі бағандар, қосымша өрістер. */
export function SettingsSection() {
  const t = useT()
  const qc = useQueryClient()
  const meta = useClientMeta()
  const canSave = meta.data?.can.settings ?? false
  const q = useQuery({ queryKey: ['clients', 'settings'], queryFn: clientsApi.settings })
  const [s, setS] = useState<ClientSettings | null>(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  useEffect(() => { if (q.data) setS(q.data) }, [q.data])
  if (!s) return <Skeleton className="h-64" />
  const patch = (p: Partial<ClientSettings>) => setS({ ...s, ...p })
  const setField = (i: number, p: Partial<CustomFieldDefinition>) => patch({ customFields: s.customFields.map((f, j) => (j === i ? { ...f, ...p } : f)) })
  const save = async () => {
    setBusy(true)
    setError(null)
    try {
      const saved = await clientsApi.saveSettings({
        requireLastName: s.requireLastName, requireBirthDate: s.requireBirthDate, activeDays: s.activeDays, passiveDays: s.passiveDays, newDays: s.newDays,
        dueSoonDays: s.dueSoonDays, defaultColumns: s.defaultColumns, customFields: s.customFields,
      })
      setS(saved)
      toast(t('cli.set.saved'))
      void qc.invalidateQueries({ queryKey: clientKeys.all })
    } catch (err) { setError(errText(err, t('cli.failed'))) } finally { setBusy(false) }
  }
  const numInput = (key: 'activeDays' | 'passiveDays' | 'newDays' | 'dueSoonDays') => (
    <Input type="number" min={0} value={s[key]} disabled={!canSave} onChange={(e) => patch({ [key]: Number(e.target.value) || 0 } as Partial<ClientSettings>)} />
  )
  return (
    <div className="grid grid-cols-1 gap-4 xl:grid-cols-2">
      <Card title={t('cli.set.activity')}>
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <Field label={t('cli.set.activeDays')} hint={t('cli.set.activeHint')}>{numInput('activeDays')}</Field>
          <Field label={t('cli.set.passiveDays')} hint={t('cli.set.passiveHint')}>{numInput('passiveDays')}</Field>
          <Field label={t('cli.set.newDays')}>{numInput('newDays')}</Field>
          <Field label={t('cli.set.dueSoonDays')}>{numInput('dueSoonDays')}</Field>
        </div>
      </Card>
      <Card title={t('cli.set.required')}>
        <div className="flex flex-col gap-3">
          <p className="text-[12px] text-ink-3">{t('cli.set.requiredHint')}</p>
          <Toggle checked={s.requireLastName} onChange={(v) => canSave && patch({ requireLastName: v })} label={t('cli.col.lastName')} />
          <Toggle checked={s.requireBirthDate} onChange={(v) => canSave && patch({ requireBirthDate: v })} label={t('cli.col.birthDate')} />
        </div>
      </Card>
      <Card title={t('cli.set.columns')}>
        <p className="mb-3 text-[12px] text-ink-3">{t('cli.set.columnsHint')}</p>
        <div className="grid grid-cols-2 gap-2 sm:grid-cols-3">
          {COLUMN_OPTIONS.map((c) => (
            <label key={c} className="flex items-center gap-2 text-[13px]">
              <input type="checkbox" className="accent-brand" disabled={!canSave} checked={s.defaultColumns.includes(c)}
                onChange={(e) => patch({ defaultColumns: e.target.checked ? COLUMN_OPTIONS.filter((x) => x === c || s.defaultColumns.includes(x)) : s.defaultColumns.filter((x) => x !== c) })} />
              {t(`cli.col.${c}` as TranslationKey)}
            </label>
          ))}
        </div>
      </Card>
      <Card title={t('cli.set.custom')} action={canSave && s.customFields.length < 20 ? (
        <Button variant="secondary" className="h-9! px-3!" icon={<Plus size={15} />} onClick={() => patch({ customFields: [...s.customFields, { key: '', label: '', type: 'text', options: [] }] })}>{t('cli.set.addField')}</Button>
      ) : undefined}>
        {s.customFields.length === 0 ? <p className="text-[13px] text-ink-3">{t('cli.set.noFields')}</p> : (
          <div className="flex flex-col gap-3">
            {s.customFields.map((f, i) => (
              <div key={i} className="grid grid-cols-1 gap-2 rounded-xl border border-line p-3 sm:grid-cols-[1fr_9rem_8rem_auto]">
                <Input placeholder={t('cli.set.fieldLabel')} value={f.label} maxLength={60} disabled={!canSave} onChange={(e) => setField(i, { label: e.target.value })} />
                <Input placeholder={t('cli.set.fieldKey')} value={f.key} maxLength={32} disabled={!canSave} onChange={(e) => setField(i, { key: e.target.value.toLowerCase().replace(/[^a-z0-9_]/g, '') })} />
                <Select value={f.type} disabled={!canSave} onChange={(e) => setField(i, { type: e.target.value as CustomFieldDefinition['type'] })}>
                  {(['text', 'number', 'date', 'select'] as const).map((x) => <option key={x} value={x}>{t(`cli.set.type.${x}` as TranslationKey)}</option>)}
                </Select>
                {canSave && <Button variant="ghost" className="h-11! px-3!" aria-label={t('cli.set.removeField')} icon={<Trash2 size={16} />} onClick={() => patch({ customFields: s.customFields.filter((_, j) => j !== i) })} />}
                {f.type === 'select' && (
                  <OptionsInput value={f.options} disabled={!canSave} placeholder={t('cli.set.options')} onChange={(options) => setField(i, { options })} />
                )}
              </div>
            ))}
            <p className="text-[12px] text-ink-3">{t('cli.set.keyHint')}</p>
          </div>
        )}
      </Card>
      <div className="flex items-center gap-3 xl:col-span-2">
        {s.updatedAt && <span className="text-[12px] text-ink-3">{t('cli.set.updated', { date: dateTime(s.updatedAt) })}</span>}
        {error && <span className="text-[13px] text-danger">{error}</span>}
        {canSave ? <Button className="ml-auto" loading={busy} onClick={() => void save()}>{t('cli.form.save')}</Button>
          : <span className="ml-auto flex items-center gap-2 text-[13px] text-ink-3"><Lock size={14} />{t('cli.set.readOnly')}</span>}
      </div>
    </div>
  )
}

/** Тізім мәндері үтір арқылы: жазу кезінде еркін, фокустан шыққанда тізімге айналады. */
function OptionsInput({ value, disabled, placeholder, onChange }: { value: string[]; disabled: boolean; placeholder: string; onChange: (v: string[]) => void }) {
  const [raw, setRaw] = useState(value.join(', '))
  return (
    <Input className="sm:col-span-4" placeholder={placeholder} value={raw} disabled={disabled} onChange={(e) => setRaw(e.target.value)}
      onBlur={() => { const list = raw.split(',').map((x) => x.trim()).filter(Boolean); onChange(list); setRaw(list.join(', ')) }} />
  )
}
