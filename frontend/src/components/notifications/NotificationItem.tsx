import { Cake, ChevronRight, CircleMinus, Gift, Info, Percent, UserCheck, type LucideIcon } from 'lucide-react'
import type { Notification, NotificationType } from '../../lib/api'
import { formatTime } from '../../lib/format'
import { storeIcon, storeTheme } from '../../lib/theme'

const STYLE: Record<NotificationType, { icon: LucideIcon; bg: string; color: string }> = {
  BonusAccrued: { icon: Gift, bg: '#E8F7EE', color: '#16A34A' },
  BonusRedeemed: { icon: CircleMinus, bg: '#FDECEC', color: '#E5484D' },
  Birthday: { icon: Cake, bg: '#EEEBFF', color: '#6D5DF6' },
  Promo: { icon: Percent, bg: '#E3EDFF', color: '#2563EB' },
  StoreAdded: { icon: Info, bg: '#3B2A22', color: '#FFFFFF' },
  ProfileUpdated: { icon: UserCheck, bg: '#E6F2FF', color: '#0A84F8' },
  System: { icon: Info, bg: '#E9EBF0', color: '#6B7280' },
}

export function NotificationItem({ n, onOpen }: { n: Notification; onOpen?: (n: Notification) => void }) {
  let { icon: Icon, bg, color } = STYLE[n.type] ?? STYLE.System
  if (n.type === 'StoreAdded' && n.storeThemeColor && n.storeIcon) {
    const t = storeTheme(n.storeThemeColor)
    Icon = storeIcon(n.storeIcon)
    bg = t.bg
    color = t.text
  }

  return (
    <button
      type="button"
      onClick={() => onOpen?.(n)}
      className="flex w-full items-start gap-3 rounded-2xl bg-surface p-3.5 text-left active:bg-gray-50"
    >
      <div className="flex size-11 shrink-0 items-center justify-center rounded-full" style={{ background: bg, color }}>
        <Icon size={22} />
      </div>
      <div className="min-w-0 flex-1">
        <div className="flex items-start justify-between gap-2">
          <div className={`text-[15px] leading-tight ${n.isRead ? 'font-semibold' : 'font-bold'}`}>
            {!n.isRead && <span className="mr-1.5 inline-block size-1.5 -translate-y-0.5 rounded-full bg-brand" />}
            {n.title}
          </div>
          <div className="shrink-0 text-xs text-ink-3">{formatTime(n.createdAt)}</div>
        </div>
        <div className="mt-1 text-[13px] leading-snug">{n.body}</div>
        {n.detail && <div className="mt-1 text-xs text-ink-2">{n.detail}</div>}
      </div>
      <ChevronRight size={18} className="mt-0.5 shrink-0 text-ink-3" />
    </button>
  )
}
