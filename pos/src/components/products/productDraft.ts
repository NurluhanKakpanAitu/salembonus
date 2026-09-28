import type { Product, SaveProduct } from '../../lib/catalogTypes'

/**
 * Форманың күйі. Сандар жол ретінде сақталады: пайдаланушы «12,5» не бос жол теруі мүмкін,
 * серверге жіберерде ғана санға айналады.
 */
export interface ProductDraft {
  name: string
  article: string
  unitId: string
  brandId: string
  nodeId: string | null
  description: string
  supplier: string
  manufacturer: string
  country: string
  warrantyMonths: string
  shelfLifeDays: string
  vatRate: string
  isMarked: boolean
  notes: string
  primaryBarcode: string
  extraBarcodes: string[]
  images: string[]
  primaryImage: string | null
  characteristics: Record<string, string>
  /** Жасалған тауардың сату бағасы (ағымдағы дүкен). Жаңа тауарда баға opening ішінде. */
  salePrice: string
  opening: { warehouseId: string; quantity: string; purchasePrice: string; salePrice: string }
}

export type SectionKey = 'main' | 'stock' | 'classification' | 'characteristics' | 'media' | 'extra'

export const SECTIONS: SectionKey[] = ['main', 'stock', 'classification', 'characteristics', 'media', 'extra']

/** Сервер қайтарған қате өрісі қай бөлімде. */
export function sectionOfField(field: string | null): SectionKey {
  if (!field) return 'main'
  if (field.startsWith('opening') || field === 'salePrice') return 'stock'
  if (field === 'nodeId') return 'classification'
  if (field === 'characteristics') return 'characteristics'
  if (field === 'barcodes' || field === 'images') return 'media'
  if (['supplier', 'manufacturer', 'country', 'warrantyMonths', 'shelfLifeDays', 'vatRate', 'notes'].includes(field)) return 'extra'
  return 'main'
}

export function emptyDraft(nodeId: string | null, unitId: string, warehouseId: string): ProductDraft {
  return {
    name: '', article: '', unitId, brandId: '', nodeId, description: '',
    supplier: '', manufacturer: '', country: '', warrantyMonths: '', shelfLifeDays: '', vatRate: '', isMarked: false, notes: '',
    primaryBarcode: '', extraBarcodes: [], images: [], primaryImage: null, characteristics: {}, salePrice: '',
    opening: { warehouseId, quantity: '', purchasePrice: '', salePrice: '' },
  }
}

export function draftFromProduct(p: Product): ProductDraft {
  const primary = p.barcodes.find((b) => b.isPrimary)?.barcode ?? p.barcodes[0]?.barcode ?? ''
  return {
    name: p.name, article: p.article ?? '', unitId: p.unitId, brandId: p.brandId ?? '', nodeId: p.nodeId,
    description: p.description ?? '', supplier: p.supplier ?? '', manufacturer: p.manufacturer ?? '', country: p.country ?? '',
    warrantyMonths: p.warrantyMonths?.toString() ?? '', shelfLifeDays: p.shelfLifeDays?.toString() ?? '',
    vatRate: p.vatRate?.toString() ?? '', isMarked: p.isMarked, notes: p.notes ?? '',
    primaryBarcode: primary,
    extraBarcodes: p.barcodes.map((b) => b.barcode).filter((b) => b !== primary),
    images: p.images.map((i) => i.url),
    primaryImage: p.images.find((i) => i.isPrimary)?.url ?? p.images[0]?.url ?? null,
    characteristics: Object.fromEntries(p.characteristics.map((c) => [c.definitionId, c.value])),
    salePrice: p.salePrice?.toString() ?? '',
    opening: { warehouseId: '', quantity: '', purchasePrice: '', salePrice: '' },
  }
}

/** «12,5» → 12.5; бос → null. Қате санды сервер тексереді (өріс астында қате шығады). */
export function toNumber(raw: string): number | null {
  const v = raw.trim().replace(/\s/g, '').replace(',', '.')
  if (!v) return null
  const n = Number(v)
  return Number.isFinite(n) ? n : NaN
}

export function toRequest(d: ProductDraft, isNew: boolean): SaveProduct {
  const opt = (v: string) => (v.trim() ? v.trim() : null)
  const int = (v: string) => {
    const n = toNumber(v)
    return n === null ? null : Math.trunc(n)
  }
  const barcodes = [d.primaryBarcode, ...d.extraBarcodes].map((b) => b.trim()).filter(Boolean)
  const hasOpening = isNew && [d.opening.quantity, d.opening.purchasePrice, d.opening.salePrice].some((v) => v.trim())
  return {
    name: d.name.trim(),
    article: opt(d.article),
    unitId: d.unitId || null,
    brandId: d.brandId || null,
    nodeId: d.nodeId,
    description: opt(d.description),
    supplier: opt(d.supplier),
    manufacturer: opt(d.manufacturer),
    country: opt(d.country),
    warrantyMonths: int(d.warrantyMonths),
    shelfLifeDays: int(d.shelfLifeDays),
    vatRate: toNumber(d.vatRate),
    isMarked: d.isMarked,
    notes: opt(d.notes),
    barcodes: barcodes.map((b, i) => ({ barcode: b, isPrimary: i === 0 && d.primaryBarcode.trim() !== '' })),
    images: d.images.map((url) => ({ url, isPrimary: url === (d.primaryImage ?? d.images[0]) })),
    characteristics: Object.entries(d.characteristics)
      .filter(([, v]) => v.trim() && v.trim() !== '..')
      .map(([definitionId, value]) => ({ definitionId, value: value.trim() })),
    opening: hasOpening
      ? {
          warehouseId: d.opening.warehouseId || null,
          quantity: toNumber(d.opening.quantity),
          purchasePrice: toNumber(d.opening.purchasePrice),
          salePrice: toNumber(d.opening.salePrice),
        }
      : null,
    salePrice: isNew ? null : toNumber(d.salePrice),
  }
}
