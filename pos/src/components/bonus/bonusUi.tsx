import type { ReactNode } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useSearchParams } from 'react-router-dom'
import {
  Award, Cake, Crown, Flame, Gem, Gift, Handshake, Heart, Megaphone, MinusCircle, PlusCircle, RotateCcw, Scale, Shield, ShoppingCart, Sprout, Star, Timer,
  Trophy, Undo2,
} from 'lucide-react'
import { presetRange } from '../stats/statsUi'
import { activeStore, can, useAuth } from '../../lib/auth'
import { bonusApi, bonusKeys } from '../../lib/bonusApi'
import type { BonusOpType } from '../../lib/bonusTypes'
import { num } from '../../lib/money'
import { Permission } from '../../lib/types'
import { useT, type TranslationKey } from '../../lib/i18n'
import { toast } from '../ui/Toast'
import { ApiError } from '../../lib/api'

// ---------- Сүзгілер: кезең мен дүкендер URL-да, бөлімдер арасында сақталады ----------

export interface BonusFilters { from: string; to: string; storeIds: string[] }

export function useBonusFilters() {
  const [params, setParams] = useSearchParams()
  const store = useAuth(activeStore)
  const def = presetRange('thisMonth')
  const filters: BonusFilters = {
    from: params.get('from') ?? def.from,
    to: params.get('to') ?? def.to,
    storeIds: params.get('stores')?.split(',').filter(Boolean) ?? (store ? [store.id] : []),
  }
  const update = (patch: Record<string, string | null>, resetPage = true) =>
    setParams((prev) => {
      const next = new URLSearchParams(prev)
      for (const [k, v] of Object.entries(patch)) { if (v) next.set(k, v); else next.delete(k) }
      if (resetPage) next.delete('page')
      return next
    }, { replace: true })
  return { filters, params, update, storeIds: filters.storeIds.join(',') || undefined }
}

/** URL параметрлерін бір жерден өзгерту (бет жаңартылса да сүзгі қалады). */
export function useParamSetter() {
  const [params, setParams] = useSearchParams()
  const set = (patch: Record<string, string | null>, keepPage = false) => setParams((p) => {
    const n = new URLSearchParams(p)
    for (const [k, v] of Object.entries(patch)) { if (v) n.set(k, v); else n.delete(k) }
    if (!keepPage) n.delete('page')
    return n
  }, { replace: true })
  return { params, set, get: (k: string) => params.get(k) ?? '' }
}

export function useBonusMeta() {
  return useQuery({ queryKey: bonusKeys.meta, queryFn: bonusApi.meta, staleTime: 60_000 })
}

/** Клиент жағындағы құқықтар — тек батырмаларды жасыру үшін; шешімді сервер қабылдайды (§12). */
export function useBonusPermissions() {
  const store = useAuth(activeStore)
  return {
    manage: can(store, Permission.BonusManage), campaigns: can(store, Permission.BonusCampaigns), accrue: can(store, Permission.BonusAccrue),
    writeOff: can(store, Permission.BonusWriteOff), reverse: can(store, Permission.BonusReverse), terms: can(store, Permission.BonusTerms),
    export: can(store, Permission.StatisticsExport), contacts: can(store, Permission.CustomersContacts),
  }
}

export function reportError(err: unknown, fallback: string) {
  toast(err instanceof ApiError ? err.message : fallback, 'error')
}

// ---------- Белгілер ----------

export const STATUS_ICON: Record<string, ReactNode> = {
  sprout: <Sprout size={15} />, star: <Star size={15} />, crown: <Crown size={15} />, gem: <Gem size={15} />, heart: <Heart size={15} />,
  award: <Award size={15} />, shield: <Shield size={15} />, handshake: <Handshake size={15} />, flame: <Flame size={15} />, trophy: <Trophy size={15} />,
}

/** Мәртебе бейджі: түс әрқашан атаумен қатар (§1.2). */
export function StatusBadge({ name, color, icon }: { name: string; color: string; icon?: string }) {
  return (
    <span className="inline-flex max-w-full items-center gap-1 rounded-lg px-2 py-0.5 text-[12px] font-semibold whitespace-nowrap" style={{ backgroundColor: `${color}1A`, color }}>
      {icon && STATUS_ICON[icon]}<span className="truncate">{name}</span>
    </span>
  )
}

/** Есептеу/қайтару — «+» жасыл, жұмсау/жану — «−» қызыл, түзету — сары, сторно — күлгін; түрі әрқашан жазулы (§2.6). */
const TYPE_STYLE: Record<BonusOpType, { tone: string; icon: ReactNode }> = {
  Accrual: { tone: 'bg-success-soft text-success', icon: <ShoppingCart size={13} /> },
  Birthday: { tone: 'bg-pink-50 text-pink-600', icon: <Cake size={13} /> },
  Promo: { tone: 'bg-brand-soft text-brand', icon: <Megaphone size={13} /> },
  Redemption: { tone: 'bg-danger-soft text-danger', icon: <Gift size={13} /> },
  Expiration: { tone: 'bg-danger-soft text-danger', icon: <Timer size={13} /> },
  ReturnRestore: { tone: 'bg-success-soft text-success', icon: <Undo2 size={13} /> },
  ReturnReversal: { tone: 'bg-amber-50 text-amber-700', icon: <MinusCircle size={13} /> },
  Adjustment: { tone: 'bg-amber-50 text-amber-700', icon: <Scale size={13} /> },
  Reversal: { tone: 'bg-violet-50 text-violet-600', icon: <RotateCcw size={13} /> },
}

export function OpTypeBadge({ type, amount }: { type: BonusOpType; amount?: number }) {
  const t = useT()
  const s = TYPE_STYLE[type] ?? TYPE_STYLE.Adjustment
  const key = type === 'Adjustment' && amount !== undefined ? (amount >= 0 ? 'bon.type.AdjustmentPlus' : 'bon.type.AdjustmentMinus') : `bon.type.${type}`
  return <span className={`inline-flex items-center gap-1 rounded-lg px-2 py-0.5 text-[12px] font-semibold whitespace-nowrap ${s.tone}`}>{s.icon}{t(key as TranslationKey)}</span>
}

export function Points({ value, signed, muted, className = '' }: { value: number; signed?: boolean; muted?: boolean; className?: string }) {
  const tone = muted ? 'text-ink-3 line-through' : !signed ? '' : value > 0 ? 'text-success' : value < 0 ? 'text-danger' : 'text-ink-2'
  return <span className={`tabular-nums whitespace-nowrap font-semibold ${tone} ${className}`}>{signed && value > 0 ? '+' : value < 0 ? '−' : ''}{num(Math.abs(value))}</span>
}

export function Pill({ tone, children }: { tone: 'green' | 'amber' | 'red' | 'blue' | 'gray' | 'violet'; children: ReactNode }) {
  const tones = {
    green: 'bg-success-soft text-success', amber: 'bg-amber-50 text-amber-700', red: 'bg-danger-soft text-danger', blue: 'bg-brand-soft text-brand',
    gray: 'bg-field text-ink-2', violet: 'bg-violet-50 text-violet-600',
  }
  return <span className={`inline-block rounded-lg px-2 py-0.5 text-[12px] font-semibold whitespace-nowrap ${tones[tone]}`}>{children}</span>
}

export const BIRTHDAY_TONE: Record<string, 'green' | 'amber' | 'red' | 'blue' | 'gray' | 'violet'> = {
  pending: 'blue', granted: 'green', manual: 'green', status: 'gray', purchases: 'amber', excluded: 'gray', noBirthDate: 'gray', disabled: 'gray', skipped: 'gray',
}

export function newKey() {
  return crypto.randomUUID()
}

export { PlusCircle }
