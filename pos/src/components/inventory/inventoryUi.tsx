import { useEffect, useState, type ReactNode } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { ChevronLeft, ChevronRight } from 'lucide-react'
import { Field, Select, Textarea } from '../ui/Field'
import { Button } from '../ui/Button'
import { Modal } from '../ui/Modal'
import { toast } from '../ui/Toast'
import { ApiError } from '../../lib/api'
import { activeStore, can, useAuth } from '../../lib/auth'
import { addDays, isoDate } from '../../lib/dates'
import { inventoryKeys } from '../../lib/inventoryApi'
import type { DocumentStatus, TransferStatus } from '../../lib/inventoryTypes'
import { useT, type TranslationKey } from '../../lib/i18n'
import { Permission } from '../../lib/types'

export const th = 'px-3 py-3 text-left text-[13px] font-semibold text-ink-2'
export const td = 'px-3 py-2.5 text-[14px]'
export const PAGE_SIZES = [10, 20, 50, 100]

/** «Склад» құқықтары (фронт тек жасырады, соңғы тексеріс серверде). */
export function useInventoryPermissions() {
  const store = useAuth(activeStore)
  return {
    view: can(store, Permission.InventoryView),
    receive: can(store, Permission.InventoryReceive),
    post: can(store, Permission.InventoryPost),
    transfer: can(store, Permission.InventoryTransfer),
    writeOff: can(store, Permission.InventoryWriteOff),
    warehouses: can(store, Permission.InventoryWarehouses),
    suppliers: can(store, Permission.InventorySuppliers),
    retail: can(store, Permission.ProductsEdit),
  }
}

/** Әрекетті орындап, «Склад» сұраныстарын жаңартып, нәтижесін хабармен көрсетеді. */
export function useInventoryAction() {
  const qc = useQueryClient()
  return async (action: () => Promise<unknown>, success?: string) => {
    try {
      await action()
      await qc.invalidateQueries({ queryKey: inventoryKeys.all })
      if (success) toast(success)
      return true
    } catch (err) {
      toast(err instanceof ApiError ? err.message : String(err), 'error')
      return false
    }
  }
}

export function useDebounced<T>(value: T, ms: number) {
  const [debounced, setDebounced] = useState(value)
  useEffect(() => {
    const id = setTimeout(() => setDebounced(value), ms)
    return () => clearTimeout(id)
  }, [value, ms])
  return debounced
}

export function StatCard({ icon, label, value, hint, tone = 'brand' }: {
  icon: ReactNode; label: string; value: ReactNode; hint?: string; tone?: 'brand' | 'violet' | 'amber' | 'success' | 'danger'
}) {
  const tones = {
    brand: 'bg-brand-soft text-brand', violet: 'bg-violet-50 text-violet-600', amber: 'bg-amber-50 text-amber-600',
    success: 'bg-success-soft text-success', danger: 'bg-danger-soft text-danger',
  }
  return (
    <div className="rounded-2xl border border-line bg-surface p-4">
      <div className="flex items-center gap-2.5">
        <span className={`flex size-10 items-center justify-center rounded-xl ${tones[tone]}`}>{icon}</span>
        <span className="text-[13px] font-medium text-ink-2">{label}</span>
      </div>
      <div className="mt-2 truncate text-[22px] font-extrabold tracking-tight">{value}</div>
      {hint && <div className="mt-0.5 text-[12px] text-ink-3">{hint}</div>}
    </div>
  )
}

const DOC_TONE: Record<DocumentStatus | TransferStatus, string> = {
  Draft: 'bg-field text-ink-2', Posted: 'bg-success-soft text-success', Cancelled: 'bg-field text-ink-3', Reversed: 'bg-danger-soft text-danger',
  InTransit: 'bg-amber-100 text-amber-800', Completed: 'bg-success-soft text-success',
}

export function DocStatus({ status, kind }: { status: DocumentStatus | TransferStatus; kind: 'doc' | 'transfer' }) {
  const t = useT()
  return (
    <span className={`inline-flex h-7 items-center whitespace-nowrap rounded-lg px-2.5 text-[12px] font-semibold ${DOC_TONE[status]}`}>
      {t(`inv.status.${kind}.${status}` as TranslationKey)}
    </span>
  )
}

export type Period = 'today' | 'week' | 'month' | 'quarter' | 'all'
export const PERIODS: Period[] = ['today', 'week', 'month', 'quarter', 'all']

export function periodRange(p: Period): { from?: string; to?: string } {
  const today = new Date()
  switch (p) {
    case 'today': return { from: isoDate(today), to: isoDate(today) }
    case 'week': return { from: isoDate(addDays(today, -6)), to: isoDate(today) }
    case 'month': return { from: isoDate(addDays(today, -29)), to: isoDate(today) }
    case 'quarter': return { from: isoDate(addDays(today, -89)), to: isoDate(today) }
    default: return {}
  }
}

export function PeriodSelect({ value, onChange }: { value: Period; onChange: (p: Period) => void }) {
  const t = useT()
  return (
    <Select className="w-40!" value={value} onChange={(e) => onChange(e.target.value as Period)}>
      {PERIODS.map((p) => <option key={p} value={p}>{t(`inv.period.${p}` as TranslationKey)}</option>)}
    </Select>
  )
}

export function Pager({ page, pageSize, total, onPage, onPageSize }: {
  page: number; pageSize: number; total: number; onPage: (p: number) => void; onPageSize: (s: number) => void
}) {
  const t = useT()
  const pages = Math.max(1, Math.ceil(total / pageSize))
  if (total === 0) return null
  return (
    <div className="mt-4 flex flex-wrap items-center gap-3 text-[14px] text-ink-2">
      <span>{t('product.shown', { from: (page - 1) * pageSize + 1, to: Math.min(page * pageSize, total), total })}</span>
      <div className="ml-auto flex items-center gap-2">
        <Select className="h-9! w-24" value={pageSize} onChange={(e) => onPageSize(Number(e.target.value))}>
          {PAGE_SIZES.map((s) => <option key={s} value={s}>{s}</option>)}
        </Select>
        <button type="button" disabled={page <= 1} onClick={() => onPage(page - 1)} aria-label="Previous"
          className="flex size-9 items-center justify-center rounded-lg border border-line bg-surface disabled:opacity-40"><ChevronLeft size={17} /></button>
        <span className="min-w-16 text-center">{page} / {pages}</span>
        <button type="button" disabled={page >= pages} onClick={() => onPage(page + 1)} aria-label="Next"
          className="flex size-9 items-center justify-center rounded-lg border border-line bg-surface disabled:opacity-40"><ChevronRight size={17} /></button>
      </div>
    </div>
  )
}

export function Qty({ value, unit }: { value: number; unit?: string | null }) {
  const tone = value <= 0 ? 'text-danger' : value < 10 ? 'text-amber-600' : 'text-success'
  return <span className={`font-semibold ${tone}`}>{new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 3 }).format(value)}{unit ? ` ${unit}` : ''}</span>
}

export function ProductThumb({ url, size = 40 }: { url: string | null; size?: number }) {
  return url
    ? <img src={url} alt="" loading="lazy" className="shrink-0 rounded-lg border border-line object-cover" style={{ width: size, height: size }} />
    : <span className="shrink-0 rounded-lg bg-field" style={{ width: size, height: size }} />
}

export function EmptyRow({ colSpan, text }: { colSpan: number; text: string }) {
  return <tr><td colSpan={colSpan} className="px-3 py-12 text-center text-[14px] text-ink-3">{text}</td></tr>
}

/** Себеп сұрайтын диалог (сторно, перемещениені тоқтату): браузердің prompt-ы орнына сайт стилінде. */
export function ReasonModal({ title, hint, confirmLabel, onClose, onSubmit }: {
  title: string; hint?: string; confirmLabel: string; onClose: () => void; onSubmit: (reason: string) => Promise<unknown> | void
}) {
  const t = useT()
  const [reason, setReason] = useState('')
  const [loading, setLoading] = useState(false)
  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!reason.trim()) return
    setLoading(true)
    try { await onSubmit(reason.trim()) } finally { setLoading(false) }
  }
  return (
    <Modal title={title} onClose={onClose} width={480}>
      <form onSubmit={submit} className="flex flex-col gap-4">
        {hint && <p className="text-[14px] leading-relaxed text-ink-2">{hint}</p>}
        <Field label={t('inv.reason')} required>
          <Textarea autoFocus value={reason} onChange={(e) => setReason(e.target.value)} maxLength={500} />
        </Field>
        <div className="flex justify-end gap-2">
          <Button type="button" variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" variant="danger" loading={loading} disabled={!reason.trim()}>{confirmLabel}</Button>
        </div>
      </form>
    </Modal>
  )
}
