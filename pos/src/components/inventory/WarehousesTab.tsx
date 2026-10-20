import { useState, type FormEvent } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import { Archive, ArchiveRestore, Pencil, Plus, Star } from 'lucide-react'
import { Button } from '../ui/Button'
import { Field, Input, Select, Textarea, Toggle } from '../ui/Field'
import { Modal } from '../ui/Modal'
import { RowMenu } from '../ui/RowMenu'
import { StatusBadge } from '../ui/StatusBadge'
import { confirmDialog } from '../ui/Confirm'
import { ApiError } from '../../lib/api'
import { inventoryApi, inventoryKeys } from '../../lib/inventoryApi'
import type { WarehouseRow } from '../../lib/inventoryTypes'
import { num } from '../../lib/money'
import { useT, type TranslationKey } from '../../lib/i18n'
import { EmptyRow, td, th, useInventoryAction, useInventoryPermissions } from './inventoryUi'

const TYPES = ['main', 'salesFloor', 'reserve'] as const

/** «Склады» (ТЗ «Склад» §6): дүкеннің қоймалары, негізгі белгі, архив (қалдығы болса — тыйым). */
export function WarehousesTab() {
  const t = useT()
  const navigate = useNavigate()
  const perms = useInventoryPermissions()
  const run = useInventoryAction()
  const { data = [], isLoading } = useQuery({ queryKey: [...inventoryKeys.warehouses, 'all'], queryFn: () => inventoryApi.warehouses(true) })
  const [editing, setEditing] = useState<WarehouseRow | 'new' | null>(null)

  return (
    <div>
      <div className="mb-4 flex flex-wrap items-center gap-2">
        {perms.warehouses && <Button icon={<Plus size={18} />} onClick={() => setEditing('new')}>{t('inv.wh.add')}</Button>}
        <p className="text-[13px] text-ink-3">{t('inv.wh.hint')}</p>
      </div>
      <div className="overflow-x-auto rounded-2xl border border-line bg-surface">
        <table className="w-full min-w-[900px] border-collapse">
          <thead className="bg-field">
            <tr>
              <th className={th}>{t('inv.wh.name')}</th>
              <th className={th}>{t('top.store')}</th>
              <th className={th}>{t('inv.wh.type')}</th>
              <th className={`${th} text-right`}>{t('inv.wh.kinds')}</th>
              <th className={`${th} text-right`}>{t('inv.wh.units')}</th>
              <th className={th}>{t('catalog.col.status')}</th>
              <th className={`${th} text-right`}>{t('catalog.col.actions')}</th>
            </tr>
          </thead>
          <tbody>
            {isLoading && <EmptyRow colSpan={7} text={t('common.loading')} />}
            {!isLoading && data.length === 0 && <EmptyRow colSpan={7} text={t('catalog.empty')} />}
            {data.map((w) => (
              <tr key={w.id} className={`border-t border-line ${w.isActive ? '' : 'text-ink-3'}`}>
                <td className={`${td} font-semibold`}>
                  <span className="flex items-center gap-2">
                    {w.name}
                    {w.isDefault && <span className="rounded-md bg-brand-soft px-1.5 py-0.5 text-[11px] font-semibold text-brand">{t('inv.wh.default')}</span>}
                  </span>
                  {w.comment && <span className="block text-[12px] font-normal text-ink-3">{w.comment}</span>}
                </td>
                <td className={td}>{w.storeName}</td>
                <td className={td}>{w.type ? (TYPES.includes(w.type as never) ? t(`inv.wh.types.${w.type}` as TranslationKey) : w.type) : '—'}</td>
                <td className={`${td} text-right`}>
                  <button type="button" className="font-medium text-brand hover:underline" onClick={() => navigate(`/warehouse?warehouseId=${w.id}`)}>{num(w.productKinds)}</button>
                </td>
                <td className={`${td} text-right font-semibold`}>{num(w.totalUnits)}</td>
                <td className={td}><StatusBadge active={w.isActive} label={w.isActive ? t('status.Active.m') : t('status.Archived.m')} /></td>
                <td className={`${td} whitespace-nowrap text-right`}>
                  {perms.warehouses && (
                    <div className="inline-flex items-center gap-1.5">
                      <Button variant="secondary" className="h-9! px-3! text-[13px]! text-brand!" icon={<Pencil size={15} />} onClick={() => setEditing(w)}>{t('catalog.edit')}</Button>
                      <RowMenu items={[
                        ...(w.isActive && !w.isDefault ? [{ label: t('inv.wh.makeDefault'), icon: <Star size={16} />, onClick: () => void run(() => inventoryApi.makeDefault(w.id), t('catalog.saved')) }] : []),
                        w.isActive
                          ? { label: t('catalog.archive'), icon: <Archive size={16} />, onClick: () => void confirmDialog({ title: t('catalog.archive'), message: t('inv.wh.archiveConfirm', { name: w.name }), confirmLabel: t('catalog.archive') }).then((ok) => ok && run(() => inventoryApi.archiveWarehouse(w.id), t('catalog.archived'))) }
                          : { label: t('catalog.restore'), icon: <ArchiveRestore size={16} />, onClick: () => void run(() => inventoryApi.restoreWarehouse(w.id), t('catalog.restored')) },
                      ]} />
                    </div>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {editing && <WarehouseModal warehouse={editing === 'new' ? undefined : editing} onClose={() => setEditing(null)}
        onSaved={(msg) => void run(async () => undefined, msg).then(() => setEditing(null))} />}
    </div>
  )
}

function WarehouseModal({ warehouse, onClose, onSaved }: { warehouse?: WarehouseRow; onClose: () => void; onSaved: (msg: string) => void }) {
  const t = useT()
  const [name, setName] = useState(warehouse?.name ?? '')
  const [type, setType] = useState(warehouse?.type ?? '')
  const [comment, setComment] = useState(warehouse?.comment ?? '')
  const [makeDefault, setMakeDefault] = useState(warehouse?.isDefault ?? false)
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [loading, setLoading] = useState(false)

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (!name.trim()) return setErrors({ name: t('common.required') })
    setLoading(true)
    setErrors({})
    try {
      const body = { name, type: type || null, comment: comment || null, makeDefault }
      if (warehouse) await inventoryApi.updateWarehouse(warehouse.id, body)
      else await inventoryApi.createWarehouse(body)
      onSaved(warehouse ? t('catalog.saved') : t('catalog.created'))
    } catch (err) {
      if (err instanceof ApiError && err.field) setErrors({ [err.field]: err.message })
      else setErrors({ form: err instanceof ApiError ? err.message : String(err) })
    } finally {
      setLoading(false)
    }
  }

  return (
    <Modal title={warehouse ? t('inv.wh.edit') : t('inv.wh.add')} onClose={onClose}>
      <form onSubmit={submit} noValidate className="flex flex-col gap-4">
        <Field label={t('inv.wh.name')} required error={errors.name}>
          <Input autoFocus value={name} onChange={(e) => setName(e.target.value)} error={!!errors.name} maxLength={100} placeholder={t('inv.wh.namePlaceholder')} />
        </Field>
        <Field label={t('inv.wh.type')}>
          <Select value={TYPES.includes(type as never) || type === '' ? type : 'custom'} onChange={(e) => setType(e.target.value === 'custom' ? ' ' : e.target.value)}>
            <option value="">—</option>
            {TYPES.map((k) => <option key={k} value={k}>{t(`inv.wh.types.${k}` as TranslationKey)}</option>)}
            <option value="custom">{t('inv.wh.types.custom')}</option>
          </Select>
          {!TYPES.includes(type as never) && type !== '' && (
            <Input className="mt-2" value={type.trim()} onChange={(e) => setType(e.target.value || ' ')} maxLength={50} placeholder={t('inv.wh.types.customPlaceholder')} />
          )}
        </Field>
        <Field label={t('node.description')}>
          <Textarea value={comment} onChange={(e) => setComment(e.target.value)} maxLength={500} />
        </Field>
        <Toggle checked={makeDefault} onChange={setMakeDefault} label={t('inv.wh.makeDefaultToggle')} />
        {errors.form && <p className="text-[14px] text-danger">{errors.form}</p>}
        <div className="mt-2 flex justify-end gap-2">
          <Button type="button" variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" loading={loading}>{t('common.save')}</Button>
        </div>
      </form>
    </Modal>
  )
}
