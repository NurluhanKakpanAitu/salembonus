import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { ArrowLeft, Ban, Check, Save, Undo2 } from 'lucide-react'
import { Button } from '../ui/Button'
import { Field, Input, Select, Textarea } from '../ui/Field'
import { RowMenu } from '../ui/RowMenu'
import { confirmDialog } from '../ui/Confirm'
import { toast } from '../ui/Toast'
import { ApiError } from '../../lib/api'
import { inventoryApi, inventoryKeys } from '../../lib/inventoryApi'
import { WRITE_OFF_REASONS, type SaveWriteOff, type WriteOffReason } from '../../lib/inventoryTypes'
import { productApi } from '../../lib/productApi'
import { dateTime, isoDate } from '../../lib/dates'
import { num } from '../../lib/money'
import { useT, type TranslationKey } from '../../lib/i18n'
import { LinesEditor, toNum, type LineDraft } from './LinesEditor'
import { DocStatus, ReasonModal, useInventoryAction, useInventoryPermissions } from './inventoryUi'

/** Списание (ТЗ «Склад» §11): себеп міндетті, «Другое» — түсініктемемен; қалдықтан асуға болмайды; сторно. */
export function WriteOffForm() {
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
  const readOnly = !!doc && doc.status !== 'Draft'

  const [h, setH] = useState({ warehouseId: '', date: isoDate(new Date()), reason: '' as WriteOffReason | '', comment: '' })
  const [lines, setLines] = useState<LineDraft[]>([])
  const [loaded, setLoaded] = useState(false)
  const [saving, setSaving] = useState<'draft' | 'post' | null>(null)
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [reversing, setReversing] = useState(false)

  useEffect(() => {
    if (loaded) return
    if (id && doc) {
      setH({ warehouseId: doc.warehouseId, date: isoDate(new Date(doc.date)), reason: doc.reason ?? '', comment: doc.comment ?? '' })
      setLines(doc.lines.map((l) => ({ productId: l.productId, name: l.name, article: l.article, barcode: l.barcode, imageUrl: l.imageUrl, unit: l.unit,
        stockNow: l.stockNow, catalogRetailPrice: null, quantity: String(l.quantity), purchasePrice: String(l.purchasePrice), retailPrice: '' })))
      setLoaded(true)
    } else if (!id && warehouses.length) {
      setH((x) => ({ ...x, warehouseId: x.warehouseId || (warehouses.find((w) => w.isDefault && w.isActive) ?? warehouses[0]).id }))
      setLoaded(true)
      const productId = params.get('productId')
      if (productId) void productApi.get(productId).then((p) => enrich(p.id).then((e) => setLines([{ productId: p.id, name: p.name, article: p.article, barcode: p.barcodes[0]?.barcode ?? null, imageUrl: p.images[0]?.url ?? null, unit: null, stockNow: null, catalogRetailPrice: null, quantity: '1', purchasePrice: '', retailPrice: '', ...e }])))
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id, doc, warehouses.length])

  const enrich = async (productId: string): Promise<Partial<LineDraft>> => {
    try {
      const p = await productApi.get(productId)
      return { stockNow: p.stock.find((s) => s.warehouseId === h.warehouseId)?.quantity ?? 0, purchasePrice: p.purchasePrice?.toString() ?? '' }
    } catch { return {} }
  }
  const totalQty = useMemo(() => lines.reduce((s, l) => s + (toNum(l.quantity) ?? 0), 0), [lines])

  const body = (): SaveWriteOff => ({
    warehouseId: h.warehouseId || null, date: h.date ? new Date(h.date).toISOString() : null, reason: h.reason || null, comment: h.comment || null, attachmentUrl: null,
    lines: lines.map((l) => ({ productId: l.productId, quantity: toNum(l.quantity) ?? 0, purchasePrice: toNum(l.purchasePrice), retailPrice: null })),
  })

  const save = async (post: boolean, e?: FormEvent) => {
    e?.preventDefault()
    const errs: Record<string, string> = {}
    if (!h.warehouseId) errs.warehouseId = t('common.required')
    if (post && !h.reason) errs.reason = t('common.required')
    if (post && h.reason === 'Other' && !h.comment.trim()) errs.comment = t('inv.wo.commentRequired')
    if (post && lines.length === 0) errs.lines = t('inv.lines.empty')
    for (const l of lines) {
      const q = toNum(l.quantity) ?? 0
      if (q <= 0) errs.lines = t('inv.lines.qtyInvalid', { name: l.name })
      else if (l.stockNow != null && q > l.stockNow) errs.lines = t('inv.wo.exceeds', { name: l.name, stock: num(l.stockNow) })
    }
    setErrors(errs)
    if (Object.keys(errs).length) return
    if (post && !(await confirmDialog({ title: t('inv.wo.postTitle'), message: t('inv.wo.postConfirm', { n: lines.length, qty: num(totalQty) }), confirmLabel: t('inv.wo.post'), danger: true }))) return
    setSaving(post ? 'post' : 'draft')
    try {
      const saved = id ? await inventoryApi.updateWriteOff(id, body()) : await inventoryApi.createWriteOff(body())
      const final = post ? await inventoryApi.postDocument(saved.id) : saved
      await qc.invalidateQueries({ queryKey: inventoryKeys.all })
      toast(post ? t('inv.wo.posted', { number: final.number }) : t('inv.doc.draftSaved', { number: final.number }))
      if (!id) navigate(`/warehouse/write-offs/${final.id}`, { replace: true })
    } catch (err) {
      if (err instanceof ApiError && err.field) setErrors({ [err.field]: err.message })
      else setErrors({ form: err instanceof Error ? err.message : String(err) })
    } finally {
      setSaving(null)
    }
  }

  const cancelDraft = async () => {
    if (!doc || !(await confirmDialog({ title: t('inv.doc.cancel'), message: t('inv.doc.cancelConfirm', { number: doc.number }), confirmLabel: t('inv.doc.cancel'), danger: true }))) return
    if (await run(() => inventoryApi.cancelDocument(doc.id), t('inv.doc.cancelled'))) navigate('/warehouse/write-offs')
  }

  if (id && docQuery.error) return <p className="py-16 text-center text-[15px] text-danger">{docQuery.error.message}</p>
  if (id && !doc) return <p className="py-16 text-center text-[14px] text-ink-3">{t('common.loading')}</p>

  return (
    <form onSubmit={(e) => void save(false, e)} noValidate>
      <div className="mb-5 flex flex-wrap items-center gap-3">
        <button type="button" onClick={() => navigate('/warehouse/write-offs')} aria-label={t('common.back')}
          className="flex size-10 items-center justify-center rounded-xl border border-line bg-surface text-ink-2 hover:bg-field"><ArrowLeft size={19} /></button>
        <div className="min-w-0">
          <h1 className="truncate text-[21px] font-bold">{doc ? `${t('inv.wo.title')} ${doc.number}` : t('inv.wo.new')}</h1>
          <p className="text-[13px] text-ink-3">{doc ? `${t('inv.doc.created')} ${dateTime(doc.createdAt)} · ${doc.createdByName}` : t('inv.wo.newHint')}</p>
        </div>
        {doc && <DocStatus status={doc.status} kind="doc" />}
        <div className="ml-auto flex flex-wrap gap-2">
          {!readOnly && perms.writeOff && (
            <>
              <Button type="submit" variant="secondary" icon={<Save size={17} />} loading={saving === 'draft'} disabled={!!saving}>{t('inv.doc.saveDraft')}</Button>
              <Button type="button" variant="danger" icon={<Check size={17} />} loading={saving === 'post'} disabled={!!saving} onClick={() => void save(true)}>{t('inv.wo.post')}</Button>
            </>
          )}
          {doc && perms.writeOff && (
            <RowMenu items={[
              ...(doc.status === 'Draft' ? [{ label: t('inv.doc.cancel'), icon: <Ban size={16} />, danger: true, onClick: () => void cancelDraft() }] : []),
              ...(doc.status === 'Posted' ? [{ label: t('inv.doc.reverse'), icon: <Undo2 size={16} />, danger: true, onClick: () => setReversing(true) }] : []),
            ]} />
          )}
        </div>
      </div>

      <div className="grid gap-5 xl:grid-cols-[1fr_300px]">
        <div className="flex min-w-0 flex-col gap-5">
          <section className="rounded-2xl border border-line bg-surface p-5">
            <div className="grid gap-4 md:grid-cols-2 2xl:grid-cols-3">
              <Field label={t('inv.mov.warehouse')} required error={errors.warehouseId}>
                <Select value={h.warehouseId} disabled={readOnly} onChange={(e) => setH({ ...h, warehouseId: e.target.value })} error={!!errors.warehouseId}>
                  <option value="">{t('product.choose')}</option>
                  {warehouses.filter((w) => w.isActive || w.id === h.warehouseId).map((w) => <option key={w.id} value={w.id}>{w.name}</option>)}
                </Select>
              </Field>
              <Field label={t('inv.wo.reason')} required error={errors.reason}>
                <Select value={h.reason} disabled={readOnly} onChange={(e) => setH({ ...h, reason: e.target.value as WriteOffReason | '' })} error={!!errors.reason}>
                  <option value="">{t('product.choose')}</option>
                  {WRITE_OFF_REASONS.map((r) => <option key={r} value={r}>{t(`inv.wo.reasons.${r}` as TranslationKey)}</option>)}
                </Select>
              </Field>
              <Field label={t('inv.doc.date')} required>
                <Input type="date" value={h.date} disabled={readOnly} onChange={(e) => setH({ ...h, date: e.target.value })} />
              </Field>
              <div className="md:col-span-2 2xl:col-span-3">
                <Field label={t('node.description')} required={h.reason === 'Other'} error={errors.comment} hint={h.reason === 'Other' ? t('inv.wo.commentRequired') : undefined}>
                  <Textarea value={h.comment} disabled={readOnly} onChange={(e) => setH({ ...h, comment: e.target.value })} maxLength={1000} />
                </Field>
              </div>
            </div>
          </section>
          <section className="rounded-2xl border border-line bg-surface p-5">
            <h2 className="mb-3 text-[16px] font-bold">{t('inv.wo.lines')} ({lines.length})</h2>
            <LinesEditor lines={lines} onChange={(l) => { setLines(l); setErrors((e) => ({ ...e, lines: '' })) }} mode="writeoff" disabled={readOnly} enrich={enrich} />
            {errors.lines && <p className="mt-2 text-[13px] text-danger">{errors.lines}</p>}
          </section>
          {errors.form && <p className="text-[14px] text-danger">{errors.form}</p>}
        </div>
        <aside className="flex flex-col gap-4">
          <section className="rounded-2xl border border-line bg-surface p-5">
            <h2 className="mb-3 text-[16px] font-bold">{t('inv.doc.totals')}</h2>
            <div className="flex justify-between py-1.5 text-[14px]"><span className="text-ink-2">{t('inv.doc.lines')}</span><b>{num(lines.length)}</b></div>
            <div className="flex justify-between py-1.5 text-[14px]"><span className="text-ink-2">{t('inv.doc.totalQty')}</span><b className="text-[16px]">{num(totalQty)}</b></div>
            {doc?.status === 'Reversed' && <p className="mt-3 rounded-xl bg-danger-soft px-3 py-2 text-[13px] text-danger">{t('inv.doc.reversedHint')}: {doc.reversalReason}</p>}
          </section>
          <p className="rounded-xl bg-brand-soft/60 px-4 py-3 text-[12px] leading-relaxed text-ink-2">{t('inv.wo.note')}</p>
        </aside>
      </div>
      {reversing && doc && (
        <ReasonModal title={t('inv.doc.reverseTitle', { number: doc.number })} hint={t('inv.wo.reverseHint')} confirmLabel={t('inv.doc.reverse')} onClose={() => setReversing(false)}
          onSubmit={async (reason) => { if (await run(() => inventoryApi.reverseDocument(doc.id, reason), t('inv.doc.reversedDone'))) setReversing(false) }} />
      )}
    </form>
  )
}
