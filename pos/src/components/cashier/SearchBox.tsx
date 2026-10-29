import { useEffect, useRef, useState } from 'react'
import { Barcode, Search, X } from 'lucide-react'
import { useFitPlaceholder } from '../ui/SearchInput'
import { useT } from '../../lib/i18n'

/**
 * Тауар іздеу (ТЗ «Касса» §2.1, §3.6–3.8): атауы, артикулы не штрихкоды бойынша. Тізім ашылмайды — нәтиже бірден ортадағы
 * тауарлар торында көрінеді (теру тоқтаған соң 250 мс), таңдалған санат ішінде. Enter: штрихкод дәл сәйкес келсе — бірден
 * себетке, әйтпесе іздеу кідіріссіз қолданылады. Esc не ✕ — іздеуді тазалайды.
 */
export function SearchBox({ value, onSearch, onScan }: {
  value: string
  onSearch: (q: string) => void
  onScan: (code: string) => Promise<boolean>
}) {
  const t = useT()
  const [q, setQ] = useState(value)
  const ref = useRef<HTMLDivElement>(null)
  const searchPlaceholder = useFitPlaceholder(t('pos.searchPlaceholder'), ref, 140)

  // Іздеу сырттан өзгерсе (торда «тазалау»), өріс те соған келеді.
  useEffect(() => { setQ((cur) => (cur.trim() === value ? cur : value)) }, [value])
  useEffect(() => {
    const term = q.trim()
    if (term === value) return
    const id = setTimeout(() => onSearch(term), 250)
    return () => clearTimeout(id)
  }, [q, value, onSearch])

  const clear = () => { setQ(''); onSearch('') }

  const onKeyDown = async (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Escape') return clear()
    if (e.key !== 'Enter') return
    e.preventDefault()
    const term = q.trim()
    if (!term) return
    // Сканер кодты теріп Enter басады: алдымен штрихкод ретінде тексереміз.
    if (/^[0-9A-Za-z.-]{4,}$/.test(term) && (await onScan(term))) return clear()
    onSearch(term)
  }

  return (
    <div ref={ref} className="relative min-w-[220px] flex-1 basis-60">
      <Search size={18} className="pointer-events-none absolute left-3.5 top-1/2 -translate-y-1/2 text-ink-3" />
      <input value={q} data-pos-search aria-label={t('pos.searchPlaceholder')}
        onChange={(e) => setQ(e.target.value)} onKeyDown={(e) => void onKeyDown(e)}
        placeholder={searchPlaceholder}
        className="h-11 w-full rounded-xl border border-line bg-surface pl-10 pr-20 text-[15px] outline-none focus:border-brand" />
      <div className="absolute right-2 top-1/2 flex -translate-y-1/2 items-center gap-1 text-ink-3">
        {q && <button type="button" aria-label={t('pos.clearSearch')} onClick={clear} className="flex size-8 items-center justify-center rounded-lg hover:bg-field"><X size={16} /></button>}
        <span className="flex size-8 items-center justify-center" title={t('pos.scanHint')}><Barcode size={19} /></span>
      </div>
    </div>
  )
}

export { useBarcodeScanner } from '../../lib/scanner'
