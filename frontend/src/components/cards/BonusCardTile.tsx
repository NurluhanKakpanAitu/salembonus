import { Crown, QrCode, Receipt, ShoppingBag, ShoppingCart, Tag, Truck, type LucideIcon } from 'lucide-react'
import type { BonusCard } from '../../lib/api'
import { formatNumber, formatTenge } from '../../lib/format'
import { storeIcon, storeTheme } from '../../lib/theme'

const PATTERN: LucideIcon[] = [Truck, ShoppingCart, ShoppingBag, Receipt, Tag, ShoppingCart, Truck, ShoppingBag]

/**
 * Бонус картасы: бір түсті фон, ірі баланс ортада, дүкен жоғарғы оң жақта,
 * төменде әлсіз иконка-өрнек және ортада батырма.
 */
export function BonusCardTile({
  card,
  size = 'full',
  onAction,
  actionLabel = 'QR-код көрсету',
}: {
  card: BonusCard
  size?: 'full' | 'compact'
  onAction?: () => void
  actionLabel?: string
}) {
  const t = storeTheme(card.themeColor)
  const Icon = storeIcon(card.icon)
  const isVip = card.level === 'VIP клиент'
  const compact = size === 'compact'

  return (
    <div
      className={`relative w-full overflow-hidden rounded-[18px] ${compact ? 'h-[168px]' : 'h-[210px]'}`}
      style={{ background: t.bg, color: t.text }}
    >
      {/* Төменгі иконка-өрнек */}
      <div className="pointer-events-none absolute inset-x-0 bottom-0 flex justify-between px-3 pb-2 opacity-[0.14]" aria-hidden="true">
        {PATTERN.map((P, i) => (
          <P key={i} size={compact ? 30 : 38} strokeWidth={1.5} className={i % 2 ? 'translate-y-1' : ''} />
        ))}
      </div>

      {/* Жоғарғы жол */}
      <div className="flex items-start justify-between px-5 pt-4">
        <div className="leading-none">
          <div className={`font-extrabold ${compact ? 'text-[22px]' : 'text-[28px]'}`}>Бонус</div>
          <div className={`mt-1 font-medium opacity-90 ${compact ? 'text-[15px]' : 'text-[19px]'}`}>картасы</div>
        </div>
        <div className="flex items-center gap-2 text-right">
          <div>
            <div className={`font-bold leading-tight ${compact ? 'text-[15px]' : 'text-[17px]'}`}>{card.storeName}</div>
            <div className="text-[11px]" style={{ color: t.muted }}>{card.category}</div>
          </div>
          <div className="flex size-9 shrink-0 items-center justify-center rounded-full bg-white" style={{ color: t.iconColor }}>
            <Icon size={18} />
          </div>
        </div>
      </div>

      {/* Баланс */}
      <div className={`absolute inset-x-0 text-center ${compact ? 'top-[62px]' : 'top-[82px]'}`}>
        <div className={`font-extrabold leading-none tracking-[-0.03em] ${compact ? 'text-[40px]' : 'text-[52px]'}`}>
          {formatNumber(card.balance)}<span className={`ml-1 font-bold opacity-80 ${compact ? 'text-[22px]' : 'text-[28px]'}`}>Б</span>
        </div>
        {!compact && (
          <div className="mt-1.5 flex items-center justify-center gap-1.5 text-xs" style={{ color: t.muted }}>
            {isVip && <Crown size={12} className="text-gold" />}
            <span className={isVip ? 'font-semibold text-gold' : ''}>{card.level}</span>
            <span>·</span>
            <span>{card.cashbackPercent}% бонус</span>
            {card.amountToNextLevel > 0 && (
              <>
                <span>·</span>
                <span>келесі деңгейге {formatTenge(card.amountToNextLevel)}</span>
              </>
            )}
          </div>
        )}
      </div>

      {/* Батырма */}
      {onAction && (
        <button
          type="button"
          onClick={(e) => {
            e.preventDefault()
            e.stopPropagation()
            onAction()
          }}
          className="absolute bottom-3.5 left-1/2 flex -translate-x-1/2 items-center gap-1.5 rounded-xl bg-white px-3.5 py-2 text-[13px] font-semibold text-ink shadow-sm active:scale-[0.98]"
        >
          <QrCode size={16} /> {actionLabel}
        </button>
      )}
    </div>
  )
}
