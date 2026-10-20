import { useRef, useState } from 'react'
import { keepPreviousData, useQuery, useQueryClient } from '@tanstack/react-query'
import { Copy, Pencil, Plus, Power, Trash2, Users } from 'lucide-react'
import { Button } from '../ui/Button'
import { Field, Input, Select, Textarea, Toggle } from '../ui/Field'
import { Drawer } from '../ui/Drawer'
import { Modal } from '../ui/Modal'
import { RowMenu } from '../ui/RowMenu'
import { SearchInput } from '../ui/SearchInput'
import { toast } from '../ui/Toast'
import { confirmDialog } from '../ui/Confirm'
import { EmptyRow, Pager, td, th, useDebounced } from '../inventory/inventoryUi'
import { ApiError } from '../../lib/api'
import { bonusApi, bonusKeys } from '../../lib/bonusApi'
import { STATUS_COLORS, STATUS_ICONS, STATUS_PERIODS, STATUS_RULES, type BonusStatus, type SaveStatus, type StatusPeriod, type StatusRule } from '../../lib/bonusTypes'
import { dateTime } from '../../lib/dates'
import { num, tenge } from '../../lib/money'
import { useT, type TranslationKey } from '../../lib/i18n'
import { BlockError, Skeleton } from '../stats/statsUi'
import { CustomerPanel } from './CustomerPanel'
import { Pill, Points, reportError, STATUS_ICON, StatusBadge, useBonusPermissions, useParamSetter } from './bonusUi'

/**
 * «Статусы клиентов» (ТЗ «Бонусы» §3): деңгейлер, өту шарты, кезең, пайыз, клиент саны (басқанда — тізім), белсенділік.
 * Өзгерту — оң жақ панельде; көшірме — өшірулі черновик; өшіру — растаумен, тарих сақталады; жою — тек ешкімде жоқ мәртебе.
 */
export function StatusesTab() {
  const t = useT()
  const qc = useQueryClient()
  const perms = useBonusPermissions()
  const { get, set } = useParamSetter()
  const [editing, setEditing] = useState<BonusStatus | 'new' | null>(null)
  const q = useQuery({ queryKey: ['bonus', 'statuses'], queryFn: bonusApi.statuses })
  const invalidate = () => { void q.refetch(); void qc.invalidateQueries({ queryKey: bonusKeys.all }) }
  const condition = (s: BonusStatus) => s.rule === 'PurchaseAmount' ? t('bon.st.condAmount', { v: tenge(s.thresholdAmount ?? 0) })
    : s.rule === 'PurchaseCount' ? t('bon.st.condCount', { n: s.thresholdCount ?? 0 }) : t(`bon.st.rule.${s.rule}` as TranslationKey)

  const act = async (s: BonusStatus, kind: 'duplicate' | 'toggle' | 'delete') => {
    try {
      if (kind === 'duplicate') { await bonusApi.duplicateStatus(s.id); toast(t('bon.st.duplicated')) }
      if (kind === 'toggle') {
        if (s.isActive && !(await confirmDialog({ message: t('bon.st.deactivateConfirm', { name: s.name, n: s.customers }), confirmLabel: t('bon.st.deactivate'), danger: true }))) return
        await bonusApi.setStatusActive(s.id, !s.isActive)
        toast(s.isActive ? t('bon.st.deactivated') : t('bon.st.activated'))
      }
      if (kind === 'delete') {
        if (s.customers > 0) {
          if (await confirmDialog({ message: t('bon.st.inUse', { n: s.customers }), confirmLabel: t('bon.st.deactivate') })) { await bonusApi.setStatusActive(s.id, false); toast(t('bon.st.deactivated')) }
          invalidate()
          return
        }
        if (!(await confirmDialog({ message: t('bon.st.deleteConfirm', { name: s.name }), confirmLabel: t('bon.delete'), danger: true }))) return
        await bonusApi.deleteStatus(s.id)
        toast(t('bon.st.deleted'))
      }
      invalidate()
    } catch (err) { reportError(err, t('bon.failed')) }
  }

  if (q.isError) return <BlockError message={(q.error as Error).message} onRetry={() => void q.refetch()} />
  return (
    <div className="flex flex-col gap-3">
      <div className="flex flex-wrap items-center gap-2">
        <p className="mr-auto text-[13px] text-ink-2">{t('bon.st.hint')}</p>
        <Button variant="secondary" className="h-10! px-3!" icon={<Users size={16} />} onClick={() => set({ customers: '1' }, true)}>{t('bon.st.allCustomers')}</Button>
        {perms.manage && <Button className="h-10! px-3!" icon={<Plus size={16} />} onClick={() => setEditing('new')}>{t('bon.st.add')}</Button>}
      </div>
      <div className="overflow-x-auto rounded-2xl border border-line bg-surface">
        <table className="w-full min-w-[920px]">
          <thead className="bg-field/60"><tr>
            <th className={`${th} w-10`}>№</th><th className={th}>{t('bon.st.name')}</th><th className={th}>{t('bon.st.condition')}</th><th className={th}>{t('bon.st.period')}</th>
            <th className={`${th} text-right`}>{t('bon.st.percent')}</th><th className={`${th} text-right`}>{t('bon.st.customers')}</th><th className={th}>{t('bon.st.state')}</th><th className={`${th} w-12`}></th>
          </tr></thead>
          <tbody>
            {!q.data ? <tr><td colSpan={8} className="p-4"><Skeleton className="h-24" /></td></tr> : q.data.length === 0 ? <EmptyRow colSpan={8} text={t('bon.empty')} /> : q.data.map((s, i) => (
              <tr key={s.id} className={`border-t border-line hover:bg-field/40 ${s.isActive ? '' : 'opacity-60'}`}>
                <td className={`${td} text-ink-3`}>{i + 1}</td>
                <td className={td}>
                  <StatusBadge name={s.name} color={s.color} icon={s.icon} />
                  {s.description && <div className="mt-0.5 max-w-xs truncate text-[12px] text-ink-3">{s.description}</div>}
                </td>
                <td className={`${td} text-ink-2`}>{condition(s)}{s.validityDays ? <span className="block text-[12px] text-ink-3">{t('bon.st.validityLine', { n: s.validityDays })}{s.autoDowngrade ? ` · ${t('bon.st.autoDowngrade')}` : ''}</span> : null}</td>
                <td className={`${td} text-ink-2`}>{s.rule === 'Registration' || s.rule === 'Manual' ? '—' : t(`bon.st.periodOpt.${s.period}` as TranslationKey)}</td>
                <td className={`${td} text-right font-bold tabular-nums`}>{s.accrualPercent}%</td>
                <td className={`${td} text-right`}>
                  <button type="button" className="font-semibold tabular-nums text-brand hover:underline" onClick={() => set({ customers: '1', status: s.id }, true)}>{num(s.customers)}</button>
                </td>
                <td className={td}><Pill tone={s.isActive ? 'green' : 'gray'}>{s.isActive ? t('bon.active') : t('bon.inactive')}</Pill></td>
                <td className={td}>
                  {perms.manage && <RowMenu items={[
                    { label: t('bon.edit'), icon: <Pencil size={16} />, onClick: () => setEditing(s) },
                    { label: t('bon.st.duplicate'), icon: <Copy size={16} />, onClick: () => void act(s, 'duplicate') },
                    { label: s.isActive ? t('bon.st.deactivate') : t('bon.st.activate'), icon: <Power size={16} />, danger: s.isActive, onClick: () => void act(s, 'toggle') },
                    { label: t('bon.delete'), icon: <Trash2 size={16} />, danger: true, onClick: () => void act(s, 'delete') },
                  ]} />}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <p className="text-[12px] text-ink-3">{t('bon.st.rankHint')}</p>
      {editing && <StatusForm initial={editing === 'new' ? null : editing} onClose={() => setEditing(null)} onSaved={() => { setEditing(null); invalidate() }} />}
      {get('customers') && <CustomersModal statuses={q.data ?? []} onClose={() => set({ customers: null, status: null, q: null }, true)} />}
    </div>
  )
}

const EMPTY: SaveStatus = {
  name: '', icon: 'star', color: '#1769FF', rule: 'PurchaseAmount', thresholdAmount: 100000, thresholdCount: null, period: 'AllTime', accrualPercent: 3,
  countReturns: true, validityDays: null, autoDowngrade: false, description: null, isActive: true,
}

/** «Новый статус» / өңдеу (§3.2): оң жақ панель, бекітілген «Отмена»/«Сохранить», өзгеріс болса — жабуды растау. */
function StatusForm({ initial, onClose, onSaved }: { initial: BonusStatus | null; onClose: () => void; onSaved: () => void }) {
  const t = useT()
  const [f, setF] = useState<SaveStatus>(initial ? {
    name: initial.name, icon: initial.icon, color: initial.color, rule: initial.rule, thresholdAmount: initial.thresholdAmount, thresholdCount: initial.thresholdCount,
    period: initial.period, accrualPercent: initial.accrualPercent, countReturns: initial.countReturns, validityDays: initial.validityDays,
    autoDowngrade: initial.autoDowngrade, description: initial.description, isActive: initial.isActive,
  } : EMPTY)
  const [busy, setBusy] = useState(false)
  const [errors, setErrors] = useState<Record<string, string>>({})
  const dirty = useRef(false)
  const patch = (p: Partial<SaveStatus>) => { dirty.current = true; setF((x) => ({ ...x, ...p })) }
  const close = async () => {
    if (dirty.current && !(await confirmDialog({ title: t('bon.unsavedTitle'), message: t('bon.unsaved'), confirmLabel: t('bon.closeWithoutSaving'), cancelLabel: t('bon.stay'), danger: true }))) return
    onClose()
  }
  const submit = async () => {
    const e: Record<string, string> = {}
    if (f.name.trim().length < 2 || f.name.trim().length > 60) e.name = t('bon.st.errName')
    if (f.rule === 'PurchaseAmount' && !(Number(f.thresholdAmount) >= 0 && f.thresholdAmount !== null)) e.thresholdAmount = t('bon.st.errThreshold')
    if (f.rule === 'PurchaseCount' && !(Number(f.thresholdCount) >= 1)) e.thresholdCount = t('bon.st.errThreshold')
    if (!(f.accrualPercent >= 0 && f.accrualPercent <= 50)) e.accrualPercent = t('bon.st.errPercent')
    setErrors(e)
    if (Object.keys(e).length) return
    setBusy(true)
    try {
      const body = { ...f, name: f.name.trim() }
      if (initial) await bonusApi.updateStatus(initial.id, body); else await bonusApi.createStatus(body)
      toast(t('bon.saved'))
      onSaved()
    } catch (err) {
      if (err instanceof ApiError && err.field) setErrors({ [err.field]: err.message })
      reportError(err, t('bon.failed'))
    } finally { setBusy(false) }
  }
  return (
    <Drawer title={initial ? t('bon.st.editTitle') : t('bon.st.newTitle')} onClose={() => void close()}
      footer={<><Button variant="secondary" onClick={() => void close()}>{t('common.cancel')}</Button><Button loading={busy} onClick={() => void submit()}>{t('common.save')}</Button></>}>
      <div className="flex flex-col gap-4">
        <div className="flex items-center gap-2 rounded-xl bg-field px-3 py-3"><span className="text-[12px] text-ink-2">{t('bon.st.preview')}:</span><StatusBadge name={f.name || t('bon.st.name')} color={f.color} icon={f.icon} /></div>
        <Field label={t('bon.st.name')} required error={errors.name}><Input autoFocus value={f.name} maxLength={60} onChange={(e) => patch({ name: e.target.value })} error={!!errors.name} /></Field>
        <Field label={t('bon.st.icon')}>
          <div className="flex flex-wrap gap-1.5">
            {STATUS_ICONS.map((i) => (
              <button key={i} type="button" onClick={() => patch({ icon: i })} aria-label={i}
                className={`flex size-9 items-center justify-center rounded-lg border ${f.icon === i ? 'border-brand bg-brand-soft text-brand' : 'border-line text-ink-2 hover:border-brand'}`}>{STATUS_ICON[i]}</button>
            ))}
          </div>
        </Field>
        <Field label={t('bon.st.color')}>
          <div className="flex flex-wrap items-center gap-1.5">
            {STATUS_COLORS.map((c) => (
              <button key={c} type="button" onClick={() => patch({ color: c })} aria-label={c} className={`size-8 rounded-full border-2 ${f.color.toUpperCase() === c ? 'border-ink' : 'border-transparent'}`} style={{ backgroundColor: c }} />
            ))}
            <Input className="h-9! w-28" value={f.color} maxLength={7} onChange={(e) => patch({ color: e.target.value })} />
          </div>
        </Field>
        <Field label={t('bon.st.ruleLabel')} required>
          <Select value={f.rule} onChange={(e) => patch({ rule: e.target.value as StatusRule })}>
            {STATUS_RULES.map((r) => <option key={r} value={r}>{t(`bon.st.rule.${r}` as TranslationKey)}</option>)}
          </Select>
        </Field>
        {f.rule === 'PurchaseAmount' && (
          <Field label={t('bon.st.minAmount')} required error={errors.thresholdAmount}>
            <Input inputMode="decimal" value={f.thresholdAmount ?? ''} onChange={(e) => patch({ thresholdAmount: e.target.value === '' ? null : Number(e.target.value.replace(',', '.')) })} error={!!errors.thresholdAmount} />
          </Field>
        )}
        {f.rule === 'PurchaseCount' && (
          <Field label={t('bon.st.minCount')} required error={errors.thresholdCount}>
            <Input inputMode="numeric" value={f.thresholdCount ?? ''} onChange={(e) => patch({ thresholdCount: e.target.value === '' ? null : Number(e.target.value.replace(/\D/g, '')) })} error={!!errors.thresholdCount} />
          </Field>
        )}
        {(f.rule === 'PurchaseAmount' || f.rule === 'PurchaseCount') && (
          <>
            <Field label={t('bon.st.period')}>
              <Select value={f.period} onChange={(e) => patch({ period: e.target.value as StatusPeriod })}>
                {STATUS_PERIODS.map((p) => <option key={p} value={p}>{t(`bon.st.periodOpt.${p}` as TranslationKey)}</option>)}
              </Select>
            </Field>
            <Toggle checked={f.countReturns} onChange={(v) => patch({ countReturns: v })} label={t('bon.st.countReturns')} />
          </>
        )}
        <Field label={t('bon.st.percent')} required error={errors.accrualPercent} hint={t('bon.st.percentHint')}>
          <Input inputMode="decimal" value={f.accrualPercent} onChange={(e) => patch({ accrualPercent: Number(e.target.value.replace(',', '.')) || 0 })} error={!!errors.accrualPercent} />
        </Field>
        <Toggle checked={f.validityDays !== null} onChange={(v) => patch({ validityDays: v ? 365 : null, autoDowngrade: v ? f.autoDowngrade : false })} label={t('bon.st.hasValidity')} />
        {f.validityDays !== null && (
          <>
            <Field label={t('bon.st.validityDays')}><Input inputMode="numeric" value={f.validityDays} onChange={(e) => patch({ validityDays: Number(e.target.value.replace(/\D/g, '')) || 1 })} /></Field>
            <Toggle checked={f.autoDowngrade} onChange={(v) => patch({ autoDowngrade: v })} label={t('bon.st.autoDowngradeLabel')} />
          </>
        )}
        <Field label={t('bon.st.description')}><Textarea value={f.description ?? ''} maxLength={1000} onChange={(e) => patch({ description: e.target.value || null })} /></Field>
        <Toggle checked={f.isActive} onChange={(v) => patch({ isActive: v })} label={t('bon.st.activeLabel')} />
        <p className="text-[12px] text-ink-3">{t('bon.st.noRetro')}</p>
      </div>
    </Drawer>
  )
}

/** Мәртебедегі клиенттер (§3.1 «клик по числу клиентов»): іздеу, сүзгі, беттеу; жолды басу — бонус карточкасы. */
function CustomersModal({ statuses, onClose }: { statuses: BonusStatus[]; onClose: () => void }) {
  const t = useT()
  const { get, set } = useParamSetter()
  const [customer, setCustomer] = useState<string | null>(null)
  const search = useDebounced(get('q'), 300)
  const page = Number(get('page')) || 1
  const statusId = get('status') || undefined
  const q = useQuery({ queryKey: ['bonus', 'customers', search, statusId, page], queryFn: () => bonusApi.customers({ search, statusId, page, pageSize: 25 }), placeholderData: keepPreviousData })
  return (
    <Modal title={t('bon.st.customersTitle')} onClose={onClose} width={900}>
      <div className="flex flex-col gap-3">
        <div className="flex flex-wrap gap-2">
          <SearchInput value={get('q')} onChange={(v) => set({ q: v })} placeholder={t('bon.cust.searchPlaceholder')} className="h-10! flex-1" />
          <Select className="h-10! w-auto! min-w-44" value={statusId ?? ''} onChange={(e) => set({ status: e.target.value })}>
            <option value="">{t('bon.allStatuses')}</option>
            {statuses.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
          </Select>
        </div>
        <div className="max-h-[60vh] overflow-auto rounded-xl border border-line">
          <table className="w-full min-w-[680px]">
            <thead className="sticky top-0 bg-field/90"><tr><th className={th}>{t('bon.customer')}</th><th className={th}>{t('bon.phone')}</th><th className={th}>{t('bon.st.name')}</th><th className={`${th} text-right`}>{t('bon.cust.balance')}</th><th className={`${th} text-right`}>{t('bon.cust.spent')}</th><th className={th}>{t('bon.cust.lastOperation')}</th></tr></thead>
            <tbody>
              {!q.data ? null : q.data.items.length === 0 ? <EmptyRow colSpan={6} text={t('bon.noCustomers')} /> : q.data.items.map((c) => (
                <tr key={c.customerId} className="cursor-pointer border-t border-line hover:bg-field/40" onClick={() => setCustomer(c.customerId)}>
                  <td className={`${td} font-medium`}>{c.name}</td>
                  <td className={`${td} text-ink-2`}>{c.phone ?? '—'}</td>
                  <td className={td}><StatusBadge name={c.statusName} color={c.statusColor} /></td>
                  <td className={`${td} text-right`}><Points value={c.balance} /></td>
                  <td className={`${td} text-right tabular-nums text-ink-2`}>{tenge(c.totalSpent)}</td>
                  <td className={`${td} text-[13px] text-ink-2`}>{c.lastOperationAt ? dateTime(c.lastOperationAt) : '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {q.data && <Pager page={page} pageSize={25} total={q.data.total} onPage={(p) => set({ page: String(p) }, true)} onPageSize={() => undefined} />}
      </div>
      {customer && <CustomerPanel customerId={customer} onClose={() => setCustomer(null)} />}
    </Modal>
  )
}
