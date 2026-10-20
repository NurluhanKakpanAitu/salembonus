import { useRef, useState } from 'react'
import { keepPreviousData, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { Download, ExternalLink, Eye, FilterX, Loader2, MinusCircle, PlusCircle, RotateCcw, Undo2 } from 'lucide-react'
import { Button } from '../ui/Button'
import { Field, Input, Select, Textarea } from '../ui/Field'
import { Drawer } from '../ui/Drawer'
import { Modal } from '../ui/Modal'
import { RowMenu } from '../ui/RowMenu'
import { SearchInput } from '../ui/SearchInput'
import { toast } from '../ui/Toast'
import { confirmDialog } from '../ui/Confirm'
import { EmptyRow, Pager, td, th, useDebounced } from '../inventory/inventoryUi'
import { ApiError } from '../../lib/api'
import { bonusApi, bonusKeys } from '../../lib/bonusApi'
import type { BonusOperation } from '../../lib/bonusTypes'
import { dateTime } from '../../lib/dates'
import { num } from '../../lib/money'
import { useT, type TranslationKey } from '../../lib/i18n'
import { AccrueModal, CustomerPanel, WriteOffModal } from './CustomerPanel'
import { newKey, OpTypeBadge, Pill, Points, reportError, useBonusPermissions, useParamSetter, type BonusFilters } from './bonusUi'

const LOCAL = ['q', 'type', 'types', 'status', 'source', 'min', 'max', 'page', 'size', 'customerId', 'campaignId']

/**
 * «История операций» (ТЗ «Бонусы» §8): барлық нақты операциялар журналы — өшірілмейді, түзету тек сторно/қолмен операция арқылы.
 * Іздеу: аты-жөні, телефон, карта, операция id, чек нөмірі. Жолды басу — «Детали операции» панелі; әкімшілік әрекеттер құқықпен.
 */
export function HistoryTab({ filters, storeIds }: { filters: BonusFilters; storeIds?: string }) {
  const t = useT()
  const qc = useQueryClient()
  const perms = useBonusPermissions()
  const { params, get, set } = useParamSetter()
  const [action, setAction] = useState<'accrue' | 'writeoff' | null>(null)
  const [customer, setCustomer] = useState<string | null>(null)
  const search = useDebounced(get('q'), 300)
  const page = Number(get('page')) || 1
  const size = Number(get('size')) || 25
  const query = {
    from: filters.from, to: filters.to, storeIds, search: search || undefined, type: get('type') || undefined, types: get('types') || undefined,
    status: get('status') || undefined, source: get('source') || undefined, minAmount: get('min') || undefined, maxAmount: get('max') || undefined,
    customerId: get('customerId') || undefined, campaignId: get('campaignId') || undefined, page, pageSize: size,
  }
  const q = useQuery({ queryKey: ['bonus', 'operations', query], queryFn: () => bonusApi.operations(query), placeholderData: keepPreviousData })
  const openId = get('op')
  const dirty = LOCAL.some((k) => params.has(k) && k !== 'page' && k !== 'size')
  const invalidate = () => { void q.refetch(); void qc.invalidateQueries({ queryKey: bonusKeys.all }) }
  const exportFile = async (format: 'xlsx' | 'csv') => {
    try { await bonusApi.exportFile('operations', format, query); toast(t('stats.exportReady')) } catch (err) { reportError(err, t('stats.exportFailed')) }
  }
  const sel = 'h-10! w-auto! min-w-36 text-[13px]'

  return (
    <div className="flex flex-col gap-3">
      <div className="flex flex-wrap items-center gap-2">
        <SearchInput value={get('q')} onChange={(v) => set({ q: v })} placeholder={t('bon.hist.search')} className="h-10! w-72" />
        <Select className={sel} value={get('type')} onChange={(e) => set({ type: e.target.value, types: null })} aria-label={t('bon.hist.type')}>
          <option value="">{t('bon.allTypes')}</option>
          {['accrual', 'redemption', 'return', 'expiration', 'adjustment', 'reversal'].map((x) => <option key={x} value={x}>{t(`bon.typeGroup.${x}` as TranslationKey)}</option>)}
        </Select>
        <Select className={sel} value={get('status')} onChange={(e) => set({ status: e.target.value })} aria-label={t('bon.hist.status')}>
          <option value="">{t('bon.hist.allStatuses')}</option>
          <option value="posted">{t('bon.hist.st.posted')}</option>
          <option value="reversed">{t('bon.hist.st.reversed')}</option>
        </Select>
        <Select className={sel} value={get('source')} onChange={(e) => set({ source: e.target.value })} aria-label={t('bon.hist.source')}>
          <option value="">{t('bon.hist.allSources')}</option>
          {['sale', 'return', 'pos', 'manual', 'birthday', 'campaign', 'expiry', 'reversal'].map((x) => <option key={x} value={x}>{t(`bon.src.${x}` as TranslationKey)}</option>)}
        </Select>
        <span className="flex items-center gap-1">
          <input inputMode="numeric" value={get('min')} onChange={(e) => set({ min: e.target.value.replace(/\D/g, '') })} placeholder={t('bon.hist.min')} className="h-10 w-20 rounded-xl border border-line bg-surface px-3 text-[13px]" aria-label={t('bon.hist.min')} />
          <span className="text-ink-3">–</span>
          <input inputMode="numeric" value={get('max')} onChange={(e) => set({ max: e.target.value.replace(/\D/g, '') })} placeholder={t('bon.hist.max')} className="h-10 w-20 rounded-xl border border-line bg-surface px-3 text-[13px]" aria-label={t('bon.hist.max')} />
        </span>
        {dirty && <Button variant="ghost" className="h-10! px-3!" icon={<FilterX size={16} />} onClick={() => set(Object.fromEntries(LOCAL.map((k) => [k, null])))}>{t('bon.reset')}</Button>}
        <span className="ml-auto" />
        {perms.export && <><Button variant="secondary" className="h-10! px-3!" icon={<Download size={16} />} onClick={() => void exportFile('xlsx')}>Excel</Button><Button variant="secondary" className="h-10! px-3!" onClick={() => void exportFile('csv')}>CSV</Button></>}
        {perms.writeOff && <Button variant="secondary" className="h-10! px-3!" icon={<MinusCircle size={16} />} onClick={() => setAction('writeoff')}>{t('bon.cust.writeOff')}</Button>}
        {perms.accrue && <Button className="h-10! px-3!" icon={<PlusCircle size={16} />} onClick={() => setAction('accrue')}>{t('bon.hist.accrue')}</Button>}
      </div>
      {get('customerId') && <p className="text-[13px] text-brand">{t('bon.hist.customerFilter')}</p>}

      <div className="overflow-x-auto rounded-2xl border border-line bg-surface">
        <table className="w-full min-w-[1080px]">
          <thead className="bg-field/60"><tr>
            <th className={th}>{t('bon.hist.date')}</th><th className={th}>{t('bon.customer')}</th><th className={th}>{t('bon.hist.phoneCard')}</th><th className={th}>{t('bon.hist.type')}</th>
            <th className={`${th} text-right`}>{t('bon.amount')}</th><th className={`${th} text-right`}>{t('bon.hist.balanceAfter')}</th><th className={th}>{t('bon.hist.basis')}</th>
            <th className={th}>{t('bon.hist.author')}</th><th className={th}>{t('bon.hist.status')}</th><th className={`${th} w-12`}></th>
          </tr></thead>
          <tbody>
            {q.isPending ? <tr><td colSpan={10} className="py-10 text-center text-ink-3"><Loader2 className="mx-auto animate-spin" size={22} /></td></tr>
              : (q.data?.items ?? []).length === 0 ? <EmptyRow colSpan={10} text={t('bon.hist.empty')} /> : q.data!.items.map((o) => (
              <tr key={o.id} className="cursor-pointer border-t border-line hover:bg-field/40" onClick={() => set({ op: o.id }, true)}>
                <td className={`${td} whitespace-nowrap text-ink-2`}>{dateTime(o.createdAt)}</td>
                <td className={td}><button type="button" className="font-medium hover:text-brand" onClick={(e) => { e.stopPropagation(); setCustomer(o.customerId) }}>{o.customerName}</button></td>
                <td className={`${td} text-[13px] text-ink-2`}>{o.phone ?? '—'}</td>
                <td className={td}><OpTypeBadge type={o.type} amount={o.amount} /></td>
                <td className={`${td} text-right`}><Points value={o.amount} signed muted={o.status === 'reversed'} /></td>
                <td className={`${td} text-right tabular-nums text-ink-2`}>{o.balanceAfter === null ? '—' : num(o.balanceAfter)}</td>
                <td className={`${td} max-w-[220px] truncate text-[13px]`}>{o.basis}</td>
                <td className={`${td} text-[13px] text-ink-2`}>{o.author}</td>
                <td className={td}><Pill tone={o.status === 'reversed' ? 'violet' : 'green'}>{t(`bon.hist.st.${o.status}` as TranslationKey)}</Pill></td>
                <td className={td} onClick={(e) => e.stopPropagation()}><RowMenu items={[
                  { label: t('bon.open'), icon: <Eye size={16} />, onClick: () => set({ op: o.id }, true) },
                ]} /></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {q.data && <Pager page={page} pageSize={size} total={q.data.total} onPage={(p) => set({ page: String(p) }, true)} onPageSize={(s) => set({ size: String(s) })} />}
      {openId && <OperationPanel id={openId} onClose={() => set({ op: null }, true)} onChanged={invalidate} onCustomer={setCustomer} />}
      {action === 'accrue' && <AccrueModal onClose={() => setAction(null)} onDone={() => { setAction(null); invalidate() }} />}
      {action === 'writeoff' && <WriteOffModal onClose={() => setAction(null)} onDone={() => { setAction(null); invalidate() }} />}
      {customer && <CustomerPanel customerId={customer} onClose={() => setCustomer(null)} />}
    </div>
  )
}

/** «Детали операции» (§8.2): ID, күн, клиент, түр, сома, баланс до/после, негіз, көз, автор, құжаттар, коммент, түзетулер тарихы. */
function OperationPanel({ id, onClose, onChanged, onCustomer }: { id: string; onClose: () => void; onChanged: () => void; onCustomer: (id: string) => void }) {
  const t = useT()
  const perms = useBonusPermissions()
  const q = useQuery({ queryKey: ['bonus', 'operation', id], queryFn: () => bonusApi.operation(id) })
  const [mode, setMode] = useState<'reverse' | 'return' | null>(null)
  const d = q.data
  const o = d?.operation
  const row = (label: string, value: React.ReactNode) => (
    <div className="flex justify-between gap-4 border-b border-line py-2 text-[14px] last:border-0"><span className="text-ink-2">{label}</span><span className="text-right font-medium">{value ?? '—'}</span></div>
  )
  return (
    <Drawer title={o ? t(`bon.type.${o.type === 'Adjustment' ? (o.amount >= 0 ? 'AdjustmentPlus' : 'AdjustmentMinus') : o.type}` as TranslationKey) : t('bon.hist.details')} subtitle={o?.id} onClose={onClose} width={500}
      footer={o && (
        <>
          {perms.accrue && o.canReturn && (d?.returnableAmount ?? 0) > 0 && <Button variant="secondary" icon={<Undo2 size={16} />} onClick={() => setMode('return')}>{t('bon.hist.return')}</Button>}
          {perms.reverse && o.canReverse && <Button variant="danger" icon={<RotateCcw size={16} />} onClick={() => setMode('reverse')}>{t('bon.hist.reverse')}</Button>}
        </>
      )}>
      {!o ? <div className="py-16 text-center text-ink-3"><Loader2 className="mx-auto animate-spin" size={24} /></div> : (
        <div className="flex flex-col gap-4">
          <div className="flex items-center gap-3"><span className="text-[26px] font-extrabold"><Points value={o.amount} signed muted={o.status === 'reversed'} /></span>
            <OpTypeBadge type={o.type} amount={o.amount} /><Pill tone={o.status === 'reversed' ? 'violet' : 'green'}>{t(`bon.hist.st.${o.status}` as TranslationKey)}</Pill></div>
          <div>
            {row(t('bon.hist.date'), dateTime(o.createdAt))}
            {row(t('bon.customer'), <button type="button" className="hover:text-brand" onClick={() => onCustomer(o.customerId)}>{o.customerName}</button>)}
            {row(t('bon.phone'), o.phone)}
            {row(t('bon.hist.balanceBefore'), o.balanceBefore === null ? '—' : num(o.balanceBefore))}
            {row(t('bon.hist.balanceAfter'), o.balanceAfter === null ? '—' : num(o.balanceAfter))}
            {row(t('bon.hist.basis'), o.basisLink ? <Link to={o.basisLink} className="inline-flex items-center gap-1 text-brand hover:underline">{o.basis}<ExternalLink size={13} /></Link> : o.basis)}
            {row(t('bon.hist.source'), t(`bon.src.${o.source}` as TranslationKey))}
            {row(t('bon.hist.author'), o.author)}
            {o.percent !== null && row(t('bon.st.percent'), `${o.percent}%`)}
            {o.expiresAt && row(t('bon.exp.date'), dateTime(o.expiresAt))}
            {o.amount > 0 && o.type !== 'Reversal' && row(t('bon.exp.remaining'), num(o.remaining))}
            {row(t('bon.hist.store'), o.storeName)}
            {o.comment && row(t('bon.comment'), o.comment)}
            {o.type === 'Redemption' && row(t('bon.hist.returnable'), num(d!.returnableAmount))}
          </div>
          {d!.related.length > 0 && (
            <div>
              <h4 className="mb-2 text-[13px] font-semibold text-ink-2">{t('bon.hist.related')}</h4>
              <ul className="flex flex-col divide-y divide-line rounded-xl border border-line">
                {d!.related.map((r: BonusOperation) => (
                  <li key={r.id} className="flex items-center gap-2 px-3 py-2 text-[13px]"><OpTypeBadge type={r.type} amount={r.amount} /><span className="flex-1 text-ink-2">{dateTime(r.createdAt)} · {r.author}</span><Points value={r.amount} signed /></li>
                ))}
              </ul>
            </div>
          )}
          {!o.canReverse && o.status === 'posted' && (o.type === 'Accrual' || o.type === 'Redemption') && <p className="text-[12px] text-ink-3">{t('bon.hist.saleHint')}</p>}
        </div>
      )}
      {mode === 'reverse' && o && <ReverseModal op={o} onClose={() => setMode(null)} onDone={() => { setMode(null); void q.refetch(); onChanged() }} />}
      {mode === 'return' && o && <ReturnModal op={o} max={d?.returnableAmount ?? 0} onClose={() => setMode(null)} onDone={() => { setMode(null); void q.refetch(); onChanged() }} />}
    </Drawer>
  )
}

/** Сторно (§8.3): себеп, түпнұсқа мен кері операция көрсетіледі; түпнұсқа журналда қалады. */
function ReverseModal({ op, onClose, onDone }: { op: BonusOperation; onClose: () => void; onDone: () => void }) {
  const t = useT()
  const [reason, setReason] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const submit = async () => {
    if (!(await confirmDialog({ message: t('bon.hist.reverseConfirm', { amount: num(Math.abs(op.amount)), sign: op.amount > 0 ? '−' : '+' }), confirmLabel: t('bon.hist.reverse'), danger: true }))) return
    setBusy(true)
    try { await bonusApi.reverse(op.id, reason); toast(t('bon.hist.reversed')); onDone() } catch (err) { setError(err instanceof ApiError ? err.message : t('bon.failed')) } finally { setBusy(false) }
  }
  return (
    <Modal title={t('bon.hist.reverse')} onClose={onClose} width={480}>
      <div className="flex flex-col gap-4">
        <div className="grid grid-cols-2 gap-2 text-[13px]">
          <div className="rounded-xl border border-line px-3 py-2"><div className="text-ink-2">{t('bon.hist.original')}</div><Points value={op.amount} signed /></div>
          <div className="rounded-xl border border-line px-3 py-2"><div className="text-ink-2">{t('bon.hist.reversal')}</div><Points value={-op.amount} signed /></div>
        </div>
        <Field label={t('bon.reason')} required><Textarea autoFocus value={reason} maxLength={500} onChange={(e) => setReason(e.target.value)} /></Field>
        {error && <p className="text-[13px] text-danger">{error}</p>}
        <div className="flex justify-end gap-2"><Button variant="secondary" onClick={onClose}>{t('common.cancel')}</Button><Button variant="danger" loading={busy} disabled={reason.trim().length < 3} onClick={() => void submit()}>{t('bon.hist.reverse')}</Button></div>
      </div>
    </Modal>
  )
}

/** «Вернуть бонусы» (§8.3): бастапқы шегеру бойынша, бұрын қайтарылғанын шегергенде қалған сомадан аспайды. */
function ReturnModal({ op, max, onClose, onDone }: { op: BonusOperation; max: number; onClose: () => void; onDone: () => void }) {
  const t = useT()
  const [amount, setAmount] = useState(String(max))
  const [reason, setReason] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const key = useRef(newKey())
  const sum = Number(amount) || 0
  const submit = async () => {
    setBusy(true)
    try { await bonusApi.returnBonus(op.id, sum, reason, key.current); toast(t('bon.hist.returned')); onDone() } catch (err) { setError(err instanceof ApiError ? err.message : t('bon.failed')) } finally { setBusy(false) }
  }
  return (
    <Modal title={t('bon.hist.return')} onClose={onClose} width={460}>
      <div className="flex flex-col gap-4">
        <p className="text-[13px] text-ink-2">{t('bon.hist.returnHint', { original: num(-op.amount), max: num(max) })}</p>
        <Field label={t('bon.amount')} required error={sum > max ? t('bon.hist.returnMax', { max: num(max) }) : null}><Input inputMode="numeric" value={amount} onChange={(e) => setAmount(e.target.value.replace(/\D/g, ''))} error={sum > max} /></Field>
        <Field label={t('bon.reason')} required><Textarea value={reason} maxLength={500} onChange={(e) => setReason(e.target.value)} className="min-h-16!" /></Field>
        {error && <p className="text-[13px] text-danger">{error}</p>}
        <div className="flex justify-end gap-2"><Button variant="secondary" onClick={onClose}>{t('common.cancel')}</Button><Button loading={busy} disabled={sum < 1 || sum > max || reason.trim().length < 3} onClick={() => void submit()}>{t('bon.hist.return')}</Button></div>
      </div>
    </Modal>
  )
}
