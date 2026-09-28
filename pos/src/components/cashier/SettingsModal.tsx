import { useEffect, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Banknote, Bell, CreditCard, HandCoins, KeyRound, Landmark, Lock, LockKeyhole, Monitor, Pencil, Plus, QrCode,
  ReceiptText, Shuffle, Store, Trash2, UserRound } from 'lucide-react'
import { Button } from '../ui/Button'
import { Field, Input, Select, Toggle } from '../ui/Field'
import { Modal } from '../ui/Modal'
import { toast } from '../ui/Toast'
import { PasswordModal, PinModal } from '../CredentialModals'
import { ApiError } from '../../lib/api'
import { useAuth } from '../../lib/auth'
import { cashierApi } from '../../lib/cashierApi'
import type { CashierSettings, PaymentMethod, TransferRecipient } from '../../lib/cashierTypes'
import { loadRegister } from '../../lib/register'
import { useT, type TranslationKey } from '../../lib/i18n'

const METHODS: { m: PaymentMethod; icon: typeof Banknote }[] = [
  { m: 'Cash', icon: Banknote }, { m: 'Card', icon: CreditCard }, { m: 'Qr', icon: QrCode },
  { m: 'Transfer', icon: Landmark }, { m: 'Debt', icon: HandCoins },
]
const NOTIFY = ['notifyOutOfStock', 'notifyLowStock', 'notifyDebt', 'notifyReturn', 'notifyCashierChange'] as const
const LOCK_OPTIONS = [5, 10, 15, 30, 60]

type Draft = Omit<CashierSettings, 'storeName' | 'storeAddress' | 'registerId' | 'recipients' | 'canManage'>

/**
 * Касса баптаулары (ТЗ «Касса» §17). Дүкен атауы тек көрінеді, чек нөмірі мен бонус пайыздары мұнда жоқ.
 * Дүкен деңгейіндегі баптауды тек құқығы барлар өзгертеді; кассир өз құпиясөзі мен PIN-ін ғана.
 */
export function SettingsModal({ onClose }: { onClose: () => void }) {
  const t = useT()
  const qc = useQueryClient()
  const me = useAuth((s) => s.me)
  const { data } = useQuery({ queryKey: ['cashier', 'settings'], queryFn: cashierApi.settings })
  const [draft, setDraft] = useState<Draft | null>(null)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [modal, setModal] = useState<'pin' | 'password' | null>(null)
  const [recipient, setRecipient] = useState<TransferRecipient | 'new' | null>(null)

  useEffect(() => {
    if (data && !draft) {
      const { storeName: _s, storeAddress: _a, registerId: _r, recipients: _p, canManage: _c, ...rest } = data
      setDraft(rest)
    }
  }, [data, draft])

  if (!data || !draft)
    return <Modal title={t('settings.title')} onClose={onClose} width={980}><p className="py-10 text-center text-ink-3">{t('common.loading')}</p></Modal>

  const can = data.canManage
  const patch = (p: Partial<Draft>) => setDraft({ ...draft, ...p })
  const toggleMethod = (m: PaymentMethod, on: boolean) =>
    patch({ enabledMethods: on ? [...draft.enabledMethods, m] : draft.enabledMethods.filter((x) => x !== m) })

  const save = async () => {
    setSaving(true)
    setError(null)
    try {
      await cashierApi.updateSettings(draft)
      await qc.invalidateQueries({ queryKey: ['cashier'] })
      await loadRegister()
      toast(t('catalog.saved'))
      onClose()
    } catch (err) {
      setError(err instanceof ApiError ? err.message : String(err))
    } finally {
      setSaving(false)
    }
  }

  const removeRecipient = async (r: TransferRecipient) => {
    if (!window.confirm(t('settings.removeRecipientConfirm', { name: `${r.bankName} ${r.account}` }))) return
    try {
      await cashierApi.removeRecipient(r.id)
      await qc.invalidateQueries({ queryKey: ['cashier'] })
    } catch (err) {
      toast(err instanceof Error ? err.message : String(err), 'error')
    }
  }

  return (
    <Modal title={t('settings.title')} onClose={onClose} width={980}>
      <div className="mb-4 flex flex-wrap items-center gap-3 rounded-xl bg-field px-4 py-3 text-[14px]">
        <Store size={20} className="text-ink-2" />
        <span className="text-ink-2">{t('settings.store')}:</span> <b>{data.storeName}</b>
        {data.storeAddress && <span className="text-ink-3">{data.storeAddress}</span>}
        <span className="ml-auto flex items-center gap-1.5 text-[12px] text-ink-3"><Lock size={14} /> {t('settings.storeReadonly')}</span>
      </div>
      {!can && <p className="mb-4 rounded-xl bg-amber-50 px-4 py-2.5 text-[13px] text-amber-800">{t('settings.readonly')}</p>}

      <div className="grid gap-4 lg:grid-cols-2">
        <Section icon={<Monitor size={20} />} title={t('settings.register')}>
          <Field label={t('settings.registerName')}>
            <Input value={draft.registerName} disabled={!can} maxLength={60} onChange={(e) => patch({ registerName: e.target.value })} />
          </Field>
          <Field label={t('settings.currentCashier')}>
            <Input value={`${me?.firstName ?? ''} ${me?.lastName ?? ''}`.trim()} disabled />
          </Field>
        </Section>

        <Section icon={<CreditCard size={20} />} title={t('settings.methods')}>
          {METHODS.map(({ m, icon: Icon }) => (
            <Line key={m} icon={<Icon size={18} className="text-brand" />} label={t(`pay.${m}`)}
              hint={m === 'Transfer' && data.recipients.length === 0 ? t('settings.transferNeedsAccount') : undefined}>
              <Toggle checked={draft.enabledMethods.includes(m)} onChange={(v) => can && toggleMethod(m, v)} label="" />
            </Line>
          ))}
          <Line icon={<Shuffle size={18} className="text-brand" />} label={t('pay.Mixed')}>
            <Toggle checked={draft.mixedEnabled} onChange={(v) => can && patch({ mixedEnabled: v })} label="" />
          </Line>
        </Section>

        <Section icon={<UserRound size={20} />} title={t('settings.cashier')}>
          <div className="flex flex-wrap gap-2">
            <Button variant="secondary" icon={<KeyRound size={17} />} onClick={() => setModal('password')}>{t('settings.changePassword')}</Button>
            <Button variant="secondary" icon={<LockKeyhole size={17} />} onClick={() => setModal('pin')}>{t('settings.changePin')}</Button>
          </div>
        </Section>

        <Section icon={<Lock size={20} />} title={t('settings.security')}>
          <Line label={t('settings.autoLock')} hint={t('settings.autoLockHint')}>
            <Toggle checked={draft.autoLockMinutes != null} onChange={(v) => can && patch({ autoLockMinutes: v ? 10 : null })} label="" />
          </Line>
          {draft.autoLockMinutes != null && (
            <Field label={t('settings.autoLockAfter')}>
              <Select value={draft.autoLockMinutes} disabled={!can} onChange={(e) => patch({ autoLockMinutes: Number(e.target.value) })}>
                {[...new Set([...LOCK_OPTIONS, draft.autoLockMinutes])].sort((a, b) => a - b).map((n) => (
                  <option key={n} value={n}>{t('settings.minutes', { n })}</option>
                ))}
              </Select>
            </Field>
          )}
        </Section>

        <Section icon={<ReceiptText size={20} />} title={t('settings.receipt')}>
          <Line label={t('settings.autoPrint')} hint={t('settings.autoPrintHint')}>
            <Toggle checked={draft.autoPrint} onChange={(v) => can && patch({ autoPrint: v })} label="" />
          </Line>
          <Line label={t('settings.eReceipt')} hint={t('settings.eReceiptHint')}>
            <Toggle checked={draft.electronicReceipt} onChange={(v) => can && patch({ electronicReceipt: v })} label="" />
          </Line>
        </Section>

        <Section icon={<Bell size={20} />} title={t('settings.notifications')}>
          {NOTIFY.map((k) => (
            <label key={k} className="flex cursor-pointer items-center gap-2.5 py-1 text-[14px]">
              <input type="checkbox" className="size-4 accent-brand" disabled={!can} checked={draft[k]} onChange={(e) => patch({ [k]: e.target.checked })} />
              {t(`settings.${k}` as TranslationKey)}
            </label>
          ))}
          <Field label={t('settings.lowStockThreshold')}>
            <Input inputMode="numeric" disabled={!can} value={String(draft.lowStockThreshold)}
              onChange={(e) => patch({ lowStockThreshold: Number(e.target.value.replace(/\D/g, '')) || 0 })} />
          </Field>
        </Section>

        <div className="lg:col-span-2">
          <Section icon={<Landmark size={20} />} title={t('settings.recipients')}
            action={can && <Button variant="secondary" className="h-9!" icon={<Plus size={16} />} onClick={() => setRecipient('new')}>{t('settings.addRecipient')}</Button>}>
            {data.recipients.length === 0 && <p className="text-[14px] text-ink-3">{t('settings.noRecipients')}</p>}
            <div className="grid gap-2 md:grid-cols-2">
              {data.recipients.map((r) => (
                <div key={r.id} className="flex items-center gap-3 rounded-xl border border-line px-3 py-2.5">
                  <div className="min-w-0 flex-1 text-[14px]">
                    <div className="font-semibold">{r.bankName}</div>
                    <div>{r.account}</div>
                    <div className="text-[13px] text-ink-2">{r.holderName}</div>
                  </div>
                  {can && (
                    <>
                      <button type="button" aria-label={t('catalog.edit')} onClick={() => setRecipient(r)}
                        className="flex size-9 items-center justify-center rounded-lg text-brand hover:bg-field"><Pencil size={16} /></button>
                      <button type="button" aria-label={t('catalog.delete')} onClick={() => void removeRecipient(r)}
                        className="flex size-9 items-center justify-center rounded-lg text-ink-3 hover:bg-field hover:text-danger"><Trash2 size={16} /></button>
                    </>
                  )}
                </div>
              ))}
            </div>
          </Section>
        </div>
      </div>

      {error && <p className="mt-4 rounded-xl bg-danger-soft px-4 py-3 text-[14px] text-danger">{error}</p>}
      <div className="mt-5 flex justify-end gap-2">
        <Button variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
        {can && <Button loading={saving} onClick={() => void save()}>{t('settings.save')}</Button>}
      </div>

      {modal === 'pin' && <PinModal hasPin={me?.hasPin ?? false} onClose={() => setModal(null)} />}
      {modal === 'password' && <PasswordModal onClose={() => setModal(null)} />}
      {recipient && (
        <RecipientModal recipient={recipient === 'new' ? null : recipient} onClose={() => setRecipient(null)}
          onSaved={async () => { setRecipient(null); await qc.invalidateQueries({ queryKey: ['cashier'] }) }} />
      )}
    </Modal>
  )
}

function Section({ icon, title, action, children }: { icon: React.ReactNode; title: string; action?: React.ReactNode; children: React.ReactNode }) {
  return (
    <section className="flex flex-col gap-3 rounded-2xl border border-line p-4">
      <div className="flex items-center gap-2.5">
        <span className="text-ink">{icon}</span>
        <h3 className="text-[16px] font-bold">{title}</h3>
        {action && <div className="ml-auto">{action}</div>}
      </div>
      {children}
    </section>
  )
}

function Line({ icon, label, hint, children }: { icon?: React.ReactNode; label: string; hint?: string; children: React.ReactNode }) {
  return (
    <div className="flex items-center gap-3 rounded-xl bg-field px-3 py-2">
      {icon}
      <div className="min-w-0 flex-1">
        <div className="text-[14px] font-medium">{label}</div>
        {hint && <div className="text-[12px] text-ink-3">{hint}</div>}
      </div>
      {children}
    </div>
  )
}

/** Аударым реквизиті (ТЗ §9.4, §17.6): банк, нөмір, алушы. */
function RecipientModal({ recipient, onClose, onSaved }: { recipient: TransferRecipient | null; onClose: () => void; onSaved: () => void }) {
  const t = useT()
  const [bankName, setBank] = useState(recipient?.bankName ?? 'Kaspi Bank')
  const [account, setAccount] = useState(recipient?.account ?? '')
  const [holderName, setHolder] = useState(recipient?.holderName ?? '')
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [busy, setBusy] = useState(false)

  const submit = async () => {
    setBusy(true)
    setErrors({})
    try {
      await cashierApi.saveRecipient(recipient?.id ?? null, { bankName, account, holderName })
      onSaved()
    } catch (err) {
      setErrors(err instanceof ApiError && err.field ? { [err.field]: err.message } : { form: String(err) })
    } finally {
      setBusy(false)
    }
  }

  return (
    <Modal title={recipient ? t('settings.editRecipient') : t('settings.addRecipient')} onClose={onClose} width={440}>
      <div className="flex flex-col gap-4">
        <Field label={t('settings.bank')} required error={errors.bankName}><Input value={bankName} maxLength={60} onChange={(e) => setBank(e.target.value)} /></Field>
        <Field label={t('settings.account')} required error={errors.account}><Input value={account} maxLength={40} placeholder="+7 7__ ___ __ __" onChange={(e) => setAccount(e.target.value)} /></Field>
        <Field label={t('settings.holder')} required error={errors.holderName}><Input value={holderName} maxLength={100} onChange={(e) => setHolder(e.target.value)} /></Field>
        {errors.form && <p className="text-[14px] text-danger">{errors.form}</p>}
        <div className="flex justify-end gap-2">
          <Button variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
          <Button loading={busy} onClick={() => void submit()}>{t('common.save')}</Button>
        </div>
      </div>
    </Modal>
  )
}
