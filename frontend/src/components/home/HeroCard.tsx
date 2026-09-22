import { ChevronRight, Crown, QrCode } from 'lucide-react'
import type { BonusCard } from '../../lib/api'
import { formatBonus, formatTenge } from '../../lib/format'
import { levelBadge, storeIcon, storeTheme } from '../../lib/theme'

export function HeroCard({ card, onShowQr }: { card: BonusCard; onShowQr?: () => void }) {
  const t = storeTheme(card.themeColor)
  const badge = levelBadge(card.level)
  const Icon = storeIcon(card.icon)
  const isVip = card.level === 'VIP клиент'

  return (
    <article
      className="relative flex h-[290px] w-full flex-col justify-between overflow-hidden rounded-[22px] p-5"
      style={{ background: t.bg, color: t.text }}
    >
      <div
        className="pointer-events-none absolute inset-y-0 right-0 w-2/3 opacity-30"
        style={{ background: `radial-gradient(120% 80% at 90% 40%, ${t.text} 0%, transparent 60%)`, mixBlendMode: 'soft-light' }}
      />

      <div className="relative flex items-center justify-between">
        <div className="flex items-center gap-2.5">
          <div className="flex size-11 items-center justify-center rounded-full bg-white" style={{ color: t.iconColor }}>
            <Icon size={22} />
          </div>
          <div>
            <div className="text-lg font-bold leading-tight">{card.storeName}</div>
            <div className="text-xs" style={{ color: t.muted }}>{card.category}</div>
          </div>
        </div>
        <span
          className="flex items-center gap-1.5 rounded-full px-3 py-1.5 text-xs font-bold"
          style={{ background: badge.bg, color: badge.text, border: badge.border ? `1.5px solid ${badge.border}` : undefined }}
        >
          {isVip && <Crown size={14} />}
          {isVip ? 'VIP' : card.level}
        </span>
      </div>

      <div className="relative flex items-end justify-between gap-3">
        <div>
          <div className="text-[13px]" style={{ color: t.muted }}>Бонус балансы</div>
          <div className="text-[34px] font-extrabold leading-none tracking-tight">{formatBonus(card.balance)}</div>
        </div>
        <div className="rounded-xl px-3 py-2.5 text-[11px]" style={{ background: t.box, color: t.muted }}>
          <div>Есептеу</div>
          <div className="text-base font-bold" style={{ color: t.text }}>{card.cashbackPercent}%</div>
          <div className="my-1.5 h-px" style={{ background: t.muted, opacity: 0.3 }} />
          <div>Келесі деңгейге дейін</div>
          <div className="text-sm font-bold" style={{ color: t.text }}>
            {card.amountToNextLevel > 0 ? formatTenge(card.amountToNextLevel) : 'Ең жоғары деңгей'}
          </div>
        </div>
      </div>

      <button
        type="button"
        onClick={onShowQr}
        className="relative flex w-fit items-center gap-2 rounded-xl bg-white px-4 py-3 text-sm font-semibold text-ink active:scale-[0.98]"
      >
        <QrCode size={20} /> QR-код көрсету <ChevronRight size={16} />
      </button>
    </article>
  )
}
