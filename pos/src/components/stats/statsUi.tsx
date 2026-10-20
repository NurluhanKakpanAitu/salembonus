import { useEffect, useRef, useState, type ReactNode } from 'react'
import { ArrowDownRight, ArrowUpRight, CalendarDays, Check, ChevronDown, Download, FileSpreadsheet, FileText, Info, Loader2, Store } from 'lucide-react'
import { Button } from '../ui/Button'
import type { Compare, Kpi, SeriesKind } from '../../lib/analyticsTypes'
import { addDays, isoDate } from '../../lib/dates'
import { num } from '../../lib/money'
import { useT, type TranslationKey } from '../../lib/i18n'
import { formatKind, periodText } from './statsFormat'

// ---------- Карточка, қате, бос күй (§24) ----------

export function Card({ title, icon, info, action, className = '', children }: {
  title: string; icon?: ReactNode; info?: string; action?: ReactNode; className?: string; children: ReactNode
}) {
  return (
    <section className={`min-w-0 rounded-2xl border border-[#e4ecf7] bg-surface p-4 shadow-[0_1px_2px_rgba(16,24,40,0.04)] ${className}`}>
      <div className="mb-3 flex items-center gap-2">
        {icon && <span className="text-brand">{icon}</span>}
        <h3 className="text-[15px] font-bold text-[#0b1f44]">{title}</h3>
        {info && <InfoTip text={info} />}
        <div className="ml-auto">{action}</div>
      </div>
      {children}
    </section>
  )
}

/** ⓘ: анықтама мен формула — тінтуірмен де, пернетақта фокусымен де ашылады (§24). */
export function InfoTip({ text }: { text: string }) {
  const [open, setOpen] = useState(false)
  return (
    <span className="relative inline-flex">
      <button type="button" aria-label={text} onMouseEnter={() => setOpen(true)} onMouseLeave={() => setOpen(false)}
        onFocus={() => setOpen(true)} onBlur={() => setOpen(false)} onClick={(e) => { e.stopPropagation(); setOpen((o) => !o) }}
        onKeyDown={(e) => e.stopPropagation()}
        className="rounded-full text-ink-3 hover:text-brand focus-visible:outline-2 focus-visible:outline-brand"><Info size={14} /></button>
      {open && (
        <span role="tooltip" className="absolute left-1/2 top-6 z-30 w-64 -translate-x-1/2 rounded-xl border border-line bg-surface px-3 py-2 text-[12px] font-normal leading-snug text-ink-2 shadow-lg">
          {text}
        </span>
      )}
    </span>
  )
}

export function BlockError({ message, onRetry }: { message: string; onRetry: () => void }) {
  const t = useT()
  return (
    <div role="alert" className="flex items-center justify-between gap-3 rounded-xl bg-danger-soft px-4 py-3 text-[13px] text-danger">
      <span>{t('stats.loadError')}<span className="block text-[12px] opacity-80">{message}</span></span>
      <button type="button" onClick={onRetry} className="shrink-0 font-semibold underline">{t('common.retry')}</button>
    </div>
  )
}

export function Skeleton({ className = 'h-24' }: { className?: string }) {
  return <div className={`animate-pulse rounded-2xl bg-[#e9eff8] ${className}`} />
}

export function Empty({ text, action }: { text: string; action?: ReactNode }) {
  return <div className="flex flex-col items-center justify-center gap-2 py-8 text-center text-[13px] text-ink-3">{text}{action}</div>
}

// ---------- KPI (§8) ----------

export const KPI_TONES = {
  blue: 'bg-brand-soft text-brand', green: 'bg-success-soft text-success', violet: 'bg-violet-50 text-violet-600',
  coral: 'bg-danger-soft text-danger', amber: 'bg-amber-50 text-amber-600', pink: 'bg-pink-50 text-pink-600', sky: 'bg-sky-50 text-sky-600',
} as const

/**
 * KPI карточкасы: мән, (қажет болса) екінші мән, салыстыру — абсолют пен пайыз, «Нет базы для сравнения»; түс жалғыз белгі емес —
 * бағыт көрсеткісі мен «+/−» мәтіні бар. invert — өсу жаман (қайтарым, қарыз).
 */
export function KpiCard({ k, label, icon, tone, kind = 'money', amountKind = 'money', info, compare, invert, onClick, small, sub }: {
  k: Kpi | undefined
  label: string
  icon: ReactNode
  tone: keyof typeof KPI_TONES
  kind?: SeriesKind | 'percent'
  amountKind?: SeriesKind
  info?: string
  compare: Compare
  invert?: boolean
  onClick?: () => void
  small?: boolean
  sub?: ReactNode
}) {
  const t = useT()
  const body = (
    <div className={`flex h-full gap-3 rounded-2xl border border-[#e4ecf7] bg-surface text-left shadow-[0_1px_2px_rgba(16,24,40,0.04)] ${small ? 'p-3' : 'p-4'} ${onClick ? 'transition hover:border-brand/40 hover:shadow-md' : ''}`}>
      <span className={`flex shrink-0 items-center justify-center rounded-xl ${small ? 'size-10' : 'size-12'} ${KPI_TONES[tone]}`}>{icon}</span>
      <div className="min-w-0 flex-1">
        <div className="flex items-center gap-1 text-[12px] font-medium text-ink-2">
          <span className="truncate">{label}</span>{info && <InfoTip text={info} />}
        </div>
        {!k ? <div className="mt-1 h-6 w-24 animate-pulse rounded bg-field" />
          : !k.available || k.value === null ? (
            <div className="mt-1 text-[15px] font-bold text-ink-3">{t('stats.unavailable')}
              {k.note && <span className="block text-[11px] font-normal">{t(`stats.note.${k.note}` as TranslationKey)}</span>}
            </div>
          ) : (
            <>
              <div className={`mt-0.5 truncate font-extrabold tracking-tight text-[#0b1f44] ${small ? 'text-[19px]' : 'text-[23px]'}`}>
                {formatKind(k.value, kind)}
                {k.amount !== null && k.amount !== undefined && <span className="ml-1.5 text-[15px] font-bold text-ink-2">· {formatKind(k.amount, amountKind)}</span>}
              </div>
              {sub ?? <Change k={k} compare={compare} invert={invert} kind={kind} />}
            </>
          )}
      </div>
    </div>
  )
  // Карточка ішінде ⓘ батырмасы бар — сондықтан сыртқы элемент <button> емес, пернетақтамен басылатын role="link" (§24, §29).
  return onClick ? (
    <div role="link" tabIndex={0} onClick={onClick} onKeyDown={(e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); onClick() } }}
      className="block w-full cursor-pointer rounded-2xl focus-visible:outline-2 focus-visible:outline-brand">{body}</div>
  ) : body
}

export function Change({ k, compare, invert, kind }: { k: Kpi; compare: Compare; invert?: boolean; kind?: SeriesKind | 'percent' }) {
  const t = useT()
  if (compare === 'none' || k.previous === null) return null
  if (k.changePercent === null) return <div className="mt-0.5 text-[11px] text-ink-3">{t('stats.noBase')}</div>
  const up = k.changePercent > 0
  const good = k.changePercent === 0 ? null : invert ? !up : up
  const Arrow = up ? ArrowUpRight : ArrowDownRight
  return (
    <div className={`mt-0.5 flex flex-wrap items-center gap-1 text-[11px] font-semibold ${good === null ? 'text-ink-3' : good ? 'text-success' : 'text-danger'}`}>
      {k.changePercent !== 0 && <Arrow size={13} />}
      {up ? '+' : ''}{num(k.changePercent)}%
      <span className="font-normal text-ink-3">({k.changeAbs !== null && k.changeAbs > 0 ? '+' : ''}{formatKind(k.changeAbs, kind === 'percent' ? 'count' : kind ?? 'money')}) {t(`stats.cmp.${compare}` as TranslationKey)}</span>
    </div>
  )
}

// ---------- Popover (басқанда ашылады, сыртқа басса не Esc — жабылады) ----------

function usePopover() {
  const [open, setOpen] = useState(false)
  const ref = useRef<HTMLDivElement>(null)
  useEffect(() => {
    if (!open) return
    const close = (e: MouseEvent) => !ref.current?.contains(e.target as Node) && setOpen(false)
    const key = (e: KeyboardEvent) => e.key === 'Escape' && setOpen(false)
    document.addEventListener('mousedown', close)
    document.addEventListener('keydown', key)
    return () => { document.removeEventListener('mousedown', close); document.removeEventListener('keydown', key) }
  }, [open])
  return { open, setOpen, ref }
}

const triggerCls = 'flex h-11 items-center gap-2 rounded-xl border border-line bg-surface px-3.5 text-[14px] font-medium hover:bg-field focus-visible:outline-2 focus-visible:outline-brand'
const panelCls = 'absolute right-0 top-12 z-40 rounded-2xl border border-line bg-surface p-3 shadow-xl'

// ---------- Кезең (§5) ----------

export type Preset = 'today' | 'yesterday' | 'week' | 'days30' | 'thisMonth' | 'lastMonth' | 'quarter' | 'year'
export const PRESETS: Preset[] = ['today', 'yesterday', 'week', 'days30', 'thisMonth', 'lastMonth', 'quarter', 'year']

export function presetRange(p: Preset): { from: string; to: string } {
  const d = new Date()
  const today = isoDate(d)
  switch (p) {
    case 'today': return { from: today, to: today }
    case 'yesterday': return { from: isoDate(addDays(d, -1)), to: isoDate(addDays(d, -1)) }
    case 'week': return { from: isoDate(addDays(d, -6)), to: today }
    case 'days30': return { from: isoDate(addDays(d, -29)), to: today }
    case 'thisMonth': return { from: isoDate(new Date(d.getFullYear(), d.getMonth(), 1)), to: today }
    case 'lastMonth': return { from: isoDate(new Date(d.getFullYear(), d.getMonth() - 1, 1)), to: isoDate(new Date(d.getFullYear(), d.getMonth(), 0)) }
    case 'quarter': return { from: isoDate(new Date(d.getFullYear(), Math.floor(d.getMonth() / 3) * 3, 1)), to: today }
    case 'year': return { from: isoDate(new Date(d.getFullYear(), 0, 1)), to: today }
  }
}

export const presetOf = (from: string, to: string): Preset | null => PRESETS.find((p) => { const r = presetRange(p); return r.from === from && r.to === to }) ?? null

/** «Произвольный период»: «С», «По», «Применить», «Отмена» — «Отмена» кезеңді өзгертпейді; соңы басынан ерте бола алмайды. */
export function PeriodPicker({ from, to, onChange }: { from: string; to: string; onChange: (r: { from: string; to: string }) => void }) {
  const t = useT()
  const { open, setOpen, ref } = usePopover()
  const [draft, setDraft] = useState({ from, to })
  useEffect(() => { if (open) setDraft({ from, to }) }, [open, from, to])
  const active = presetOf(from, to)
  const invalid = !draft.from || !draft.to || draft.to < draft.from

  return (
    <div ref={ref} className="relative">
      <button type="button" className={triggerCls} aria-haspopup="dialog" aria-expanded={open} onClick={() => setOpen((o) => !o)}>
        <CalendarDays size={17} className="text-ink-2" />{periodText(from, to)}<ChevronDown size={16} className="text-ink-3" />
      </button>
      {open && (
        <div role="dialog" aria-label={t('stats.period')} className={`${panelCls} flex w-[min(92vw,460px)] flex-col gap-3 sm:flex-row`}>
          <ul className="grid grid-cols-2 gap-1 sm:w-40 sm:grid-cols-1">
            {PRESETS.map((p) => (
              <li key={p}>
                <button type="button" onClick={() => { onChange(presetRange(p)); setOpen(false) }}
                  className={`w-full rounded-lg px-3 py-2 text-left text-[13px] ${active === p ? 'bg-brand-soft font-semibold text-brand' : 'hover:bg-field'}`}>
                  {t(`stats.preset.${p}` as TranslationKey)}
                </button>
              </li>
            ))}
          </ul>
          <div className="flex flex-1 flex-col gap-2 border-t border-line pt-3 sm:border-l sm:border-t-0 sm:pl-3 sm:pt-0">
            <div className="text-[13px] font-semibold">{t('stats.preset.custom')}</div>
            <label className="text-[12px] text-ink-2">{t('stats.from')}
              <input type="date" value={draft.from} onChange={(e) => setDraft((d) => ({ ...d, from: e.target.value }))}
                className="mt-1 h-10 w-full rounded-lg border border-line bg-field px-2 text-[14px] outline-none focus:border-brand" />
            </label>
            <label className="text-[12px] text-ink-2">{t('stats.to')}
              <input type="date" value={draft.to} min={draft.from} onChange={(e) => setDraft((d) => ({ ...d, to: e.target.value }))}
                className="mt-1 h-10 w-full rounded-lg border border-line bg-field px-2 text-[14px] outline-none focus:border-brand" />
            </label>
            {draft.to < draft.from && <p className="text-[12px] text-danger">{t('stats.endBeforeStart')}</p>}
            <div className="mt-auto flex justify-end gap-2">
              <Button variant="secondary" className="h-9! px-3! text-[13px]!" onClick={() => setOpen(false)}>{t('common.cancel')}</Button>
              <Button className="h-9! px-3! text-[13px]!" disabled={invalid} onClick={() => { onChange(draft); setOpen(false) }}>{t('stats.apply')}</Button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}

// ---------- Дүкендер (§5) ----------

export function StorePicker({ stores, value, onChange }: {
  stores: { id: string; name: string }[]
  value: string[]
  onChange: (ids: string[]) => void
}) {
  const t = useT()
  const { open, setOpen, ref } = usePopover()
  const [draft, setDraft] = useState<string[]>(value)
  const [q, setQ] = useState('')
  useEffect(() => { if (open) { setDraft(value); setQ('') } }, [open, value])
  const label = value.length === stores.length && stores.length > 1 ? t('stats.allStores')
    : value.length === 1 ? stores.find((s) => s.id === value[0])?.name ?? '—' : t('stats.storesN', { n: value.length })
  const shown = stores.filter((s) => s.name.toLowerCase().includes(q.trim().toLowerCase()))
  const toggle = (id: string) => setDraft((d) => (d.includes(id) ? d.filter((x) => x !== id) : [...d, id]))

  return (
    <div ref={ref} className="relative">
      <button type="button" className={triggerCls} aria-haspopup="dialog" aria-expanded={open} onClick={() => setOpen((o) => !o)}>
        <Store size={17} className="text-ink-2" /><span className="max-w-44 truncate">{label}</span><ChevronDown size={16} className="text-ink-3" />
      </button>
      {open && (
        <div role="dialog" aria-label={t('stats.stores')} className={`${panelCls} w-72`}>
          {stores.length > 6 && (
            <input value={q} onChange={(e) => setQ(e.target.value)} placeholder={t('stats.storeSearch')}
              className="mb-2 h-9 w-full rounded-lg border border-line bg-field px-2.5 text-[13px] outline-none focus:border-brand" />
          )}
          {stores.length > 1 && (
            <label className="flex cursor-pointer items-center gap-2 rounded-lg px-2 py-1.5 text-[13px] font-semibold hover:bg-field">
              <input type="checkbox" checked={draft.length === stores.length} onChange={(e) => setDraft(e.target.checked ? stores.map((s) => s.id) : [])} />
              {t('stats.allStores')}
            </label>
          )}
          <ul className="max-h-64 overflow-y-auto">
            {shown.map((s) => (
              <li key={s.id}>
                <label className="flex cursor-pointer items-center gap-2 rounded-lg px-2 py-1.5 text-[13px] hover:bg-field">
                  <input type="checkbox" checked={draft.includes(s.id)} onChange={() => toggle(s.id)} />{s.name}
                </label>
              </li>
            ))}
          </ul>
          <div className="mt-2 flex justify-end gap-2 border-t border-line pt-2">
            <Button variant="secondary" className="h-9! px-3! text-[13px]!" onClick={() => setOpen(false)}>{t('common.cancel')}</Button>
            <Button className="h-9! px-3! text-[13px]!" disabled={draft.length === 0} onClick={() => { onChange(draft); setOpen(false) }}>{t('stats.apply')}</Button>
          </div>
        </div>
      )}
    </div>
  )
}

// ---------- Экспорт (§6): dropdown, модаль емес ----------

export function ExportMenu({ busy, onExcel, onCsv, onPdf }: { busy: boolean; onExcel: () => void; onCsv: () => void; onPdf: () => void }) {
  const t = useT()
  const { open, setOpen, ref } = usePopover()
  const item = 'flex w-full items-center gap-2.5 rounded-lg px-3 py-2 text-left text-[14px] hover:bg-field'
  return (
    <div ref={ref} className="relative">
      <Button icon={busy ? <Loader2 size={17} className="animate-spin" /> : <Download size={17} />} aria-haspopup="menu" aria-expanded={open} onClick={() => setOpen((o) => !o)}>
        {busy ? t('stats.exportBusy') : t('stats.export')}
      </Button>
      {open && (
        <div role="menu" className={`${panelCls} w-56 p-1.5`}>
          <button type="button" role="menuitem" className={item} onClick={() => { setOpen(false); onExcel() }}><FileSpreadsheet size={17} className="text-success" />Excel (.xlsx)</button>
          <button type="button" role="menuitem" className={item} onClick={() => { setOpen(false); onCsv() }}><FileSpreadsheet size={17} className="text-ink-2" />CSV</button>
          <button type="button" role="menuitem" className={item} onClick={() => { setOpen(false); onPdf() }}><FileText size={17} className="text-danger" />{t('stats.pdfReport')}</button>
        </div>
      )}
    </div>
  )
}

/** Таңдау тобы (вкладка ішіндегі ауыстырғыштар: «По количеству / По сумме / По прибыли»). */
export function Segmented<T extends string>({ value, options, onChange, disabled }: {
  value: T; options: { key: T; label: string }[]; onChange: (v: T) => void; disabled?: Partial<Record<T, string>>
}) {
  return (
    <div role="tablist" className="inline-flex flex-wrap gap-1 rounded-xl bg-field p-1">
      {options.map((o) => (
        <button key={o.key} type="button" role="tab" aria-selected={value === o.key} disabled={!!disabled?.[o.key]} title={disabled?.[o.key]}
          onClick={() => onChange(o.key)}
          className={`rounded-lg px-3 py-1.5 text-[12px] font-semibold disabled:cursor-not-allowed disabled:opacity-40 ${value === o.key ? 'bg-surface text-brand shadow-sm' : 'text-ink-2 hover:text-ink'}`}>
          {o.label}
        </button>
      ))}
    </div>
  )
}

export function CheckMark() { return <Check size={14} /> }
