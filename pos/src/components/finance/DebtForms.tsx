import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Search, UserPlus } from 'lucide-react'
import { Button } from '../ui/Button'
import { Field, Input, Select, Textarea } from '../ui/Field'
import { Modal } from '../ui/Modal'
import { toast } from '../ui/Toast'
import { ApiError } from '../../lib/api'
import { clientsApi } from '../../lib/clientsApi'
import { isoDate } from '../../lib/dates'
import { financeApi } from '../../lib/financeApi'
import { inventoryApi, inventoryKeys } from '../../lib/inventoryApi'
import { tenge } from '../../lib/money'
import { useT, type TranslationKey } from '../../lib/i18n'

/**
 * Дәптердегі қарыздар (қолмен енгізу) және «Закрыть долг». Ақшаға, выручкаға, кассаға тимейді — тек қарыз тіркеледі не жабылады,
 * әр әрекет тарихта (кім, қашан, себебі) қалады.
 */

const toNumber = (raw: string) => Number(raw.replace(/\s/g, '').replace(',', '.')) || 0
const addDays = (days: number) => { const d = new Date(); d.setDate(d.getDate() + days); return isoDate(d) }

/** Іздеу жолы телефон ба (тек цифр, +, бос орын, жақша, сызықша) — жаңа клиенттің телефонына, әйтпесе атына қойылады. */
const PHONE_LIKE = /^[+\d\s()-]{4,}$/

type PickedClient = { customerId: string; name: string; phone: string | null }

/** Клиентті іздеу (аты, телефон); табылмаса — осы жерде жаңасын қосу (аты, тегі, телефон). */
function ClientDebtPicker({ value, onChange }: { value: PickedClient | null; onChange: (c: PickedClient | null) => void }) {
  const t = useT()
  const [search, setSearch] = useState('')
  const [creating, setCreating] = useState(false)
  const [first, setFirst] = useState('')
  const [last, setLast] = useState('')
  const [phone, setPhone] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const q = useQuery({
    queryKey: ['finance', 'debt-client-picker', search],
    queryFn: () => clientsApi.search({ filter: { search: search.trim() }, pageSize: 8 }),
    enabled: search.trim().length >= 2 && !value && !creating,
  })
  const create = async () => {
    setBusy(true); setError(null)
    try {
      const card = await clientsApi.create({ firstName: first.trim(), lastName: last.trim(), phone: phone.trim() || null })
      onChange({ customerId: card.client.customerId, name: card.client.name, phone: card.client.phone })
      setCreating(false)
    } catch (err) { setError(err instanceof ApiError ? err.message : t('fin.failed')) } finally { setBusy(false) }
  }
  if (value) return (
    <div className="flex items-center gap-3 rounded-xl border border-brand bg-brand-soft/40 px-3 py-2">
      <span className="min-w-0 flex-1"><span className="block font-semibold">{value.name}</span>{value.phone && <span className="block text-[12px] text-ink-2">{value.phone}</span>}</span>
      <Button type="button" variant="ghost" className="h-8! px-2!" onClick={() => onChange(null)}>{t('fin.debt.change')}</Button>
    </div>
  )
  if (creating) return (
    <div className="flex flex-col gap-3 rounded-xl border border-line p-3">
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <Field label={t('fin.debt.firstName')} required><Input autoFocus aria-label={t('fin.debt.firstName')} value={first} maxLength={100} onChange={(e) => setFirst(e.target.value)} /></Field>
        <Field label={t('fin.debt.lastName')}><Input aria-label={t('fin.debt.lastName')} value={last} maxLength={100} onChange={(e) => setLast(e.target.value)} /></Field>
      </div>
      <Field label={t('fin.phone')} required><Input type="tel" inputMode="tel" aria-label={t('fin.phone')} value={phone} placeholder="+7 7__ ___ __ __" onChange={(e) => setPhone(e.target.value)} /></Field>
      {error && <p className="text-[13px] text-danger">{error}</p>}
      <div className="flex justify-end gap-2">
        <Button type="button" variant="secondary" onClick={() => setCreating(false)}>{t('common.cancel')}</Button>
        <Button type="button" loading={busy} disabled={!first.trim() || !phone.trim()} onClick={() => void create()}>{t('fin.debt.addClient')}</Button>
      </div>
    </div>
  )
  const rows = q.data?.rows ?? []
  return (
    <div className="relative">
      <label className="flex h-11 items-center gap-2.5 rounded-xl border border-line bg-surface px-3.5 focus-within:border-brand">
        <Search size={18} className="text-ink-3" />
        <input autoFocus value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t('fin.debt.clientSearch')} aria-label={t('fin.debt.clientSearch')}
          className="h-full min-w-0 flex-1 bg-transparent outline-none" />
      </label>
      {search.trim().length >= 2 && !q.isFetching && (
        <ul className="absolute inset-x-0 top-full z-10 mt-1 max-h-64 overflow-y-auto rounded-xl border border-line bg-surface p-1 shadow-lg">
          {rows.map((c) => (
            <li key={c.customerId}>
              <button type="button" onClick={() => onChange({ customerId: c.customerId, name: c.name, phone: c.phone })}
                className="flex w-full items-center gap-2 rounded-lg px-3 py-2 text-left text-[14px] hover:bg-field">
                <span className="min-w-0 flex-1 truncate">{c.name}<span className="ml-2 text-[12px] text-ink-3">{c.phone}</span></span>
                {c.debt > 0 && <span className="text-[12px] text-danger">{tenge(c.debt)}</span>}
              </button>
            </li>
          ))}
          <li>
            <button type="button" onClick={() => { const isPhone = PHONE_LIKE.test(search.trim()); setCreating(true); setFirst(isPhone ? '' : search.trim()); setPhone(isPhone ? search.trim() : '') }}
              className="flex w-full items-center gap-2 rounded-lg px-3 py-2 text-left text-[14px] font-semibold text-brand hover:bg-field">
              <UserPlus size={16} />{rows.length === 0 ? t('fin.debt.notFoundAdd') : t('fin.debt.addClient')}
            </button>
          </li>
        </ul>
      )}
    </div>
  )
}

/** Сома, қашан пайда болды, қашанға дейін, түсініктеме — екі қарыз түріне ортақ өрістер. */
function DebtFields({ amount, setAmount, date, setDate, due, setDue, dueRequired }: {
  amount: string; setAmount: (v: string) => void; date: string; setDate: (v: string) => void; due: string; setDue: (v: string) => void; dueRequired?: boolean
}) {
  const t = useT()
  return (
    <>
      <Field label={t('fin.debt.amount')} required><Input inputMode="decimal" aria-label={t('fin.debt.amount')} value={amount} placeholder="0" onChange={(e) => setAmount(e.target.value)} /></Field>
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <Field label={t('fin.debt.date')} hint={t('fin.debt.dateHint')}><Input type="date" aria-label={t('fin.debt.date')} value={date} max={isoDate(new Date())} onChange={(e) => setDate(e.target.value)} /></Field>
        <Field label={t('fin.dueDate')} required={dueRequired}><Input type="date" aria-label={t('fin.dueDate')} value={due} min={date || undefined} onChange={(e) => setDue(e.target.value)} /></Field>
      </div>
    </>
  )
}

/** «Добавить долг» клиента: дәптерден — сатылымсыз қарыз. */
export function AddCustomerDebtModal({ onClose, onDone }: { onClose: () => void; onDone: (customerId: string) => void }) {
  const t = useT()
  const [client, setClient] = useState<PickedClient | null>(null)
  const [amount, setAmount] = useState('')
  const [date, setDate] = useState(isoDate(new Date()))
  const [due, setDue] = useState(addDays(30))
  const [comment, setComment] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const sum = toNumber(amount)
  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!client || sum <= 0) return
    setBusy(true); setError(null)
    try {
      await financeApi.addCustomerDebt({ customerId: client.customerId, amount: sum, date: date || null, dueDate: due || null, comment: comment.trim() || null })
      toast(t('fin.debt.added', { amount: tenge(sum) }))
      onDone(client.customerId)
    } catch (err) { setError(err instanceof ApiError ? err.message : t('fin.failed')) } finally { setBusy(false) }
  }
  return (
    <Modal title={t('fin.debt.addCustomer')} onClose={onClose} width={560}>
      <form onSubmit={submit} className="flex flex-col gap-4">
        <p className="text-[13px] text-ink-2">{t('fin.debt.addCustomerHint')}</p>
        <Field label={t('fin.cd.customer')} required><ClientDebtPicker value={client} onChange={setClient} /></Field>
        <DebtFields amount={amount} setAmount={setAmount} date={date} setDate={setDate} due={due} setDue={setDue} dueRequired />
        <Field label={t('fin.comment')}><Textarea aria-label={t('fin.comment')} value={comment} maxLength={500} placeholder={t('fin.debt.commentPlaceholder')} onChange={(e) => setComment(e.target.value)} /></Field>
        {error && <p className="text-[13px] text-danger">{error}</p>}
        <div className="flex justify-end gap-2">
          <Button type="button" variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" loading={busy} disabled={!client || sum <= 0}>{t('fin.debt.add')}</Button>
        </div>
      </form>
    </Modal>
  )
}

/** «Добавить долг» поставщику: дәптерден — тауарсыз «Начальный долг» құжаты (қалдыққа, закупкаға тимейді). */
export function AddSupplierDebtModal({ supplierId: initial, onClose, onDone }: { supplierId?: string; onClose: () => void; onDone: (supplierId: string) => void }) {
  const t = useT()
  const suppliers = useQuery({ queryKey: inventoryKeys.suppliers, queryFn: inventoryApi.suppliers })
  const [supplierId, setSupplierId] = useState(initial ?? '')
  const [amount, setAmount] = useState('')
  const [date, setDate] = useState(isoDate(new Date()))
  const [due, setDue] = useState('')
  const [documentNumber, setDocumentNumber] = useState('')
  const [comment, setComment] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const sum = toNumber(amount)
  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!supplierId || sum <= 0) return
    setBusy(true); setError(null)
    try {
      await financeApi.addSupplierDebt({ supplierId, amount: sum, date: date || null, dueDate: due || null, documentNumber: documentNumber.trim() || null, comment: comment.trim() || null })
      toast(t('fin.debt.added', { amount: tenge(sum) }))
      onDone(supplierId)
    } catch (err) { setError(err instanceof ApiError ? err.message : t('fin.failed')) } finally { setBusy(false) }
  }
  return (
    <Modal title={t('fin.debt.addSupplier')} onClose={onClose} width={560}>
      <form onSubmit={submit} className="flex flex-col gap-4">
        <p className="text-[13px] text-ink-2">{t('fin.debt.addSupplierHint')}</p>
        <Field label={t('fin.sd.supplier')} required>
          <Select autoFocus aria-label={t('fin.sd.supplier')} value={supplierId} onChange={(e) => setSupplierId(e.target.value)}>
            <option value="">{t('fin.debt.chooseSupplier')}</option>
            {(suppliers.data ?? []).filter((s) => s.status === 'Active' || s.id === supplierId).map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
          </Select>
        </Field>
        <DebtFields amount={amount} setAmount={setAmount} date={date} setDate={setDate} due={due} setDue={setDue} />
        <Field label={t('fin.debt.documentNumber')}><Input aria-label={t('fin.debt.documentNumber')} value={documentNumber} maxLength={64} onChange={(e) => setDocumentNumber(e.target.value)} /></Field>
        <Field label={t('fin.comment')}><Textarea aria-label={t('fin.comment')} value={comment} maxLength={1000} placeholder={t('fin.debt.commentPlaceholder')} onChange={(e) => setComment(e.target.value)} /></Field>
        {error && <p className="text-[13px] text-danger">{error}</p>}
        <div className="flex justify-end gap-2">
          <Button type="button" variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" loading={busy} disabled={!supplierId || sum <= 0}>{t('fin.debt.add')}</Button>
        </div>
      </form>
    </Modal>
  )
}

const CLOSE_REASONS: TranslationKey[] = ['fin.debt.reason.paidByNotebook', 'fin.debt.reason.settledByGoods', 'fin.debt.reason.forgiven']

/** «Закрыть долг»: қалдықты ақшасыз жабу. Себеп міндетті (жылдам нұсқалар бар), тарихта көрінеді; шот пен касса өзгермейді. */
export function CloseDebtModal({ label, remaining, onClose, onSubmit }: { label: string; remaining: number; onClose: () => void; onSubmit: (reason: string) => Promise<void> }) {
  const t = useT()
  const [reason, setReason] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (reason.trim().length < 3) return
    setBusy(true); setError(null)
    try { await onSubmit(reason.trim()) } catch (err) { setError(err instanceof ApiError ? err.message : t('fin.failed')) } finally { setBusy(false) }
  }
  return (
    <Modal title={`${t('fin.debt.close')} · ${label}`} onClose={onClose} width={500}>
      <form onSubmit={submit} className="flex flex-col gap-4">
        <p className="text-[14px]">{t('fin.debt.closeSum')}: <b>{tenge(remaining)}</b></p>
        <p className="rounded-xl bg-amber-50 px-3 py-2 text-[13px] text-amber-800">{t('fin.debt.closeHint')}</p>
        <div className="flex flex-wrap gap-1.5">
          {CLOSE_REASONS.map((k) => (
            <button key={k} type="button" onClick={() => setReason(t(k))}
              className={`rounded-lg border px-2.5 py-1 text-[13px] ${reason === t(k) ? 'border-brand bg-brand-soft text-brand' : 'border-line hover:border-brand'}`}>{t(k)}</button>
          ))}
        </div>
        <Field label={t('inv.reason')} required><Textarea autoFocus aria-label={t('inv.reason')} value={reason} maxLength={500} onChange={(e) => setReason(e.target.value)} /></Field>
        {error && <p className="text-[13px] text-danger">{error}</p>}
        <div className="flex justify-end gap-2">
          <Button type="button" variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" variant="danger" loading={busy} disabled={reason.trim().length < 3}>{t('fin.debt.closeBtn')}</Button>
        </div>
      </form>
    </Modal>
  )
}
