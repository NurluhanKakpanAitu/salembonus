import { useRef, useState, type ReactNode } from 'react'
import { ArrowLeft, ArrowRight, Barcode, ImagePlus, Loader2, Plus, ScanLine, Star, Trash2, Wand2, X } from 'lucide-react'
import { Button } from '../ui/Button'
import { Field, Input, Select, Textarea, Toggle } from '../ui/Field'
import { ClassificationPicker } from './ClassificationPicker'
import { toNumber, type ProductDraft } from './productDraft'
import type { Brand, CatalogNode, Characteristic, Product, Unit, Warehouse } from '../../lib/catalogTypes'
import { productApi } from '../../lib/productApi'
import { uploadImage } from '../../lib/upload'
import { ApiError } from '../../lib/api'
import { toast } from '../ui/Toast'
import { activeStore, useAuth } from '../../lib/auth'
import { useT } from '../../lib/i18n'

type Patch = (patch: Partial<ProductDraft>) => void
type Errors = Record<string, string>

const MAX_IMAGES = 10

function SectionTitle({ title, hint }: { title: string; hint?: string }) {
  return (
    <div className="mb-5">
      <h2 className="text-[18px] font-bold">{title}</h2>
      {hint && <p className="mt-1 text-[14px] text-ink-2">{hint}</p>}
    </div>
  )
}

const money = (n: number) => new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 2 }).format(n)

// ---------------- 6.1 Основная информация ----------------

export function MainSection({ draft, patch, errors, units, brands }: {
  draft: ProductDraft; patch: Patch; errors: Errors; units: Unit[]; brands: Brand[]
}) {
  const t = useT()
  // Архивтегі анықтамалық жаңа таңдауға ұсынылмайды, бірақ бұрын таңдалғаны көрінеді.
  const unitOptions = units.filter((u) => u.status === 'Active' || u.id === draft.unitId)
  const brandOptions = brands.filter((b) => b.status === 'Active' || b.id === draft.brandId)
  return (
    <div>
      <SectionTitle title={t('product.section.main')} hint={t('product.section.mainHint')} />
      <div className="grid gap-4 md:grid-cols-2">
        <div className="md:col-span-2">
          <Field label={t('product.name')} required error={errors.name}>
            <Input autoFocus value={draft.name} onChange={(e) => patch({ name: e.target.value })} error={!!errors.name} maxLength={200} />
          </Field>
        </div>
        <Field label={t('product.article')} error={errors.article}>
          <Input value={draft.article} onChange={(e) => patch({ article: e.target.value })} maxLength={64} />
        </Field>
        <Field label={t('product.unit')} required error={errors.unitId}>
          <Select value={draft.unitId} onChange={(e) => patch({ unitId: e.target.value })} error={!!errors.unitId}>
            <option value="">{t('product.choose')}</option>
            {unitOptions.map((u) => <option key={u.id} value={u.id}>{u.name} ({u.shortName})</option>)}
          </Select>
        </Field>
        <Field label={t('product.brand')} error={errors.brandId}>
          <Select value={draft.brandId} onChange={(e) => patch({ brandId: e.target.value })} error={!!errors.brandId}>
            <option value="">{t('product.noBrand')}</option>
            {brandOptions.map((b) => <option key={b.id} value={b.id}>{b.name}</option>)}
          </Select>
        </Field>
        <div className="md:col-span-2">
          <Field label={t('product.description')}>
            <Textarea value={draft.description} onChange={(e) => patch({ description: e.target.value })} maxLength={1000} />
          </Field>
        </div>
      </div>
    </div>
  )
}

// ---------------- 6.2 Остатки и цены ----------------

export function StockSection({ draft, patch, errors, warehouses, product, canEditPrice }: {
  draft: ProductDraft; patch: Patch; errors: Errors; warehouses: Warehouse[]; product?: Product; canEditPrice: boolean
}) {
  const t = useT()
  const store = useAuth(activeStore)
  const o = draft.opening
  const set = (p: Partial<ProductDraft['opening']>) => patch({ opening: { ...o, ...p } })

  // Жасалған тауарда қалдық пен кіріс бағасы тек көрсетіледі — олар «Склад» арқылы (ТЗ §6.2).
  // Сату бағасын «Склад» модулі жасалғанша осында өзгертуге болады (ағымдағы дүкен үшін).
  if (product) {
    return (
      <div>
        <SectionTitle title={t('product.section.stock')} hint={t('product.stockReadonly')} />
        <div className="grid gap-4 sm:grid-cols-2">
          {canEditPrice
            ? (
              <Field label={t('product.salePrice')} error={errors.salePrice} hint={t('product.salePriceStore', { store: store?.name ?? '' })}>
                <Input inputMode="decimal" value={draft.salePrice} onChange={(e) => patch({ salePrice: e.target.value })}
                  placeholder="0 ₸" error={!!errors.salePrice} />
              </Field>
            )
            : <Info label={t('product.salePrice')} value={product.salePrice != null ? `${money(product.salePrice)} ₸` : '—'} />}
          {product.purchasePrice != null && <Info label={t('product.purchasePrice')} value={`${money(product.purchasePrice)} ₸`} />}
        </div>
        <div className="mt-5 overflow-hidden rounded-xl border border-line">
          <table className="w-full text-[14px]">
            <thead className="bg-field text-left text-[13px] text-ink-2">
              <tr><th className="px-3 py-2.5 font-semibold">{t('product.warehouse')}</th><th className="px-3 py-2.5 text-right font-semibold">{t('product.quantity')}</th></tr>
            </thead>
            <tbody>
              {product.stock.length === 0 && <tr><td colSpan={2} className="px-3 py-4 text-center text-ink-3">{t('product.noStock')}</td></tr>}
              {product.stock.map((s) => (
                <tr key={s.warehouseId} className="border-t border-line">
                  <td className="px-3 py-2.5">{s.warehouseName} <span className="text-ink-3">· {s.storeName}</span></td>
                  <td className="px-3 py-2.5 text-right font-semibold">{money(s.quantity)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    )
  }

  const qty = toNumber(o.quantity)
  const cost = toNumber(o.purchasePrice)
  const sum = qty && cost && !Number.isNaN(qty) && !Number.isNaN(cost) ? qty * cost : null
  return (
    <div>
      <SectionTitle title={t('product.section.stock')} hint={t('product.stockHint')} />
      <div className="grid gap-4 md:grid-cols-2">
        <Field label={t('product.warehouse')} error={errors['opening.warehouseId']}>
          <Select value={o.warehouseId} onChange={(e) => set({ warehouseId: e.target.value })}>
            {warehouses.map((w) => <option key={w.id} value={w.id}>{w.name}</option>)}
          </Select>
        </Field>
        <Field label={t('product.openingQty')} error={errors['opening.quantity']} hint={t('product.openingQtyHint')}>
          <Input inputMode="decimal" value={o.quantity} onChange={(e) => set({ quantity: e.target.value })} placeholder="0" error={!!errors['opening.quantity']} />
        </Field>
        <Field label={t('product.purchasePrice')} error={errors['opening.purchasePrice']}>
          <Input inputMode="decimal" value={o.purchasePrice} onChange={(e) => set({ purchasePrice: e.target.value })} placeholder="0 ₸" error={!!errors['opening.purchasePrice']} />
        </Field>
        <Field label={t('product.salePrice')} error={errors['opening.salePrice']}>
          <Input inputMode="decimal" value={o.salePrice} onChange={(e) => set({ salePrice: e.target.value })} placeholder="0 ₸" error={!!errors['opening.salePrice']} />
        </Field>
      </div>
      <div className="mt-4 flex items-center justify-between rounded-xl bg-field px-4 py-3 text-[14px]">
        <span className="text-ink-2">{t('product.stockSum')}</span>
        <span className="text-[16px] font-bold">{sum != null ? `${money(sum)} ₸` : '—'}</span>
      </div>
    </div>
  )
}

function Info({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-xl border border-line px-4 py-3">
      <div className="text-[13px] text-ink-2">{label}</div>
      <div className="mt-0.5 text-[17px] font-bold">{value}</div>
    </div>
  )
}

// ---------------- 6.3 Категория и группа ----------------

export function ClassificationSection({ draft, patch, errors, nodes }: { draft: ProductDraft; patch: Patch; errors: Errors; nodes: CatalogNode[] }) {
  const t = useT()
  return (
    <div>
      <SectionTitle title={t('product.section.classification')} hint={t('product.classHint')} />
      <ClassificationPicker nodes={nodes} value={draft.nodeId} onChange={(nodeId) => patch({ nodeId })} />
      {errors.nodeId && <p className="mt-2 text-[13px] text-danger">{errors.nodeId}</p>}
    </div>
  )
}

// ---------------- 6.4 Характеристики ----------------

export function CharacteristicsSection({ draft, patch, errors, definitions }: {
  draft: ProductDraft; patch: Patch; errors: Errors; definitions: Characteristic[]
}) {
  const t = useT()
  const [extra, setExtra] = useState<string[]>([])
  const values = draft.characteristics
  // Көрсетілетіндері: міндеттілері, мәні барлары және қолмен қосылғандары.
  const shown = definitions.filter((d) => (d.status === 'Active' && d.isRequired) || d.id in values || extra.includes(d.id))
  const available = definitions.filter((d) => d.status === 'Active' && !shown.includes(d))

  const set = (id: string, value: string) => patch({ characteristics: { ...values, [id]: value } })
  const remove = (id: string) => {
    const next = { ...values }
    delete next[id]
    patch({ characteristics: next })
    setExtra((e) => e.filter((x) => x !== id))
  }

  return (
    <div>
      <SectionTitle title={t('product.section.characteristics')} hint={t('product.charHint')} />
      <div className="flex flex-col gap-3">
        {shown.length === 0 && <p className="rounded-xl bg-field px-4 py-5 text-center text-[14px] text-ink-3">{t('product.charEmpty')}</p>}
        {shown.map((d) => (
          <div key={d.id} className="grid items-center gap-2 sm:grid-cols-[220px_1fr_auto]">
            <div className="text-[14px] font-medium">
              {d.name}{d.isRequired && <span className="text-danger"> *</span>}
            </div>
            <CharacteristicInput def={d} value={values[d.id] ?? ''} onChange={(v) => set(d.id, v)} />
            {d.isRequired && d.status === 'Active'
              ? <span className="w-9" />
              : <button type="button" onClick={() => remove(d.id)} aria-label="Remove"
                  className="flex size-9 items-center justify-center rounded-lg text-ink-3 hover:bg-field hover:text-danger"><X size={17} /></button>}
          </div>
        ))}
      </div>
      {errors.characteristics && <p className="mt-3 text-[13px] text-danger">{errors.characteristics}</p>}
      {available.length > 0 && (
        <div className="mt-5 max-w-sm">
          <Select value="" onChange={(e) => e.target.value && setExtra((x) => [...x, e.target.value])}>
            <option value="">+ {t('product.addCharacteristic')}</option>
            {available.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
          </Select>
        </div>
      )}
    </div>
  )
}

function CharacteristicInput({ def, value, onChange }: { def: Characteristic; value: string; onChange: (v: string) => void }) {
  const t = useT()
  switch (def.type) {
    case 'List':
      return (
        <Select value={value} onChange={(e) => onChange(e.target.value)}>
          <option value="">{t('product.choose')}</option>
          {def.options.map((o) => <option key={o} value={o}>{o}</option>)}
        </Select>
      )
    case 'Boolean':
      return (
        <Select value={value} onChange={(e) => onChange(e.target.value)}>
          <option value="">{t('product.choose')}</option>
          <option value="true">{t('common.yes')}</option>
          <option value="false">{t('common.no')}</option>
        </Select>
      )
    case 'Date':
      return <Input type="date" value={value} onChange={(e) => onChange(e.target.value)} />
    case 'Number':
      return <Input inputMode="decimal" value={value} onChange={(e) => onChange(e.target.value)} />
    case 'Range': {
      const [from = '', to = ''] = value.split('..')
      return (
        <div className="flex items-center gap-2">
          <Input inputMode="decimal" placeholder={t('product.rangeFrom')} value={from} onChange={(e) => onChange(`${e.target.value}..${to}`)} />
          <span className="text-ink-3">—</span>
          <Input inputMode="decimal" placeholder={t('product.rangeTo')} value={to} onChange={(e) => onChange(`${from}..${e.target.value}`)} />
        </div>
      )
    }
    default:
      return <Input value={value} onChange={(e) => onChange(e.target.value)} maxLength={200} />
  }
}

// ---------------- 6.5 Фото и штрихкод ----------------

export function MediaSection({ draft, patch, errors, productId, canUpload }: {
  draft: ProductDraft; patch: Patch; errors: Errors; productId?: string; canUpload: boolean
}) {
  const t = useT()
  const fileRef = useRef<HTMLInputElement>(null)
  const primaryRef = useRef<HTMLInputElement>(null)
  const [uploading, setUploading] = useState(0)
  const [scanHint, setScanHint] = useState(false)
  const [taken, setTaken] = useState<Record<string, string>>({})
  const [generating, setGenerating] = useState(false)

  const primaryImage = draft.primaryImage ?? draft.images[0] ?? null

  const addFiles = async (files: FileList | null) => {
    if (!files?.length) return
    const room = MAX_IMAGES - draft.images.length
    const list = Array.from(files).slice(0, room)
    if (files.length > room) toast(t('product.imagesLimit', { max: MAX_IMAGES }), 'error')
    setUploading((n) => n + list.length)
    const urls: string[] = []
    for (const file of list) {
      try {
        urls.push(await uploadImage(file, 'product'))
      } catch (err) {
        toast(err instanceof ApiError ? err.message : t('product.uploadFailed'), 'error')
      } finally {
        setUploading((n) => n - 1)
      }
    }
    if (urls.length) patchImages([...draft.images, ...urls])
  }

  // Жүктеу кезінде басқа өзгеріс болуы мүмкін — соңғы күйге қосамыз.
  const patchImages = (images: string[], primary = primaryImage) =>
    patch({ images, primaryImage: primary && images.includes(primary) ? primary : images[0] ?? null })

  const move = (i: number, dir: -1 | 1) => {
    const next = [...draft.images]
    ;[next[i], next[i + dir]] = [next[i + dir], next[i]]
    patchImages(next)
  }

  /** Код басқа тауарда ма — өрістен шыққанда тексереміз, қате сақтағанға дейін көрінсін. */
  const check = async (code: string) => {
    const c = code.trim()
    if (!c) return
    try {
      const r = await productApi.checkBarcode(c, productId)
      setTaken((prev) => {
        const next = { ...prev }
        if (r.available) delete next[c]
        else next[c] = t('product.barcodeTaken', { name: r.productName ?? '' })
        return next
      })
    } catch (err) {
      if (err instanceof ApiError) setTaken((prev) => ({ ...prev, [c]: err.message }))
    }
  }

  const generate = async () => {
    setGenerating(true)
    try {
      const { barcode } = await productApi.generateBarcode()
      patch({ primaryBarcode: barcode })
    } catch (err) {
      toast(err instanceof ApiError ? err.message : String(err), 'error')
    } finally {
      setGenerating(false)
    }
  }

  // Сканер пернетақта сияқты кодты теріп, Enter басады — форма жіберілмеуі керек.
  const noSubmit = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter') {
      e.preventDefault()
      setScanHint(false)
      void check((e.target as HTMLInputElement).value)
    }
  }

  return (
    <div className="flex flex-col gap-8">
      <div>
        <SectionTitle title={t('product.photos')} hint={t('product.photosHint', { max: MAX_IMAGES })} />
        <div className="grid grid-cols-3 gap-3 sm:grid-cols-4 xl:grid-cols-6">
          {draft.images.map((url, i) => (
            <div key={url} className={`group relative overflow-hidden rounded-xl border-2 ${url === primaryImage ? 'border-brand' : 'border-line'}`}>
              <img src={url} alt="" className="aspect-square w-full bg-field object-cover" />
              {url === primaryImage && (
                <span className="absolute left-2 top-2 rounded-md bg-brand px-2 py-0.5 text-[11px] font-semibold text-white">{t('product.primary')}</span>
              )}
              <div className="absolute inset-x-0 bottom-0 flex justify-center gap-1 bg-black/55 p-1.5">
                <IconBtn label={t('product.makePrimary')} disabled={url === primaryImage} onClick={() => patch({ primaryImage: url })}><Star size={15} /></IconBtn>
                <IconBtn label={t('product.moveLeft')} disabled={i === 0} onClick={() => move(i, -1)}><ArrowLeft size={15} /></IconBtn>
                <IconBtn label={t('product.moveRight')} disabled={i === draft.images.length - 1} onClick={() => move(i, 1)}><ArrowRight size={15} /></IconBtn>
                <IconBtn label={t('catalog.delete')} onClick={() => patchImages(draft.images.filter((x) => x !== url))}><Trash2 size={15} /></IconBtn>
              </div>
            </div>
          ))}
          {Array.from({ length: uploading }, (_, i) => (
            <div key={`u${i}`} className="flex aspect-square items-center justify-center rounded-xl border-2 border-dashed border-line text-ink-3">
              <Loader2 size={22} className="animate-spin" />
            </div>
          ))}
          {canUpload && draft.images.length + uploading < MAX_IMAGES && (
            <button type="button" onClick={() => fileRef.current?.click()}
              className="flex aspect-square flex-col items-center justify-center gap-2 rounded-xl border-2 border-dashed border-line text-[13px] font-medium text-brand hover:bg-brand-soft">
              <ImagePlus size={24} /> {draft.images.length === 0 ? t('product.addMainPhoto') : t('product.addPhoto')}
            </button>
          )}
        </div>
        <input ref={fileRef} type="file" accept="image/jpeg,image/png,image/webp" multiple hidden
          onChange={(e) => { void addFiles(e.target.files); e.target.value = '' }} />
        {errors.images && <p className="mt-2 text-[13px] text-danger">{errors.images}</p>}
      </div>

      <div>
        <SectionTitle title={t('product.barcodes')} hint={t('product.barcodesHint')} />
        <Field label={t('product.primaryBarcode')} error={taken[draft.primaryBarcode.trim()]}
          hint={scanHint ? t('product.scanHint') : undefined}>
          <div className="flex flex-wrap gap-2">
            <div className="relative min-w-56 flex-1">
              <Barcode size={18} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-ink-3" />
              <Input ref={primaryRef} className="pl-10 font-mono" value={draft.primaryBarcode} maxLength={64}
                error={!!taken[draft.primaryBarcode.trim()]}
                onChange={(e) => patch({ primaryBarcode: e.target.value })}
                onBlur={(e) => { setScanHint(false); void check(e.target.value) }} onKeyDown={noSubmit} />
            </div>
            <Button type="button" variant="secondary" icon={<ScanLine size={17} />}
              onClick={() => { setScanHint(true); primaryRef.current?.select(); primaryRef.current?.focus() }}>
              {t('product.scan')}
            </Button>
            <Button type="button" variant="secondary" icon={<Wand2 size={17} />} loading={generating} onClick={() => void generate()}>
              {t('product.generate')}
            </Button>
          </div>
        </Field>

        <div className="mt-4 flex flex-col gap-2">
          <div className="text-[13px] font-medium text-ink-2">{t('product.extraBarcodes')}</div>
          {draft.extraBarcodes.map((code, i) => (
            <div key={i}>
              <div className="flex gap-2">
                {/* Жаңа жол бірден фокусқа түседі: сканер кодты сол жерге жазсын. */}
                <Input className="font-mono" value={code} maxLength={64} error={!!taken[code.trim()]} onKeyDown={noSubmit}
                  autoFocus={i === draft.extraBarcodes.length - 1 && code === ''}
                  onChange={(e) => patch({ extraBarcodes: draft.extraBarcodes.map((x, j) => (j === i ? e.target.value : x)) })}
                  onBlur={(e) => void check(e.target.value)} />
                <button type="button" aria-label="Remove" onClick={() => patch({ extraBarcodes: draft.extraBarcodes.filter((_, j) => j !== i) })}
                  className="flex size-11 shrink-0 items-center justify-center rounded-xl text-ink-3 hover:bg-field hover:text-danger"><X size={18} /></button>
              </div>
              {taken[code.trim()] && <p className="mt-1 text-[13px] text-danger">{taken[code.trim()]}</p>}
            </div>
          ))}
          <div>
            <Button type="button" variant="secondary" icon={<Plus size={17} />}
              onClick={() => patch({ extraBarcodes: [...draft.extraBarcodes, ''] })}>
              {t('product.addBarcode')}
            </Button>
          </div>
        </div>
        {errors.barcodes && <p className="mt-3 text-[13px] text-danger">{errors.barcodes}</p>}
      </div>
    </div>
  )
}

function IconBtn({ label, onClick, disabled, children }: { label: string; onClick: () => void; disabled?: boolean; children: ReactNode }) {
  return (
    <button type="button" title={label} aria-label={label} disabled={disabled} onClick={onClick}
      className="flex size-7 items-center justify-center rounded-md text-white hover:bg-white/20 disabled:opacity-35">
      {children}
    </button>
  )
}

// ---------------- 6.6 Дополнительно ----------------

export function ExtraSection({ draft, patch, errors }: { draft: ProductDraft; patch: Patch; errors: Errors }) {
  const t = useT()
  return (
    <div>
      <SectionTitle title={t('product.section.extra')} hint={t('product.extraHint')} />
      <div className="grid gap-4 md:grid-cols-2">
        <Field label={t('product.supplier')} error={errors.supplier}>
          <Input value={draft.supplier} onChange={(e) => patch({ supplier: e.target.value })} maxLength={200} />
        </Field>
        <Field label={t('product.manufacturer')} error={errors.manufacturer}>
          <Input value={draft.manufacturer} onChange={(e) => patch({ manufacturer: e.target.value })} maxLength={200} />
        </Field>
        <Field label={t('product.country')} error={errors.country}>
          <Input value={draft.country} onChange={(e) => patch({ country: e.target.value })} maxLength={64} />
        </Field>
        <Field label={t('product.warranty')} error={errors.warrantyMonths}>
          <Input inputMode="numeric" value={draft.warrantyMonths} onChange={(e) => patch({ warrantyMonths: e.target.value })} error={!!errors.warrantyMonths} />
        </Field>
        <Field label={t('product.shelfLife')} error={errors.shelfLifeDays}>
          <Input inputMode="numeric" value={draft.shelfLifeDays} onChange={(e) => patch({ shelfLifeDays: e.target.value })} error={!!errors.shelfLifeDays} />
        </Field>
        <Field label={t('product.vat')} error={errors.vatRate} hint={t('product.vatHint')}>
          <Input inputMode="decimal" value={draft.vatRate} onChange={(e) => patch({ vatRate: e.target.value })} error={!!errors.vatRate} />
        </Field>
        <div className="md:col-span-2">
          <Toggle checked={draft.isMarked} onChange={(isMarked) => patch({ isMarked })} label={t('product.marked')} />
        </div>
        <div className="md:col-span-2">
          <Field label={t('product.notes')} hint={t('product.notesHint')}>
            <Textarea value={draft.notes} onChange={(e) => patch({ notes: e.target.value })} maxLength={1000} />
          </Field>
        </div>
      </div>
    </div>
  )
}
