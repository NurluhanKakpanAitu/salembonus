import { useMemo, type ReactNode } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useSearchParams } from 'react-router-dom'
import { activeStore, can, useAuth } from '../../lib/auth'
import { clientKeys, clientsApi } from '../../lib/clientsApi'
import type { ClientActivity, ClientFilter, QuickFilter } from '../../lib/clientsTypes'
import { Permission } from '../../lib/types'
import { useT, type TranslationKey } from '../../lib/i18n'
import { Pill, useParamSetter } from '../bonus/bonusUi'

export { useParamSetter }

export function useClientMeta() {
  return useQuery({ queryKey: clientKeys.meta, queryFn: clientsApi.meta, staleTime: 60_000 })
}

/** Дүкендер (ТЗ «Клиенты» §1.3): clients.view бар дүкендер; таңдалғаны URL-да, бөлімдер арасында сақталады. */
export function useClientScope() {
  const [params, setParams] = useSearchParams()
  const me = useAuth((s) => s.me)
  const store = useAuth(activeStore)
  const stores = useMemo(() => (me?.stores ?? []).filter((s) => can(s, Permission.ClientsView)).map((s) => ({ id: s.id, name: s.name })), [me])
  const selected = params.get('stores')?.split(',').filter(Boolean) ?? (store ? [store.id] : [])
  const setStores = (ids: string[]) => setParams((p) => {
    const n = new URLSearchParams(p)
    if (ids.length && !(ids.length === 1 && ids[0] === store?.id)) n.set('stores', ids.join(','))
    else n.delete('stores')
    n.delete('page')
    return n
  }, { replace: true })
  return { stores, selected, storeIds: params.get('stores') ?? undefined, setStores }
}

function parseFilter(raw: string | null): ClientFilter {
  if (!raw) return {}
  try {
    const v = JSON.parse(raw) as unknown
    return v && typeof v === 'object' ? (v as ClientFilter) : {}
  } catch { return {} }
}

/** Тізім сүзгісі URL-да: іздеу, жылдам сүзгі, мәртебе, сегмент, кеңейтілген шарттар (f), сұрыптау, бет. */
export function useListFilter() {
  const { params, set } = useParamSetter()
  const adv = parseFilter(params.get('f'))
  const filter: ClientFilter = {
    ...adv,
    search: params.get('q') || null,
    quick: (params.get('quick') as QuickFilter | null) || null,
    statusIds: params.get('status')?.split(',').filter(Boolean) ?? [],
  }
  return {
    params, set, adv, filter,
    segmentId: params.get('segment') || null,
    sort: params.get('sort') || '',
    dir: (params.get('dir') === 'asc' ? 'asc' : 'desc') as 'asc' | 'desc',
    page: Number(params.get('page')) || 1,
    pageSize: Number(params.get('size')) || 25,
  }
}

const SIMPLE_KEYS = new Set(['search', 'quick', 'statusIds'])

/** Кеңейтілген шарттардың саны («Фильтры (3)»). */
export function advancedCount(f: ClientFilter) {
  return Object.entries(f).filter(([k, v]) => !SIMPLE_KEYS.has(k) && v !== null && v !== undefined && v !== '' && !(Array.isArray(v) && v.length === 0)).length
}

/** Бос мәндерсіз сүзгі — URL мен сегментке сақтау үшін. */
export function compact(f: ClientFilter): ClientFilter {
  return Object.fromEntries(Object.entries(f).filter(([, v]) => v !== null && v !== undefined && v !== '' && !(Array.isArray(v) && v.length === 0))) as ClientFilter
}

export function useOpenClient() {
  const { set } = useParamSetter()
  return (id: string) => set({ client: id }, true)
}

export const clientNo = (n: number) => String(n).padStart(6, '0')

export const ACTIVITY_TONE: Record<ClientActivity, 'green' | 'amber' | 'red' | 'gray'> = { active: 'green', cooling: 'amber', passive: 'red', noPurchases: 'gray' }
export const ACTIVITY_COLOR: Record<ClientActivity, string> = { active: '#16a34a', cooling: '#f59e0b', passive: '#e11d48', noPurchases: '#94a3b8' }

export function ActivityPill({ activity }: { activity: ClientActivity }) {
  const t = useT()
  return <Pill tone={ACTIVITY_TONE[activity]}>{t(`cli.activity.${activity}` as TranslationKey)}</Pill>
}

const AVATAR_COLORS = ['#1769FF', '#8B5CF6', '#14B8A6', '#F59E0B', '#E11D48', '#0EA5E9', '#16A34A', '#F472B6']

export function Avatar({ name, size = 40 }: { name: string; size?: number }) {
  const initials = name.split(' ').filter(Boolean).slice(0, 2).map((x) => x[0]?.toUpperCase()).join('') || '?'
  const color = AVATAR_COLORS[[...name].reduce((s, ch) => s + ch.charCodeAt(0), 0) % AVATAR_COLORS.length]
  return (
    <span className="flex shrink-0 items-center justify-center rounded-full font-bold text-white" style={{ width: size, height: size, background: color, fontSize: size * 0.38 }}>
      {initials}
    </span>
  )
}

const TILE_TONES = {
  blue: 'bg-brand-soft text-brand', green: 'bg-success-soft text-success', violet: 'bg-violet-50 text-violet-600', sky: 'bg-sky-50 text-sky-600',
  gray: 'bg-field text-ink-2', amber: 'bg-amber-50 text-amber-600', red: 'bg-danger-soft text-danger', pink: 'bg-pink-50 text-pink-600',
}

/** KPI плиткасы: басылса — тізім сол жылдам сүзгімен (KPI мен тізім бір есептен). */
export function KpiTile({ label, value, icon, tone, active, onClick, hint }: {
  label: string; value: ReactNode; icon: ReactNode; tone: keyof typeof TILE_TONES; active?: boolean; onClick?: () => void; hint?: string
}) {
  return (
    <button type="button" onClick={onClick} disabled={!onClick}
      className={`flex min-w-0 flex-col rounded-2xl border bg-surface p-3.5 text-left transition-colors ${active ? 'border-brand ring-2 ring-brand/15' : 'border-line hover:border-brand/50'} disabled:cursor-default`}>
      <span className="flex items-center gap-2">
        <span className={`flex size-8 shrink-0 items-center justify-center rounded-lg ${TILE_TONES[tone]}`}>{icon}</span>
        <span className="truncate text-[12px] font-medium text-ink-2">{label}</span>
      </span>
      <span className="mt-2 truncate text-[22px] font-extrabold tracking-tight text-[#0b1f44]">{value}</span>
      {hint && <span className="truncate text-[11px] text-ink-3">{hint}</span>}
    </button>
  )
}

/** CSV файлын браузерде жасау (импорт қателері): «;» бөлгіш, UTF-8 BOM — Excel кириллицаны дұрыс ашады. */
export function downloadCsv(name: string, headers: string[], rows: (string | number | null)[][]) {
  const esc = (v: string | number | null) => {
    const s = v === null ? '' : String(v)
    return /[;"\n\r]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s
  }
  const text = [headers, ...rows].map((r) => r.map(esc).join(';')).join('\r\n')
  const url = URL.createObjectURL(new Blob(['﻿' + text], { type: 'text/csv;charset=utf-8' }))
  const a = document.createElement('a')
  a.href = url
  a.download = name
  a.click()
  setTimeout(() => URL.revokeObjectURL(url), 1000)
}

export function Section({ title, action, children }: { title: string; action?: ReactNode; children: ReactNode }) {
  return (
    <section className="flex flex-col gap-2">
      <div className="flex items-center gap-2">
        <h3 className="text-[14px] font-bold text-[#0b1f44]">{title}</h3>
        {action && <span className="ml-auto">{action}</span>}
      </div>
      {children}
    </section>
  )
}

export function InfoRow({ label, value }: { label: string; value: ReactNode }) {
  return (
    <div className="flex items-start gap-3 py-1.5 text-[13px]">
      <span className="w-36 shrink-0 text-ink-2">{label}</span>
      <span className="min-w-0 flex-1 break-words font-medium">{value ?? '—'}</span>
    </div>
  )
}
