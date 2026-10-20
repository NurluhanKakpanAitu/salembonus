import { useEffect, useRef, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Plus, ScanLine, Search, Trash2 } from 'lucide-react'
import { Input } from '../ui/Field'
import { toast } from '../ui/Toast'
import { ApiError } from '../../lib/api'
import { productApi } from '../../lib/productApi'
import type { ProductListItem } from '../../lib/catalogTypes'
import { useBarcodeScanner } from '../../lib/scanner'
import { num, tenge } from '../../lib/money'
import { useT } from '../../lib/i18n'
import { ProductThumb, Qty, td, th, useDebounced } from './inventoryUi'

/** Құжат жолы формада: сандар жол ретінде (бос, «12,5»), серверге жіберерде ғана санға айналады. */
export interface LineDraft {
  productId: string
  name: string
  article: string | null
  barcode: string | null
  imageUrl: string | null
  unit: string | null
  stockNow: number | null
  catalogRetailPrice: number | null
  quantity: string
  purchasePrice: string
  retailPrice: string
}

export const toNum = (raw: string): number | null => {
  const v = raw.trim().replace(/\s/g, '').replace(',', '.')
  if (!v) return null
  const n = Number(v)
  return Number.isFinite(n) ? n : null
}

export function lineFromProduct(p: ProductListItem): LineDraft {
  return {
    productId: p.id, name: p.name, article: p.article, barcode: p.barcode, imageUrl: p.imageUrl, unit: p.unitShortName,
    stockNow: null, catalogRetailPrice: null, quantity: '1', purchasePrice: '', retailPrice: '',
  }
}

/**
 * Құжат жолдарының редакторы (ТЗ «Склад» §7.3, §7.5): іздеу атау/артикул/штрихкод бойынша, сканер
 * форманың кез келген жерінде, Enter — келесі жол. Приёмкада баға бағандары, списание мен перемещенияда — тек сан.
 */
export function LinesEditor({ lines, onChange, mode, canRetail, disabled, enrich }: {
  lines: LineDraft[]
  onChange: (lines: LineDraft[]) => void
  mode: 'receipt' | 'writeoff' | 'transfer'
  canRetail?: boolean
  disabled?: boolean
  /** Жол қосылғанда толтырылатын қосымша дерек: таңдалған қоймадағы қалдық, каталогтағы розничная баға. */
  enrich?: (productId: string) => Promise<Partial<LineDraft>>
}) {
  const t = useT()
  const [query, setQuery] = useState('')
  const debounced = useDebounced(query.trim(), 250)
  const [open, setOpen] = useState(false)
  const [active, setActive] = useState(0)
  const boxRef = useRef<HTMLDivElement>(null)
  const inputRef = useRef<HTMLInputElement>(null)
  const qtyRefs = useRef<Record<string, HTMLInputElement | null>>({})

  const search = useQuery({
    queryKey: ['inventory', 'product-search', debounced],
    queryFn: () => productApi.list({ search: debounced, status: 'Active', page: 1, pageSize: 8 }),
    enabled: debounced.length >= 2 && !disabled,
  })
  const found = search.data?.items ?? []

  useEffect(() => {
    if (!open) return
    const close = (e: MouseEvent) => !boxRef.current?.contains(e.target as Node) && setOpen(false)
    document.addEventListener('mousedown', close)
    return () => document.removeEventListener('mousedown', close)
  }, [open])

  const add = async (p: ProductListItem) => {
    const existing = lines.find((l) => l.productId === p.id)
    if (existing) {
      onChange(lines.map((l) => (l.productId === p.id ? { ...l, quantity: String((toNum(l.quantity) ?? 0) + 1) } : l)))
    } else {
      const line = { ...lineFromProduct(p), ...(enrich ? await enrich(p.id) : {}) }
      onChange([...lines, line])
      setTimeout(() => qtyRefs.current[p.id]?.select(), 50)
    }
    setQuery('')
    setOpen(false)
  }

  const scan = async (code: string) => {
    try {
      await add(await productApi.byBarcode(code))
      toast(t('product.scanned', { code }))
    } catch (err) {
      toast(err instanceof ApiError && err.status === 404 ? t('pos.barcodeNotFound', { code }) : String(err), 'error')
    }
  }
  useBarcodeScanner((code) => void scan(code), !disabled)

  const onKey = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter') {
      e.preventDefault()
      // Сканер өріске жазса: ұзын сан + Enter → штрихкод.
      if (/^\d{6,}$/.test(query.trim())) return void scan(query.trim())
      if (found[active]) void add(found[active])
    } else if (e.key === 'ArrowDown') { e.preventDefault(); setActive((i) => Math.min(i + 1, found.length - 1)) }
    else if (e.key === 'ArrowUp') { e.preventDefault(); setActive((i) => Math.max(i - 1, 0)) }
    else if (e.key === 'Escape') setOpen(false)
  }

  const patch = (id: string, p: Partial<LineDraft>) => onChange(lines.map((l) => (l.productId === id ? { ...l, ...p } : l)))
  const remove = (id: string) => onChange(lines.filter((l) => l.productId !== id))
  const lineTotal = (l: LineDraft) => (toNum(l.quantity) ?? 0) * (toNum(l.purchasePrice) ?? 0)
  const showPrices = mode === 'receipt'

  return (
    <div>
      {!disabled && (
        <div ref={boxRef} className="relative mb-3">
          <div className="relative">
            <Search size={18} className="pointer-events-none absolute left-3.5 top-1/2 -translate-y-1/2 text-ink-3" />
            <Input ref={inputRef} className="pl-11 pr-11" value={query} placeholder={t('inv.lines.search')} autoComplete="off"
              onChange={(e) => { setQuery(e.target.value); setOpen(true); setActive(0) }} onFocus={() => setOpen(true)} onKeyDown={onKey} />
            <ScanLine size={18} className="pointer-events-none absolute right-3.5 top-1/2 -translate-y-1/2 text-ink-3" />
          </div>
          {open && debounced.length >= 2 && (
            <ul className="absolute inset-x-0 top-full z-20 mt-1 max-h-72 overflow-y-auto rounded-xl border border-line bg-surface py-1 shadow-lg">
              {search.isLoading && <li className="px-3.5 py-2.5 text-[14px] text-ink-3">{t('common.loading')}</li>}
              {!search.isLoading && found.length === 0 && <li className="px-3.5 py-2.5 text-[14px] text-ink-3">{t('catalog.nothingFound')}</li>}
              {found.map((p, i) => (
                <li key={p.id}>
                  <button type="button" onMouseDown={(e) => e.preventDefault()} onClick={() => void add(p)}
                    className={`flex w-full items-center gap-3 px-3.5 py-2 text-left ${i === active ? 'bg-field' : 'hover:bg-field'}`}>
                    <ProductThumb url={p.imageUrl} size={34} />
                    <span className="min-w-0 flex-1">
                      <span className="block truncate text-[14px] font-medium">{p.name}</span>
                      <span className="block truncate text-[12px] text-ink-3">{[p.article, p.barcode, p.brandName].filter(Boolean).join(' · ')}</span>
                    </span>
                    <Plus size={16} className="text-brand" />
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}

      <div className="overflow-x-auto rounded-xl border border-line">
        <table className="w-full min-w-[760px] border-collapse">
          <thead className="bg-field">
            <tr>
              <th className={`${th} w-10`}>№</th>
              <th className={th}>{t('inv.lines.product')}</th>
              <th className={th}>{t('inv.lines.code')}</th>
              <th className={`${th} text-right`}>{t('inv.lines.stockNow')}</th>
              <th className={`${th} w-32 text-right`}>{t('inv.lines.qty')}</th>
              {showPrices && <th className={`${th} w-36 text-right`}>{t('inv.lines.purchase')}</th>}
              {showPrices && <th className={`${th} w-36 text-right`}>{t('inv.lines.sum')}</th>}
              {showPrices && canRetail && <th className={`${th} w-36 text-right`}>{t('inv.lines.retail')}</th>}
              {!disabled && <th className="w-10" />}
            </tr>
          </thead>
          <tbody>
            {lines.length === 0 && (
              <tr><td colSpan={9} className="px-3 py-10 text-center text-[14px] text-ink-3">{t('inv.lines.empty')}</td></tr>
            )}
            {lines.map((l, i) => (
              <tr key={l.productId} className="border-t border-line">
                <td className={`${td} text-ink-3`}>{i + 1}</td>
                <td className={td}>
                  <div className="flex items-center gap-2.5">
                    <ProductThumb url={l.imageUrl} size={36} />
                    <span className="min-w-0">
                      <span className="block truncate font-semibold">{l.name}</span>
                      {l.unit && <span className="block text-[12px] text-ink-3">{l.unit}</span>}
                    </span>
                  </div>
                </td>
                <td className={`${td} text-[13px] text-ink-2`}>
                  {l.article && <div>{l.article}</div>}
                  {l.barcode && <div className="font-mono text-[12px] text-ink-3">{l.barcode}</div>}
                </td>
                <td className={`${td} text-right`}>{l.stockNow == null ? '—' : <Qty value={l.stockNow} />}</td>
                <td className={td}>
                  <Input ref={(el) => { qtyRefs.current[l.productId] = el }} inputMode="decimal" className="h-10! text-right" value={l.quantity}
                    disabled={disabled} onChange={(e) => patch(l.productId, { quantity: e.target.value })}
                    onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); inputRef.current?.focus() } }} />
                </td>
                {showPrices && (
                  <td className={td}>
                    <Input inputMode="decimal" className="h-10! text-right" value={l.purchasePrice} disabled={disabled} placeholder="0"
                      onChange={(e) => patch(l.productId, { purchasePrice: e.target.value })} />
                  </td>
                )}
                {showPrices && <td className={`${td} text-right font-semibold`}>{tenge(lineTotal(l))}</td>}
                {showPrices && canRetail && (
                  <td className={td}>
                    <Input inputMode="decimal" className="h-10! text-right" value={l.retailPrice} disabled={disabled}
                      placeholder={l.catalogRetailPrice != null ? num(l.catalogRetailPrice) : '—'}
                      title={l.catalogRetailPrice != null ? t('inv.lines.retailFromCatalog', { price: tenge(l.catalogRetailPrice) }) : undefined}
                      onChange={(e) => patch(l.productId, { retailPrice: e.target.value })} />
                  </td>
                )}
                {!disabled && (
                  <td className={td}>
                    <button type="button" aria-label="Remove" onClick={() => remove(l.productId)}
                      className="flex size-9 items-center justify-center rounded-lg text-ink-3 hover:bg-field hover:text-danger"><Trash2 size={16} /></button>
                  </td>
                )}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}
