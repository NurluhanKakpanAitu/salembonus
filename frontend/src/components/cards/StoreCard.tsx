import { ChevronRight, Crown } from 'lucide-react'
import { Link } from 'react-router-dom'
import type { BonusCard } from '../../lib/api'
import { formatBonus, formatTenge } from '../../lib/format'
import { levelBadge, storeIcon, storeTheme } from '../../lib/theme'

export function StoreCard({ card }: { card: BonusCard }) {
  const t = storeTheme(card.themeColor)
  const badge = levelBadge(card.level)
  const Icon = storeIcon(card.icon)
  const isVip = card.level === 'VIP клиент'

  return (
    <Link
      to={`/cards/${card.storeId}`}
      className="relative flex h-[150px] items-center justify-between overflow-hidden rounded-[20px] p-4 active:scale-[0.99]"
      style={{ background: t.bg, color: t.text }}
    >
      <div
        className="pointer-events-none absolute inset-y-0 right-0 w-1/2 opacity-30"
        style={{ background: `radial-gradient(90% 90% at 70% 50%, ${t.text} 0%, transparent 70%)`, mixBlendMode: 'soft-light' }}
      />

      <div className="relative">
        <div className="flex items-center gap-2.5">
          <div className="flex size-10 items-center justify-center rounded-full bg-white" style={{ color: t.iconColor }}>
            <Icon size={20} />
          </div>
          <div>
            <div className="whitespace-nowrap text-[17px] font-bold leading-tight">{card.storeName}</div>
            <div className="whitespace-nowrap text-xs" style={{ color: t.muted }}>{card.category}</div>
          </div>
        </div>
        <div className="mt-2.5 text-[28px] font-extrabold leading-none tracking-tight">{formatBonus(card.balance)}</div>
      </div>

      <div className="relative flex flex-col items-end gap-2">
        <span
          className="flex items-center gap-1 rounded-full px-2.5 py-1 text-[11px] font-bold"
          style={{ background: badge.bg, color: badge.text, border: badge.border ? `1.5px solid ${badge.border}` : undefined }}
        >
          {isVip && <Crown size={13} />}
          {isVip ? 'VIP' : card.level}
        </span>
        <div className="flex items-center gap-2">
          <div className="whitespace-nowrap rounded-[10px] px-2.5 py-2 text-[10px]" style={{ background: t.box, color: t.muted }}>
            <div className="text-[13px] font-bold" style={{ color: t.text }}>Бонус: {card.cashbackPercent}%</div>
            <div className="mt-0.5">{isVip ? 'Сіздің деңгейіңіз' : 'Келесі деңгейге дейін:'}</div>
            <div className="text-xs font-bold" style={{ color: isVip ? '#F5B301' : t.text }}>
              {isVip ? card.level : formatTenge(card.amountToNextLevel)}
            </div>
          </div>
          <div className="flex size-[30px] items-center justify-center rounded-full bg-white text-ink">
            <ChevronRight size={16} />
          </div>
        </div>
      </div>
    </Link>
  )
}
