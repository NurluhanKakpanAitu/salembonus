import { useEffect, useRef, useState } from 'react'
import { keepPreviousData, useQuery, useQueryClient } from '@tanstack/react-query'
import { CalendarClock, Download, Eye, Play } from 'lucide-react'
import { Button } from '../ui/Button'
import { Field, Input, Select, Textarea, Toggle } from '../ui/Field'
import { Modal } from '../ui/Modal'
import { RowMenu } from '../ui/RowMenu'
import { SearchInput } from '../ui/SearchInput'
import { toast } from '../ui/Toast'
import { confirmDialog } from '../ui/Confirm'
import { EmptyRow, Pager, td, th, useDebounced } from '../inventory/inventoryUi'
import { bonusApi, bonusKeys } from '../../lib/bonusApi'
import type { ExpiringLot, ExpirySettings } from '../../lib/bonusTypes'
import { dateOnly, dateTime } from '../../lib/dates'
import { num } from '../../lib/money'
import { useT, type TranslationKey } from '../../lib/i18n'
import { BlockError, Card, Skeleton } from '../stats/statsUi'
import { CustomerPanel } from './CustomerPanel'
import { OpTypeBadge, Pill, Points, reportError, StatusBadge, useBonusMeta, useBonusPermissions, useParamSetter, type BonusFilters } from './bonusUi'

const WINDOWS = [7, 30, 60, 90]
const EXCLUDABLE = ['Adjustment', 'ReturnRestore', 'Reversal']

/**
 * «Сгорание бонусов» (ТЗ «Бонусы» §4): ереже (қосу, мерзім, мәртебелер, ерекшеліктер, ескерту), «Скоро сгорят» реестрі.
 * Мерзім әр партияға жеке; ескі партияларға қолдану — бөлек растау. «Обработать сейчас» — фондық тапсырманы күтпей (қайталауға қауіпсіз).
 */
export function ExpiryTab({ storeIds }: { filters: BonusFilters; storeIds?: string }) {
  const t = useT()
  const qc = useQueryClient()
  const perms = useBonusPermissions()
  const meta = useBonusMeta()
  const { get, set } = useParamSetter()
  const [customer, setCustomer] = useState<string | null>(null)
  const [lot, setLot] = useState<ExpiringLot | null>(null)
  const [running, setRunning] = useState(false)
  const settings = useQuery({ queryKey: ['bonus', 'expiry', 'settings'], queryFn: bonusApi.expirySettings })
  const days = Number(get('days')) || 30
  const search = useDebounced(get('q'), 300)
  const page = Number(get('page')) || 1
  const size = Number(get('size')) || 25
  const statusId = get('status') || undefined
  const list = useQuery({ queryKey: ['bonus', 'expiring', days, search, statusId, page, size, storeIds], queryFn: () => bonusApi.expiring({ days, search, statusId, page, pageSize: size, storeIds }), placeholderData: keepPreviousData })
  const exportFile = async (format: 'xlsx' | 'csv') => {
    try { await bonusApi.exportFile('expiring', format, { days, search, statusId, storeIds }); toast(t('stats.exportReady')) } catch (err) { reportError(err, t('stats.exportFailed')) }
  }
  const run = async () => {
    setRunning(true)
    try { const r = await bonusApi.runJobs(); toast(t('bon.jobsDone', { expired: r.expired, birthdays: r.birthdays, warnings: r.warnings })); void qc.invalidateQueries({ queryKey: bonusKeys.all }) }
    catch (err) { reportError(err, t('bon.failed')) } finally { setRunning(false) }
  }

  return (
    <div className="grid grid-cols-1 gap-4 xl:grid-cols-[420px_1fr]">
      <Card title={t('bon.exp.settings')} icon={<CalendarClock size={18} />}>
        {settings.isError ? <BlockError message={(settings.error as Error).message} onRetry={() => void settings.refetch()} />
          : !settings.data ? <Skeleton className="h-80" /> : <ExpiryForm key={settings.data.updatedAt} s={settings.data} canEdit={perms.manage} statuses={meta.data?.statuses ?? []}
            onSaved={() => { void settings.refetch(); void list.refetch(); void qc.invalidateQueries({ queryKey: bonusKeys.meta }) }} />}
      </Card>
      <div className="flex min-w-0 flex-col gap-3">
        <div className="flex flex-wrap items-center gap-2">
          <h3 className="mr-auto text-[16px] font-bold text-[#0b1f44]">{t('bon.exp.soon')}</h3>
          <SearchInput value={get('q')} onChange={(v) => set({ q: v })} placeholder={t('bon.cust.searchPlaceholder')} className="h-10! w-56" />
          <Select className="h-10! w-auto! min-w-36 text-[13px]" value={days} onChange={(e) => set({ days: e.target.value })} aria-label={t('bon.exp.window')}>
            {WINDOWS.map((d) => <option key={d} value={d}>{t('bon.exp.withinDays', { n: d })}</option>)}
          </Select>
          <Select className="h-10! w-auto! min-w-40 text-[13px]" value={statusId ?? ''} onChange={(e) => set({ status: e.target.value })}>
            <option value="">{t('bon.allStatuses')}</option>
            {(meta.data?.statuses ?? []).map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
          </Select>
          {perms.export && <Button variant="secondary" className="h-10! px-3!" icon={<Download size={16} />} onClick={() => void exportFile('xlsx')}>Excel</Button>}
          {perms.manage && <Button variant="secondary" className="h-10! px-3!" icon={<Play size={16} />} loading={running} onClick={() => void run()} title={t('bon.runJobsHint')}>{t('bon.runJobs')}</Button>}
        </div>
        {list.data && (
          <div className="flex flex-wrap gap-x-5 rounded-xl bg-field/60 px-4 py-2 text-[13px] text-ink-2">
            <span>{t('bon.exp.lots', { n: list.data.total })}</span>
            <span>{t('bon.exp.sum')}: <b className="text-danger">{num(list.data.sum)}</b></span>
            {!list.data.enabled && <span className="text-amber-700">{t('bon.exp.offHint')}</span>}
          </div>
        )}
        <div className="overflow-x-auto rounded-2xl border border-line bg-surface">
          <table className="w-full min-w-[820px]">
            <thead className="bg-field/60"><tr>
              <th className={th}>{t('bon.exp.date')}</th><th className={th}>{t('bon.customer')}</th><th className={th}>{t('bon.phone')}</th><th className={th}>{t('bon.st.name')}</th>
              <th className={`${th} text-right`}>{t('bon.amount')}</th><th className={th}>{t('bon.exp.notification')}</th><th className={th}>{t('bon.exp.state')}</th><th className={`${th} w-12`}></th>
            </tr></thead>
            <tbody>
              {!list.data ? null : list.data.items.length === 0 ? <EmptyRow colSpan={8} text={t('bon.noExpiring')} /> : list.data.items.map((x) => (
                <tr key={x.lotId} className="border-t border-line hover:bg-field/40">
                  <td className={`${td} whitespace-nowrap`}>{dateTime(x.expiresAt)}</td>
                  <td className={td}><button type="button" onClick={() => setCustomer(x.customerId)} className="font-medium hover:text-brand">{x.customerName}</button></td>
                  <td className={`${td} text-ink-2`}>{x.phone ?? '—'}</td>
                  <td className={td}><StatusBadge name={x.statusName} color={x.statusColor} /></td>
                  <td className={`${td} text-right`}><Points value={x.amount} /></td>
                  <td className={td}><Pill tone={x.notification === 'sent' ? 'green' : x.notification === 'scheduled' ? 'blue' : 'gray'}>{t(`bon.notify.${x.notification}` as TranslationKey)}</Pill></td>
                  <td className={td}><Pill tone={x.state === 'pending' ? 'amber' : 'gray'}>{t(`bon.exp.st.${x.state}` as TranslationKey)}</Pill></td>
                  <td className={td}><RowMenu items={[
                    { label: t('bon.openCustomer'), icon: <Eye size={16} />, onClick: () => setCustomer(x.customerId) },
                    { label: t('bon.exp.viewLot'), icon: <CalendarClock size={16} />, onClick: () => setLot(x) },
                  ]} /></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {list.data && <Pager page={page} pageSize={size} total={list.data.total} onPage={(p) => set({ page: String(p) }, true)} onPageSize={(s) => set({ size: String(s) })} />}
      </div>
      {customer && <CustomerPanel customerId={customer} onClose={() => setCustomer(null)} />}
      {lot && <LotModal lot={lot} canEdit={perms.manage} onClose={() => setLot(null)} onChanged={() => { setLot(null); void list.refetch() }} />}
    </div>
  )
}

function ExpiryForm({ s, canEdit, statuses, onSaved }: { s: ExpirySettings; canEdit: boolean; statuses: { id: string; name: string }[]; onSaved: () => void }) {
  const t = useT()
  const [f, setF] = useState({ enabled: s.enabled, value: s.value, unit: s.unit, statusIds: s.statusIds, excludedTypes: s.excludedTypes, warnEnabled: s.warnEnabled, warnDays: s.warnDays, warnChannel: s.warnChannel })
  const [busy, setBusy] = useState(false)
  const dirty = useRef(false)
  useEffect(() => { dirty.current = false }, [s])
  const patch = (p: Partial<typeof f>) => { dirty.current = true; setF((x) => ({ ...x, ...p })) }
  const all = f.statusIds.length === 0
  const save = async () => {
    // Ескі партияларға қолдану — бөлек растау (§4.1, §15).
    const apply = s.openLots > 0 && await confirmDialog({ title: t('bon.exp.applyTitle'), message: t('bon.exp.applyMessage', { n: s.openLots }), confirmLabel: t('bon.exp.applyExisting'), cancelLabel: t('bon.exp.applyNewOnly') })
    setBusy(true)
    try { await bonusApi.saveExpiry({ ...f, applyToExisting: apply }); toast(t('bon.saved')); onSaved() } catch (err) { reportError(err, t('bon.failed')) } finally { setBusy(false) }
  }
  const toggleStatus = (id: string, on: boolean) => {
    const base = all ? statuses.map((x) => x.id) : f.statusIds
    const next = on ? [...new Set([...base, id])] : base.filter((x) => x !== id)
    patch({ statusIds: next.length === statuses.length ? [] : next })
  }
  return (
    <fieldset disabled={!canEdit} className="flex flex-col gap-4">
      <Toggle checked={f.enabled} onChange={(v) => patch({ enabled: v })} label={t('bon.exp.enabled')} />
      <Field label={t('bon.exp.validity')} hint={t('bon.exp.fromAccrual')}>
        <div className="flex gap-2">
          <Input className="w-24" inputMode="numeric" value={f.value} onChange={(e) => patch({ value: Number(e.target.value.replace(/\D/g, '')) || 1 })} />
          <Select value={f.unit} onChange={(e) => patch({ unit: e.target.value as 'Days' | 'Months' })} className="flex-1">
            <option value="Months">{t('bon.exp.months')}</option>
            <option value="Days">{t('bon.exp.days')}</option>
          </Select>
        </div>
      </Field>
      <Field label={t('bon.exp.statuses')}>
        <div className="flex flex-col gap-1.5">
          {statuses.map((x) => (
            <label key={x.id} className="flex items-center gap-2 text-[14px]">
              <input type="checkbox" className="accent-brand" checked={all || f.statusIds.includes(x.id)} onChange={(e) => toggleStatus(x.id, e.target.checked)} />{x.name}
            </label>
          ))}
        </div>
      </Field>
      <Field label={t('bon.exp.exclusions')} hint={t('bon.exp.exclusionsHint')}>
        <div className="flex flex-col gap-1.5">
          {EXCLUDABLE.map((x) => (
            <label key={x} className="flex items-center gap-2 text-[14px]">
              <input type="checkbox" className="accent-brand" checked={f.excludedTypes.includes(x)} onChange={(e) => patch({ excludedTypes: e.target.checked ? [...f.excludedTypes, x] : f.excludedTypes.filter((y) => y !== x) })} />
              {t(`bon.exp.excl.${x}` as TranslationKey)}
            </label>
          ))}
        </div>
      </Field>
      <Toggle checked={f.warnEnabled} onChange={(v) => patch({ warnEnabled: v })} label={t('bon.exp.warn')} />
      {f.warnEnabled && (
        <>
          <Field label={t('bon.exp.warnDays')} hint={t('bon.exp.warnDaysHint')}>
            <Input value={f.warnDays.join(', ')} onChange={(e) => patch({ warnDays: e.target.value.split(/[ ,;]+/).map(Number).filter((n) => n > 0) })} />
          </Field>
          <Field label={t('bon.exp.channel')}>
            <Select value="app" onChange={() => undefined}>
              <option value="app">{t('bon.channel.app')}</option>
              <option value="sms" disabled>{t('bon.channel.sms')} — {t('bon.channel.off')}</option>
              <option value="whatsapp" disabled>{t('bon.channel.whatsapp')} — {t('bon.channel.off')}</option>
            </Select>
          </Field>
        </>
      )}
      <p className="text-[12px] text-ink-3">{t('bon.exp.openLots', { n: s.openLots, w: s.openLotsWithoutExpiry })}</p>
      {canEdit && <Button loading={busy} onClick={() => void save()}>{t('bon.saveSettings')}</Button>}
    </fieldset>
  )
}

/** «Просмотреть лот» (§4.3): бастапқы есептеу, қалдық, күндері; мерзімді өзгерту — арнайы құқық пен себеп. */
function LotModal({ lot, canEdit, onClose, onChanged }: { lot: ExpiringLot; canEdit: boolean; onClose: () => void; onChanged: () => void }) {
  const t = useT()
  const [date, setDate] = useState(lot.expiresAt.slice(0, 10))
  const [reason, setReason] = useState('')
  const [busy, setBusy] = useState(false)
  const save = async () => {
    setBusy(true)
    try { await bonusApi.setLotExpiry(lot.lotId, new Date(`${date}T23:59:00`).toISOString(), reason); toast(t('bon.saved')); onChanged() } catch (err) { reportError(err, t('bon.failed')) } finally { setBusy(false) }
  }
  return (
    <Modal title={t('bon.exp.lotTitle')} onClose={onClose} width={480}>
      <div className="flex flex-col gap-3 text-[14px]">
        <div className="flex items-center gap-2"><OpTypeBadge type={lot.type} amount={lot.original} /><span className="font-semibold">{lot.customerName}</span></div>
        <div className="grid grid-cols-2 gap-2">
          <div className="rounded-xl border border-line px-3 py-2"><div className="text-[12px] text-ink-2">{t('bon.exp.original')}</div><b>{num(lot.original)}</b></div>
          <div className="rounded-xl border border-line px-3 py-2"><div className="text-[12px] text-ink-2">{t('bon.exp.remaining')}</div><b>{num(lot.amount)}</b> <span className="text-[12px] text-ink-3">({t('bon.exp.used')} {num(lot.original - lot.amount)})</span></div>
          <div className="rounded-xl border border-line px-3 py-2"><div className="text-[12px] text-ink-2">{t('bon.exp.accrued')}</div>{dateOnly(lot.accruedAt)}</div>
          <div className="rounded-xl border border-line px-3 py-2"><div className="text-[12px] text-ink-2">{t('bon.exp.date')}</div>{dateTime(lot.expiresAt)}</div>
        </div>
        {canEdit && (
          <div className="flex flex-col gap-3 border-t border-line pt-3">
            <Field label={t('bon.exp.newDate')}><Input type="date" value={date} onChange={(e) => setDate(e.target.value)} /></Field>
            <Field label={t('bon.reason')} required><Textarea value={reason} maxLength={500} onChange={(e) => setReason(e.target.value)} className="min-h-16!" /></Field>
            <div className="flex justify-end gap-2"><Button variant="secondary" onClick={onClose}>{t('common.cancel')}</Button><Button loading={busy} disabled={reason.trim().length < 3} onClick={() => void save()}>{t('bon.exp.changeDate')}</Button></div>
          </div>
        )}
      </div>
    </Modal>
  )
}
