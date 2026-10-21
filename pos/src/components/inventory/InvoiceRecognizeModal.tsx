import { useEffect, useRef, useState, type DragEvent } from 'react'
import { useQuery } from '@tanstack/react-query'
import { AlertTriangle, Camera, Check, FileText, Loader2, RotateCcw, Search, Sparkles, Upload, X } from 'lucide-react'
import { Button } from '../ui/Button'
import { Input } from '../ui/Field'
import { Modal } from '../ui/Modal'
import { ApiError } from '../../lib/api'
import { inventoryApi } from '../../lib/inventoryApi'
import type { InvoiceRecognition, RecognizedLine, RecognizedProduct, Supplier } from '../../lib/inventoryTypes'
import { productApi } from '../../lib/productApi'
import type { ProductListItem } from '../../lib/catalogTypes'
import { num, tenge } from '../../lib/money'
import { useT, type TranslationKey } from '../../lib/i18n'
import { toNum, type LineDraft } from './LinesEditor'
import { ProductThumb, td, th, useDebounced } from './inventoryUi'

const ACCEPT = 'image/jpeg,image/png,image/webp,application/pdf'
const MAX_BYTES = 10 * 1024 * 1024

/** Тексеру кестесіндегі жол: танылған мән + пайдаланушының түзетуі (тауар, сан, баға, өткізіп жіберу). */
interface ReviewLine {
  line: RecognizedLine
  product: RecognizedProduct | null
  quantity: string
  price: string
  skip: boolean
}

export interface RecognizedHeader { supplierId: string | null; supplierName: string | null; invoiceNumber: string | null; date: string | null }

function toReview(r: InvoiceRecognition): ReviewLine[] {
  return r.lines.map((line) => ({
    line, product: line.product, skip: line.status === 'NotFound',
    quantity: line.quantity == null ? '1' : String(line.quantity),
    price: line.price != null ? String(line.price) : line.product?.purchasePrice != null ? String(line.product.purchasePrice) : '',
  }))
}

function fromProduct(p: ProductListItem): RecognizedProduct {
  return { id: p.id, name: p.name, article: p.article, barcode: p.barcode, imageUrl: p.imageUrl, unit: p.unitShortName, stockNow: null, purchasePrice: null, catalogRetailPrice: null }
}

/**
 * «Распознать накладную» (ТЗ «Склад» §8): файл → Claude → тексеру кестесі → черновикке қолдану.
 * ИИ ешқашан проведение жасамайды: нәтиже тек формаға түседі, сақтау мен проведение — әдеттегідей.
 * Табылмаған тауар автоматты жасалмайды — оны іздеп таңдайды не өткізіп жібереді.
 */
export function InvoiceRecognizeModal({ warehouseId, suppliers, onClose, onApply }: {
  warehouseId: string | null
  suppliers: Supplier[]
  onClose: () => void
  onApply: (lines: LineDraft[], header: RecognizedHeader) => void
}) {
  const t = useT()
  const inputRef = useRef<HTMLInputElement>(null)
  const cameraRef = useRef<HTMLInputElement>(null)
  const [file, setFile] = useState<File | null>(null)
  const [preview, setPreview] = useState<string | null>(null)
  const [status, setStatus] = useState<'idle' | 'recognizing' | 'ready' | 'error'>('idle')
  const [error, setError] = useState<string | null>(null)
  const [result, setResult] = useState<InvoiceRecognition | null>(null)
  const [rows, setRows] = useState<ReviewLine[]>([])
  const [dragging, setDragging] = useState(false)

  useEffect(() => {
    if (!file) { setPreview(null); return }
    const url = URL.createObjectURL(file)
    setPreview(url)
    return () => URL.revokeObjectURL(url)
  }, [file])

  const pick = (f: File | undefined) => {
    if (!f) return
    if (!ACCEPT.split(',').includes(f.type)) { setError(t('inv.ai.typeInvalid')); return }
    if (f.size > MAX_BYTES) { setError(t('inv.ai.tooLarge')); return }
    setFile(f)
    setResult(null)
    setRows([])
    setError(null)
    setStatus('idle')
  }

  const onDrop = (e: DragEvent) => {
    e.preventDefault()
    setDragging(false)
    pick(e.dataTransfer.files?.[0])
  }

  const recognize = async () => {
    if (!file) return
    setStatus('recognizing')
    setError(null)
    try {
      const r = await inventoryApi.recognizeInvoice(file, warehouseId)
      setResult(r)
      setRows(toReview(r))
      setStatus('ready')
    } catch (err) {
      setError(err instanceof ApiError ? err.message : String(err))
      setStatus('error')
    }
  }

  const patch = (i: number, p: Partial<ReviewLine>) => setRows((rs) => rs.map((r, j) => (j === i ? { ...r, ...p } : r)))

  const applicable = rows.filter((r) => !r.skip && r.product)
  const invalid = applicable.find((r) => (toNum(r.quantity) ?? 0) <= 0 || toNum(r.price) == null)

  const apply = () => {
    if (!result) return
    const lines: LineDraft[] = applicable.map((r) => ({
      productId: r.product!.id, name: r.product!.name, article: r.product!.article, barcode: r.product!.barcode, imageUrl: r.product!.imageUrl,
      unit: r.product!.unit, stockNow: r.product!.stockNow, catalogRetailPrice: r.product!.catalogRetailPrice,
      quantity: r.quantity, purchasePrice: r.price, retailPrice: '',
    }))
    onApply(lines, { supplierId: result.supplierId, supplierName: result.supplierName, invoiceNumber: result.invoiceNumber, date: result.date })
  }

  const supplierLabel = result?.supplierId ? suppliers.find((s) => s.id === result.supplierId)?.name ?? result.supplierName : result?.supplierName

  return (
    <Modal title={t('inv.ai.title')} onClose={onClose} width={status === 'ready' ? 1040 : 640}>
      <div className="flex flex-col gap-4">
        {status !== 'ready' && (
          <>
            <p className="text-[14px] leading-relaxed text-ink-2">{t('inv.ai.hint')}</p>
            <div
              role="button" tabIndex={0}
              onClick={() => inputRef.current?.click()}
              onKeyDown={(e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); inputRef.current?.click() } }}
              onDragOver={(e) => { e.preventDefault(); setDragging(true) }}
              onDragLeave={() => setDragging(false)}
              onDrop={onDrop}
              className={`flex cursor-pointer items-center gap-3 rounded-xl border-2 border-dashed px-4 py-5 text-left transition-colors ${dragging ? 'border-brand bg-brand-soft' : 'border-line hover:border-brand hover:bg-brand-soft'}`}
            >
              {file?.type === 'application/pdf' ? <FileText size={28} className="shrink-0 text-brand" /> : <Upload size={28} className="shrink-0 text-brand" />}
              <span className="min-w-0 flex-1">
                <span className="block break-words text-[15px] font-semibold">{file ? file.name : t('inv.ai.choose')}</span>
                <span className="text-[13px] text-ink-3">{file ? `${(file.size / 1024).toFixed(0)} KB` : t('inv.ai.formats')}</span>
              </span>
              <button type="button" aria-label={t('inv.ai.camera')} title={t('inv.ai.camera')}
                onClick={(e) => { e.stopPropagation(); cameraRef.current?.click() }}
                className="flex size-10 shrink-0 items-center justify-center rounded-xl border border-line bg-surface text-ink-2 hover:bg-field"><Camera size={18} /></button>
            </div>
            <input ref={inputRef} type="file" hidden accept={ACCEPT} onChange={(e) => { pick(e.target.files?.[0]); e.target.value = '' }} />
            <input ref={cameraRef} type="file" hidden accept="image/*" capture="environment" onChange={(e) => { pick(e.target.files?.[0]); e.target.value = '' }} />

            {preview && file && (
              <div className="overflow-hidden rounded-xl border border-line bg-field">
                {file.type === 'application/pdf'
                  ? <iframe title={file.name} src={preview} className="h-72 w-full" />
                  : <img src={preview} alt="" className="mx-auto max-h-72 object-contain" />}
              </div>
            )}

            {status === 'recognizing' && (
              <div className="flex items-center gap-2.5 rounded-xl bg-brand-soft px-4 py-3 text-[14px] text-brand">
                <Loader2 size={18} className="animate-spin" /> {t('inv.ai.recognizing')}
              </div>
            )}
            {error && <p className="rounded-xl bg-danger-soft px-4 py-3 text-[14px] text-danger">{error}</p>}

            <div className="flex justify-end gap-2">
              <Button type="button" variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
              <Button type="button" icon={<Sparkles size={17} />} disabled={!file} loading={status === 'recognizing'} onClick={() => void recognize()}>
                {status === 'error' ? t('inv.ai.retry') : t('inv.ai.recognize')}
              </Button>
            </div>
          </>
        )}

        {status === 'ready' && result && (
          <>
            <div className="grid gap-3 rounded-xl bg-field px-4 py-3 text-[14px] sm:grid-cols-3">
              <div><div className="text-[12px] text-ink-3">{t('inv.receipts.supplier')}</div>
                <div className="font-semibold">{supplierLabel ?? '—'}</div>
                {result.supplierName && !result.supplierId && <div className="text-[12px] text-amber-600">{t('inv.ai.supplierUnknown')}</div>}
              </div>
              <div><div className="text-[12px] text-ink-3">{t('inv.receipts.invoiceNumber')}</div><div className="font-semibold">{result.invoiceNumber ?? '—'}</div></div>
              <div><div className="text-[12px] text-ink-3">{t('inv.doc.date')}</div><div className="font-semibold">{result.date ?? '—'}</div></div>
            </div>

            <div className="flex flex-wrap items-center gap-2 text-[13px]">
              <Badge tone="success">{t('inv.ai.matched')}: {result.matched}</Badge>
              <Badge tone="amber">{t('inv.ai.review')}: {result.review}</Badge>
              <Badge tone="danger">{t('inv.ai.notFound')}: {result.notFound}</Badge>
              <span className="ml-auto text-ink-3">{t('inv.ai.reviewHint')}</span>
            </div>

            <div className="overflow-x-auto rounded-xl border border-line">
              <table className="w-full min-w-[860px] border-collapse">
                <thead className="bg-field">
                  <tr>
                    <th className={`${th} w-10`}>№</th>
                    <th className={th}>{t('inv.ai.recognized')}</th>
                    <th className={th}>{t('inv.ai.product')}</th>
                    <th className={`${th} w-28 text-right`}>{t('inv.lines.qty')}</th>
                    <th className={`${th} w-32 text-right`}>{t('inv.lines.purchase')}</th>
                    <th className={`${th} w-12`} />
                  </tr>
                </thead>
                <tbody>
                  {rows.map((r, i) => (
                    <tr key={r.line.index} className={`border-t border-line ${r.skip ? 'opacity-50' : ''}`}>
                      <td className={`${td} text-ink-3`}>{r.line.index}</td>
                      <td className={td}>
                        <div className="font-medium">{r.line.name ?? '—'}</div>
                        <div className="text-[12px] text-ink-3">
                          {[r.line.article, r.line.barcode, r.line.lineTotal != null ? `= ${tenge(r.line.lineTotal)}` : null].filter(Boolean).join(' · ')}
                        </div>
                      </td>
                      <td className={td}>
                        <ProductCell row={r} onPick={(p) => patch(i, { product: p, skip: false, price: r.price || (p.purchasePrice != null ? String(p.purchasePrice) : '') })} />
                      </td>
                      <td className={td}>
                        <Input inputMode="decimal" className="h-10! text-right" value={r.quantity} disabled={r.skip} onChange={(e) => patch(i, { quantity: e.target.value })} />
                      </td>
                      <td className={td}>
                        <Input inputMode="decimal" className="h-10! text-right" value={r.price} disabled={r.skip} placeholder="0" onChange={(e) => patch(i, { price: e.target.value })} />
                      </td>
                      <td className={td}>
                        <button type="button" title={r.skip ? t('inv.ai.include') : t('inv.ai.skip')} aria-label={r.skip ? t('inv.ai.include') : t('inv.ai.skip')}
                          onClick={() => patch(i, { skip: !r.skip })}
                          className={`flex size-9 items-center justify-center rounded-lg hover:bg-field ${r.skip ? 'text-success' : 'text-ink-3 hover:text-danger'}`}>
                          {r.skip ? <RotateCcw size={16} /> : <X size={16} />}
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {invalid && <p className="text-[13px] text-danger">{(toNum(invalid.quantity) ?? 0) <= 0 ? t('inv.lines.qtyInvalid', { name: invalid.product!.name }) : t('inv.lines.priceInvalid', { name: invalid.product!.name })}</p>}
            <p className="text-[12px] text-ink-3">{t('inv.ai.applyNote')}</p>

            <div className="flex flex-wrap justify-end gap-2">
              <Button type="button" variant="ghost" icon={<RotateCcw size={16} />} onClick={() => { setStatus('idle'); setResult(null); setRows([]) }}>{t('inv.ai.again')}</Button>
              <Button type="button" variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
              <Button type="button" icon={<Check size={17} />} disabled={applicable.length === 0 || !!invalid} onClick={apply}>
                {t('inv.ai.apply', { n: applicable.length })}
              </Button>
            </div>
          </>
        )}
      </div>
    </Modal>
  )
}

function Badge({ tone, children }: { tone: 'success' | 'amber' | 'danger'; children: React.ReactNode }) {
  const tones = { success: 'bg-success-soft text-success', amber: 'bg-amber-50 text-amber-700', danger: 'bg-danger-soft text-danger' }
  return <span className={`inline-flex h-7 items-center rounded-lg px-2.5 font-semibold ${tones[tone]}`}>{children}</span>
}

/** Тауар бағаны: сәйкес келгені (ауыстыруға болады) не каталогтан іздеу — табылмаған және «требует выбора» жолдар үшін. */
function ProductCell({ row, onPick }: { row: ReviewLine; onPick: (p: RecognizedProduct) => void }) {
  const t = useT()
  const [searching, setSearching] = useState(row.product == null)
  const [query, setQuery] = useState('')
  const debounced = useDebounced(query.trim(), 250)
  const boxRef = useRef<HTMLDivElement>(null)
  const inputRef = useRef<HTMLInputElement>(null)
  // Кесте overflow-x: auto ішінде — absolute тізім қиылып қалады, сондықтан өрістің астына fixed етіп шығарамыз.
  const [anchor, setAnchor] = useState<{ top: number; left: number; width: number } | null>(null)
  const place = () => { const r = inputRef.current?.getBoundingClientRect(); if (r) setAnchor({ top: r.bottom + 4, left: r.left, width: Math.max(r.width, 320) }) }
  const search = useQuery({
    queryKey: ['inventory', 'product-search', debounced],
    queryFn: () => productApi.list({ search: debounced, status: 'Active', page: 1, pageSize: 6 }),
    enabled: searching && debounced.length >= 2,
  })
  const found = search.data?.items ?? []

  useEffect(() => {
    if (!searching) return
    const close = (e: MouseEvent) => { if (!boxRef.current?.contains(e.target as Node) && row.product) setSearching(false) }
    document.addEventListener('mousedown', close)
    return () => document.removeEventListener('mousedown', close)
  }, [searching, row.product])

  if (!searching && row.product) {
    const status = row.line.status
    return (
      <button type="button" onClick={() => setSearching(true)} className="flex w-full items-center gap-2.5 rounded-lg text-left hover:bg-field" title={t('inv.ai.change')}>
        <ProductThumb url={row.product.imageUrl} size={34} />
        <span className="min-w-0 flex-1">
          <span className="block truncate text-[14px] font-medium">{row.product.name}</span>
          <span className={`block truncate text-[12px] ${status === 'Review' ? 'text-amber-600' : 'text-ink-3'}`}>
            {status === 'Review' && <AlertTriangle size={12} className="mr-1 inline" />}
            {t(`inv.ai.by.${row.line.matchedBy}` as TranslationKey)}{row.line.matchedBy === 'Name' ? ` · ${num(Math.round(row.line.confidence * 100))}%` : ''}
            {row.product.stockNow != null ? ` · ${t('inv.lines.stockNow')}: ${num(row.product.stockNow)}` : ''}
          </span>
        </span>
      </button>
    )
  }

  return (
    <div ref={boxRef} className="relative">
      <div className="relative">
        <Search size={16} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-ink-3" />
        <Input ref={inputRef} autoFocus={row.product == null && row.line.status !== 'NotFound'} className="h-10! pl-9" value={query} placeholder={row.line.status === 'NotFound' ? t('inv.ai.pickProduct') : t('inv.lines.search')}
          autoComplete="off" onFocus={place} onChange={(e) => { setQuery(e.target.value); place() }} onKeyDown={(e) => { if (e.key === 'Escape' && row.product) setSearching(false) }} />
      </div>
      {debounced.length >= 2 && anchor && (
        <ul style={anchor} className="fixed z-50 max-h-60 overflow-y-auto rounded-xl border border-line bg-surface py-1 shadow-lg">
          {search.isLoading && <li className="px-3 py-2 text-[13px] text-ink-3">{t('common.loading')}</li>}
          {!search.isLoading && found.length === 0 && <li className="px-3 py-2 text-[13px] text-ink-3">{t('catalog.nothingFound')}</li>}
          {found.map((p) => (
            <li key={p.id}>
              <button type="button" onMouseDown={(e) => e.preventDefault()} onClick={() => { onPick(fromProduct(p)); setSearching(false); setQuery('') }}
                className="flex w-full items-center gap-2.5 px-3 py-2 text-left hover:bg-field">
                <ProductThumb url={p.imageUrl} size={30} />
                <span className="min-w-0 flex-1">
                  <span className="block truncate text-[13px] font-medium">{p.name}</span>
                  <span className="block truncate text-[12px] text-ink-3">{[p.article, p.barcode].filter(Boolean).join(' · ')}</span>
                </span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
