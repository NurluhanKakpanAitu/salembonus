import { useRef, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useSearchParams } from 'react-router-dom'
import { ArrowLeftRight, Download, FileText, History, Pencil, Plus, Power, Scale } from 'lucide-react'
import { Button } from '../ui/Button'
import { Field, Input, Select, Textarea, Toggle } from '../ui/Field'
import { Modal } from '../ui/Modal'
import { RowMenu } from '../ui/RowMenu'
import { toast } from '../ui/Toast'
import { confirmDialog } from '../ui/Confirm'
import { EmptyRow, td, th } from '../inventory/inventoryUi'
import { ApiError } from '../../lib/api'
import { dateOnly, dateTime, isoDate } from '../../lib/dates'
import { financeApi, financeKeys } from '../../lib/financeApi'
import { ACCOUNT_TYPES, PAY_METHODS, type AccountType, type FinanceAccount, type PayMethod } from '../../lib/financeTypes'
import { tenge } from '../../lib/money'
import { useT, type TranslationKey } from '../../lib/i18n'
import { BlockError, Skeleton } from '../stats/statsUi'
import { ACCOUNT_ICON, ACCOUNT_TONE, Amount, OperationCard, reportError, TypeBadge, useFinancePermissions, type FinanceFilters } from './financeUi'
import { TransferModal } from './TransferModal'

/**
 * «Счета» (ТЗ «Финансы» §9): ақша орындары — касса, сейф, банк, Kaspi, жолдағы ақша, басқа. Қарыздар шот емес.
 * Әрекеттер: выписка, аударым, реквизит өзгерту, өшіру (тарихы бар шот жойылмайды), қалдық түзетуі (тек арнайы құқықпен, себеппен).
 */
export function AccountsTab({ filters, storeIds }: { filters: FinanceFilters; storeIds?: string }) {
  const t = useT()
  const qc = useQueryClient()
  const [params, setParams] = useSearchParams()
  const { canManage, canOperate, canReverse } = useFinancePermissions()
  const [showAll, setShowAll] = useState(false)
  const [form, setForm] = useState<{ a?: FinanceAccount } | null>(null)
  const [transfer, setTransfer] = useState<{ from?: string; collection?: boolean } | null>(null)
  const [adjust, setAdjust] = useState<FinanceAccount | null>(null)
  const [backfilling, setBackfilling] = useState(false)
  const q = useQuery({ queryKey: [...financeKeys.accounts(showAll), storeIds], queryFn: () => financeApi.accounts(storeIds, showAll) })
  const invalidate = () => void qc.invalidateQueries({ queryKey: financeKeys.all })
  const statementId = params.get('statement')
  const d = q.data
  const multiStore = d ? new Set(d.accounts.map((a) => a.storeId)).size > 1 : false

  // Тарихты жүктеу (§3.1): модуль қосылғанға дейінгі сатылым, қайтарым, қарыз бен жеткізуші төлемдері шоттарға бір рет жазылады; қайталау қауіпсіз.
  const backfill = async () => {
    if (!(await confirmDialog({ title: t('fin.backfill'), message: t('fin.backfillConfirm'), confirmLabel: t('fin.backfill') }))) return
    setBackfilling(true)
    try { const r = await financeApi.backfill(); toast(t('fin.backfillDone', { posted: r.posted, skipped: r.skipped })); invalidate() }
    catch (err) { reportError(err, t('fin.failed')) } finally { setBackfilling(false) }
  }

  const toggleActive = async (a: FinanceAccount) => {
    if (a.isActive && !(await confirmDialog({ message: t('fin.deactivateConfirm', { name: a.name }), confirmLabel: t('fin.deactivate'), danger: true }))) return
    try { await financeApi.setAccountActive(a.id, !a.isActive); toast(a.isActive ? t('fin.deactivated') : t('fin.activated')); invalidate() }
    catch (err) { reportError(err, t('fin.failed')) }
  }

  return (
    <div className="flex flex-col gap-4">
      {q.isError ? <BlockError message={(q.error as Error).message} onRetry={() => void q.refetch()} /> : !d ? <Skeleton className="h-20" /> : (
        <div className="grid grid-cols-2 gap-3 xl:grid-cols-4">
          {[['total', d.total], ['cash', d.cash], ['nonCash', d.nonCash], ['inTransit', d.inTransit]].map(([k, v]) => (
            <div key={k as string} className="rounded-2xl border border-line bg-surface p-3">
              <div className="text-[12px] font-medium text-ink-2">{t(`fin.acc.${k}` as TranslationKey)}</div>
              <div className="mt-0.5 text-[20px] font-extrabold tracking-tight tabular-nums">{tenge(v as number)}</div>
            </div>
          ))}
        </div>
      )}
      <div className="flex flex-wrap items-center gap-2">
        <Toggle checked={showAll} onChange={setShowAll} label={t('fin.showInactive')} />
        <span className="ml-auto" />
        {canManage && <Button variant="ghost" className="h-10! px-3!" icon={<History size={16} />} loading={backfilling} onClick={() => void backfill()} title={t('fin.backfillHint')}>{t('fin.backfill')}</Button>}
        {canOperate && <Button variant="secondary" className="h-10! px-3!" icon={<ArrowLeftRight size={16} />} onClick={() => setTransfer({})}>{t('fin.transfer')}</Button>}
        {canManage && <Button className="h-10! px-3!" icon={<Plus size={16} />} onClick={() => setForm({})}>{t('fin.addAccount')}</Button>}
      </div>
      <div className="overflow-x-auto rounded-2xl border border-line bg-surface">
        <table className="w-full min-w-[820px]">
          <thead className="bg-field/60">
            <tr>
              <th className={th}>{t('fin.accountName')}</th>
              <th className={th}>{t('fin.col.type')}</th>
              {multiStore && <th className={th}>{t('fin.store')}</th>}
              <th className={th}>{t('fin.methods')}</th>
              <th className={`${th} text-right`}>{t('fin.balance')}</th>
              <th className={th}>{t('fin.opening')}</th>
              <th className={th}>{t('fin.col.status')}</th>
              <th className={`${th} w-12`}></th>
            </tr>
          </thead>
          <tbody>
            {!d ? null : d.accounts.length === 0 ? <EmptyRow colSpan={8} text={t('fin.noAccounts')} /> : d.accounts.map((a) => (
              <tr key={a.id} className={`border-t border-line hover:bg-field/40 ${a.isActive ? '' : 'text-ink-3'}`}>
                <td className={td}>
                  <button type="button" onClick={() => setParams((p) => { const n = new URLSearchParams(p); n.set('statement', a.id); return n }, { replace: true })} className="flex items-center gap-3 text-left hover:text-brand">
                    <span className={`flex size-9 shrink-0 items-center justify-center rounded-lg ${ACCOUNT_TONE[a.type]}`}>{ACCOUNT_ICON[a.type]}</span>
                    <span><span className="block font-medium">{a.name}</span>{a.comment && <span className="block text-[12px] text-ink-3">{a.comment}</span>}</span>
                  </button>
                </td>
                <td className={`${td} text-ink-2`}>{t(`fin.accountType.${a.type}` as TranslationKey)}</td>
                {multiStore && <td className={`${td} text-ink-2`}>{a.storeName}</td>}
                <td className={td}>
                  <span className="flex flex-wrap gap-1">{a.methods.map((m) => <span key={m} className="rounded-md bg-field px-1.5 py-0.5 text-[11px] font-semibold text-ink-2">{t(`pay.${m}` as TranslationKey)}</span>)}</span>
                </td>
                <td className={`${td} text-right text-[15px] font-bold tabular-nums ${a.balance < 0 ? 'text-danger' : ''}`}>{tenge(a.balance)}</td>
                <td className={`${td} text-[13px] text-ink-2`}>{a.openingBalance !== 0 ? `${tenge(a.openingBalance)}${a.openingDate ? ` · ${dateOnly(a.openingDate)}` : ''}` : '—'}</td>
                <td className={td}><span className={`rounded-lg px-2 py-0.5 text-[12px] font-semibold ${a.isActive ? 'bg-success-soft text-success' : 'bg-field text-ink-3'}`}>{a.isActive ? t('fin.active') : t('fin.inactive')}</span></td>
                <td className={td}>
                  <RowMenu items={[
                    { label: t('fin.statement'), icon: <FileText size={16} />, onClick: () => setParams((p) => { const n = new URLSearchParams(p); n.set('statement', a.id); return n }, { replace: true }) },
                    ...(canOperate && a.isActive ? [{ label: t('fin.transfer'), icon: <ArrowLeftRight size={16} />, onClick: () => setTransfer({ from: a.id }) }] : []),
                    ...(canManage ? [{ label: t('fin.editAccount'), icon: <Pencil size={16} />, onClick: () => setForm({ a }) }] : []),
                    ...(canReverse && a.isActive ? [{ label: t('fin.adjust'), icon: <Scale size={16} />, onClick: () => setAdjust(a) }] : []),
                    ...(canManage ? [{ label: a.isActive ? t('fin.deactivate') : t('fin.activate'), icon: <Power size={16} />, danger: a.isActive, onClick: () => void toggleActive(a) }] : []),
                  ]} />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {form && <AccountForm initial={form.a} onClose={() => setForm(null)} onSaved={() => { setForm(null); invalidate() }} />}
      {transfer && <TransferModal accounts={d?.accounts ?? []} from={transfer.from} collection={transfer.collection} onClose={() => setTransfer(null)} onDone={() => { setTransfer(null); invalidate() }} />}
      {adjust && <AdjustModal account={adjust} onClose={() => setAdjust(null)} onDone={() => { setAdjust(null); invalidate() }} />}
      {statementId && <StatementModal id={statementId} filters={filters} onClose={() => setParams((p) => { const n = new URLSearchParams(p); n.delete('statement'); n.delete('op'); return n }, { replace: true })} />}
    </div>
  )
}

/** Шот формасы (§9): аты, түрі, төлем әдістері (бір дүкенде бір әдіс — бір шот), бастапқы қалдық (проводкасы жоқ шотқа ғана). */
function AccountForm({ initial, onClose, onSaved }: { initial?: FinanceAccount; onClose: () => void; onSaved: () => void }) {
  const t = useT()
  const [name, setName] = useState(initial?.name ?? '')
  const [type, setType] = useState<AccountType>(initial?.type ?? 'Cash')
  const [methods, setMethods] = useState<PayMethod[]>(initial?.methods ?? [])
  const [comment, setComment] = useState(initial?.comment ?? '')
  const [opening, setOpening] = useState(initial?.openingBalance ? String(initial.openingBalance) : '')
  const [openingDate, setOpeningDate] = useState(initial?.openingDate ?? isoDate(new Date()))
  const [busy, setBusy] = useState(false)
  const [errors, setErrors] = useState<Record<string, string>>({})
  const locked = !!initial?.hasEntries
  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (name.trim().length < 2) { setErrors({ name: t('common.required') }); return }
    setBusy(true)
    try {
      const body = { name: name.trim(), type, methods, comment: comment.trim() || null, openingBalance: locked ? undefined : Number(opening.replace(',', '.')) || 0, openingDate: locked ? undefined : openingDate }
      if (initial) await financeApi.updateAccount(initial.id, body); else await financeApi.createAccount(body)
      toast(t('fin.saved'))
      onSaved()
    } catch (err) {
      if (err instanceof ApiError && err.field) setErrors({ [err.field]: err.message })
      reportError(err, t('fin.failed'))
    } finally { setBusy(false) }
  }
  return (
    <Modal title={initial ? t('fin.editAccount') : t('fin.addAccount')} onClose={onClose} width={520}>
      <form onSubmit={submit} className="flex flex-col gap-4">
        <Field label={t('fin.accountName')} required error={errors.name}><Input autoFocus value={name} maxLength={120} onChange={(e) => setName(e.target.value)} error={!!errors.name} /></Field>
        <Field label={t('fin.col.type')} required>
          <Select value={type} onChange={(e) => setType(e.target.value as AccountType)}>
            {ACCOUNT_TYPES.map((x) => <option key={x} value={x}>{t(`fin.accountType.${x}` as TranslationKey)}</option>)}
          </Select>
        </Field>
        <Field label={t('fin.methods')} hint={t('fin.methodsHint')}>
          <div className="flex flex-wrap gap-2">
            {PAY_METHODS.map((m) => (
              <label key={m} className={`flex h-10 cursor-pointer items-center gap-2 rounded-xl border px-3 text-[14px] ${methods.includes(m) ? 'border-brand bg-brand-soft text-brand' : 'border-line'}`}>
                <input type="checkbox" className="accent-brand" checked={methods.includes(m)} onChange={(e) => setMethods((xs) => e.target.checked ? [...xs, m] : xs.filter((x) => x !== m))} />
                {t(`pay.${m}` as TranslationKey)}
              </label>
            ))}
          </div>
        </Field>
        <div className="grid grid-cols-2 gap-3">
          <Field label={t('fin.opening')} error={errors.openingBalance} hint={locked ? t('fin.openingLocked') : undefined}>
            <Input inputMode="decimal" value={opening} disabled={locked} onChange={(e) => setOpening(e.target.value)} error={!!errors.openingBalance} placeholder="0" />
          </Field>
          <Field label={t('fin.openingDate')} error={errors.openingDate}>
            <Input type="date" value={openingDate} disabled={locked} max={isoDate(new Date())} onChange={(e) => setOpeningDate(e.target.value)} error={!!errors.openingDate} />
          </Field>
        </div>
        <Field label={t('fin.comment')}><Textarea value={comment} maxLength={500} onChange={(e) => setComment(e.target.value)} className="min-h-16!" /></Field>
        <div className="flex justify-end gap-2">
          <Button type="button" variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" loading={busy}>{t('common.save')}</Button>
        </div>
      </form>
    </Modal>
  )
}

/** Қалдық түзетуі (§9, §14): тек finance.reverse құқығымен, себеп міндетті, аудитке жазылады; P&L-ге түспейді. */
function AdjustModal({ account, onClose, onDone }: { account: FinanceAccount; onClose: () => void; onDone: () => void }) {
  const t = useT()
  const [amount, setAmount] = useState('')
  const [reason, setReason] = useState('')
  const [documentNumber, setDocumentNumber] = useState('')
  const [busy, setBusy] = useState(false)
  const requestId = useRef(crypto.randomUUID())
  const sum = Number(amount.replace(',', '.')) || 0
  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (sum === 0 || reason.trim().length < 3) return
    if (!(await confirmDialog({ message: t('fin.adjustConfirm', { name: account.name, amount: tenge(sum), after: tenge(account.balance + sum) }), confirmLabel: t('fin.adjust'), danger: true }))) return
    setBusy(true)
    try { await financeApi.adjust({ accountId: account.id, amount: sum, reason: reason.trim(), documentNumber: documentNumber.trim() || null, clientRequestId: requestId.current }); toast(t('fin.adjusted')); onDone() }
    catch (err) { reportError(err, t('fin.failed')) } finally { setBusy(false) }
  }
  return (
    <Modal title={`${t('fin.adjust')} · ${account.name}`} onClose={onClose} width={480}>
      <form onSubmit={submit} className="flex flex-col gap-4">
        <p className="text-[13px] text-ink-2">{t('fin.adjustHint')} {t('fin.balance')}: <b>{tenge(account.balance)}</b>{sum !== 0 && <> → <b>{tenge(account.balance + sum)}</b></>}</p>
        <Field label={t('fin.adjustAmount')} required hint={t('fin.adjustAmountHint')}><Input autoFocus inputMode="decimal" value={amount} onChange={(e) => setAmount(e.target.value)} placeholder="±0" /></Field>
        <Field label={t('inv.reason')} required><Textarea value={reason} maxLength={500} onChange={(e) => setReason(e.target.value)} /></Field>
        <Field label={t('fin.col.document')}><Input value={documentNumber} maxLength={64} onChange={(e) => setDocumentNumber(e.target.value)} /></Field>
        <div className="flex justify-end gap-2">
          <Button type="button" variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" variant="danger" loading={busy} disabled={sum === 0 || reason.trim().length < 3}>{t('fin.adjust')}</Button>
        </div>
      </form>
    </Modal>
  )
}

/** Шот выпискасы (§9): басындағы қалдық, қозғалыстар жүгіртпе қалдықпен, соңындағы қалдық; экспорт. */
function StatementModal({ id, filters, onClose }: { id: string; filters: FinanceFilters; onClose: () => void }) {
  const t = useT()
  const [params, setParams] = useSearchParams()
  const { canExport } = useFinancePermissions()
  const [exporting, setExporting] = useState(false)
  const q = useQuery({ queryKey: ['finance', 'statement', id, filters.from, filters.to], queryFn: () => financeApi.statement(id, filters.from, filters.to) })
  const d = q.data
  const opId = params.get('op')
  const exportFile = async (format: 'xlsx' | 'csv') => {
    setExporting(true)
    try { await financeApi.exportFile('statement', format, { id, from: filters.from, to: filters.to }); toast(t('stats.exportReady')) } catch (err) { reportError(err, t('stats.exportFailed')) } finally { setExporting(false) }
  }
  return (
    <Modal title={d ? `${t('fin.statement')} · ${d.account.name}` : t('fin.statement')} onClose={onClose} width={860}>
      {!d ? <Skeleton className="h-48" /> : (
        <div className="flex flex-col gap-3">
          <div className="flex flex-wrap items-center gap-x-5 gap-y-1 text-[13px] text-ink-2">
            <span>{dateOnly(d.from)} – {dateOnly(d.to)}</span>
            <span>{t('fin.cf.opening')}: <b>{tenge(d.opening)}</b></span>
            <span>{t('fin.cf.inflow')}: <b className="text-success">{tenge(d.inflow)}</b></span>
            <span>{t('fin.cf.outflow')}: <b className="text-danger">{tenge(d.outflow)}</b></span>
            <span>{t('fin.cf.closing')}: <b>{tenge(d.closing)}</b></span>
            {canExport && <span className="ml-auto flex gap-1"><Button variant="secondary" className="h-9! px-3! text-[13px]!" icon={<Download size={14} />} disabled={exporting} onClick={() => void exportFile('xlsx')}>Excel</Button><Button variant="secondary" className="h-9! px-3! text-[13px]!" disabled={exporting} onClick={() => void exportFile('csv')}>CSV</Button></span>}
          </div>
          <div className="max-h-[60vh] overflow-auto rounded-xl border border-line">
            <table className="w-full min-w-[640px]">
              <thead className="sticky top-0 bg-field/90"><tr><th className={th}>{t('fin.col.date')}</th><th className={th}>{t('fin.col.type')}</th><th className={th}>{t('fin.col.description')}</th><th className={`${th} text-right`}>{t('fin.col.amount')}</th><th className={`${th} text-right`}>{t('fin.balance')}</th></tr></thead>
              <tbody>
                {d.rows.length === 0 ? <EmptyRow colSpan={5} text={t('fin.emptyPeriod')} /> : d.rows.map((r) => (
                  <tr key={`${r.operationId}-${r.date}-${r.amount}`} className="border-t border-line">
                    <td className={`${td} whitespace-nowrap text-ink-2`}>{dateTime(r.date)}</td>
                    <td className={td}><TypeBadge type={r.type} /></td>
                    <td className={td}><button type="button" className="text-left hover:text-brand" onClick={() => setParams((p) => { const n = new URLSearchParams(p); n.set('op', r.operationId); return n }, { replace: true })}>{r.description}<span className="block text-[12px] text-ink-3">{r.number}{r.counterpartyName ? ` · ${r.counterpartyName}` : ''}{r.documentNumber ? ` · ${r.documentNumber}` : ''}</span></button></td>
                    <td className={`${td} text-right`}><Amount value={Math.abs(r.amount)} type={r.type} entries={[{ accountId: id, accountName: '', amount: r.amount, balanceAfter: r.balanceAfter }]} accountId={id} /></td>
                    <td className={`${td} text-right tabular-nums text-ink-2`}>{tenge(r.balanceAfter)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
      {opId && <OperationCard id={opId} onClose={() => setParams((p) => { const n = new URLSearchParams(p); n.delete('op'); return n }, { replace: true })} onChanged={() => void q.refetch()} />}
    </Modal>
  )
}
