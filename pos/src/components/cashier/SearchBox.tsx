import { useEffect, useRef, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Barcode, Search, X } from 'lucide-react'
import { cashierApi } from '../../lib/cashierApi'
import type { CashierProduct } from '../../lib/cashierTypes'
import { tenge } from '../../lib/money'
import { useT } from '../../lib/i18n'
import { ProductImage, StockBadge } from './CatalogPanel'

function useDebounced<T>(value: T, ms: number) {
  const [v, setV] = useState(value)
  useEffect(() => {
    const id = setTimeout(() => setV(value), ms)
    return () => clearTimeout(id)
  }, [value, ms])
  return v
}

/** Сәйкес бөлікті ерекшелеу: «Тормозные <b>колодки</b>». */
function Highlight({ text, q }: { text: string; q: string }) {
  const i = q ? text.toLowerCase().indexOf(q.toLowerCase()) : -1
  if (i < 0) return <>{text}</>
  return <>{text.slice(0, i)}<mark className="rounded bg-brand-soft px-0.5 text-brand">{text.slice(i, i + q.length)}</mark>{text.slice(i + q.length)}</>
}

/**
 * Тауар іздеу (ТЗ «Касса» §2.1, §3.6–3.8): атауы, артикулы не штрихкоды бойынша, нәтиже бетті
 * ауыстырмай ашылады. Enter: штрихкод дәл сәйкес келсе не нәтиже біреу болса — бірден себетке.
 */
export function SearchBox({ onAdd, onShowAll, onScan }: {
  onAdd: (p: CashierProduct) => void
  onShowAll: (q: string) => void
  onScan: (code: string) => Promise<boolean>
}) {
  const t = useT()
  const [q, setQ] = useState('')
  const [open, setOpen] = useState(false)
  const [active, setActive] = useState(0)
  const ref = useRef<HTMLDivElement>(null)
  const input = useRef<HTMLInputElement>(null)
  const term = useDebounced(q.trim(), 250)

  const { data, isFetching } = useQuery({
    queryKey: ['cashier', 'search', term],
    queryFn: () => cashierApi.catalog({ search: term, sort: 'popular', page: 1, pageSize: 8 }),
    enabled: term.length >= 2,
  })
  const items = term.length >= 2 ? data?.items ?? [] : []

  useEffect(() => {
    const close = (e: MouseEvent) => !ref.current?.contains(e.target as Node) && setOpen(false)
    document.addEventListener('mousedown', close)
    return () => document.removeEventListener('mousedown', close)
  }, [])

  const reset = () => {
    setQ('')
    setOpen(false)
    setActive(0)
  }

  const pick = (p: CashierProduct) => {
    onAdd(p)
    reset()
    input.current?.focus()
  }

  const onKeyDown = async (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Escape') return reset()
    if (e.key === 'ArrowDown') { e.preventDefault(); setActive((a) => Math.min(a + 1, items.length - 1)) }
    if (e.key === 'ArrowUp') { e.preventDefault(); setActive((a) => Math.max(a - 1, 0)) }
    if (e.key !== 'Enter') return
    e.preventDefault()
    const value = q.trim()
    if (!value) return
    // Сканер кодты теріп Enter басады: алдымен штрихкод ретінде тексереміз.
    if (/^[0-9A-Za-z.-]{4,}$/.test(value) && (await onScan(value))) return reset()
    if (items[active] && term === value) return pick(items[active])
    onShowAll(value)
    reset()
  }

  return (
    <div ref={ref} className="relative min-w-0 flex-1">
      <Search size={18} className="pointer-events-none absolute left-3.5 top-1/2 -translate-y-1/2 text-ink-3" />
      <input ref={input} value={q} data-pos-search
        onChange={(e) => { setQ(e.target.value); setOpen(true); setActive(0) }}
        onFocus={() => setOpen(true)} onKeyDown={(e) => void onKeyDown(e)}
        placeholder={t('pos.searchPlaceholder')}
        className="h-11 w-full rounded-xl border border-line bg-surface pl-10 pr-20 text-[15px] outline-none focus:border-brand" />
      <div className="absolute right-2 top-1/2 flex -translate-y-1/2 items-center gap-1 text-ink-3">
        {q && <button type="button" aria-label="Clear" onClick={reset} className="flex size-8 items-center justify-center rounded-lg hover:bg-field"><X size={16} /></button>}
        <span className="flex size-8 items-center justify-center" title={t('pos.scanHint')}><Barcode size={19} /></span>
      </div>

      {open && term.length >= 2 && (
        <div className="absolute inset-x-0 top-full z-30 mt-1.5 overflow-hidden rounded-2xl border border-line bg-surface shadow-xl">
          {items.length === 0 && (
            <p className="px-4 py-4 text-[14px] text-ink-3">{isFetching ? t('common.loading') : t('catalog.nothingFound')}</p>
          )}
          {items.map((p, i) => (
            <button key={p.id} type="button" onMouseEnter={() => setActive(i)} onClick={() => pick(p)} disabled={p.price == null}
              className={`flex w-full items-center gap-3 px-4 py-2.5 text-left ${i === active ? 'bg-brand-soft/60' : ''}`}>
              <ProductImage url={p.imageUrl} className="size-12 shrink-0 rounded-lg" />
              <div className="min-w-0 flex-1">
                <div className="truncate text-[14px] font-semibold"><Highlight text={p.name} q={term} /></div>
                <div className="truncate text-[12px] text-ink-3">
                  {[p.article && t('pos.article', { a: p.article }), p.brandName].filter(Boolean).join(' · ')}
                </div>
              </div>
              <div className="shrink-0 text-right">
                <div className="text-[15px] font-bold">{p.price != null ? tenge(p.price) : t('pos.noPrice')}</div>
                <StockBadge p={p} />
              </div>
            </button>
          ))}
          {data && data.total > items.length && (
            <button type="button" onClick={() => { onShowAll(term); reset() }}
              className="flex w-full items-center justify-between border-t border-line px-4 py-3 text-[14px] text-brand hover:bg-field">
              <span className="flex items-center gap-2"><Search size={16} /> {t('pos.showAllFor', { q: term })}</span>
              <span className="text-ink-3">{t('pos.found', { n: data.total })}</span>
            </button>
          )}
        </div>
      )}
    </div>
  )
}

/**
 * USB-сканер пернетақта сияқты кодты тез теріп, Enter басады. Фокус өрісте болмаса да
 * (мысалы, кассир себетпен жұмыс істеп тұрса) кодты ұстап алып, тауарды қосамыз.
 */
export function useBarcodeScanner(onScan: (code: string) => void) {
  const handler = useRef(onScan)
  handler.current = onScan
  useEffect(() => {
    let buffer = ''
    let last = 0
    const onKey = (e: KeyboardEvent) => {
      const target = e.target as HTMLElement
      if (target.closest('input, textarea, select, [contenteditable=true]')) return
      const now = Date.now()
      if (now - last > 60) buffer = ''
      last = now
      if (e.key === 'Enter') {
        if (buffer.length >= 4) handler.current(buffer)
        buffer = ''
      } else if (e.key.length === 1) buffer += e.key
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [])
}
