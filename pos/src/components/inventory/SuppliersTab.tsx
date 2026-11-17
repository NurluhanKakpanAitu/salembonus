import { useState, type FormEvent } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useNavigate, useParams } from 'react-router-dom'
import { Archive, ArchiveRestore, ArrowLeft, Pencil, Plus } from 'lucide-react'
import { Button } from '../ui/Button'
import { Field, Input, Select, Textarea } from '../ui/Field'
import { Modal } from '../ui/Modal'
import { RowMenu } from '../ui/RowMenu'
import { SearchInput } from '../ui/SearchInput'
import { StatusBadge } from '../ui/StatusBadge'
import { ApiError } from '../../lib/api'
import { inventoryApi, inventoryKeys } from '../../lib/inventoryApi'
import type { SaveSupplier, Supplier, SupplierType } from '../../lib/inventoryTypes'
import { dateOnly, dateTime } from '../../lib/dates'
import { tenge } from '../../lib/money'
import { matchesPhone } from '../../lib/phone'
import { useT, type TranslationKey } from '../../lib/i18n'
import { DocStatus, EmptyRow, ReasonModal, StatCard, td, th, useInventoryAction, useInventoryPermissions } from './inventoryUi'

const SUPPLIER_TYPES: SupplierType[] = ['Company', 'Entrepreneur', 'Person']

/** «Поставщики» (ТЗ «Склад» §12): анықтамалық, приёмка саны, қарыз, архив. */
export function SuppliersTab() {
  const t = useT()
  const navigate = useNavigate()
  const perms = useInventoryPermissions()
  const run = useInventoryAction()
  const { data = [], isLoading } = useQuery({ queryKey: inventoryKeys.suppliers, queryFn: inventoryApi.suppliers })
  const [query, setQuery] = useState('')
  const [editing, setEditing] = useState<Supplier | 'new' | null>(null)
  const list = data.filter((s) => `${s.name} ${s.taxId ?? ''} ${s.phone ?? ''}`.toLowerCase().includes(query.trim().toLowerCase()) || matchesPhone(s.phone, query))

  return (
    <div>
      <div className="mb-4 flex flex-wrap items-center gap-2">
        {(perms.suppliers || perms.receive) && <Button icon={<Plus size={18} />} onClick={() => setEditing('new')}>{t('inv.sup.add')}</Button>}
        <SearchInput className="ml-auto w-full sm:w-72" value={query} onChange={setQuery} placeholder={t('inv.sup.search')} />
      </div>
      <div className="overflow-x-auto rounded-2xl border border-line bg-surface">
        <table className="w-full min-w-[960px] border-collapse">
          <thead className="bg-field">
            <tr>
              <th className={th}>{t('inv.sup.name')}</th>
              <th className={th}>{t('inv.sup.type')}</th>
              <th className={th}>{t('inv.sup.contacts')}</th>
              <th className={th}>{t('inv.sup.taxId')}</th>
              <th className={`${th} text-right`}>{t('inv.sup.receipts')}</th>
              <th className={`${th} text-right`}>{t('inv.sup.debt')}</th>
              <th className={th}>{t('catalog.col.status')}</th>
              <th className={`${th} text-right`}>{t('catalog.col.actions')}</th>
            </tr>
          </thead>
          <tbody>
            {isLoading && <EmptyRow colSpan={8} text={t('common.loading')} />}
            {!isLoading && list.length === 0 && <EmptyRow colSpan={8} text={query ? t('catalog.nothingFound') : t('inv.sup.empty')} />}
            {list.map((s) => (
              <tr key={s.id} className={`border-t border-line hover:bg-field/40 ${s.status === 'Archived' ? 'text-ink-3' : ''}`}>
                <td className={td}>
                  <button type="button" className="text-left font-semibold hover:text-brand" onClick={() => navigate(`/warehouse/suppliers/${s.id}`)}>{s.name}</button>
                </td>
                <td className={`${td} text-[13px]`}>{t(`inv.sup.types.${s.type}` as TranslationKey)}</td>
                <td className={`${td} text-[13px] text-ink-2`}>{[s.phone, s.email].filter(Boolean).join(' · ') || '—'}</td>
                <td className={`${td} font-mono text-[13px] text-ink-2`}>{s.taxId ?? '—'}</td>
                <td className={`${td} text-right text-brand`}>{s.receiptCount}</td>
                <td className={`${td} text-right font-semibold ${s.debt > 0 ? 'text-danger' : ''}`}>{tenge(s.debt)}</td>
                <td className={td}><StatusBadge active={s.status === 'Active'} label={t(`status.${s.status}.m` as TranslationKey)} /></td>
                <td className={`${td} whitespace-nowrap text-right`}>
                  <div className="inline-flex items-center gap-1.5">
                    {perms.suppliers && (
                      <Button variant="secondary" className="h-9! px-3! text-[13px]! text-brand!" icon={<Pencil size={15} />} onClick={() => setEditing(s)}>{t('catalog.edit')}</Button>
                    )}
                    {perms.suppliers && (
                      <RowMenu items={[s.status === 'Active'
                        ? { label: t('catalog.archive'), icon: <Archive size={16} />, onClick: () => void run(() => inventoryApi.archiveSupplier(s.id), t('catalog.archived')) }
                        : { label: t('catalog.restore'), icon: <ArchiveRestore size={16} />, onClick: () => void run(() => inventoryApi.restoreSupplier(s.id), t('catalog.restored')) }]} />
                    )}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {editing && <SupplierModal supplier={editing === 'new' ? undefined : editing} onClose={() => setEditing(null)}
        onSaved={() => void run(async () => undefined, editing === 'new' ? t('catalog.created') : t('catalog.saved')).then(() => setEditing(null))} />}
    </div>
  )
}

export function SupplierModal({ supplier, onClose, onSaved }: { supplier?: Supplier; onClose: () => void; onSaved: (s: Supplier) => void }) {
  const t = useT()
  const [d, setD] = useState<SaveSupplier>({
    name: supplier?.name ?? '', type: supplier?.type ?? 'Company', taxId: supplier?.taxId ?? '', phone: supplier?.phone ?? '',
    email: supplier?.email ?? '', address: supplier?.address ?? '', comment: supplier?.comment ?? '',
  })
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [loading, setLoading] = useState(false)
  const set = (p: Partial<SaveSupplier>) => setD((x) => ({ ...x, ...p }))

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (!d.name.trim()) return setErrors({ name: t('common.required') })
    setLoading(true)
    setErrors({})
    try {
      const body: SaveSupplier = { ...d, taxId: d.taxId || null, phone: d.phone || null, email: d.email || null, address: d.address || null, comment: d.comment || null }
      onSaved(supplier ? await inventoryApi.updateSupplier(supplier.id, body) : await inventoryApi.createSupplier(body))
    } catch (err) {
      if (err instanceof ApiError && err.field) setErrors({ [err.field]: err.message })
      else setErrors({ form: err instanceof ApiError ? err.message : String(err) })
    } finally {
      setLoading(false)
    }
  }

  return (
    <Modal title={supplier ? t('inv.sup.edit') : t('inv.sup.add')} onClose={onClose} width={560}>
      <form onSubmit={submit} noValidate className="flex flex-col gap-4">
        <div className="grid gap-3 sm:grid-cols-[1fr_180px]">
          <Field label={t('inv.sup.name')} required error={errors.name}>
            <Input autoFocus value={d.name} onChange={(e) => set({ name: e.target.value })} error={!!errors.name} maxLength={150} />
          </Field>
          <Field label={t('inv.sup.type')}>
            <Select value={d.type} onChange={(e) => set({ type: e.target.value as SupplierType })}>
              {SUPPLIER_TYPES.map((k) => <option key={k} value={k}>{t(`inv.sup.types.${k}` as TranslationKey)}</option>)}
            </Select>
          </Field>
        </div>
        <div className="grid gap-3 sm:grid-cols-2">
          <Field label={t('inv.sup.taxId')} error={errors.taxId}><Input value={d.taxId ?? ''} onChange={(e) => set({ taxId: e.target.value })} maxLength={20} /></Field>
          <Field label={t('inv.sup.phone')} error={errors.phone}><Input value={d.phone ?? ''} onChange={(e) => set({ phone: e.target.value })} maxLength={32} /></Field>
          <Field label="Email" error={errors.email}><Input value={d.email ?? ''} onChange={(e) => set({ email: e.target.value })} maxLength={120} /></Field>
          <Field label={t('inv.sup.address')} error={errors.address}><Input value={d.address ?? ''} onChange={(e) => set({ address: e.target.value })} maxLength={300} /></Field>
        </div>
        <Field label={t('node.description')}><Textarea value={d.comment ?? ''} onChange={(e) => set({ comment: e.target.value })} maxLength={1000} /></Field>
        {errors.form && <p className="text-[14px] text-danger">{errors.form}</p>}
        <div className="mt-2 flex justify-end gap-2">
          <Button type="button" variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" loading={loading}>{t('common.save')}</Button>
        </div>
      </form>
    </Modal>
  )
}

/** Жеткізуші карточкасы (ТЗ §12.2): приёмкалар, төлемдер, қарыз. */
export function SupplierCard() {
  const t = useT()
  const navigate = useNavigate()
  const { id } = useParams()
  const perms = useInventoryPermissions()
  const run = useInventoryAction()
  const { data, isLoading, error } = useQuery({ queryKey: inventoryKeys.supplier(id!), queryFn: () => inventoryApi.supplier(id!) })
  const [editing, setEditing] = useState(false)
  const [reversing, setReversing] = useState<string | null>(null)
  if (error) return <p className="py-16 text-center text-[15px] text-danger">{error.message}</p>
  if (isLoading || !data) return <p className="py-16 text-center text-[14px] text-ink-3">{t('common.loading')}</p>
  const s = data.supplier

  return (
    <div>
      <div className="mb-5 flex flex-wrap items-center gap-3">
        <button type="button" onClick={() => navigate('/warehouse/suppliers')} aria-label={t('common.back')}
          className="flex size-10 items-center justify-center rounded-xl border border-line bg-surface text-ink-2 hover:bg-field"><ArrowLeft size={19} /></button>
        <div className="min-w-0">
          <h1 className="truncate text-[21px] font-bold">{s.name}</h1>
          <p className="text-[13px] text-ink-3">{t(`inv.sup.types.${s.type}` as TranslationKey)}{s.taxId ? ` · ${t('inv.sup.taxId')} ${s.taxId}` : ''}</p>
        </div>
        <StatusBadge active={s.status === 'Active'} label={t(`status.${s.status}.m` as TranslationKey)} />
        <div className="ml-auto flex gap-2">
          {perms.suppliers && <Button variant="secondary" icon={<Pencil size={17} />} onClick={() => setEditing(true)}>{t('catalog.edit')}</Button>}
          {perms.receive && s.status === 'Active' && <Button icon={<Plus size={17} />} onClick={() => navigate(`/warehouse/receipts/new?supplierId=${s.id}`)}>{t('inv.receipts.new')}</Button>}
        </div>
      </div>
      <div className="mb-5 grid grid-cols-2 gap-3 xl:grid-cols-4">
        <StatCard icon={<Plus size={20} />} label={t('inv.sup.receipts')} value={s.receiptCount} />
        <StatCard icon={<Plus size={20} />} label={t('inv.sup.totalReceipts')} value={tenge(s.totalReceipts)} tone="violet" />
        <StatCard icon={<Plus size={20} />} label={t('inv.sup.totalPaid')} value={tenge(s.totalPaid)} tone="success" />
        <StatCard icon={<Plus size={20} />} label={t('inv.sup.debt')} value={tenge(s.debt)} tone={s.debt > 0 ? 'danger' : 'brand'} />
      </div>
      <div className="grid gap-5 xl:grid-cols-[1fr_380px]">
        <div className="min-w-0">
          <h2 className="mb-2 text-[16px] font-bold">{t('inv.sup.receiptsHistory')}</h2>
          <div className="overflow-x-auto rounded-2xl border border-line bg-surface">
            <table className="w-full min-w-[640px] border-collapse">
              <thead className="bg-field"><tr>
                <th className={th}>{t('inv.doc.number')}</th><th className={th}>{t('inv.mov.date')}</th><th className={th}>{t('inv.mov.warehouse')}</th>
                <th className={`${th} text-right`}>{t('inv.doc.total')}</th><th className={`${th} text-right`}>{t('inv.doc.paid')}</th>
                <th className={`${th} text-right`}>{t('inv.doc.remaining')}</th><th className={th}>{t('catalog.col.status')}</th>
              </tr></thead>
              <tbody>
                {data.receipts.length === 0 && <EmptyRow colSpan={7} text={t('catalog.nothingFound')} />}
                {data.receipts.map((r) => (
                  <tr key={r.id} className="cursor-pointer border-t border-line hover:bg-field/40" onClick={() => navigate(`/warehouse/receipts/${r.id}`)}>
                    <td className={`${td} font-semibold text-brand`}>{r.number}</td>
                    <td className={`${td} text-[13px]`}>{dateTime(r.date)}</td>
                    <td className={td}>{r.warehouseName}</td>
                    <td className={`${td} text-right`}>{tenge(r.total)}</td>
                    <td className={`${td} text-right`}>{tenge(r.paid)}</td>
                    <td className={`${td} text-right font-semibold ${r.remaining > 0 && r.status === 'Posted' ? 'text-danger' : ''}`}>{tenge(r.remaining)}</td>
                    <td className={td}><DocStatus status={r.status} kind="doc" /></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
        <div className="min-w-0">
          <h2 className="mb-2 text-[16px] font-bold">{t('inv.sup.paymentsHistory')}</h2>
          <div className="overflow-hidden rounded-2xl border border-line bg-surface">
            {data.payments.length === 0 && <p className="px-4 py-8 text-center text-[14px] text-ink-3">{t('inv.sup.noPayments')}</p>}
            {data.payments.map((p) => (
              <div key={p.id} className={`flex items-center gap-3 border-b border-line px-4 py-3 last:border-b-0 ${p.status === 'Reversed' ? 'opacity-60' : ''}`}>
                <div className="min-w-0 flex-1">
                  <div className="text-[14px] font-semibold">{tenge(p.amount)} <span className="font-normal text-ink-3">· {t(`pay.${p.method}` as TranslationKey)}</span></div>
                  <div className="truncate text-[12px] text-ink-3">{dateOnly(p.date)}{p.documentNumber ? ` · ${p.documentNumber}` : ''}{p.reference ? ` · №${p.reference}` : ''} · {p.createdByName}</div>
                  {p.status === 'Reversed' && <div className="text-[12px] text-danger">{t('inv.pay.reversed')}: {p.reversalReason}</div>}
                </div>
                {p.status === 'Registered' && perms.post && (
                  <button type="button" className="text-[12px] text-ink-3 hover:text-danger" onClick={() => setReversing(p.id)}>
                    {t('inv.pay.reverse')}
                  </button>
                )}
              </div>
            ))}
          </div>
        </div>
      </div>
      {editing && <SupplierModal supplier={s} onClose={() => setEditing(false)}
        onSaved={() => void run(async () => undefined, t('catalog.saved')).then(() => setEditing(false))} />}
      {reversing && (
        <ReasonModal title={t('inv.pay.reverse')} confirmLabel={t('inv.pay.reverse')} onClose={() => setReversing(null)}
          onSubmit={async (reason) => { if (await run(() => inventoryApi.reversePayment(reversing, reason), t('catalog.saved'))) setReversing(null) }} />
      )}
    </div>
  )
}
