import { useEffect, useMemo, useRef, useState, type FormEvent } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useLocation, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { Archive, ArchiveRestore, ArrowLeft, Boxes, FolderTree, Image, Info, ListChecks, Loader2, SlidersHorizontal } from 'lucide-react'
import { Button } from '../../components/ui/Button'
import { StatusBadge } from '../../components/ui/StatusBadge'
import { toast } from '../../components/ui/Toast'
import {
  CharacteristicsSection, ClassificationSection, ExtraSection, MainSection, MediaSection, StockSection,
} from '../../components/products/ProductSections'
import {
  draftFromProduct, emptyDraft, sectionOfField, SECTIONS, toNumber, toRequest, type ProductDraft, type SectionKey,
} from '../../components/products/productDraft'
import { ApiError } from '../../lib/api'
import { catalogKeys, useBrands, useCatalogNodes, useCatalogPermissions, useCharacteristics, useUnits } from '../../lib/catalogHooks'
import { productApi } from '../../lib/productApi'
import { useT, type TranslationKey } from '../../lib/i18n'

const ICONS: Record<SectionKey, typeof Info> = {
  main: Info, stock: Boxes, classification: FolderTree, characteristics: ListChecks, media: Image, extra: SlidersHorizontal,
}

/** Сандық өрістер: бос болуы мүмкін, бірақ толтырылса — сан болуы керек. */
const NUMERIC: { field: string; get: (d: ProductDraft) => string }[] = [
  { field: 'opening.quantity', get: (d) => d.opening.quantity },
  { field: 'opening.purchasePrice', get: (d) => d.opening.purchasePrice },
  { field: 'opening.salePrice', get: (d) => d.opening.salePrice },
  { field: 'salePrice', get: (d) => d.salePrice },
  { field: 'warrantyMonths', get: (d) => d.warrantyMonths },
  { field: 'shelfLifeDays', get: (d) => d.shelfLifeDays },
  { field: 'vatRate', get: (d) => d.vatRate },
]

/** Сілтеме басылғанда сақталмаған өзгерісті сұрау (сол жақ мәзір, тақырып т.б.). */
function useLeaveGuard(dirty: boolean, message: string) {
  useEffect(() => {
    if (!dirty) return
    const onBeforeUnload = (e: BeforeUnloadEvent) => e.preventDefault()
    const onClick = (e: MouseEvent) => {
      const link = (e.target as HTMLElement).closest('a[href]')
      if (link && !window.confirm(message)) {
        e.preventDefault()
        e.stopPropagation()
      }
    }
    window.addEventListener('beforeunload', onBeforeUnload)
    document.addEventListener('click', onClick, true)
    return () => {
      window.removeEventListener('beforeunload', onBeforeUnload)
      document.removeEventListener('click', onClick, true)
    }
  }, [dirty, message])
}

/**
 * «Добавить / Редактировать товар» (ТЗ «Товар» §6): ішкі сол жақ навигация, алты бөлім.
 * Бөлімдер арасында ауысқанда енгізілген мән жоғалмайды — бәрі бір күйде.
 */
export function ProductFormPage() {
  const { id } = useParams()
  const isNew = !id
  const productQuery = useQuery({ queryKey: [...catalogKeys.products, 'one', id], queryFn: () => productApi.get(id!), enabled: !isNew })
  const unitsQuery = useUnits()
  const warehousesQuery = useQuery({ queryKey: ['warehouses'], queryFn: productApi.warehouses, enabled: isNew })

  const ready = isNew
    ? unitsQuery.data && warehousesQuery.data
    : productQuery.data && unitsQuery.data
  const t = useT()

  if (productQuery.error)
    return <p className="py-16 text-center text-[15px] text-danger">{productQuery.error.message}</p>
  if (!ready)
    return <div className="flex justify-center py-20 text-ink-3"><Loader2 className="animate-spin" size={28} aria-label={t('common.loading')} /></div>

  return <ProductForm key={id ?? 'new'} product={productQuery.data} />
}

function ProductForm({ product }: { product?: import('../../lib/catalogTypes').Product }) {
  const t = useT()
  const navigate = useNavigate()
  const location = useLocation()
  const [params] = useSearchParams()
  const qc = useQueryClient()
  const perms = useCatalogPermissions()
  const nodes = useCatalogNodes().data ?? []
  const units = useUnits().data ?? []
  const brands = useBrands().data ?? []
  const definitions = useCharacteristics().data ?? []
  const warehouses = useQuery({ queryKey: ['warehouses'], queryFn: productApi.warehouses, enabled: !product }).data ?? []

  const initial = useMemo<ProductDraft>(() => {
    if (product) return draftFromProduct(product)
    const unit = units.find((u) => u.status === 'Active' && u.shortName === 'шт') ?? units.find((u) => u.status === 'Active')
    const warehouse = warehouses.find((w) => w.isDefault) ?? warehouses[0]
    return emptyDraft(params.get('nodeId'), unit?.id ?? '', warehouse?.id ?? '')
    // Бастапқы күй бір рет есептеледі: кейін анықтамалық жаңарса да, форма өзгермеуі керек.
  }, [])
  const [draft, setDraft] = useState(initial)
  const [section, setSection] = useState<SectionKey>('main')
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [saving, setSaving] = useState(false)
  const savedRef = useRef(false)

  const canSave = product ? perms.edit : perms.create
  const dirty = JSON.stringify(draft) !== JSON.stringify(initial)
  useLeaveGuard(dirty && !savedRef.current, t('product.leaveConfirm'))

  const patch = (p: Partial<ProductDraft>) => setDraft((d) => ({ ...d, ...p }))
  const backTo = `/products${(location.state as { from?: string } | null)?.from ?? ''}`
  const close = () => {
    if (dirty && !window.confirm(t('product.leaveConfirm'))) return
    navigate(backTo)
  }

  const errorSections = new Set(Object.keys(errors).map((f) => (f === 'form' ? 'main' : sectionOfField(f))))

  const submit = async (e?: FormEvent) => {
    e?.preventDefault()
    const local: Record<string, string> = {}
    if (!draft.name.trim()) local.name = t('common.required')
    if (!draft.unitId) local.unitId = t('product.unitRequired')
    for (const n of NUMERIC) if (Number.isNaN(toNumber(n.get(draft)))) local[n.field] = t('product.numberInvalid')
    if (Object.keys(local).length) {
      setErrors(local)
      setSection(sectionOfField(Object.keys(local)[0]))
      return
    }

    setSaving(true)
    setErrors({})
    try {
      const body = toRequest(draft, !product)
      if (product) await productApi.update(product.id, body)
      else await productApi.create(body)
      savedRef.current = true
      await qc.invalidateQueries({ queryKey: catalogKeys.products })
      await qc.invalidateQueries({ queryKey: catalogKeys.nodes })
      toast(product ? t('catalog.saved') : t('product.created'))
      navigate(backTo)
    } catch (err) {
      if (err instanceof ApiError && err.field) {
        setErrors({ [err.field]: err.message })
        setSection(sectionOfField(err.field))
      } else setErrors({ form: err instanceof Error ? err.message : String(err) })
    } finally {
      setSaving(false)
    }
  }

  const lifecycle = async (action: 'archive' | 'restore') => {
    if (!product) return
    if (action === 'archive' && !window.confirm(t('product.archiveConfirm', { name: product.name }))) return
    try {
      await (action === 'archive' ? productApi.archive(product.id) : productApi.restore(product.id))
      await qc.invalidateQueries({ queryKey: catalogKeys.products })
      toast(action === 'archive' ? t('catalog.archived') : t('catalog.restored'))
    } catch (err) {
      toast(err instanceof Error ? err.message : String(err), 'error')
    }
  }

  const common = { draft, patch, errors }
  return (
    <form onSubmit={(e) => void submit(e)} noValidate>
      <div className="mb-5 flex flex-wrap items-center gap-3">
        <button type="button" onClick={close} aria-label={t('common.back')}
          className="flex size-10 items-center justify-center rounded-xl border border-line bg-surface text-ink-2 hover:bg-field">
          <ArrowLeft size={19} />
        </button>
        <div className="min-w-0">
          <h1 className="truncate text-[21px] font-bold">{product ? product.name : t('product.add')}</h1>
          <p className="text-[13px] text-ink-3">{product ? t('product.editTitle') : t('product.newHint')}</p>
        </div>
        {product && (
          <StatusBadge active={product.status === 'Active'} label={t(`status.${product.status}.m` as TranslationKey)} />
        )}
        <div className="ml-auto flex gap-2">
          {product && perms.lifecycle && (product.status === 'Active'
            ? <Button type="button" variant="secondary" icon={<Archive size={17} />} onClick={() => void lifecycle('archive')}>{t('catalog.archive')}</Button>
            : <Button type="button" variant="secondary" icon={<ArchiveRestore size={17} />} onClick={() => void lifecycle('restore')}>{t('catalog.restore')}</Button>)}
        </div>
      </div>

      <div className="grid gap-5 lg:grid-cols-[240px_1fr]">
        <nav className="flex gap-1 overflow-x-auto rounded-2xl border border-line bg-surface p-2 lg:sticky lg:top-4 lg:flex-col lg:self-start">
          {SECTIONS.map((key) => {
            const Icon = ICONS[key]
            const active = section === key
            return (
              <button key={key} type="button" onClick={() => setSection(key)}
                className={`flex shrink-0 items-center gap-2.5 rounded-xl px-3.5 py-2.5 text-left text-[14px] font-medium transition-colors ${
                  active ? 'bg-brand-soft text-brand' : 'text-ink-2 hover:bg-field hover:text-ink'}`}>
                <Icon size={18} />
                <span className="flex-1">{t(`product.section.${key}` as TranslationKey)}</span>
                {errorSections.has(key) && <span className="size-2 rounded-full bg-danger" />}
              </button>
            )
          })}
        </nav>

        <div className="min-w-0 rounded-2xl border border-line bg-surface p-5 lg:p-7">
          {section === 'main' && <MainSection {...common} units={units} brands={brands} />}
          {section === 'stock' && <StockSection {...common} warehouses={warehouses} product={product} canEditPrice={perms.edit} />}
          {section === 'classification' && <ClassificationSection {...common} nodes={nodes} />}
          {section === 'characteristics' && <CharacteristicsSection {...common} definitions={definitions} />}
          {section === 'media' && <MediaSection {...common} productId={product?.id} canUpload={canSave} />}
          {section === 'extra' && <ExtraSection {...common} />}
        </div>
      </div>

      <div className="sticky bottom-0 z-10 -mx-4 mt-5 flex flex-wrap items-center justify-end gap-2 border-t border-line bg-bg/95 px-4 py-3 backdrop-blur lg:-mx-6 lg:px-6">
        {errors.form && <p className="mr-auto text-[14px] text-danger">{errors.form}</p>}
        {!errors.form && Object.keys(errors).length > 0 && <p className="mr-auto text-[14px] text-danger">{t('product.fixErrors')}</p>}
        <Button type="button" variant="secondary" onClick={close}>{t('common.cancel')}</Button>
        {canSave && <Button type="submit" loading={saving}>{product ? t('common.save') : t('product.create')}</Button>}
      </div>
    </form>
  )
}
