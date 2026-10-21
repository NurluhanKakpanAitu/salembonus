import { useRef, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Ban, BellOff, Loader2, MinusCircle, PlusCircle, Search, ShieldCheck } from 'lucide-react'
import { Button } from '../ui/Button'
import { Field, Input, Select, Textarea, Toggle } from '../ui/Field'
import { useFitPlaceholder } from '../ui/SearchInput'
import { Drawer } from '../ui/Drawer'
import { Modal } from '../ui/Modal'
import { toast } from '../ui/Toast'
import { confirmDialog } from '../ui/Confirm'
import { ApiError } from '../../lib/api'
import { bonusApi, bonusKeys } from '../../lib/bonusApi'
import type { BonusCustomer, BonusCustomerCard, BonusStatus } from '../../lib/bonusTypes'
import { dateOnly, dateTime } from '../../lib/dates'
import { num, tenge } from '../../lib/money'
import { useT, type TranslationKey } from '../../lib/i18n'
import { Segmented } from '../stats/statsUi'
import { newKey, OpTypeBadge, Pill, Points, reportError, StatusBadge, useBonusMeta, useBonusPermissions } from './bonusUi'

/**
 * Клиенттің бонус карточкасы (ТЗ «Бонусы» §10): мәртебе, қолжетімді баланс, жақын жану, туған күн (құқықпен), соңғы операция;
 * «Бонусы» — партиялар (бастапқы, қалдық, күндері, көзі), «История» — операциялар; әрекеттер тек құқықпен.
 */
export function CustomerPanel({ customerId, onClose }: { customerId: string; onClose: () => void }) {
  const t = useT()
  const qc = useQueryClient()
  const perms = useBonusPermissions()
  const meta = useBonusMeta()
  const [tab, setTab] = useState<'lots' | 'history'>('lots')
  const [action, setAction] = useState<'accrue' | 'writeoff' | 'status' | null>(null)
  const q = useQuery({ queryKey: ['bonus', 'customer', customerId], queryFn: () => bonusApi.customer(customerId) })
  const c = q.data
  const refresh = () => { void q.refetch(); void qc.invalidateQueries({ queryKey: bonusKeys.all }) }
  const status = meta.data?.statuses.find((s) => s.id === c?.customer.statusId)

  const toggleOptOut = async (value: boolean) => {
    try { await bonusApi.optOut(customerId, value); toast(value ? t('bon.cust.optedOut') : t('bon.cust.optedIn')); refresh() } catch (err) { reportError(err, t('bon.failed')) }
  }

  return (
    <Drawer title={c?.customer.name ?? t('bon.cust.title')} subtitle={c ? [c.customer.phone, c.customer.cardCode].filter(Boolean).join(' · ') : undefined} onClose={onClose} width={520}
      footer={c && (
        <>
          {perms.manage && <Button variant="secondary" icon={<ShieldCheck size={16} />} onClick={() => setAction('status')}>{t('bon.cust.changeStatus')}</Button>}
          {perms.writeOff && <Button variant="secondary" icon={<MinusCircle size={16} />} onClick={() => setAction('writeoff')} disabled={c.customer.balance <= 0}>{t('bon.cust.writeOff')}</Button>}
          {perms.accrue && <Button icon={<PlusCircle size={16} />} onClick={() => setAction('accrue')}>{t('bon.cust.accrue')}</Button>}
        </>
      )}>
      {!c ? <div className="py-16 text-center text-ink-3"><Loader2 className="mx-auto animate-spin" size={24} /></div> : (
        <div className="flex flex-col gap-4">
          <div className="flex flex-wrap items-center gap-2">
            <StatusBadge name={c.customer.statusName} color={c.customer.statusColor} icon={status?.icon} />
            {c.customer.statusManual && <Pill tone="violet">{t('bon.cust.manualStatus')}</Pill>}
            {c.customer.optOut && <Pill tone="gray"><BellOff size={12} className="mr-1 inline" />{t('bon.cust.optOut')}</Pill>}
            <span className="ml-auto text-[13px] text-ink-2">{t('bon.cust.percent', { p: c.percent })}</span>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <Stat label={t('bon.cust.balance')} value={<Points value={c.customer.balance} />} />
            <Stat label={t('bon.cust.nextExpiry')} value={c.nextExpiryAt ? `${num(c.nextExpiryAmount ?? 0)} · ${dateOnly(c.nextExpiryAt)}` : '—'} />
            <Stat label={t('bon.cust.purchases')} value={`${tenge(c.purchases.amount)} · ${c.purchases.count}`} />
            <Stat label={t('bon.cust.lastOperation')} value={c.customer.lastOperationAt ? dateTime(c.customer.lastOperationAt) : '—'} />
            {c.customer.birthDate && <Stat label={t('bon.cust.birthDate')} value={dateOnly(c.customer.birthDate)} />}
            <Stat label={t('bon.cust.registered')} value={dateOnly(c.customer.registeredAt)} />
          </div>
          {(c.statusReason || c.statusExpiresAt) && (
            <p className="rounded-xl bg-field px-3 py-2 text-[13px] text-ink-2">
              {c.statusReason && <>{t('bon.cust.statusReason')}: {c.statusReason}. </>}
              {c.statusExpiresAt && <>{t('bon.cust.statusUntil')}: {dateOnly(c.statusExpiresAt)}</>}
            </p>
          )}
          {perms.campaigns && <Toggle checked={c.customer.optOut} onChange={(v) => void toggleOptOut(v)} label={t('bon.cust.optOutToggle')} />}
          <Segmented value={tab} options={[{ key: 'lots', label: `${t('bon.cust.lots')} (${c.lots.length})` }, { key: 'history', label: t('bon.cust.history') }]} onChange={setTab} />
          {tab === 'lots' ? (
            c.lots.length === 0 ? <p className="py-6 text-center text-[13px] text-ink-3">{t('bon.cust.noLots')}</p> : (
              <ul className="flex flex-col divide-y divide-line rounded-xl border border-line">
                {c.lots.map((l) => (
                  <li key={l.id} className="flex items-center gap-3 px-3 py-2 text-[13px]">
                    <OpTypeBadge type={l.type} amount={l.original} />
                    <span className="min-w-0 flex-1">
                      <span className="block">{t('bon.cust.lotLine', { original: num(l.original), remaining: num(l.remaining) })}</span>
                      <span className="block text-[12px] text-ink-3">{dateOnly(l.accruedAt)} → {l.expiresAt ? dateOnly(l.expiresAt) : t('bon.noExpiry')}</span>
                    </span>
                    <Pill tone={l.state === 'active' ? 'green' : l.state === 'expired' ? 'red' : l.state === 'reversed' ? 'violet' : 'gray'}>{t(`bon.lot.${l.state}` as TranslationKey)}</Pill>
                  </li>
                ))}
              </ul>
            )
          ) : (
            <ul className="flex flex-col divide-y divide-line rounded-xl border border-line">
              {c.operations.length === 0 && <li className="py-6 text-center text-[13px] text-ink-3">{t('bon.empty')}</li>}
              {c.operations.map((o) => (
                <li key={o.id} className="flex items-center gap-3 px-3 py-2 text-[13px]">
                  <OpTypeBadge type={o.type} amount={o.amount} />
                  <span className="min-w-0 flex-1"><span className="block truncate">{o.basis}</span><span className="block text-[12px] text-ink-3">{dateTime(o.createdAt)} · {o.author}</span></span>
                  <Points value={o.amount} signed muted={o.status === 'reversed'} />
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
      {action === 'accrue' && c && <AccrueModal customer={c.customer} onClose={() => setAction(null)} onDone={() => { setAction(null); refresh() }} />}
      {action === 'writeoff' && c && <WriteOffModal customer={c.customer} onClose={() => setAction(null)} onDone={() => { setAction(null); refresh() }} />}
      {action === 'status' && c && <ChangeStatusModal card={c} statuses={meta.data?.statuses ?? []} onClose={() => setAction(null)} onDone={() => { setAction(null); refresh() }} />}
    </Drawer>
  )
}

function Stat({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="rounded-xl border border-line px-3 py-2">
      <div className="text-[12px] text-ink-2">{label}</div>
      <div className="mt-0.5 truncate text-[15px] font-bold">{value}</div>
    </div>
  )
}

/** Клиент іздеу (тарату, қолмен операциялар): аты-жөні, телефон, карта нөмірі. */
export function CustomerPicker({ value, onChange }: { value: BonusCustomer | null; onChange: (c: BonusCustomer | null) => void }) {
  const t = useT()
  const searchRef = useRef<HTMLInputElement>(null)
  const searchPlaceholder = useFitPlaceholder(t('bon.cust.searchPlaceholder'), searchRef, 80)
  const [search, setSearch] = useState('')
  const q = useQuery({ queryKey: ['bonus', 'picker', search], queryFn: () => bonusApi.customers({ search, pageSize: 8 }), enabled: search.trim().length >= 2 && !value })
  if (value) return (
    <div className="flex items-center gap-3 rounded-xl border border-brand bg-brand-soft/40 px-3 py-2">
      <span className="min-w-0 flex-1"><span className="block font-semibold">{value.name}</span><span className="block text-[12px] text-ink-2">{value.phone} · {t('bon.cust.balance')}: {num(value.balance)}</span></span>
      <Button variant="ghost" className="h-8! px-2!" onClick={() => onChange(null)}>{t('bon.change')}</Button>
    </div>
  )
  return (
    <div className="relative">
      <label className="flex h-11 items-center gap-2.5 rounded-xl border border-line bg-surface px-3.5 focus-within:border-brand">
        <Search size={18} className="text-ink-3" />
        <input ref={searchRef} autoFocus value={search} onChange={(e) => setSearch(e.target.value)} placeholder={searchPlaceholder} className="h-full min-w-0 flex-1 bg-transparent outline-none" />
      </label>
      {(q.data?.items.length ?? 0) > 0 && (
        <ul className="absolute inset-x-0 top-full z-10 mt-1 max-h-64 overflow-y-auto rounded-xl border border-line bg-surface p-1 shadow-lg">
          {q.data!.items.map((c) => (
            <li key={c.customerId}>
              <button type="button" onClick={() => onChange(c)} className="flex w-full items-center gap-2 rounded-lg px-3 py-2 text-left text-[14px] hover:bg-field">
                <span className="min-w-0 flex-1 truncate">{c.name}<span className="ml-2 text-[12px] text-ink-3">{c.phone}</span></span>
                <span className="text-[12px] text-ink-2">{num(c.balance)}</span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

/** «Начислить бонусы» (§8.3): клиент, сома, себеп (міндетті), партия мерзімі, коммент; алдымен итог көрсетіледі. */
export function AccrueModal({ customer: initial, onClose, onDone }: { customer?: BonusCustomer; onClose: () => void; onDone: () => void }) {
  const t = useT()
  const [customer, setCustomer] = useState<BonusCustomer | null>(initial ?? null)
  const [amount, setAmount] = useState('')
  const [reason, setReason] = useState('')
  const [validity, setValidity] = useState<'rule' | 'days' | 'unlimited'>('rule')
  const [days, setDays] = useState('30')
  const [comment, setComment] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const key = useRef(newKey())
  const sum = Math.floor(Number(amount) || 0)
  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!customer || sum <= 0 || reason.trim().length < 3) return
    if (!(await confirmDialog({ message: t('bon.accrueConfirm', { amount: num(sum), name: customer.name, after: num(customer.balance + sum) }), confirmLabel: t('bon.accrue') }))) return
    setBusy(true)
    try {
      await bonusApi.accrue({ customerId: customer.customerId, amount: sum, reason: reason.trim(), validityDays: validity === 'days' ? Number(days) || null : null, unlimited: validity === 'unlimited', comment, clientRequestId: key.current })
      toast(t('bon.accrued'))
      onDone()
    } catch (err) { setError(err instanceof ApiError ? err.message : t('bon.failed')) } finally { setBusy(false) }
  }
  return (
    <Modal title={t('bon.accrueTitle')} onClose={onClose} width={500}>
      <form onSubmit={submit} className="flex flex-col gap-4">
        <Field label={t('bon.customer')} required><CustomerPicker value={customer} onChange={setCustomer} /></Field>
        <Field label={t('bon.amount')} required><Input inputMode="numeric" value={amount} onChange={(e) => setAmount(e.target.value.replace(/\D/g, ''))} placeholder="0" /></Field>
        <Field label={t('bon.reason')} required><Input value={reason} maxLength={300} onChange={(e) => setReason(e.target.value)} placeholder={t('bon.reasonAccrueHint')} /></Field>
        <Field label={t('bon.lotValidity')} hint={validity === 'rule' ? t('bon.lotValidityRuleHint') : undefined}>
          <div className="flex gap-2">
            <Select value={validity} onChange={(e) => setValidity(e.target.value as typeof validity)} className="flex-1">
              <option value="rule">{t('bon.validity.rule')}</option>
              <option value="days">{t('bon.validity.days')}</option>
              <option value="unlimited">{t('bon.validity.unlimited')}</option>
            </Select>
            {validity === 'days' && <Input className="w-28" inputMode="numeric" value={days} onChange={(e) => setDays(e.target.value.replace(/\D/g, ''))} />}
          </div>
        </Field>
        <Field label={t('bon.comment')}><Textarea value={comment} maxLength={300} onChange={(e) => setComment(e.target.value)} className="min-h-16!" /></Field>
        {customer && sum > 0 && <p className="rounded-xl bg-success-soft px-3 py-2 text-[13px] text-success">{t('bon.accruePreview', { amount: num(sum), after: num(customer.balance + sum) })}</p>}
        {error && <p className="text-[13px] text-danger">{error}</p>}
        <div className="flex justify-end gap-2">
          <Button type="button" variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" loading={busy} disabled={!customer || sum <= 0 || reason.trim().length < 3}>{t('bon.accrue')}</Button>
        </div>
      </form>
    </Modal>
  )
}

/** «Списать вручную» (§8.3): бөлек құқық, қолжетімді балансты тексеру, себеп пен растау. */
export function WriteOffModal({ customer: initial, onClose, onDone }: { customer?: BonusCustomer; onClose: () => void; onDone: () => void }) {
  const t = useT()
  const [customer, setCustomer] = useState<BonusCustomer | null>(initial ?? null)
  const [amount, setAmount] = useState('')
  const [reason, setReason] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const key = useRef(newKey())
  const sum = Math.floor(Number(amount) || 0)
  const over = !!customer && sum > customer.balance
  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!customer || sum <= 0 || over || reason.trim().length < 3) return
    if (!(await confirmDialog({ message: t('bon.writeOffConfirm', { amount: num(sum), name: customer.name, after: num(customer.balance - sum) }), confirmLabel: t('bon.writeOff'), danger: true }))) return
    setBusy(true)
    try { await bonusApi.writeOff({ customerId: customer.customerId, amount: sum, reason: reason.trim(), clientRequestId: key.current }); toast(t('bon.writtenOff')); onDone() }
    catch (err) { setError(err instanceof ApiError ? err.message : t('bon.failed')) } finally { setBusy(false) }
  }
  return (
    <Modal title={t('bon.writeOffTitle')} onClose={onClose} width={500}>
      <form onSubmit={submit} className="flex flex-col gap-4">
        <Field label={t('bon.customer')} required><CustomerPicker value={customer} onChange={setCustomer} /></Field>
        <Field label={t('bon.amount')} required error={over ? t('bon.insufficient', { balance: num(customer!.balance) }) : null}>
          <Input inputMode="numeric" value={amount} onChange={(e) => setAmount(e.target.value.replace(/\D/g, ''))} error={over} placeholder="0" />
        </Field>
        <Field label={t('bon.reason')} required><Input value={reason} maxLength={300} onChange={(e) => setReason(e.target.value)} /></Field>
        {error && <p className="text-[13px] text-danger">{error}</p>}
        <div className="flex justify-end gap-2">
          <Button type="button" variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" variant="danger" loading={busy} disabled={!customer || sum <= 0 || over || reason.trim().length < 3}>{t('bon.writeOff')}</Button>
        </div>
      </form>
    </Modal>
  )
}

/** Мәртебені өзгерту (§3.4): «По правилам» не «Вручную» (себеп міндетті). Бонус есептелмейді. */
function ChangeStatusModal({ card, statuses, onClose, onDone }: { card: BonusCustomerCard; statuses: BonusStatus[]; onClose: () => void; onDone: () => void }) {
  const t = useT()
  const [mode, setMode] = useState<'manual' | 'rules'>('manual')
  const [statusId, setStatusId] = useState(card.customer.statusId ?? '')
  const [reason, setReason] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    setBusy(true)
    try { await bonusApi.changeStatus(card.customer.customerId, { mode, statusId: mode === 'manual' ? statusId : null, reason }); toast(t('bon.statusChanged')); onDone() }
    catch (err) { setError(err instanceof ApiError ? err.message : t('bon.failed')) } finally { setBusy(false) }
  }
  return (
    <Modal title={t('bon.cust.changeStatus')} onClose={onClose} width={480}>
      <form onSubmit={submit} className="flex flex-col gap-4">
        <p className="text-[13px] text-ink-2">{t('bon.cust.currentStatus')}: <b>{card.customer.statusName}</b>{card.customer.statusManual ? ` (${t('bon.cust.manualStatus')})` : ''}</p>
        <Segmented value={mode} options={[{ key: 'manual', label: t('bon.cust.modeManual') }, { key: 'rules', label: t('bon.cust.modeRules') }]} onChange={setMode} />
        {mode === 'manual' ? (
          <Field label={t('bon.cust.newStatus')} required>
            <Select value={statusId} onChange={(e) => setStatusId(e.target.value)}>
              {statuses.filter((s) => s.isActive).map((s) => <option key={s.id} value={s.id}>{s.name} · {s.accrualPercent}%</option>)}
            </Select>
          </Field>
        ) : <p className="rounded-xl bg-brand-soft px-3 py-2 text-[13px] text-brand">{t('bon.cust.rulesHint')}</p>}
        <Field label={t('bon.reason')} required={mode === 'manual'}><Textarea value={reason} maxLength={500} onChange={(e) => setReason(e.target.value)} className="min-h-16!" /></Field>
        <p className="text-[12px] text-ink-3"><Ban size={12} className="mr-1 inline" />{t('bon.cust.noRetroBonus')}</p>
        {error && <p className="text-[13px] text-danger">{error}</p>}
        <div className="flex justify-end gap-2">
          <Button type="button" variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" loading={busy} disabled={mode === 'manual' && (!statusId || reason.trim().length < 3)}>{t('common.save')}</Button>
        </div>
      </form>
    </Modal>
  )
}
