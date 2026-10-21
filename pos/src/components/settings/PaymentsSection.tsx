import { useEffect, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Pencil, Plus, Trash2 } from 'lucide-react'
import { Button } from '../ui/Button'
import { confirmDialog } from '../ui/Confirm'
import { Field, Input, Textarea, Toggle } from '../ui/Field'
import { Modal } from '../ui/Modal'
import { toast } from '../ui/Toast'
import { BlockError, Skeleton } from '../stats/statsUi'
import { Section } from '../staff/staffUi'
import { ApiError } from '../../lib/api'
import { useT, type TranslationKey } from '../../lib/i18n'
import { settingsApi, type StoreCashierSettings, type TransferRecipient, type UpdateStoreCashierSettings } from '../../lib/staffAdminApi'

const METHODS = ['Cash', 'Card', 'Qr', 'Transfer', 'Debt']

const pick = (s: StoreCashierSettings): UpdateStoreCashierSettings => ({
  enabledMethods: s.enabledMethods, mixedEnabled: s.mixedEnabled, autoPrint: s.autoPrint, electronicReceipt: s.electronicReceipt,
  notifyOutOfStock: s.notifyOutOfStock, notifyLowStock: s.notifyLowStock, notifyDebt: s.notifyDebt, notifyReturn: s.notifyReturn, notifyCashierChange: s.notifyCashierChange,
  lowStockThreshold: s.lowStockThreshold, receiptHeader: s.receiptHeader, receiptFooter: s.receiptFooter,
  receiptShowAddress: s.receiptShowAddress, receiptShowPhone: s.receiptShowPhone, receiptShowBin: s.receiptShowBin,
})

/** Дүкен деңгейіндегі касса баптауларын жүктеу/сақтау — «Оплата и чек» пен «Уведомления» ортақ қолданады. */
export function useCashierSettings() {
  const qc = useQueryClient()
  const q = useQuery({ queryKey: ['settings', 'cashier'], queryFn: settingsApi.cashier })
  const [f, setF] = useState<UpdateStoreCashierSettings | null>(null)
  const [busy, setBusy] = useState(false)
  useEffect(() => { if (q.data) setF(pick(q.data)) }, [q.data])
  const dirty = !!q.data && !!f && JSON.stringify(f) !== JSON.stringify(pick(q.data))
  const save = async (t: (k: TranslationKey) => string) => {
    if (!f) return
    setBusy(true)
    try { qc.setQueryData(['settings', 'cashier'], await settingsApi.updateCashier(f)); toast(t('set.saved')) }
    catch (err) { toast(err instanceof ApiError ? err.message : t('login.failed'), 'error') } finally { setBusy(false) }
  }
  return { q, f, patch: (p: Partial<UpdateStoreCashierSettings>) => setF((v) => (v ? { ...v, ...p } : v)), reset: () => q.data && setF(pick(q.data)), dirty, busy, save }
}

/** «Настройка → Оплата и чек»: төлем түрлері, аударым реквизиттері, чек үлгісі мен алдын ала қарауы. */
export function PaymentsSection() {
  const t = useT()
  const qc = useQueryClient()
  const { q, f, patch, reset, dirty, busy, save } = useCashierSettings()
  const [recipient, setRecipient] = useState<TransferRecipient | 'new' | null>(null)
  if (q.isError) return <BlockError message={(q.error as Error).message} onRetry={() => void q.refetch()} />
  if (!q.data || !f) return <Skeleton className="h-96" />
  const s = q.data
  const toggleMethod = (m: string, on: boolean) => patch({ enabledMethods: on ? [...new Set([...f.enabledMethods, m])] : f.enabledMethods.filter((x) => x !== m) })
  const removeRecipient = async (r: TransferRecipient) => {
    if (!(await confirmDialog({ message: t('settings.removeRecipientConfirm', { name: `${r.bankName} · ${r.account}` }), danger: true }))) return
    try { await settingsApi.removeRecipient(r.id); await qc.invalidateQueries({ queryKey: ['settings', 'cashier'] }) } catch (err) { toast(err instanceof ApiError ? err.message : t('login.failed'), 'error') }
  }

  return (
    <div className="flex flex-col gap-4">
      <div className="grid gap-4 xl:grid-cols-[1fr_360px]">
        <div className="flex flex-col gap-4">
          <Section title={t('set.pay.methods')} hint={t('set.pay.methodsHint')}>
            <div className="flex flex-col gap-3">
              {METHODS.map((m) => <Toggle key={m} checked={f.enabledMethods.includes(m)} onChange={(v) => toggleMethod(m, v)} label={t(`pay.${m}` as TranslationKey)} />)}
              {f.enabledMethods.includes('Transfer') && s.recipients.length === 0 && <p className="text-[12px] text-amber-700">{t('settings.transferNeedsAccount')}</p>}
              <div className="mt-2 border-t border-line pt-3"><Toggle checked={f.mixedEnabled} onChange={(v) => patch({ mixedEnabled: v })} label={`${t('set.pay.mixed')} — ${t('set.pay.mixedHint')}`} /></div>
            </div>
          </Section>

          <Section title={t('set.pay.recipients')} hint={t('set.pay.recipientsHint')} action={<Button variant="secondary" className="h-9! px-3! text-[13px]!" icon={<Plus size={15} />} onClick={() => setRecipient('new')}>{t('settings.addRecipient')}</Button>}>
            {s.recipients.length === 0 ? <p className="text-[13px] text-ink-3">{t('settings.noRecipients')}</p> : (
              <ul className="divide-y divide-line">
                {s.recipients.map((r) => (
                  <li key={r.id} className="flex items-center gap-3 py-2.5 text-[14px]">
                    <span className="min-w-0 flex-1 break-words"><b>{r.bankName}</b> · {r.account} · {r.holderName}</span>
                    <Button variant="ghost" className="h-9! px-2.5!" icon={<Pencil size={15} />} onClick={() => setRecipient(r)} aria-label={t('settings.editRecipient')} />
                    <Button variant="ghost" className="h-9! px-2.5! text-danger" icon={<Trash2 size={15} />} onClick={() => void removeRecipient(r)} aria-label={t('set.pay.removeRecipient')} />
                  </li>
                ))}
              </ul>
            )}
          </Section>

          <Section title={t('set.pay.receipt')}>
            <div className="flex flex-col gap-4">
              <Field label={t('set.pay.header')} hint={t('set.pay.headerHint')}><Input value={f.receiptHeader ?? ''} maxLength={300} onChange={(e) => patch({ receiptHeader: e.target.value || null })} /></Field>
              <Field label={t('set.pay.footer')} hint={t('set.pay.footerHint')}><Textarea value={f.receiptFooter ?? ''} maxLength={300} onChange={(e) => patch({ receiptFooter: e.target.value || null })} className="min-h-16" /></Field>
              <div className="flex flex-col gap-3">
                <Toggle checked={f.receiptShowAddress} onChange={(v) => patch({ receiptShowAddress: v })} label={t('set.pay.showAddress')} />
                <Toggle checked={f.receiptShowPhone} onChange={(v) => patch({ receiptShowPhone: v })} label={t('set.pay.showPhone')} />
                <Toggle checked={f.receiptShowBin} onChange={(v) => patch({ receiptShowBin: v })} label={t('set.pay.showBin')} />
                {f.receiptShowBin && !s.organizationBin && <p className="pl-14 text-[12px] text-amber-700">{t('set.pay.noBin')}</p>}
              </div>
              <div className="flex flex-col gap-3 border-t border-line pt-3">
                <Toggle checked={f.autoPrint} onChange={(v) => patch({ autoPrint: v })} label={t('set.pay.autoPrint')} />
                <Toggle checked={f.electronicReceipt} onChange={(v) => patch({ electronicReceipt: v })} label={t('set.pay.eReceipt')} />
              </div>
            </div>
          </Section>
        </div>

        <aside className="xl:sticky xl:top-20 xl:self-start">
          <div className="mb-2 text-[13px] font-semibold text-ink-2">{t('set.pay.preview')}</div>
          <ReceiptPreview f={f} s={s} />
        </aside>
      </div>

      <div className="sticky bottom-0 flex justify-end gap-2 rounded-2xl border border-line bg-surface/95 p-3 backdrop-blur">
        <Button variant="secondary" disabled={!dirty} onClick={reset}>{t('set.cancel')}</Button>
        <Button loading={busy} disabled={!dirty} onClick={() => void save(t)}>{t('set.save')}</Button>
      </div>
      {recipient && <RecipientModal recipient={recipient === 'new' ? null : recipient} onClose={() => setRecipient(null)}
        onSaved={async () => { setRecipient(null); await qc.invalidateQueries({ queryKey: ['settings', 'cashier'] }) }} />}
    </div>
  )
}

/** Чектің алдын ала қарауы — 80 мм таспа, шынайы чекпен бірдей құрылым (ReceiptView.receiptHtml). */
function ReceiptPreview({ f, s }: { f: UpdateStoreCashierSettings; s: StoreCashierSettings }) {
  const t = useT()
  const meta = [f.receiptShowPhone && s.storePhone ? s.storePhone : '', f.receiptShowBin && s.organizationBin ? `${t('receipt.bin')} ${s.organizationBin}` : ''].filter(Boolean).join(' · ')
  return (
    <div className="mx-auto w-full max-w-[300px] rounded-xl border border-line bg-white p-4 font-mono text-[12px] leading-[1.35] text-black shadow-sm">
      {f.receiptHeader && <div className="text-center text-[11px] text-[#555]">{f.receiptHeader}</div>}
      <div className="text-center text-[15px] font-bold">{s.storeName}</div>
      {f.receiptShowAddress && s.storeAddress && <div className="text-center text-[11px] text-[#555]">{s.storeAddress}</div>}
      {meta && <div className="text-center text-[11px] text-[#555]">{meta}</div>}
      <div className="text-center">{t('pos.receiptNo', { n: 1024 })}</div>
      <div className="text-center text-[11px] text-[#555]">21.10.2026 14:05 · Касса №1</div>
      <div className="text-center text-[11px] text-[#555]">{t('receipt.cashier')}: Айгерим</div>
      <div className="my-1.5 border-t border-dashed border-black" />
      <div>1. {t('set.pay.sample')} A</div><div className="flex justify-between text-[11px] text-[#555]"><span>2 × 1 500 ₸</span><span className="text-black">3 000 ₸</span></div>
      <div>2. {t('set.pay.sample')} B</div><div className="flex justify-between text-[11px] text-[#555]"><span>1 × 2 400 ₸</span><span className="text-black">2 400 ₸</span></div>
      <div className="my-1.5 border-t border-dashed border-black" />
      <div className="flex justify-between text-[15px] font-bold"><span>{t('receipt.total')}</span><span>5 400 ₸</span></div>
      <div className="flex justify-between"><span>{t('pay.Cash')}</span><span>5 400 ₸</span></div>
      <div className="my-1.5 border-t border-dashed border-black" />
      <div className="whitespace-pre-wrap text-center">{f.receiptFooter || t('receipt.thanks')}</div>
    </div>
  )
}

function RecipientModal({ recipient, onClose, onSaved }: { recipient: TransferRecipient | null; onClose: () => void; onSaved: () => Promise<void> }) {
  const t = useT()
  const [f, setF] = useState({ bankName: recipient?.bankName ?? '', account: recipient?.account ?? '', holderName: recipient?.holderName ?? '' })
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [busy, setBusy] = useState(false)
  const submit = async () => {
    setBusy(true)
    try { await settingsApi.saveRecipient(recipient?.id ?? null, { bankName: f.bankName.trim(), account: f.account.trim(), holderName: f.holderName.trim() }); await onSaved() }
    catch (err) { if (err instanceof ApiError && err.field) setErrors({ [err.field]: err.message }); else toast(err instanceof ApiError ? err.message : t('login.failed'), 'error') }
    finally { setBusy(false) }
  }
  return (
    <Modal title={recipient ? t('settings.editRecipient') : t('settings.addRecipient')} onClose={onClose} width={440}>
      <div className="flex flex-col gap-3">
        <Field label={t('settings.bank')} required error={errors.bankName}><Input value={f.bankName} maxLength={60} autoFocus onChange={(e) => setF({ ...f, bankName: e.target.value })} /></Field>
        <Field label={t('settings.account')} required error={errors.account}><Input value={f.account} maxLength={40} onChange={(e) => setF({ ...f, account: e.target.value })} /></Field>
        <Field label={t('settings.holder')} required error={errors.holderName}><Input value={f.holderName} maxLength={100} onChange={(e) => setF({ ...f, holderName: e.target.value })} /></Field>
        <div className="flex justify-end gap-2"><Button variant="secondary" onClick={onClose}>{t('common.cancel')}</Button><Button loading={busy} disabled={!f.bankName.trim() || !f.account.trim() || !f.holderName.trim()} onClick={() => void submit()}>{t('common.save')}</Button></div>
      </div>
    </Modal>
  )
}
