import { useMemo, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { AlertTriangle, ArrowLeft, ArrowRight, Check, Loader2, Undo2, X } from 'lucide-react'
import { Button } from '../ui/Button'
import { confirmDialog } from '../ui/Confirm'
import { Field, Select, Textarea } from '../ui/Field'
import { Modal } from '../ui/Modal'
import { toast } from '../ui/Toast'
import { ApiError } from '../../lib/api'
import { cashierApi } from '../../lib/cashierApi'
import type { PaymentMethod, Receipt } from '../../lib/cashierTypes'
import { dateTime } from '../../lib/dates'
import { num, round2, tenge } from '../../lib/money'
import { salesApi, salesKeys } from '../../lib/salesApi'
import { RETURN_CONDITIONS, RETURN_REASONS, type ReturnCondition, type ReturnReason, type ReturnStockAction } from '../../lib/salesTypes'
import { useT, type TranslationKey } from '../../lib/i18n'

type Step = 0 | 1 | 2
const REFUND_METHODS: PaymentMethod[] = ['Cash', 'Card', 'Qr', 'Transfer']

/**
 * Қайтару шебері (ТЗ «Продажи» §10, «Касса» §13) — кассаға да, кабинетке де ортақ, backend бір:
 * 1) тауарлар мен саны (қалғанынан артық емес), 2) себеп, тауар күйі, қоймалық әрекет,
 * 3) ақша (алдымен ашық қарыз азаяды, қалғаны таңдалған түрмен) және растау. Кілт бір рет жасалады —
 * «Подтвердить» екі рет басылса да, бір қайтару.
 */
export function ReturnWizard({ saleId, source, onClose, onDone }: {
  saleId: string
  /** cashier — касса экраны (sales.create), office — «Продажи» бөлімі (sales.view). */
  source: 'cashier' | 'office'
  onClose: () => void
  onDone: (r: Receipt) => void
}) {
  const t = useT()
  const qc = useQueryClient()
  const { data: r } = useQuery({
    queryKey: source === 'cashier' ? ['cashier', 'sale', saleId] : salesKeys.receipt(saleId),
    queryFn: () => (source === 'cashier' ? cashierApi.sale(saleId) : salesApi.receipt(saleId)),
  })
  const [step, setStep] = useState<Step>(0)
  const [qty, setQty] = useState<Record<string, number>>({})
  const [reasonCode, setReasonCode] = useState<ReturnReason | ''>('')
  const [comment, setComment] = useState('')
  const [condition, setCondition] = useState<ReturnCondition | ''>('')
  const [stockAction, setStockAction] = useState<ReturnStockAction | ''>('')
  const [method, setMethod] = useState<PaymentMethod | ''>('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [requestId] = useState(() => crypto.randomUUID())

  // Сервердегідей: позиция түгел қайтса — қалған соманың бәрі, әйтпесе үлесі (тиын айырмасы болуы мүмкін).
  const lines = useMemo(() => (r?.items ?? []).map((i) => {
    const available = round2(i.quantity - i.returnedQuantity)
    const q = Math.min(qty[i.id] ?? 0, available)
    const paid = i.lineTotal - i.discount
    const exact = (paid * q) / i.quantity
    const amount = q <= 0 ? 0 : q === available ? round2(paid * available / i.quantity) : paid % 1 === 0 ? Math.round(exact) : round2(exact)
    return { ...i, available, q, amount }
  }), [r, qty])
  const total = round2(lines.reduce((s, l) => s + l.amount, 0))
  const count = lines.reduce((s, l) => s + l.q, 0)
  const nothingLeft = lines.length > 0 && lines.every((l) => l.available <= 0)
  const debtLeft = r?.debt?.status === 'Open' ? r.debt.remaining : 0
  const refund = Math.max(0, round2(total - Math.min(total, debtLeft)))
  const defaultMethod = r?.payments.find((p) => p.method !== 'Debt')?.method ?? 'Cash'
  const chosen = (method || defaultMethod) as PaymentMethod

  // Себепке сай әдепкі: брак → бүлінген → списание (ТЗ §10.2). Қолмен өзгертуге болады.
  const effectiveCondition: ReturnCondition = condition || (reasonCode === 'Defect' ? 'Damaged' : 'Sellable')
  const effectiveAction: ReturnStockAction = stockAction || (effectiveCondition === 'Damaged' ? 'WriteOff' : 'ToStock')
  const writeOffAllowed = effectiveCondition !== 'Sellable'
  const commentMissing = reasonCode === 'Other' && !comment.trim()
  const dirty = count > 0 || reasonCode !== '' || comment.trim() !== ''

  const close = async () => {
    if (!dirty || await confirmDialog({ title: t('receipts.return'), message: t('return.leave'), confirmLabel: t('common.close'), danger: true })) onClose()
  }

  const setQ = (id: string, value: number, max: number) => setQty((x) => ({ ...x, [id]: Math.max(0, Math.min(max, round2(value))) }))

  const submit = async () => {
    if (!r || !reasonCode) return
    setBusy(true)
    setError(null)
    try {
      const result = await cashierApi.createReturn(r.id, {
        clientRequestId: requestId,
        items: lines.filter((l) => l.q > 0).map((l) => ({ saleItemId: l.id, quantity: l.q })),
        refundMethod: refund > 0 ? chosen : null,
        reason: comment.trim() || null,
        reasonCode,
        condition: effectiveCondition,
        stockAction: writeOffAllowed ? effectiveAction : 'ToStock',
      })
      await Promise.all([qc.invalidateQueries({ queryKey: ['cashier'] }), qc.invalidateQueries({ queryKey: salesKeys.all }), qc.invalidateQueries({ queryKey: ['inventory'] })])
      toast(t('return.done', { n: result.number }))
      onDone(result)
    } catch (err) {
      setError(err instanceof ApiError ? err.message : String(err))
    } finally {
      setBusy(false)
    }
  }

  const steps: TranslationKey[] = ['return.step.items', 'return.step.reason', 'return.step.money']

  return (
    <Modal title={r ? t('return.title', { n: r.number }) : t('receipts.return')} onClose={() => void close()} width={800}>
      {!r ? <div className="flex justify-center py-10 text-ink-3"><Loader2 className="animate-spin" /></div> : (
        <div className="flex flex-col gap-4">
          <ol className="flex flex-wrap items-center gap-2 text-[13px]">
            {steps.map((key, i) => (
              <li key={key} className="flex items-center gap-2">
                <span className={`flex size-6 items-center justify-center rounded-full text-[12px] font-bold ${i < step ? 'bg-success text-white' : i === step ? 'bg-brand text-white' : 'bg-field text-ink-3'}`}>
                  {i < step ? <Check size={13} /> : i + 1}
                </span>
                <span className={i === step ? 'font-semibold' : 'text-ink-2'}>{t(key)}</span>
                {i < steps.length - 1 && <span className="mx-1 h-px w-6 bg-line" />}
              </li>
            ))}
          </ol>

          <div className="flex flex-wrap gap-x-5 gap-y-1 rounded-xl bg-field px-4 py-2.5 text-[13px] text-ink-2">
            <span>{t('return.original')}: <b className="text-ink">{t('pos.receiptNo', { n: r.number })}</b></span>
            <span>{dateTime(r.createdAt)}</span>
            <span>{r.storeName} · {r.registerName}</span>
            <span>{t('return.cashier')}: {r.cashierName}</span>
            {r.customer && <span>{t('pos.customer')}: {r.customer.fullName}</span>}
          </div>

          {step === 0 && (
            <div className="overflow-x-auto rounded-xl border border-line">
              <table className="w-full min-w-[640px] text-[13px]">
                <thead className="bg-field text-left text-[12px] text-ink-2">
                  <tr>
                    <th className="px-3 py-2 font-medium">{t('receipt.item')}</th>
                    <th className="px-2 py-2 text-right font-medium">{t('receipt.price')}</th>
                    <th className="px-2 py-2 text-right font-medium">{t('return.sold')}</th>
                    <th className="px-2 py-2 text-right font-medium">{t('return.returnedBefore')}</th>
                    <th className="px-2 py-2 text-center font-medium">{t('return.back')}</th>
                    <th className="px-3 py-2 text-right font-medium">{t('return.sum')}</th>
                  </tr>
                </thead>
                <tbody>
                  {lines.map((l) => (
                    <tr key={l.id} className={`border-t border-line ${l.available <= 0 ? 'text-ink-3' : ''}`}>
                      <td className="px-3 py-2.5">
                        <div className="font-medium">{l.name}</div>
                        {l.article && <div className="text-[11px] text-ink-3">{t('pos.article', { a: l.article })}</div>}
                      </td>
                      <td className="px-2 py-2.5 text-right whitespace-nowrap">{tenge(l.price)}</td>
                      <td className="px-2 py-2.5 text-right">{num(l.quantity)}</td>
                      <td className="px-2 py-2.5 text-right">{num(l.returnedQuantity)}</td>
                      <td className="px-2 py-2.5">
                        {l.available > 0 ? (
                          <div className="mx-auto flex w-fit items-center rounded-lg bg-field">
                            <button type="button" disabled={l.q <= 0} onClick={() => setQ(l.id, l.q - 1, l.available)}
                              className="flex size-8 items-center justify-center text-brand disabled:text-ink-3">−</button>
                            <input value={l.q} onChange={(e) => setQ(l.id, Number(e.target.value) || 0, l.available)} inputMode="decimal"
                              className="w-12 bg-transparent text-center font-semibold outline-none" aria-label={t('return.back')} />
                            <button type="button" disabled={l.q >= l.available} onClick={() => setQ(l.id, l.q + 1, l.available)}
                              className="flex size-8 items-center justify-center text-brand disabled:text-ink-3">+</button>
                          </div>
                        ) : <div className="text-center text-[12px]">—</div>}
                        <div className="mt-0.5 text-center text-[11px] text-ink-3">{t('return.available')}: {num(l.available)}</div>
                      </td>
                      <td className="px-3 py-2.5 text-right font-semibold whitespace-nowrap">{tenge(l.amount)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
              {nothingLeft && <p className="px-4 py-3 text-[13px] text-danger">{t('return.nothingLeft')}</p>}
            </div>
          )}

          {step === 1 && (
            <div className="grid gap-4 md:grid-cols-2">
              <Field label={t('return.reasonCode')} required>
                <Select value={reasonCode} onChange={(e) => { setReasonCode(e.target.value as ReturnReason); setCondition(''); setStockAction('') }}>
                  <option value="">{t('product.choose')}</option>
                  {RETURN_REASONS.map((k) => <option key={k} value={k}>{t(`return.reasonCode.${k}`)}</option>)}
                </Select>
              </Field>
              <Field label={t('return.condition')}>
                <Select value={effectiveCondition} onChange={(e) => { setCondition(e.target.value as ReturnCondition); setStockAction('') }}>
                  {RETURN_CONDITIONS.map((k) => <option key={k} value={k}>{t(`return.condition.${k}`)}</option>)}
                </Select>
              </Field>
              <Field label={t('return.stock')} hint={writeOffAllowed ? t('return.stockHint') : t('return.stockNeedsCondition')}>
                <Select value={writeOffAllowed ? effectiveAction : 'ToStock'} disabled={!writeOffAllowed}
                  onChange={(e) => setStockAction(e.target.value as ReturnStockAction)}>
                  <option value="ToStock">{t('return.stock.ToStock')}</option>
                  <option value="WriteOff">{t('return.stock.WriteOff')}</option>
                </Select>
              </Field>
              <Field label={t('return.reason')} required={reasonCode === 'Other'} error={commentMissing ? t('return.commentRequired') : null}>
                <Textarea value={comment} onChange={(e) => setComment(e.target.value)} maxLength={300} className="min-h-20!" />
              </Field>
            </div>
          )}

          {step === 2 && (
            <div className="grid gap-4 md:grid-cols-2">
              <div className="flex flex-col gap-3">
                <div className="rounded-xl border border-line p-4 text-[13px]">
                  <div className="mb-1 font-semibold">{t('return.originalPayments')}</div>
                  {r.payments.map((p) => (
                    <div key={p.method} className="flex justify-between text-ink-2"><span>{t(`pay.${p.method}`)}</span><span>{tenge(p.amount)}</span></div>
                  ))}
                </div>
                {refund > 0 && (
                  <Field label={t('return.method')}>
                    <Select value={chosen} onChange={(e) => setMethod(e.target.value as PaymentMethod)}>
                      {REFUND_METHODS.map((m) => <option key={m} value={m}>{t(`pay.${m}`)}</option>)}
                    </Select>
                  </Field>
                )}
                <div className="rounded-xl bg-field px-4 py-3 text-[13px] text-ink-2">
                  <div>{t('return.reasonCode')}: <b className="text-ink">{reasonCode ? t(`return.reasonCode.${reasonCode}`) : '—'}</b>{comment.trim() ? ` · ${comment.trim()}` : ''}</div>
                  <div>{t('return.condition')}: <b className="text-ink">{t(`return.condition.${effectiveCondition}`)}</b></div>
                  <div>{t('return.stock')}: <b className="text-ink">{t(`return.stock.${writeOffAllowed ? effectiveAction : 'ToStock'}`)}</b></div>
                </div>
              </div>
              <div className="rounded-xl border border-line p-4 text-[14px]">
                <div className="flex justify-between"><span className="text-ink-2">{t('return.count')}</span><b>{num(count)}</b></div>
                <div className="mt-1 flex justify-between"><span className="text-ink-2">{t('return.total')}</span><b className="text-[18px]">{tenge(total)}</b></div>
                {debtLeft > 0 && total > 0 && (
                  <div className="mt-1 flex justify-between text-amber-700"><span>{t('return.debtFirst')}</span><b>− {tenge(Math.min(total, debtLeft))}</b></div>
                )}
                {total > 0 && (
                  <div className="mt-1 flex justify-between"><span className="text-ink-2">{t('return.refund', { method: t(`pay.${chosen}`) })}</span><b className="text-danger">{tenge(refund)}</b></div>
                )}
                {(r.bonusRedeemed > 0 || r.bonusAccrued > 0) && <p className="mt-3 text-[12px] text-ink-3">{t('return.bonusNote')}</p>}
                <p className="mt-3 flex items-start gap-2 rounded-lg bg-amber-50 px-3 py-2 text-[12px] text-amber-800">
                  <AlertTriangle size={15} className="mt-0.5 shrink-0" />{t('return.separateOp')}
                </p>
              </div>
            </div>
          )}

          {error && <p className="rounded-xl bg-danger-soft px-4 py-3 text-[14px] text-danger">{error}</p>}
          <div className="flex flex-wrap justify-end gap-2">
            <Button variant="secondary" icon={<X size={17} />} onClick={() => void close()}>{t('common.cancel')}</Button>
            {step > 0 && <Button variant="secondary" icon={<ArrowLeft size={17} />} onClick={() => setStep((s) => (s - 1) as Step)}>{t('return.prev')}</Button>}
            {step < 2 && (
              <Button iconRight={<ArrowRight size={17} />} disabled={step === 0 ? count <= 0 : !reasonCode || commentMissing}
                onClick={() => setStep((s) => (s + 1) as Step)}>{t('return.next')}</Button>
            )}
            {step === 2 && (
              <Button variant="danger" icon={<Undo2 size={17} />} loading={busy} disabled={count <= 0 || !reasonCode || commentMissing}
                onClick={() => void submit()}>{t('return.confirm')}</Button>
            )}
          </div>
        </div>
      )}
    </Modal>
  )
}
