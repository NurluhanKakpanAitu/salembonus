import { useMemo, useRef, useState } from 'react'
import { Paperclip, Plus } from 'lucide-react'
import { Button } from '../ui/Button'
import { Field, Input, Select, Textarea } from '../ui/Field'
import { Modal } from '../ui/Modal'
import { toast } from '../ui/Toast'
import { confirmDialog } from '../ui/Confirm'
import { ApiError } from '../../lib/api'
import { financeApi } from '../../lib/financeApi'
import type { CounterpartyType, FinanceAccount, FinanceCategory, FinanceOperation, PnlClass } from '../../lib/financeTypes'
import { tenge } from '../../lib/money'
import { uploadImage } from '../../lib/upload'
import { useT, type TranslationKey } from '../../lib/i18n'
import { reportError, useFinancePermissions } from './financeUi'

const nowLocal = () => {
  const d = new Date()
  const p = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}T${p(d.getHours())}:${p(d.getMinutes())}`
}
const toIso = (local: string) => (local ? new Date(local).toISOString() : null)
const fromIso = (iso: string) => {
  const d = new Date(iso)
  const p = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}T${p(d.getHours())}:${p(d.getMinutes())}`
}

/**
 * Қолмен операция (ТЗ «Финансы» §7): кіріс/шығыс — тип, дата, санат, сипаттама, сома, шот, контрагент, құжат, вложение, коммент.
 * «Сохранить черновик» қалдықты қозғамайды; «Провести» — серверде атомарлы тексеру (құқық, сома, шот, қалдық, дубль).
 */
export function OperationForm({ accounts, categories, initial, kind, onClose, onSaved, onCategoriesChanged }: {
  accounts: FinanceAccount[]; categories: FinanceCategory[]; initial?: FinanceOperation | null; kind?: 'Income' | 'Expense'
  onClose: () => void; onSaved: (op: FinanceOperation) => void; onCategoriesChanged: () => void
}) {
  const t = useT()
  const { canManage } = useFinancePermissions()
  const [type, setType] = useState<'Income' | 'Expense'>(initial?.type === 'Income' ? 'Income' : initial?.type === 'Expense' ? 'Expense' : kind ?? 'Expense')
  const [date, setDate] = useState(initial ? fromIso(initial.date) : nowLocal())
  const [categoryId, setCategoryId] = useState(initial?.categoryId ?? '')
  const [description, setDescription] = useState(initial?.description ?? '')
  const [amount, setAmount] = useState(initial ? String(initial.amount) : '')
  const [accountId, setAccountId] = useState(initial?.fromAccountId ?? initial?.toAccountId ?? accounts.find((a) => a.isActive)?.id ?? '')
  const [cpType, setCpType] = useState<CounterpartyType | ''>(initial?.counterpartyType ?? '')
  const [cpName, setCpName] = useState(initial?.counterpartyName ?? '')
  const [documentNumber, setDocumentNumber] = useState(initial?.documentNumber ?? '')
  const [attachmentUrl, setAttachmentUrl] = useState(initial?.attachmentUrl ?? '')
  const [comment, setComment] = useState(initial?.comment ?? '')
  const [uploading, setUploading] = useState(false)
  const [busy, setBusy] = useState<'draft' | 'post' | null>(null)
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [newCategory, setNewCategory] = useState(false)
  const requestId = useRef(crypto.randomUUID())
  const fileRef = useRef<HTMLInputElement>(null)
  const dirty = useRef(false)
  const mark = <T,>(set: (v: T) => void) => (v: T) => { dirty.current = true; set(v) }

  const cats = useMemo(() => categories.filter((c) => c.isActive && c.type === type), [categories, type])
  const account = accounts.find((a) => a.id === accountId)
  const sum = Number(amount.replace(',', '.')) || 0
  const after = account ? (type === 'Income' ? account.balance + sum : account.balance - sum) : null

  const validate = (posting: boolean) => {
    const e: Record<string, string> = {}
    if (!categoryId) e.categoryId = t('fin.err.category')
    if (description.trim().length < 3 || description.trim().length > 250) e.description = t('fin.err.description')
    if (!(sum > 0)) e.amount = t('fin.err.amount')
    if (posting && !accountId) e.accountId = t('fin.err.account')
    if (posting && type === 'Expense' && account && account.balance < sum) e.amount = t('fin.err.insufficient', { balance: tenge(account.balance) })
    if (cpType && !cpName.trim()) e.counterpartyName = t('fin.err.counterparty')
    setErrors(e)
    return Object.keys(e).length === 0
  }

  const submit = async (post: boolean) => {
    if (!validate(post)) return
    setBusy(post ? 'post' : 'draft')
    try {
      const body = {
        type, date: toIso(date), categoryId: categoryId || null, description: description.trim(), amount: sum, accountId: accountId || null,
        counterpartyType: cpType || null, counterpartyName: cpName.trim() || null, documentNumber: documentNumber.trim() || null,
        attachmentUrl: attachmentUrl || null, comment: comment.trim() || null, clientRequestId: requestId.current, post,
      }
      let op: FinanceOperation
      if (initial) {
        op = await financeApi.updateDraft(initial.id, body)
      } else op = await financeApi.createOperation(body)
      toast(post ? t('fin.posted') : t('fin.draftSaved'))
      onSaved(op)
    } catch (err) {
      if (err instanceof ApiError && err.field) setErrors({ [err.field]: err.message })
      reportError(err, t('fin.failed'))
    } finally { setBusy(null) }
  }

  const close = async () => {
    if (dirty.current && !(await confirmDialog({ message: t('fin.discardChanges'), confirmLabel: t('fin.discard'), danger: true }))) return
    onClose()
  }

  const pick = async (file: File | undefined) => {
    if (!file) return
    if (!['image/jpeg', 'image/png', 'image/webp', 'application/pdf'].includes(file.type)) { toast(t('fin.err.fileType'), 'error'); return }
    setUploading(true)
    try { setAttachmentUrl(await uploadImage(file, 'finance')); dirty.current = true } catch (err) { reportError(err, t('fin.err.upload')) } finally { setUploading(false) }
  }

  return (
    <Modal title={initial ? `${t('fin.editDraft')} · ${initial.number}` : type === 'Income' ? t('fin.newIncome') : t('fin.newExpense')} onClose={() => void close()} width={720}>
      <form onSubmit={(e) => { e.preventDefault(); void submit(true) }} className="flex flex-col gap-4">
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <Field label={t('fin.col.type')} required>
            <Select value={type} onChange={(e) => { mark(setType)(e.target.value as 'Income' | 'Expense'); setCategoryId('') }}>
              <option value="Income">{t('fin.type.Income')}</option>
              <option value="Expense">{t('fin.type.Expense')}</option>
            </Select>
          </Field>
          <Field label={t('fin.col.date')} required error={errors.date}>
            <Input type="datetime-local" value={date} max={nowLocal()} onChange={(e) => mark(setDate)(e.target.value)} error={!!errors.date} />
          </Field>
          <Field label={t('fin.col.category')} required error={errors.categoryId}>
            <div className="flex gap-2">
              <Select value={categoryId} onChange={(e) => mark(setCategoryId)(e.target.value)} error={!!errors.categoryId} className="flex-1">
                <option value="">{t('fin.chooseCategory')}</option>
                {cats.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
              </Select>
              {canManage && <Button type="button" variant="secondary" className="h-11! px-3!" title={t('fin.addCategory')} onClick={() => setNewCategory(true)}><Plus size={18} /></Button>}
            </div>
          </Field>
          <Field label={t('fin.col.amount')} required error={errors.amount}>
            <Input inputMode="decimal" value={amount} onChange={(e) => mark(setAmount)(e.target.value)} placeholder="0" error={!!errors.amount} />
          </Field>
          <Field label={t('fin.account')} required hint={account ? `${t('fin.balance')}: ${tenge(account.balance)}${after !== null && sum > 0 ? ` → ${tenge(after)}` : ''}` : undefined} error={errors.accountId}>
            <Select value={accountId} onChange={(e) => mark(setAccountId)(e.target.value)} error={!!errors.accountId}>
              <option value="">{t('fin.chooseAccount')}</option>
              {accounts.filter((a) => a.isActive).map((a) => <option key={a.id} value={a.id}>{a.name}</option>)}
            </Select>
          </Field>
          <Field label={t('fin.col.document')}>
            <Input value={documentNumber} maxLength={64} onChange={(e) => mark(setDocumentNumber)(e.target.value)} placeholder={t('fin.documentHint')} />
          </Field>
          <Field label={t('fin.col.counterparty')}>
            <Select value={cpType} onChange={(e) => mark(setCpType)(e.target.value as CounterpartyType | '')}>
              <option value="">—</option>
              {(['Customer', 'Supplier', 'Staff', 'Other'] as CounterpartyType[]).map((c) => <option key={c} value={c}>{t(`fin.cp.${c}` as TranslationKey)}</option>)}
            </Select>
          </Field>
          <Field label={t('fin.counterpartyName')} error={errors.counterpartyName}>
            <Input value={cpName} maxLength={200} onChange={(e) => mark(setCpName)(e.target.value)} error={!!errors.counterpartyName} disabled={!cpType} />
          </Field>
        </div>
        <Field label={t('fin.col.description')} required error={errors.description}>
          <Input value={description} maxLength={250} onChange={(e) => mark(setDescription)(e.target.value)} error={!!errors.description} placeholder={t('fin.descriptionHint')} />
        </Field>
        <Field label={t('fin.comment')}>
          <Textarea value={comment} maxLength={1000} onChange={(e) => mark(setComment)(e.target.value)} className="min-h-16!" />
        </Field>
        <div className="flex flex-wrap items-center gap-3">
          <input ref={fileRef} type="file" accept="image/jpeg,image/png,image/webp,application/pdf" className="hidden" onChange={(e) => void pick(e.target.files?.[0])} />
          <Button type="button" variant="secondary" icon={<Paperclip size={16} />} loading={uploading} onClick={() => fileRef.current?.click()}>{t('fin.attachFile')}</Button>
          {attachmentUrl && (
            <span className="flex items-center gap-2 text-[13px]">
              <a href={attachmentUrl} target="_blank" rel="noopener" className="text-brand underline">{t('fin.attachment')}</a>
              <button type="button" className="text-ink-3 hover:text-danger" onClick={() => { setAttachmentUrl(''); dirty.current = true }}>×</button>
            </span>
          )}
          <span className="text-[12px] text-ink-3">{t('fin.attachHint')}</span>
        </div>
        <div className="flex flex-wrap justify-end gap-2 border-t border-line pt-4">
          <Button type="button" variant="ghost" onClick={() => void close()}>{t('common.cancel')}</Button>
          <Button type="button" variant="secondary" loading={busy === 'draft'} disabled={busy !== null} onClick={() => void submit(false)}>{t('fin.saveDraft')}</Button>
          <Button type="submit" loading={busy === 'post'} disabled={busy !== null}>{t('fin.post')}</Button>
        </div>
      </form>
      {newCategory && <CategoryForm type={type} onClose={() => setNewCategory(false)} onSaved={(c) => { setNewCategory(false); onCategoriesChanged(); setCategoryId(c.id) }} />}
    </Modal>
  )
}

/** Санат (§15.1): аты, түрі, P&L-классификациясы. Мұрағаттау — тізімнен кетеді, тарихта қалады. */
export function CategoryForm({ type, initial, onClose, onSaved }: {
  type: 'Income' | 'Expense'; initial?: FinanceCategory | null; onClose: () => void; onSaved: (c: FinanceCategory) => void
}) {
  const t = useT()
  const [name, setName] = useState(initial?.name ?? '')
  const [catType, setCatType] = useState<'Income' | 'Expense'>(initial?.type ?? type)
  const [pnl, setPnl] = useState<PnlClass>(initial?.pnlClass ?? (type === 'Income' ? 'OtherIncome' : 'OperatingExpense'))
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const classes: PnlClass[] = catType === 'Income' ? ['OtherIncome', 'NotInPnl'] : ['OperatingExpense', 'OtherExpense', 'Tax', 'NotInPnl']
  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (name.trim().length < 2) { setError(t('common.required')); return }
    setBusy(true)
    try {
      const body = { name: name.trim(), type: catType, pnlClass: classes.includes(pnl) ? pnl : classes[0] }
      onSaved(initial ? await financeApi.updateCategory(initial.id, body) : await financeApi.createCategory(body))
      toast(t('fin.saved'))
    } catch (err) { setError(err instanceof ApiError ? err.message : t('fin.failed')) } finally { setBusy(false) }
  }
  return (
    <Modal title={initial ? t('fin.editCategory') : t('fin.addCategory')} onClose={onClose} width={460}>
      <form onSubmit={submit} className="flex flex-col gap-4">
        <Field label={t('fin.categoryName')} required error={error}>
          <Input autoFocus value={name} maxLength={120} onChange={(e) => setName(e.target.value)} error={!!error} />
        </Field>
        <Field label={t('fin.col.type')} required>
          <Select value={catType} disabled={!!initial} onChange={(e) => { const v = e.target.value as 'Income' | 'Expense'; setCatType(v); setPnl(v === 'Income' ? 'OtherIncome' : 'OperatingExpense') }}>
            <option value="Income">{t('fin.type.Income')}</option>
            <option value="Expense">{t('fin.type.Expense')}</option>
          </Select>
        </Field>
        <Field label={t('fin.pnlClass')} required hint={t('fin.pnlClassHint')}>
          <Select value={pnl} onChange={(e) => setPnl(e.target.value as PnlClass)}>
            {classes.map((c) => <option key={c} value={c}>{t(`fin.pnl.class.${c}` as TranslationKey)}</option>)}
          </Select>
        </Field>
        <div className="flex justify-end gap-2">
          <Button type="button" variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" loading={busy}>{t('common.save')}</Button>
        </div>
      </form>
    </Modal>
  )
}
