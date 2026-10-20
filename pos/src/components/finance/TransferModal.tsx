import { useRef, useState } from 'react'
import { ArrowRight } from 'lucide-react'
import { Button } from '../ui/Button'
import { Field, Input, Select, Textarea, Toggle } from '../ui/Field'
import { Modal } from '../ui/Modal'
import { toast } from '../ui/Toast'
import { ApiError } from '../../lib/api'
import { financeApi } from '../../lib/financeApi'
import type { FinanceAccount, FinanceOperation } from '../../lib/financeTypes'
import { tenge } from '../../lib/money'
import { useT } from '../../lib/i18n'
import { ACCOUNT_ICON, ACCOUNT_TONE, reportError } from './financeUi'

/**
 * Шоттар арасындағы аударым / инкассация (ТЗ «Финансы» §9.1, §14): бір операция, екі проводка, жалпы қалдық өзгермейді, P&L-ге түспейді.
 * Бірдей шот — тыйым; қайнар көздің қалдығы мен жаңа қалдық көрсетіледі; серверде қалдық тексеріледі (овердрафт жоқ).
 */
export function TransferModal({ accounts, from, collection: initialCollection, onClose, onDone }: {
  accounts: FinanceAccount[]; from?: string; collection?: boolean; onClose: () => void; onDone: (op: FinanceOperation) => void
}) {
  const t = useT()
  const active = accounts.filter((a) => a.isActive)
  const [fromId, setFromId] = useState(from ?? active.find((a) => a.type === 'Cash')?.id ?? active[0]?.id ?? '')
  const [collection, setCollection] = useState(!!initialCollection)
  const [toId, setToId] = useState(() => {
    const transit = active.find((a) => a.type === 'InTransit')
    return initialCollection && transit ? transit.id : active.find((a) => a.id !== (from ?? active[0]?.id))?.id ?? ''
  })
  const [amount, setAmount] = useState('')
  const [comment, setComment] = useState('')
  const [documentNumber, setDocumentNumber] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<{ field: string; message: string } | null>(null)
  const requestId = useRef(crypto.randomUUID())
  const src = active.find((a) => a.id === fromId)
  const dst = active.find((a) => a.id === toId)
  const sum = Number(amount.replace(',', '.')) || 0
  const same = !!fromId && fromId === toId
  const over = !!src && sum > src.balance

  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (same) { setError({ field: 'toAccountId', message: t('fin.err.sameAccount') }); return }
    if (!(sum > 0)) { setError({ field: 'amount', message: t('fin.err.amount') }); return }
    if (over) { setError({ field: 'amount', message: t('fin.err.insufficient', { balance: tenge(src!.balance) }) }); return }
    setBusy(true)
    try {
      const op = await financeApi.transfer({ fromAccountId: fromId, toAccountId: toId, amount: sum, comment: comment.trim() || null, documentNumber: documentNumber.trim() || null, collection, clientRequestId: requestId.current })
      toast(collection ? t('fin.collectionDone') : t('fin.transferDone'))
      onDone(op)
    } catch (err) {
      if (err instanceof ApiError && err.field) setError({ field: err.field, message: err.message })
      reportError(err, t('fin.failed'))
    } finally { setBusy(false) }
  }

  const AccountOption = ({ a }: { a: FinanceAccount }) => <option value={a.id}>{a.name} · {tenge(a.balance)}</option>

  return (
    <Modal title={collection ? t('fin.collection') : t('fin.transfer')} onClose={onClose} width={560}>
      <form onSubmit={submit} className="flex flex-col gap-4">
        <Toggle checked={collection} onChange={(v) => { setCollection(v); const transit = active.find((a) => a.type === 'InTransit'); if (v && transit) setToId(transit.id) }} label={t('fin.collectionToggle')} />
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-[1fr_auto_1fr] sm:items-end">
          <Field label={t('fin.fromAccount')} required>
            <Select value={fromId} onChange={(e) => { setFromId(e.target.value); setError(null) }}>
              {active.map((a) => <AccountOption key={a.id} a={a} />)}
            </Select>
          </Field>
          <ArrowRight className="hidden text-ink-3 sm:mb-3 sm:block" size={20} />
          <Field label={t('fin.toAccount')} required error={error?.field === 'toAccountId' ? error.message : same ? t('fin.err.sameAccount') : null}>
            <Select value={toId} onChange={(e) => { setToId(e.target.value); setError(null) }} error={same}>
              {active.map((a) => <AccountOption key={a.id} a={a} />)}
            </Select>
          </Field>
        </div>
        <Field label={t('fin.col.amount')} required error={error?.field === 'amount' ? error.message : over ? t('fin.err.insufficient', { balance: tenge(src?.balance ?? 0) }) : null}
          hint={src && dst && sum > 0 && !over && !same ? `${src.name}: ${tenge(src.balance)} → ${tenge(src.balance - sum)} · ${dst.name}: ${tenge(dst.balance)} → ${tenge(dst.balance + sum)}` : undefined}>
          <Input autoFocus inputMode="decimal" value={amount} onChange={(e) => { setAmount(e.target.value); setError(null) }} placeholder="0" error={over || error?.field === 'amount'} />
        </Field>
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <Field label={t('fin.col.document')}><Input value={documentNumber} maxLength={64} onChange={(e) => setDocumentNumber(e.target.value)} /></Field>
          <Field label={t('fin.comment')}><Textarea value={comment} maxLength={1000} onChange={(e) => setComment(e.target.value)} className="min-h-11! h-11" /></Field>
        </div>
        {src && dst && (
          <div className="flex items-center gap-2 rounded-xl bg-field px-3 py-2 text-[13px] text-ink-2">
            <span className={`flex size-7 items-center justify-center rounded-lg ${ACCOUNT_TONE[src.type]}`}>{ACCOUNT_ICON[src.type]}</span>{src.name}
            <ArrowRight size={14} />
            <span className={`flex size-7 items-center justify-center rounded-lg ${ACCOUNT_TONE[dst.type]}`}>{ACCOUNT_ICON[dst.type]}</span>{dst.name}
            <span className="ml-auto">{t('fin.transferNoPnl')}</span>
          </div>
        )}
        <div className="flex justify-end gap-2">
          <Button type="button" variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" loading={busy} disabled={same || over || !(sum > 0)}>{collection ? t('fin.doCollection') : t('fin.doTransfer')}</Button>
        </div>
      </form>
    </Modal>
  )
}
