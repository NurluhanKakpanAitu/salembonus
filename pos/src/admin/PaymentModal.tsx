import { useEffect, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Button } from '../components/ui/Button'
import { Field, Input, Select, Textarea, Toggle } from '../components/ui/Field'
import { Modal } from '../components/ui/Modal'
import { toast } from '../components/ui/Toast'
import { tenge } from '../lib/money'
import { AdminApiError, adminApi } from './adminApi'
import { METHODS, date, errText, isoToday } from './ui'

/**
 * «Принять оплату»: бизнес, неше ай (кезең төленген күннен кейін автоматты жалғасады) не өз кезеңі, сома (тариф × ай), күні, тәсілі,
 * құжат нөмірі. Сақталғаннан кейін бизнестің «оплачено до» күні қайта есептеледі.
 */
export function PaymentModal({ organizationId, onClose, onDone }: { organizationId?: string; onClose: () => void; onDone: () => void }) {
  const qc = useQueryClient()
  const [orgId, setOrgId] = useState(organizationId ?? '')
  const [months, setMonths] = useState(1)
  const [custom, setCustom] = useState(false)
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [amount, setAmount] = useState('')
  const [amountTouched, setAmountTouched] = useState(false)
  const [paidOn, setPaidOn] = useState(isoToday())
  const [method, setMethod] = useState('kaspi')
  const [reference, setReference] = useState('')
  const [comment, setComment] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const businesses = useQuery({ queryKey: ['admin', 'businesses', 'all-for-payment'], queryFn: () => adminApi.businesses({ pageSize: 200, sort: 'name' }), enabled: !organizationId })
  const suggest = useQuery({ queryKey: ['admin', 'suggest', orgId, months], queryFn: () => adminApi.suggest(orgId, months), enabled: !!orgId })
  useEffect(() => {
    if (!suggest.data) return
    if (!amountTouched) setAmount(String(suggest.data.amount))
    if (!custom) { setFrom(suggest.data.periodFrom); setTo(suggest.data.periodTo) }
  }, [suggest.data]) // eslint-disable-line react-hooks/exhaustive-deps

  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    const sum = Number(amount.replace(',', '.').replace(/\s/g, ''))
    if (!orgId) { setError('Выберите бизнес'); return }
    if (!(sum > 0)) { setError('Укажите сумму'); return }
    setBusy(true)
    setError(null)
    try {
      await adminApi.createPayment({
        organizationId: orgId, amount: sum, paidOn, method, reference: reference.trim() || null, comment: comment.trim() || null,
        ...(custom ? { periodFrom: from, periodTo: to } : { months }),
      })
      toast(`Оплата ${tenge(sum)} принята`)
      void qc.invalidateQueries({ queryKey: ['admin'] })
      onDone()
    } catch (err) { setError(err instanceof AdminApiError ? err.message : errText(err)) } finally { setBusy(false) }
  }

  return (
    <Modal title="Принять оплату" onClose={onClose} width={560}>
      <form onSubmit={submit} className="flex flex-col gap-4">
        {!organizationId && (
          <Field label="Бизнес" required>
            <Select value={orgId} onChange={(e) => { setOrgId(e.target.value); setAmountTouched(false) }}>
              <option value="">— выберите —</option>
              {(businesses.data?.rows ?? []).map((b) => <option key={b.id} value={b.id}>{b.name}{b.city ? ` · ${b.city}` : ''}</option>)}
            </Select>
          </Field>
        )}
        <div className="flex flex-wrap items-center gap-2">
          {[1, 3, 6, 12].map((m) => (
            <button key={m} type="button" disabled={custom} onClick={() => { setMonths(m); setAmountTouched(false) }}
              className={`rounded-xl border px-3 py-2 text-[13px] font-semibold disabled:opacity-40 ${months === m && !custom ? 'border-brand bg-brand-soft text-brand' : 'border-line text-ink-2'}`}>
              {m} мес.
            </button>
          ))}
          <span className="ml-auto"><Toggle checked={custom} onChange={setCustom} label="Свой период" /></span>
        </div>
        {custom ? (
          <div className="grid grid-cols-2 gap-3">
            <Field label="Период с"><Input type="date" value={from} onChange={(e) => setFrom(e.target.value)} /></Field>
            <Field label="по"><Input type="date" value={to} onChange={(e) => setTo(e.target.value)} /></Field>
          </div>
        ) : (
          <p className="rounded-xl bg-field px-3 py-2 text-[13px] text-ink-2">
            {suggest.data ? <>Период: <b>{date(suggest.data.periodFrom)} — {date(suggest.data.periodTo)}</b> · тариф {tenge(suggest.data.monthlyFee)}/мес</> : orgId ? 'Расчёт периода…' : 'Выберите бизнес'}
          </p>
        )}
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <Field label="Сумма, ₸" required><Input inputMode="decimal" value={amount} onChange={(e) => { setAmount(e.target.value); setAmountTouched(true) }} /></Field>
          <Field label="Дата оплаты"><Input type="date" value={paidOn} onChange={(e) => setPaidOn(e.target.value)} /></Field>
          <Field label="Способ"><Select value={method} onChange={(e) => setMethod(e.target.value)}>{Object.entries(METHODS).map(([k, v]) => <option key={k} value={k}>{v}</option>)}</Select></Field>
          <Field label="Номер документа / чека Kaspi"><Input value={reference} maxLength={120} onChange={(e) => setReference(e.target.value)} /></Field>
        </div>
        <Field label="Комментарий"><Textarea value={comment} maxLength={500} onChange={(e) => setComment(e.target.value)} className="min-h-16" /></Field>
        {error && <p className="text-[13px] text-danger">{error}</p>}
        <div className="flex justify-end gap-2">
          <Button type="button" variant="secondary" onClick={onClose}>Отмена</Button>
          <Button type="submit" loading={busy}>Принять оплату</Button>
        </div>
      </form>
    </Modal>
  )
}
