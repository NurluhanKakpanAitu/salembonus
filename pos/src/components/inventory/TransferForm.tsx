import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useNavigate, useParams } from 'react-router-dom'
import { ArrowLeft, ArrowRight, Ban, Check, Save, Send, Undo2 } from 'lucide-react'
import { Button } from '../ui/Button'
import { Field, Input, Select } from '../ui/Field'
import { RowMenu } from '../ui/RowMenu'
import { confirmDialog } from '../ui/Confirm'
import { toast } from '../ui/Toast'
import { ApiError } from '../../lib/api'
import { activeStore, useAuth } from '../../lib/auth'
import { inventoryApi, inventoryKeys } from '../../lib/inventoryApi'
import type { SaveTransfer, Transfer } from '../../lib/inventoryTypes'
import { productApi } from '../../lib/productApi'
import { dateTime } from '../../lib/dates'
import { num } from '../../lib/money'
import { useT } from '../../lib/i18n'
import { LinesEditor, toNum, type LineDraft } from './LinesEditor'
import { DocStatus, ProductThumb, Qty, ReasonModal, td, th, useInventoryAction, useInventoryPermissions } from './inventoryUi'

/**
 * Перемещение (ТЗ «Склад» §10): черновик → «Отправить» (жіберуші қоймадан шығыс) → алушы «Подтвердить приёмку»
 * (нақты сан, айырмашылыққа себеп). Жолда тұрғанда жіберуші тоқтата алады — тауар қоймаға қайтады.
 */
export function TransferForm() {
  const t = useT()
  const navigate = useNavigate()
  const qc = useQueryClient()
  const { id } = useParams()
  const store = useAuth(activeStore)
  const perms = useInventoryPermissions()
  const run = useInventoryAction()
  const trQuery = useQuery({ queryKey: inventoryKeys.transfer(id ?? ''), queryFn: () => inventoryApi.transfer(id!), enabled: !!id })
  const tr = trQuery.data
  const warehouses = useQuery({ queryKey: inventoryKeys.warehouses, queryFn: () => inventoryApi.warehouses() }).data ?? []
  const targets = useQuery({ queryKey: inventoryKeys.targets, queryFn: inventoryApi.transferTargets }).data ?? []
  const readOnly = !!tr && tr.status !== 'Draft'

  const [h, setH] = useState({ sourceWarehouseId: '', destinationStoreId: '', destinationWarehouseId: '', comment: '' })
  const [lines, setLines] = useState<LineDraft[]>([])
  const [received, setReceived] = useState<Record<string, { qty: string; reason: string }>>({})
  const [loaded, setLoaded] = useState(false)
  const [saving, setSaving] = useState<'draft' | 'dispatch' | 'receive' | null>(null)
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [cancelling, setCancelling] = useState(false)

  useEffect(() => {
    if (loaded) return
    if (id && tr) {
      setH({ sourceWarehouseId: tr.sourceWarehouseId, destinationStoreId: tr.destinationStoreId, destinationWarehouseId: tr.destinationWarehouseId, comment: tr.comment ?? '' })
      setLines(tr.lines.map((l) => ({ productId: l.productId, name: l.name, article: l.article, barcode: l.barcode, imageUrl: l.imageUrl, unit: l.unit,
        stockNow: l.sourceStock, catalogRetailPrice: null, quantity: String(l.quantitySent), purchasePrice: '', retailPrice: '' })))
      setReceived(Object.fromEntries(tr.lines.map((l) => [l.productId, { qty: String(l.quantityReceived ?? l.quantitySent), reason: l.discrepancyReason ?? '' }])))
      setLoaded(true)
    } else if (!id && warehouses.length && store) {
      setH((x) => ({ ...x, sourceWarehouseId: x.sourceWarehouseId || (warehouses.find((w) => w.isDefault && w.isActive) ?? warehouses[0]).id, destinationStoreId: x.destinationStoreId || store.id }))
      setLoaded(true)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id, tr, warehouses.length, store?.id])

  const destWarehouses = targets.find((x) => x.storeId === h.destinationStoreId)?.warehouses ?? []
  const enrich = async (productId: string): Promise<Partial<LineDraft>> => {
    try {
      const p = await productApi.get(productId)
      return { stockNow: p.stock.find((s) => s.warehouseId === h.sourceWarehouseId)?.quantity ?? 0 }
    } catch { return {} }
  }
  const totalQty = useMemo(() => lines.reduce((s, l) => s + (toNum(l.quantity) ?? 0), 0), [lines])

  const body = (): SaveTransfer => ({
    sourceWarehouseId: h.sourceWarehouseId || null, destinationStoreId: h.destinationStoreId || null, destinationWarehouseId: h.destinationWarehouseId || null,
    comment: h.comment || null, lines: lines.map((l) => ({ productId: l.productId, quantity: toNum(l.quantity) ?? 0 })),
  })

  const save = async (dispatch: boolean, e?: FormEvent) => {
    e?.preventDefault()
    const errs: Record<string, string> = {}
    if (!h.sourceWarehouseId) errs.sourceWarehouseId = t('common.required')
    if (!h.destinationWarehouseId) errs.destinationWarehouseId = t('common.required')
    if (h.destinationWarehouseId && h.destinationWarehouseId === h.sourceWarehouseId) errs.destinationWarehouseId = t('inv.tr.sameWarehouse')
    if (dispatch && lines.length === 0) errs.lines = t('inv.lines.empty')
    for (const l of lines) {
      const q = toNum(l.quantity) ?? 0
      if (q <= 0) errs.lines = t('inv.lines.qtyInvalid', { name: l.name })
      else if (dispatch && l.stockNow != null && q > l.stockNow) errs.lines = t('inv.wo.exceeds', { name: l.name, stock: num(l.stockNow) })
    }
    setErrors(errs)
    if (Object.keys(errs).length) return
    if (dispatch && !(await confirmDialog({ title: t('inv.tr.dispatchTitle'), message: t('inv.tr.dispatchConfirm', { n: lines.length, qty: num(totalQty) }), confirmLabel: t('inv.tr.dispatch') }))) return
    setSaving(dispatch ? 'dispatch' : 'draft')
    try {
      const saved = id ? await inventoryApi.updateTransfer(id, body()) : await inventoryApi.createTransfer(body())
      const final = dispatch ? await inventoryApi.dispatchTransfer(saved.id) : saved
      await qc.invalidateQueries({ queryKey: inventoryKeys.all })
      toast(dispatch ? t('inv.tr.dispatched', { number: final.number }) : t('inv.doc.draftSaved', { number: final.number }))
      if (!id) navigate(`/warehouse/transfers/${final.id}`, { replace: true })
    } catch (err) {
      if (err instanceof ApiError && err.field) setErrors({ [err.field]: err.message })
      else setErrors({ form: err instanceof Error ? err.message : String(err) })
    } finally {
      setSaving(null)
    }
  }

  const receive = async () => {
    if (!tr) return
    const errs: Record<string, string> = {}
    const payload = tr.lines.map((l) => {
      const r = received[l.productId] ?? { qty: String(l.quantitySent), reason: '' }
      const q = toNum(r.qty) ?? 0
      if (q < 0 || q > l.quantitySent) errs.receive = t('inv.tr.receivedTooMuch', { name: l.name, sent: num(l.quantitySent) })
      else if (q < l.quantitySent && !r.reason.trim()) errs.receive = t('inv.tr.reasonRequired', { name: l.name })
      return { productId: l.productId, quantityReceived: q, discrepancyReason: r.reason.trim() || null }
    })
    setErrors(errs)
    if (Object.keys(errs).length) return
    const short = payload.filter((p, i) => p.quantityReceived < tr.lines[i].quantitySent).length
    if (!(await confirmDialog({ title: t('inv.tr.receiveTitle'), message: short ? t('inv.tr.receiveConfirmShort', { n: short }) : t('inv.tr.receiveConfirm'), confirmLabel: t('inv.tr.receive') }))) return
    setSaving('receive')
    try {
      await inventoryApi.receiveTransfer(tr.id, { lines: payload })
      await qc.invalidateQueries({ queryKey: inventoryKeys.all })
      toast(t('inv.tr.received', { number: tr.number }))
    } catch (err) {
      setErrors({ form: err instanceof Error ? err.message : String(err) })
    } finally {
      setSaving(null)
    }
  }

  const cancelDraft = async () => {
    if (!tr || !(await confirmDialog({ title: t('inv.doc.cancel'), message: t('inv.doc.cancelConfirm', { number: tr.number }), confirmLabel: t('inv.doc.cancel'), danger: true }))) return
    if (await run(() => inventoryApi.cancelTransfer(tr.id, null), t('inv.doc.cancelled'))) navigate('/warehouse/transfers')
  }

  if (id && trQuery.error) return <p className="py-16 text-center text-[15px] text-danger">{trQuery.error.message}</p>
  if (id && !tr) return <p className="py-16 text-center text-[14px] text-ink-3">{t('common.loading')}</p>
  const isSource = !tr || tr.sourceStoreId === store?.id
  const showReceive = !!tr && tr.status === 'InTransit' && tr.canReceive

  return (
    <form onSubmit={(e) => void save(false, e)} noValidate>
      <div className="mb-5 flex flex-wrap items-center gap-3">
        <button type="button" onClick={() => navigate('/warehouse/transfers')} aria-label={t('common.back')}
          className="flex size-10 items-center justify-center rounded-xl border border-line bg-surface text-ink-2 hover:bg-field"><ArrowLeft size={19} /></button>
        <div className="min-w-0">
          <h1 className="truncate text-[21px] font-bold">{tr ? `${t('inv.tr.title')} ${tr.number}` : t('inv.tr.new')}</h1>
          <p className="text-[13px] text-ink-3">{tr ? `${t('inv.doc.created')} ${dateTime(tr.createdAt)} · ${tr.createdByName}` : t('inv.tr.newHint')}</p>
        </div>
        {tr && <DocStatus status={tr.status} kind="transfer" />}
        <div className="ml-auto flex flex-wrap gap-2">
          {!readOnly && perms.transfer && isSource && (
            <>
              <Button type="submit" variant="secondary" icon={<Save size={17} />} loading={saving === 'draft'} disabled={!!saving}>{t('inv.doc.saveDraft')}</Button>
              <Button type="button" icon={<Send size={17} />} loading={saving === 'dispatch'} disabled={!!saving} onClick={() => void save(true)}>{t('inv.tr.dispatch')}</Button>
            </>
          )}
          {showReceive && <Button type="button" icon={<Check size={17} />} loading={saving === 'receive'} disabled={!!saving} onClick={() => void receive()}>{t('inv.tr.receive')}</Button>}
          {tr && perms.transfer && isSource && (tr.status === 'Draft' || tr.status === 'InTransit') && (
            <RowMenu items={[tr.status === 'Draft'
              ? { label: t('inv.doc.cancel'), icon: <Ban size={16} />, danger: true, onClick: () => void cancelDraft() }
              : { label: t('inv.tr.cancelInTransit'), icon: <Undo2 size={16} />, danger: true, onClick: () => setCancelling(true) }]} />
          )}
        </div>
      </div>

      <div className="grid gap-5 xl:grid-cols-[1fr_300px]">
        <div className="flex min-w-0 flex-col gap-5">
          <section className="rounded-2xl border border-line bg-surface p-5">
            <div className="grid items-end gap-4 md:grid-cols-2 2xl:grid-cols-[1fr_auto_1fr_1fr]">
              <Field label={t('inv.tr.sourceWarehouse')} required error={errors.sourceWarehouseId}>
                <Select value={h.sourceWarehouseId} disabled={readOnly || !isSource} onChange={(e) => setH({ ...h, sourceWarehouseId: e.target.value })} error={!!errors.sourceWarehouseId}>
                  {tr && !isSource ? <option value={tr.sourceWarehouseId}>{tr.sourceStoreName} / {tr.sourceWarehouseName}</option> : <>
                    <option value="">{t('product.choose')}</option>
                    {warehouses.filter((w) => w.isActive || w.id === h.sourceWarehouseId).map((w) => <option key={w.id} value={w.id}>{store?.name} / {w.name}</option>)}
                  </>}
                </Select>
              </Field>
              <ArrowRight size={22} className="mb-3 hidden text-ink-3 2xl:block" />
              <Field label={t('inv.tr.destStore')} required>
                <Select value={h.destinationStoreId} disabled={readOnly} onChange={(e) => setH({ ...h, destinationStoreId: e.target.value, destinationWarehouseId: '' })}>
                  {tr && readOnly ? <option value={tr.destinationStoreId}>{tr.destinationStoreName}</option> : targets.map((x) => <option key={x.storeId} value={x.storeId}>{x.storeName}</option>)}
                </Select>
              </Field>
              <Field label={t('inv.tr.destWarehouse')} required error={errors.destinationWarehouseId}>
                <Select value={h.destinationWarehouseId} disabled={readOnly} onChange={(e) => setH({ ...h, destinationWarehouseId: e.target.value })} error={!!errors.destinationWarehouseId}>
                  {tr && readOnly ? <option value={tr.destinationWarehouseId}>{tr.destinationWarehouseName}</option> : <>
                    <option value="">{t('product.choose')}</option>
                    {destWarehouses.filter((w) => w.id !== h.sourceWarehouseId).map((w) => <option key={w.id} value={w.id}>{w.name}{w.isDefault ? ` · ${t('inv.wh.default')}` : ''}</option>)}
                  </>}
                </Select>
              </Field>
              <div className="md:col-span-2 2xl:col-span-4">
                <Field label={t('node.description')}>
                  <Input value={h.comment} disabled={readOnly} onChange={(e) => setH({ ...h, comment: e.target.value })} maxLength={1000} placeholder={t('inv.doc.optional')} />
                </Field>
              </div>
            </div>
          </section>

          <section className="rounded-2xl border border-line bg-surface p-5">
            <h2 className="mb-3 text-[16px] font-bold">{t('inv.receipts.lines')} ({lines.length})</h2>
            {tr && tr.status !== 'Draft' ? (
              <div className="overflow-x-auto rounded-xl border border-line">
                <table className="w-full min-w-[720px] border-collapse">
                  <thead className="bg-field"><tr>
                    <th className={`${th} w-10`}>№</th><th className={th}>{t('inv.lines.product')}</th>
                    <th className={`${th} text-right`}>{t('inv.tr.sent')}</th>
                    <th className={`${th} w-36 text-right`}>{t('inv.tr.receivedQty')}</th>
                    <th className={th}>{t('inv.tr.discrepancy')}</th>
                  </tr></thead>
                  <tbody>
                    {tr.lines.map((l, i) => {
                      const r = received[l.productId] ?? { qty: String(l.quantitySent), reason: '' }
                      const q = toNum(r.qty) ?? 0
                      return (
                        <tr key={l.id} className="border-t border-line">
                          <td className={`${td} text-ink-3`}>{i + 1}</td>
                          <td className={td}><div className="flex items-center gap-2.5"><ProductThumb url={l.imageUrl} size={36} /><span><span className="block font-semibold">{l.name}</span><span className="block text-[12px] text-ink-3">{[l.article, l.unit].filter(Boolean).join(' · ')}</span></span></div></td>
                          <td className={`${td} text-right`}><Qty value={l.quantitySent} /></td>
                          <td className={td}>
                            {showReceive
                              ? <Input inputMode="decimal" className="h-10! text-right" value={r.qty} onChange={(e) => setReceived({ ...received, [l.productId]: { ...r, qty: e.target.value } })} />
                              : <span className={`block text-right font-semibold ${l.quantityReceived != null && l.quantityReceived < l.quantitySent ? 'text-danger' : ''}`}>{l.quantityReceived == null ? '—' : num(l.quantityReceived)}</span>}
                          </td>
                          <td className={td}>
                            {showReceive
                              ? (q < l.quantitySent && <Input className="h-10!" value={r.reason} placeholder={t('inv.tr.discrepancyPlaceholder')} onChange={(e) => setReceived({ ...received, [l.productId]: { ...r, reason: e.target.value } })} maxLength={300} />)
                              : <span className="text-[13px] text-ink-2">{l.discrepancyReason ?? ''}</span>}
                          </td>
                        </tr>
                      )
                    })}
                  </tbody>
                </table>
              </div>
            ) : (
              <LinesEditor lines={lines} onChange={(l) => { setLines(l); setErrors((e) => ({ ...e, lines: '' })) }} mode="transfer" disabled={readOnly || !isSource} enrich={enrich} />
            )}
            {errors.lines && <p className="mt-2 text-[13px] text-danger">{errors.lines}</p>}
            {errors.receive && <p className="mt-2 text-[13px] text-danger">{errors.receive}</p>}
          </section>
          {errors.form && <p className="text-[14px] text-danger">{errors.form}</p>}
        </div>

        <aside className="flex flex-col gap-4">
          <section className="rounded-2xl border border-line bg-surface p-5">
            <h2 className="mb-3 text-[16px] font-bold">{t('inv.doc.totals')}</h2>
            <div className="flex justify-between py-1.5 text-[14px]"><span className="text-ink-2">{t('inv.doc.lines')}</span><b>{num(lines.length)}</b></div>
            <div className="flex justify-between py-1.5 text-[14px]"><span className="text-ink-2">{t('inv.tr.sent')}</span><b className="text-[16px]">{num(totalQty)}</b></div>
            {tr?.totalReceived != null && <div className="flex justify-between py-1.5 text-[14px]"><span className="text-ink-2">{t('inv.tr.receivedQty')}</span><b className={`text-[16px] ${tr.totalReceived < tr.totalQuantity ? 'text-danger' : 'text-success'}`}>{num(tr.totalReceived)}</b></div>}
            {tr?.sentAt && <div className="mt-2 text-[12px] text-ink-3">{t('inv.tr.sentAt')}: {dateTime(tr.sentAt)} · {tr.sentByName}</div>}
            {tr?.receivedAt && <div className="text-[12px] text-ink-3">{t('inv.tr.receivedAt')}: {dateTime(tr.receivedAt)} · {tr.receivedByName}</div>}
          </section>
          <p className="rounded-xl bg-brand-soft/60 px-4 py-3 text-[12px] leading-relaxed text-ink-2">
            {tr?.status === 'InTransit' && !tr.canReceive && tr.destinationStoreId !== store?.id ? t('inv.tr.waitingOtherStore', { store: tr.destinationStoreName }) : t('inv.tr.note')}
          </p>
        </aside>
      </div>
      {cancelling && tr && (
        <ReasonModal title={t('inv.tr.cancelInTransit')} hint={t('inv.tr.cancelInTransitHint')} confirmLabel={t('inv.tr.cancelInTransit')} onClose={() => setCancelling(false)}
          onSubmit={async (reason) => { if (await run(() => inventoryApi.cancelTransfer(tr.id, reason), t('inv.doc.cancelled'))) setCancelling(false) }} />
      )}
    </form>
  )
}

export type TransferView = Transfer
