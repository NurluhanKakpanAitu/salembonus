import { useState } from 'react'
import { CheckCircle2, Printer, ShieldCheck } from 'lucide-react'
import { Button } from '../ui/Button'
import { CodeInput } from '../ui/CodeInput'
import { Field, Select } from '../ui/Field'
import { Modal } from '../ui/Modal'
import type { CashierContext, Receipt } from '../../lib/cashierTypes'
import { num, tenge } from '../../lib/money'
import { useT } from '../../lib/i18n'

/**
 * Жеңілдік кассир шегінен асқанда (ТЗ «Касса» §6.6): құқығы бар қызметкер өз PIN-імен растайды.
 * PIN тек серверде тексеріледі; қате PIN бұғат есептегішін арттырады.
 */
export function ApprovalModal({ ctx, percent, error, busy, onClose, onApprove }: {
  ctx: CashierContext
  percent: number
  error: string | null
  busy: boolean
  onClose: () => void
  onApprove: (staffUserId: string, pin: string) => void
}) {
  const t = useT()
  const [approver, setApprover] = useState(ctx.approvers[0]?.id ?? '')
  const [pin, setPin] = useState('')
  return (
    <Modal title={t('pos.approvalTitle')} onClose={onClose} width={420}>
      <div className="flex flex-col gap-4">
        <div className="flex items-start gap-3 rounded-xl bg-amber-50 px-4 py-3 text-[14px] text-amber-800">
          <ShieldCheck size={20} className="mt-0.5 shrink-0" />
          <span>{t('pos.approvalHint', { percent: num(Math.round(percent * 10) / 10), limit: num(ctx.maxDiscountPercent) })}</span>
        </div>
        {ctx.approvers.length === 0 ? (
          <p className="text-[14px] text-danger">{t('pos.noApprovers')}</p>
        ) : (
          <>
            <Field label={t('pos.approver')}>
              <Select value={approver} onChange={(e) => setApprover(e.target.value)}>
                {ctx.approvers.map((a) => <option key={a.id} value={a.id}>{a.name}</option>)}
              </Select>
            </Field>
            <CodeInput label={t('lock.pin')} value={pin} onChange={(v) => {
              setPin(v)
              if (v.length === 4 && approver) onApprove(approver, v)
            }} mask autoFocus error={!!error} />
            {error && <p className="text-center text-[14px] text-danger">{error}</p>}
          </>
        )}
        <div className="flex justify-end gap-2">
          <Button type="button" variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="button" loading={busy} disabled={pin.length !== 4 || !approver} onClick={() => onApprove(approver, pin)}>
            {t('pos.approve')}
          </Button>
        </div>
      </div>
    </Modal>
  )
}

/** Сәтті сатылымнан кейін (ТЗ §20.8–20.9): чек нөмірі, қайтарым, бонус. Жабылғанда — келесі сатылым. */
export function ReceiptDoneModal({ receipt, onClose, onPrint }: { receipt: Receipt; onClose: () => void; onPrint: () => void }) {
  const t = useT()
  const change = receipt.payments.find((p) => p.method === 'Cash')?.change ?? 0
  return (
    <Modal title={t('pos.saleDone')} onClose={onClose} width={440}>
      <div className="flex flex-col items-center gap-2 text-center">
        <CheckCircle2 size={52} className="text-success" />
        <div className="text-[15px] text-ink-2">{t('pos.receiptNo', { n: receipt.number })}</div>
        <div className="text-[32px] font-extrabold tracking-tight">{tenge(receipt.total)}</div>
        <div className="flex flex-wrap justify-center gap-2 text-[13px]">
          {receipt.payments.map((p) => (
            <span key={p.method} className="rounded-lg bg-field px-2.5 py-1">{t(`pay.${p.method}`)}: {tenge(p.amount)}</span>
          ))}
        </div>
      </div>
      {change > 0 && (
        <div className="mt-4 flex items-center justify-between rounded-xl bg-success-soft px-4 py-3 text-success">
          <span className="text-[15px] font-medium">{t('pay.change')}</span>
          <span className="text-[24px] font-bold">{tenge(change)}</span>
        </div>
      )}
      {receipt.customer && (
        <div className="mt-3 rounded-xl border border-line px-4 py-3 text-[14px]">
          <div className="font-semibold">{receipt.customer.fullName}</div>
          <div className="mt-1 flex justify-between text-ink-2">
            <span>{t('pos.accrued')}: <b className="text-success">+{num(receipt.bonusAccrued)} Б</b></span>
            {receipt.bonusRedeemed > 0 && <span>{t('pos.redeemed')}: <b>−{num(receipt.bonusRedeemed)} Б</b></span>}
          </div>
          {receipt.customer.bonusBalance != null && (
            <div className="mt-1 text-ink-2">{t('pos.newBalance')}: <b className="text-ink">{num(receipt.customer.bonusBalance)} Б</b></div>
          )}
        </div>
      )}
      {receipt.debt && (
        <div className="mt-3 rounded-xl bg-amber-50 px-4 py-3 text-[14px] text-amber-800">
          {t('pos.debtCreated', { sum: tenge(receipt.debt.amount) })}
        </div>
      )}
      <div className="mt-5 grid grid-cols-[1fr_2fr] gap-2">
        <Button variant="secondary" className="h-12!" icon={<Printer size={18} />} onClick={onPrint}>{t('receipts.print')}</Button>
        <Button className="h-12!" autoFocus onClick={onClose}>{t('pos.nextSale')}</Button>
      </div>
    </Modal>
  )
}
