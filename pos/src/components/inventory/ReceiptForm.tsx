import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { ArrowLeft, Ban, Check, Pencil, Plus, Save, Sparkles, Undo2, X } from 'lucide-react'
import { Button } from '../ui/Button'
import { Field, Input, Select } from '../ui/Field'
import { RowMenu } from '../ui/RowMenu'
import { confirmDialog } from '../ui/Confirm'
import { toast } from '../ui/Toast'
import { ApiError } from '../../lib/api'
import { inventoryApi, inventoryKeys } from '../../lib/inventoryApi'
import type { SaveReceipt, StockDocument, Supplier } from '../../lib/inventoryTypes'
import { productApi } from '../../lib/productApi'
import { dateTime, isoDate } from '../../lib/dates'
import { num, tenge } from '../../lib/money'
import { useT } from '../../lib/i18n'
import { LinesEditor, toNum, type LineDraft } from './LinesEditor'
import { SupplierModal } from './SuppliersTab'
import { InvoiceRecognizeModal, type RecognizedHeader } from './InvoiceRecognizeModal'
import { DocStatus, ReasonModal, useInventoryAction, useInventoryPermissions } from './inventoryUi'

interface Header { warehouseId: string; date: string; supplierId: string; supplierInvoiceNumber: string; basis: string; comment: string }

function linesFromDoc(d: StockDocument): LineDraft[] {
  return d.lines.map((l) => ({
    productId: l.productId, name: l.name, article: l.article, barcode: l.barcode, imageUrl: l.imageUrl, unit: l.unit,
    stockNow: l.stockNow, catalogRetailPrice: l.catalogRetailPrice, quantity: String(l.quantity), purchasePrice: String(l.purchasePrice),
    retailPrice: l.retailPrice == null ? '' : String(l.retailPrice),
  }))
}

/**
 * Приёмка (ТЗ «Склад» §7, §9, §13): полная және быстрая (quick) — бір форма, быстрая реквизитсіз.
 * Черновик қалдыққа тимейді; «Провести» бір рет қозғалыс жазады. Төлем — проведённый құжатта.
 */
export function ReceiptForm({ quick = false }: { quick?: boolean }) {
  const t = useT()
  const navigate = useNavigate()
  const qc = useQueryClient()
  const { id } = useParams()
  const [params] = useSearchParams()
  const perms = useInventoryPermissions()
  const run = useInventoryAction()
  const docQuery = useQuery({ queryKey: inventoryKeys.document(id ?? ''), queryFn: () => inventoryApi.document(id!), enabled: !!id })
  const doc = docQuery.data
  const warehouses = useQuery({ queryKey: inventoryKeys.warehouses, queryFn: () => inventoryApi.warehouses() }).data ?? []
  const suppliers = useQuery({ queryKey: inventoryKeys.suppliers, queryFn: inventoryApi.suppliers }).data ?? []
  const isQuick = doc ? doc.isQuick : quick
  // Проведённая приёмка да түзетіледі («Редактировать»): қоймаға тек айырма жазылады, қоймасы өзгермейді (сервер тексереді).
  const [editing, setEditing] = useState(false)
  const posted = doc?.status === 'Posted'
  const readOnly = !!doc && doc.status !== 'Draft' && !(posted && editing)

  const [h, setH] = useState<Header>({ warehouseId: '', date: isoDate(new Date()), supplierId: params.get('supplierId') ?? '', supplierInvoiceNumber: '', basis: '', comment: '' })
  const [lines, setLines] = useState<LineDraft[]>([])
  const [loaded, setLoaded] = useState(false)
  const [saving, setSaving] = useState<'draft' | 'post' | null>(null)
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [addingSupplier, setAddingSupplier] = useState(false)
  const [reversing, setReversing] = useState<{ kind: 'doc' } | null>(null)
  const [recognizing, setRecognizing] = useState(false)

  // Жүктелген құжат → форма (бір рет); жаңа құжат → әдепкі қойма.
  useEffect(() => {
    if (loaded) return
    if (id && doc) {
      setH({ warehouseId: doc.warehouseId, date: isoDate(new Date(doc.date)), supplierId: doc.supplierId ?? '', supplierInvoiceNumber: doc.supplierInvoiceNumber ?? '', basis: doc.basis ?? '', comment: doc.comment ?? '' })
      setLines(linesFromDoc(doc))
      setLoaded(true)
    } else if (!id && warehouses.length) {
      setH((x) => ({ ...x, warehouseId: x.warehouseId || (warehouses.find((w) => w.isDefault && w.isActive) ?? warehouses[0]).id }))
      setLoaded(true)
      const productId = params.get('productId')
      if (productId) void productApi.get(productId).then((p) => enrich(p.id).then((e) => setLines([{ productId: p.id, name: p.name, article: p.article, barcode: p.barcodes[0]?.barcode ?? null, imageUrl: p.images[0]?.url ?? null, unit: null, stockNow: null, catalogRetailPrice: null, quantity: '1', purchasePrice: p.purchasePrice?.toString() ?? '', retailPrice: '', ...e }])))
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id, doc, warehouses.length])

  const set = (p: Partial<Header>) => setH((x) => ({ ...x, ...p }))
  const enrich = async (productId: string): Promise<Partial<LineDraft>> => {
    try {
      const p = await productApi.get(productId)
      return { stockNow: p.stock.find((s) => s.warehouseId === h.warehouseId)?.quantity ?? 0, catalogRetailPrice: p.salePrice, purchasePrice: p.purchasePrice?.toString() ?? '' }
    } catch { return {} }
  }

  const totals = useMemo(() => ({
    count: lines.length,
    qty: lines.reduce((s, l) => s + (toNum(l.quantity) ?? 0), 0),
    sum: lines.reduce((s, l) => s + (toNum(l.quantity) ?? 0) * (toNum(l.purchasePrice) ?? 0), 0),
  }), [lines])

  const body = (): SaveReceipt => ({
    warehouseId: h.warehouseId || null, date: h.date ? new Date(h.date).toISOString() : null, supplierId: h.supplierId || null,
    supplierInvoiceNumber: h.supplierInvoiceNumber || null, basis: h.basis || null, attachmentUrl: null, comment: h.comment || null, isQuick,
    lines: lines.map((l) => ({ productId: l.productId, quantity: toNum(l.quantity) ?? 0, purchasePrice: toNum(l.purchasePrice), retailPrice: toNum(l.retailPrice) })),
  })

  const save = async (post: boolean, e?: FormEvent) => {
    e?.preventDefault()
    const errs: Record<string, string> = {}
    if (!h.warehouseId) errs.warehouseId = t('common.required')
    if (post && lines.length === 0) errs.lines = t('inv.lines.empty')
    for (const l of lines) {
      if ((toNum(l.quantity) ?? 0) <= 0) errs.lines = t('inv.lines.qtyInvalid', { name: l.name })
      else if (toNum(l.purchasePrice) == null) errs.lines = t('inv.lines.priceInvalid', { name: l.name })
    }
    setErrors(errs)
    if (Object.keys(errs).length) return
    if (post && !(await confirmDialog({ title: t('inv.receipts.postTitle'), message: t('inv.receipts.postConfirm', { n: totals.count, sum: tenge(totals.sum) }), confirmLabel: t('inv.receipts.post') }))) return
    if (posted && editing && !(await confirmDialog({ title: t('inv.receipts.editTitle', { number: doc!.number }), message: t('inv.receipts.editConfirm'), confirmLabel: t('inv.receipts.saveChanges') }))) return

    setSaving(post ? 'post' : 'draft')
    try {
      const saved = id ? await inventoryApi.updateReceipt(id, body()) : await inventoryApi.createReceipt(body())
      const final = post ? await inventoryApi.postDocument(saved.id) : saved
      await qc.invalidateQueries({ queryKey: inventoryKeys.all })
      toast(post ? t('inv.receipts.posted', { number: final.number }) : posted ? t('inv.receipts.edited', { number: final.number }) : t('inv.doc.draftSaved', { number: final.number }))
      if (posted) setEditing(false)
      if (!id) navigate(`/warehouse/receipts/${final.id}`, { replace: true })
    } catch (err) {
      if (err instanceof ApiError && err.field) setErrors({ [err.field]: err.message })
      else setErrors({ form: err instanceof Error ? err.message : String(err) })
    } finally {
      setSaving(null)
    }
  }

  /** ИИ ұсынысын формаға қолдану (ТЗ §8): реквизиттер толтырылады, жолдар бар жолдарға қосылады; сақтау — пайдаланушыда. */
  const applyRecognized = (recognized: LineDraft[], header: RecognizedHeader) => {
    set({
      supplierId: header.supplierId ?? h.supplierId,
      supplierInvoiceNumber: header.invoiceNumber ?? h.supplierInvoiceNumber,
      date: header.date ?? h.date,
    })
    setLines((current) => {
      const next = [...current]
      for (const l of recognized) {
        const i = next.findIndex((x) => x.productId === l.productId)
        if (i >= 0) next[i] = { ...next[i], quantity: String((toNum(next[i].quantity) ?? 0) + (toNum(l.quantity) ?? 0)), purchasePrice: l.purchasePrice || next[i].purchasePrice }
        else next.push(l)
      }
      return next
    })
    setErrors((e) => ({ ...e, lines: '' }))
    setRecognizing(false)
    toast(t('inv.ai.applied', { n: recognized.length }))
  }

  const cancelDraft = async () => {
    if (!doc || !(await confirmDialog({ title: t('inv.doc.cancel'), message: t('inv.doc.cancelConfirm', { number: doc.number }), confirmLabel: t('inv.doc.cancel'), danger: true }))) return
    if (await run(() => inventoryApi.cancelDocument(doc.id), t('inv.doc.cancelled'))) navigate('/warehouse/receipts')
  }

  if (id && docQuery.error) return <p className="py-16 text-center text-[15px] text-danger">{docQuery.error.message}</p>
  if (id && !doc) return <p className="py-16 text-center text-[14px] text-ink-3">{t('common.loading')}</p>
  const supplier = suppliers.find((s) => s.id === h.supplierId)
  const activeWarehouses = warehouses.filter((w) => w.isActive || w.id === h.warehouseId)

  return (
    <form onSubmit={(e) => void save(false, e)} noValidate>
      <div className="mb-5 flex flex-wrap items-center gap-3">
        <button type="button" onClick={() => navigate('/warehouse/receipts')} aria-label={t('common.back')}
          className="flex size-10 items-center justify-center rounded-xl border border-line bg-surface text-ink-2 hover:bg-field"><ArrowLeft size={19} /></button>
        <div className="min-w-0">
          <h1 className="truncate text-[21px] font-bold">{doc ? `${t('inv.receipts.title')} ${doc.number}` : isQuick ? t('inv.receipts.quick') : t('inv.receipts.new')}</h1>
          <p className="text-[13px] text-ink-3">{doc ? `${t('inv.doc.created')} ${dateTime(doc.createdAt)} · ${doc.createdByName}` : isQuick ? t('inv.receipts.quickHint') : t('inv.receipts.newHint')}</p>
        </div>
        {doc && <DocStatus status={doc.status} kind="doc" />}
        <div className="ml-auto flex flex-wrap gap-2">
          {!readOnly && !posted && perms.receive && (
            <Button type="button" variant="secondary" icon={<Sparkles size={17} />} disabled={!!saving} onClick={() => setRecognizing(true)}>{t('inv.ai.button')}</Button>
          )}
          {!readOnly && !posted && perms.receive && (
            <Button type="submit" variant="secondary" icon={<Save size={17} />} loading={saving === 'draft'} disabled={!!saving}>{t('inv.doc.saveDraft')}</Button>
          )}
          {posted && perms.post && !editing && (
            <Button type="button" variant="secondary" icon={<Pencil size={17} />} onClick={() => setEditing(true)}>{t('inv.receipts.edit')}</Button>
          )}
          {posted && editing && (
            <>
              <Button type="button" variant="secondary" icon={<X size={17} />} disabled={!!saving} onClick={() => { setEditing(false); setErrors({}); if (doc) { setLines(linesFromDoc(doc)); setH({ warehouseId: doc.warehouseId, date: isoDate(new Date(doc.date)), supplierId: doc.supplierId ?? '', supplierInvoiceNumber: doc.supplierInvoiceNumber ?? '', basis: doc.basis ?? '', comment: doc.comment ?? '' }) } }}>{t('common.cancel')}</Button>
              <Button type="submit" icon={<Save size={17} />} loading={saving === 'draft'} disabled={!!saving}>{t('inv.receipts.saveChanges')}</Button>
            </>
          )}
          {!readOnly && !posted && perms.post && (
            <Button type="button" icon={<Check size={17} />} loading={saving === 'post'} disabled={!!saving} onClick={() => void save(true)}>
              {isQuick ? t('inv.receipts.accept') : t('inv.receipts.post')}
            </Button>
          )}
          {doc && (
            <RowMenu items={[
              ...(doc.status === 'Draft' && perms.receive ? [{ label: t('inv.doc.cancel'), icon: <Ban size={16} />, danger: true, onClick: () => void cancelDraft() }] : []),
              ...(doc.status === 'Posted' && perms.post ? [{ label: t('inv.doc.reverse'), icon: <Undo2 size={16} />, danger: true, onClick: () => setReversing({ kind: 'doc' }) }] : []),
            ]} />
          )}
        </div>
      </div>

      <div className="grid gap-5 xl:grid-cols-[1fr_320px]">
        <div className="flex min-w-0 flex-col gap-5">
          <section className="rounded-2xl border border-line bg-surface p-5">
            <div className={`grid gap-4 ${isQuick ? 'md:grid-cols-2' : 'md:grid-cols-2 2xl:grid-cols-3'}`}>
              <Field label={t('inv.mov.warehouse')} required error={errors.warehouseId}>
                <Select value={h.warehouseId} disabled={readOnly || posted} onChange={(e) => set({ warehouseId: e.target.value })} error={!!errors.warehouseId}>
                  <option value="">{t('product.choose')}</option>
                  {activeWarehouses.map((w) => <option key={w.id} value={w.id}>{w.name}{w.isDefault ? ` · ${t('inv.wh.default')}` : ''}</option>)}
                </Select>
              </Field>
              <Field label={t('inv.receipts.supplier')} error={errors.supplierId} required={!isQuick}>
                <div className="flex gap-2">
                  <Select value={h.supplierId} disabled={readOnly} onChange={(e) => set({ supplierId: e.target.value })} error={!!errors.supplierId}>
                    <option value="">{isQuick ? t('inv.receipts.noSupplier') : t('product.choose')}</option>
                    {suppliers.filter((s) => s.status === 'Active' || s.id === h.supplierId).map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
                  </Select>
                  {!readOnly && (perms.suppliers || perms.receive) && (
                    <Button type="button" variant="secondary" className="shrink-0 px-3!" icon={<Plus size={17} />} onClick={() => setAddingSupplier(true)} title={t('inv.sup.add')} />
                  )}
                </div>
              </Field>
              {!isQuick && (
                <Field label={t('inv.doc.date')} required>
                  <Input type="date" value={h.date} disabled={readOnly} onChange={(e) => set({ date: e.target.value })} />
                </Field>
              )}
              {!isQuick && (
                <Field label={t('inv.receipts.basis')}>
                  <Input value={h.basis} disabled={readOnly} onChange={(e) => set({ basis: e.target.value })} maxLength={120} placeholder={t('inv.receipts.basisPlaceholder')} />
                </Field>
              )}
              {!isQuick && (
                <Field label={t('inv.receipts.invoiceNumber')}>
                  <Input value={h.supplierInvoiceNumber} disabled={readOnly} onChange={(e) => set({ supplierInvoiceNumber: e.target.value })} maxLength={64} />
                </Field>
              )}
              <Field label={t('node.description')}>
                <Input value={h.comment} disabled={readOnly} onChange={(e) => set({ comment: e.target.value })} maxLength={1000} placeholder={t('inv.doc.optional')} />
              </Field>
            </div>
          </section>

          <section className="rounded-2xl border border-line bg-surface p-5">
            <div className="mb-3 flex items-center justify-between">
              <h2 className="text-[16px] font-bold">{t('inv.receipts.lines')} ({lines.length})</h2>
              {!readOnly && <span className="text-[12px] text-ink-3">{t('inv.lines.scanHint')}</span>}
            </div>
            <LinesEditor lines={lines} onChange={(l) => { setLines(l); setErrors((e) => ({ ...e, lines: '' })) }} mode="receipt" canRetail={perms.retail}
              disabled={readOnly} enrich={enrich} />
            {errors.lines && <p className="mt-2 text-[13px] text-danger">{errors.lines}</p>}
          </section>

          {errors.form && <p className="text-[14px] text-danger">{errors.form}</p>}
        </div>

        <aside className="flex flex-col gap-4">
          <section className="rounded-2xl border border-line bg-surface p-5">
            <h2 className="mb-3 text-[16px] font-bold">{t('inv.doc.totals')}</h2>
            <Row label={t('inv.doc.lines')} value={num(totals.count)} />
            <Row label={t('inv.doc.totalQty')} value={num(totals.qty)} />
            <Row label={t('inv.doc.purchaseSum')} value={tenge(totals.sum)} bold />
            {doc?.status === 'Reversed' && <p className="mt-3 rounded-xl bg-danger-soft px-3 py-2 text-[13px] text-danger">{t('inv.doc.reversedHint')}: {doc.reversalReason}</p>}
          </section>
          {supplier && (
            <section className="rounded-2xl border border-line bg-surface p-5">
              <div className="mb-2 flex items-center justify-between">
                <h2 className="text-[16px] font-bold">{t('inv.receipts.supplier')}</h2>
                <button type="button" className="text-[13px] font-medium text-brand hover:underline" onClick={() => navigate(`/warehouse/suppliers/${supplier.id}`)}>{t('inv.sup.open')}</button>
              </div>
              <div className="text-[15px] font-semibold">{supplier.name}</div>
              <div className="mt-1 text-[13px] text-ink-2">{[supplier.taxId && `${t('inv.sup.taxId')}: ${supplier.taxId}`, supplier.phone, supplier.email, supplier.address].filter(Boolean).map((x) => <div key={String(x)}>{x}</div>)}</div>
            </section>
          )}
          {!doc && (
            <p className="rounded-xl bg-brand-soft/60 px-4 py-3 text-[12px] leading-relaxed text-ink-2">{isQuick ? t('inv.receipts.quickNote') : t('inv.receipts.note')}</p>
          )}
        </aside>
      </div>

      {addingSupplier && <SupplierModal onClose={() => setAddingSupplier(false)} onSaved={(s: Supplier) => { void qc.invalidateQueries({ queryKey: inventoryKeys.suppliers }); set({ supplierId: s.id }); setAddingSupplier(false); toast(t('catalog.created')) }} />}
      {recognizing && <InvoiceRecognizeModal warehouseId={h.warehouseId || null} suppliers={suppliers} onClose={() => setRecognizing(false)} onApply={applyRecognized} />}
      {reversing?.kind === 'doc' && doc && (
        <ReasonModal title={t('inv.doc.reverseTitle', { number: doc.number })} hint={t('inv.doc.reverseHint')} confirmLabel={t('inv.doc.reverse')} onClose={() => setReversing(null)}
          onSubmit={async (reason) => { if (await run(() => inventoryApi.reverseDocument(doc.id, reason), t('inv.doc.reversedDone'))) setReversing(null) }} />
      )}
    </form>
  )
}

function Row({ label, value, bold }: { label: string; value: string; bold?: boolean }) {
  return (
    <div className="flex items-center justify-between py-1.5 text-[14px]">
      <span className="text-ink-2">{label}</span>
      <span className={bold ? 'text-[16px] font-bold' : 'font-semibold'}>{value}</span>
    </div>
  )
}
