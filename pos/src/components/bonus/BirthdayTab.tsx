import { useRef, useState } from 'react'
import { keepPreviousData, useQuery, useQueryClient } from '@tanstack/react-query'
import { Cake, Download, Eye, Gift, Plus, X } from 'lucide-react'
import { Button } from '../ui/Button'
import { Field, Input, Select, Textarea, Toggle } from '../ui/Field'
import { TimeInput } from '../ui/TimeInput'
import { Modal } from '../ui/Modal'
import { RowMenu } from '../ui/RowMenu'
import { SearchInput } from '../ui/SearchInput'
import { toast } from '../ui/Toast'
import { EmptyRow, Pager, td, th, useDebounced } from '../inventory/inventoryUi'
import { bonusApi, bonusKeys } from '../../lib/bonusApi'
import type { BirthdaySettings, BirthdayTiming, BirthdayValidity, BonusCustomer, CustomerRef, UpcomingBirthday } from '../../lib/bonusTypes'
import { dateOnly, dateTime } from '../../lib/dates'
import { num } from '../../lib/money'
import { useT, type TranslationKey } from '../../lib/i18n'
import { BlockError, Card, Skeleton } from '../stats/statsUi'
import { CustomerPanel, CustomerPicker } from './CustomerPanel'
import { BIRTHDAY_TONE, Pill, reportError, StatusBadge, useBonusMeta, useBonusPermissions, useParamSetter } from './bonusUi'

const VARIABLES = ['{{Имя}}', '{{ФИО}}', '{{Бонусы}}', '{{ДатаСгорания}}', '{{ДатаРождения}}', '{{НазваниеМагазина}}']

/**
 * «Бонусы на день рождения» (ТЗ «Бонусы» §5): сома, қашан (күні / N күн бұрын / кейін), уақыт, мерзім, мәртебелер, сатып алу шарттары,
 * ерекшеліктер, хабарлама үлгісі, алдын ала қарау; жақын туған күндер күйімен. Жылына бір сыйлық — сервер қорғайды.
 */
export function BirthdayTab() {
  const t = useT()
  const qc = useQueryClient()
  const perms = useBonusPermissions()
  const meta = useBonusMeta()
  const { get, set } = useParamSetter()
  const [customer, setCustomer] = useState<string | null>(null)
  const [grant, setGrant] = useState<UpcomingBirthday | null>(null)
  const settings = useQuery({ queryKey: ['bonus', 'birthday', 'settings'], queryFn: bonusApi.birthdaySettings })
  const days = Number(get('days')) || 30
  const search = useDebounced(get('q'), 300)
  const page = Number(get('page')) || 1
  const statusId = get('status') || undefined
  const state = get('state') || undefined
  const list = useQuery({ queryKey: ['bonus', 'birthdays', days, search, statusId, state, page], queryFn: () => bonusApi.birthdays({ days, search, statusId, state, page, pageSize: 25 }), placeholderData: keepPreviousData })
  const exportFile = async () => {
    try { await bonusApi.exportFile('birthdays', 'xlsx', { days, search, statusId }); toast(t('stats.exportReady')) } catch (err) { reportError(err, t('stats.exportFailed')) }
  }

  return (
    <div className="grid grid-cols-1 gap-4 xl:grid-cols-[460px_1fr]">
      <Card title={t('bon.bd.settings')} icon={<Cake size={18} />}>
        {settings.isError ? <BlockError message={(settings.error as Error).message} onRetry={() => void settings.refetch()} />
          : !settings.data ? <Skeleton className="h-96" /> : <BirthdayForm key={settings.data.updatedAt} s={settings.data} canEdit={perms.manage} statuses={meta.data?.statuses ?? []}
            onSaved={() => { void settings.refetch(); void list.refetch(); void qc.invalidateQueries({ queryKey: bonusKeys.meta }) }} />}
      </Card>
      <div className="flex min-w-0 flex-col gap-3">
        <div className="flex flex-wrap items-center gap-2">
          <h3 className="mr-auto text-[16px] font-bold text-[#0b1f44]">{t('bon.upcomingBirthdays')}</h3>
          <SearchInput value={get('q')} onChange={(v) => set({ q: v })} placeholder={t('bon.cust.searchPlaceholder')} className="h-10! w-52" />
          <Select className="h-10! w-auto! text-[13px]" value={days} onChange={(e) => set({ days: e.target.value })}>
            {[7, 30, 90].map((d) => <option key={d} value={d}>{t('bon.exp.withinDays', { n: d })}</option>)}
          </Select>
          <Select className="h-10! w-auto! min-w-36 text-[13px]" value={statusId ?? ''} onChange={(e) => set({ status: e.target.value })}>
            <option value="">{t('bon.allStatuses')}</option>
            {(meta.data?.statuses ?? []).map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
          </Select>
          <Select className="h-10! w-auto! min-w-36 text-[13px]" value={state ?? ''} onChange={(e) => set({ state: e.target.value })}>
            <option value="">{t('bon.bd.allStates')}</option>
            {['pending', 'granted', 'status', 'purchases', 'excluded', 'disabled'].map((s) => <option key={s} value={s}>{t(`bon.bday.${s}` as TranslationKey)}</option>)}
          </Select>
          {perms.export && <Button variant="secondary" className="h-10! px-3!" icon={<Download size={16} />} onClick={() => void exportFile()}>Excel</Button>}
        </div>
        {list.data && list.data.withoutBirthDate > 0 && <p className="text-[12px] text-ink-3">{t('bon.bd.noDate', { n: list.data.withoutBirthDate })}</p>}
        <div className="overflow-x-auto rounded-2xl border border-line bg-surface">
          <table className="w-full min-w-[820px]">
            <thead className="bg-field/60"><tr>
              <th className={th}>{t('bon.bd.date')}</th><th className={th}>{t('bon.customer')}</th><th className={th}>{t('bon.phone')}</th><th className={th}>{t('bon.st.name')}</th>
              <th className={`${th} text-right`}>{t('bon.bd.gift')}</th><th className={th}>{t('bon.bd.planned')}</th><th className={th}>{t('bon.exp.state')}</th><th className={`${th} w-12`}></th>
            </tr></thead>
            <tbody>
              {!list.data ? null : list.data.items.length === 0 ? <EmptyRow colSpan={8} text={t('bon.noBirthdays')} /> : list.data.items.map((b) => (
                <tr key={b.customerId} className="border-t border-line hover:bg-field/40">
                  <td className={`${td} whitespace-nowrap`}>{dateOnly(b.birthday)}<span className="block text-[12px] text-ink-3">{b.daysLeft === 0 ? t('bon.bd.today') : t('bon.bd.inDays', { n: b.daysLeft })}</span></td>
                  <td className={td}><button type="button" onClick={() => setCustomer(b.customerId)} className="font-medium hover:text-brand">{b.customerName}</button></td>
                  <td className={`${td} text-ink-2`}>{b.phone ?? '—'}</td>
                  <td className={td}><StatusBadge name={b.statusName} color={b.statusColor} /></td>
                  <td className={`${td} text-right tabular-nums`}>{b.amount ? num(b.amount) : '—'}</td>
                  <td className={`${td} text-ink-2`}>{dateOnly(b.plannedDate)}</td>
                  <td className={td}><Pill tone={BIRTHDAY_TONE[b.state] ?? 'gray'}>{t(`bon.bday.${b.state}` as TranslationKey)}</Pill></td>
                  <td className={td}><RowMenu items={[
                    { label: t('bon.openCustomer'), icon: <Eye size={16} />, onClick: () => setCustomer(b.customerId) },
                    ...(perms.accrue && b.state !== 'granted' ? [{ label: t('bon.bd.grantManual'), icon: <Gift size={16} />, onClick: () => setGrant(b) }] : []),
                  ]} /></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {list.data && <Pager page={page} pageSize={25} total={list.data.total} onPage={(p) => set({ page: String(p) }, true)} onPageSize={() => undefined} />}
      </div>
      {customer && <CustomerPanel customerId={customer} onClose={() => setCustomer(null)} />}
      {grant && <GrantModal b={grant} amount={settings.data?.amount ?? 0} onClose={() => setGrant(null)} onDone={() => { setGrant(null); void list.refetch() }} />}
    </div>
  )
}

function BirthdayForm({ s, canEdit, statuses, onSaved }: { s: BirthdaySettings; canEdit: boolean; statuses: { id: string; name: string }[]; onSaved: () => void }) {
  const t = useT()
  const [f, setF] = useState({ ...s, excluded: s.excluded as CustomerRef[] })
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [preview, setPreview] = useState<string | null>(null)
  const [picker, setPicker] = useState<BonusCustomer | null>(null)
  const messageRef = useRef<HTMLTextAreaElement>(null)
  const patch = (p: Partial<typeof f>) => setF((x) => ({ ...x, ...p }))
  const incompatible = f.validity === 'UntilBirthday' && f.timing !== 'DaysBefore'
  const all = f.statusIds.length === 0
  const toggleStatus = (id: string, on: boolean) => {
    const base = all ? statuses.map((x) => x.id) : f.statusIds
    const next = on ? [...new Set([...base, id])] : base.filter((x) => x !== id)
    patch({ statusIds: next.length === statuses.length ? [] : next })
  }
  const insert = (v: string) => {
    const el = messageRef.current
    const start = el?.selectionStart ?? f.message.length
    const end = el?.selectionEnd ?? f.message.length
    patch({ message: f.message.slice(0, start) + v + f.message.slice(end) })
    requestAnimationFrame(() => { el?.focus(); el?.setSelectionRange(start + v.length, start + v.length) })
  }
  const save = async () => {
    setBusy(true)
    setError(null)
    try {
      const { excluded, updatedAt: _u, ...rest } = f
      await bonusApi.saveBirthday({ ...rest, excludedCustomerIds: excluded.map((x) => x.id) })
      toast(t('bon.saved'))
      onSaved()
    } catch (err) { setError(err instanceof Error ? err.message : t('bon.failed')); reportError(err, t('bon.failed')) } finally { setBusy(false) }
  }
  const showPreview = async () => {
    try { const p = await bonusApi.birthdayPreview(); setPreview(`${p.customer}: ${p.message}\n${t('bon.bd.previewLine', { date: dateOnly(p.plannedDate), until: p.expiresAt ? dateTime(p.expiresAt) : t('bon.noExpiry') })}`) }
    catch (err) { reportError(err, t('bon.failed')) }
  }
  return (
    <fieldset disabled={!canEdit} className="flex flex-col gap-4">
      <Toggle checked={f.enabled} onChange={(v) => patch({ enabled: v })} label={t('bon.bd.enabled')} />
      <div className="grid grid-cols-2 gap-3">
        <Field label={t('bon.bd.amount')} required><Input inputMode="numeric" value={f.amount || ''} onChange={(e) => patch({ amount: Number(e.target.value.replace(/\D/g, '')) || 0 })} placeholder="5000" /></Field>
        <Field label={t('bon.bd.time')}><TimeInput value={f.time} onChange={(time) => patch({ time })} /></Field>
      </div>
      <Field label={t('bon.bd.when')}>
        <div className="flex flex-col gap-2 sm:flex-row">
          <Select value={f.timing} onChange={(e) => patch({ timing: e.target.value as BirthdayTiming, timingDays: e.target.value === 'OnDay' ? 0 : f.timingDays || 3 })} className="flex-1">
            {(['OnDay', 'DaysBefore', 'DaysAfter'] as BirthdayTiming[]).map((x) => <option key={x} value={x}>{t(`bon.bd.timing.${x}` as TranslationKey)}</option>)}
          </Select>
          {f.timing !== 'OnDay' && <Input className="w-20" inputMode="numeric" value={f.timingDays} onChange={(e) => patch({ timingDays: Number(e.target.value.replace(/\D/g, '')) || 1 })} />}
        </div>
      </Field>
      <Field label={t('bon.bd.validity')} error={incompatible ? t('bon.bd.incompatible') : null}>
        <div className="flex flex-col gap-2 sm:flex-row">
          <Select value={f.validity} onChange={(e) => patch({ validity: e.target.value as BirthdayValidity })} className="flex-1" error={incompatible}>
            {(['DaysAfterAccrual', 'UntilBirthday', 'DaysAfterBirthday', 'FixedDate', 'Unlimited'] as BirthdayValidity[]).map((x) => <option key={x} value={x}>{t(`bon.bd.val.${x}` as TranslationKey)}</option>)}
          </Select>
          {(f.validity === 'DaysAfterAccrual' || f.validity === 'DaysAfterBirthday') && <Input className="w-20" inputMode="numeric" value={f.validityDays ?? 3} onChange={(e) => patch({ validityDays: Number(e.target.value.replace(/\D/g, '')) || 1 })} />}
          {f.validity === 'FixedDate' && <Input type="date" className="w-40" value={f.validityDate ?? ''} onChange={(e) => patch({ validityDate: e.target.value || null })} />}
        </div>
      </Field>
      <Field label={t('bon.exp.statuses')}>
        <div className="grid grid-cols-2 gap-1.5">
          {statuses.map((x) => (
            <label key={x.id} className="flex items-center gap-2 text-[14px]"><input type="checkbox" className="accent-brand" checked={all || f.statusIds.includes(x.id)} onChange={(e) => toggleStatus(x.id, e.target.checked)} />{x.name}</label>
          ))}
        </div>
      </Field>
      <div className="grid grid-cols-2 gap-3">
        <Field label={t('bon.bd.minAmount')}><Input inputMode="decimal" value={f.minPurchaseAmount ?? ''} onChange={(e) => patch({ minPurchaseAmount: e.target.value ? Number(e.target.value) : null })} placeholder="—" /></Field>
        <Field label={t('bon.bd.minCount')}><Input inputMode="numeric" value={f.minPurchaseCount ?? ''} onChange={(e) => patch({ minPurchaseCount: e.target.value ? Number(e.target.value.replace(/\D/g, '')) : null })} placeholder="—" /></Field>
      </div>
      <p className="-mt-2 text-[12px] text-ink-3">{t('bon.bd.periodHint', { n: f.purchasePeriodDays })} {t('bon.bd.oncePerYear')}</p>
      <Field label={t('bon.bd.exclusions')}>
        <div className="flex flex-col gap-2">
          {f.excluded.map((x) => (
            <span key={x.id} className="flex items-center gap-2 rounded-lg bg-field px-2 py-1 text-[13px]">{x.name}<span className="text-ink-3">{x.phone}</span>
              <button type="button" className="ml-auto text-ink-3 hover:text-danger" onClick={() => patch({ excluded: f.excluded.filter((y) => y.id !== x.id) })} aria-label="×"><X size={14} /></button></span>
          ))}
          <div className="flex gap-2">
            <div className="flex-1"><CustomerPicker value={picker} onChange={setPicker} /></div>
            <Button variant="secondary" className="h-11! px-3!" icon={<Plus size={16} />} disabled={!picker} onClick={() => { if (picker && !f.excluded.some((x) => x.id === picker.customerId)) patch({ excluded: [...f.excluded, { id: picker.customerId, name: picker.name, phone: picker.phone }] }); setPicker(null) }} />
          </div>
        </div>
      </Field>
      <Field label={t('bon.bd.message')}>
        <Textarea ref={messageRef} value={f.message} maxLength={1000} onChange={(e) => patch({ message: e.target.value })} />
        <div className="mt-1 flex flex-wrap gap-1">{VARIABLES.map((v) => <button key={v} type="button" onClick={() => insert(v)} className="rounded-md bg-field px-1.5 py-0.5 text-[11px] font-semibold text-ink-2 hover:bg-brand-soft hover:text-brand">{v}</button>)}</div>
      </Field>
      {preview && <p className="whitespace-pre-line rounded-xl bg-brand-soft px-3 py-2 text-[13px] text-brand">{preview}</p>}
      {error && <p className="text-[13px] text-danger">{error}</p>}
      <div className="flex gap-2">
        <Button variant="secondary" onClick={() => void showPreview()}>{t('bon.bd.preview')}</Button>
        {canEdit && <Button loading={busy} disabled={incompatible} onClick={() => void save()} className="flex-1">{t('common.save')}</Button>}
      </div>
    </fieldset>
  )
}

/** «Начислить вручную» (§5.3): бөлек құқық, себеп пен растау; жылына бір сыйлық. */
function GrantModal({ b, amount, onClose, onDone }: { b: UpcomingBirthday; amount: number; onClose: () => void; onDone: () => void }) {
  const t = useT()
  const [reason, setReason] = useState('')
  const [busy, setBusy] = useState(false)
  const submit = async () => {
    setBusy(true)
    try { await bonusApi.grantBirthday(b.customerId, reason); toast(t('bon.accrued')); onDone() } catch (err) { reportError(err, t('bon.failed')) } finally { setBusy(false) }
  }
  return (
    <Modal title={t('bon.bd.grantManual')} onClose={onClose} width={460}>
      <div className="flex flex-col gap-4">
        <p className="text-[14px]">{t('bon.bd.grantConfirm', { amount: num(amount), name: b.customerName })}</p>
        <Field label={t('bon.reason')} required><Textarea autoFocus value={reason} maxLength={500} onChange={(e) => setReason(e.target.value)} /></Field>
        <div className="flex justify-end gap-2"><Button variant="secondary" onClick={onClose}>{t('common.cancel')}</Button><Button loading={busy} disabled={reason.trim().length < 3 || amount <= 0} onClick={() => void submit()}>{t('bon.accrue')}</Button></div>
      </div>
    </Modal>
  )
}
