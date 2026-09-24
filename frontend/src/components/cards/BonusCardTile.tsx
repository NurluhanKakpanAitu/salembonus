import { QrCode, Receipt, ShoppingBag, ShoppingCart, Tag, Truck, type LucideIcon } from 'lucide-react'
import type { BonusCard } from '../../lib/api'
import { formatNumber } from '../../lib/format'
import { storeTheme } from '../../lib/theme'

const PATTERN: LucideIcon[] = [Truck, ShoppingCart, ShoppingBag, Receipt, Tag, ShoppingCart, Truck, ShoppingBag]

/**
 * Бонус картасы: бір түсті фон, ірі баланс ортада, дүкен атауы жоғарғы оң жақта,
 * төменде әлсіз өрнек және ортада QR батырмасы.
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
  const compact = size === 'compact'

  return (
    <div
      className={`relative w-full overflow-hidden rounded-[18px] ${compact ? 'h-[178px]' : 'h-[210px]'}`}
      style={{ background: t.bg, color: t.text }}
    >
      <div className="pointer-events-none absolute inset-x-0 bottom-0 flex justify-between px-3 pb-2 opacity-[0.14]" aria-hidden="true">
        {PATTERN.map((P, i) => (
          <P key={i} size={compact ? 30 : 38} strokeWidth={1.5} className={i % 2 ? 'translate-y-1' : ''} />
        ))}
      </div>

      <div className="flex items-start justify-between px-5 pt-4">
        <div className="leading-none">
          <div className={`font-extrabold ${compact ? 'text-[22px]' : 'text-[26px]'}`}>Бонус</div>
          <div className={`mt-1 font-medium opacity-90 ${compact ? 'text-[15px]' : 'text-[18px]'}`}>картасы</div>
        </div>
        <div className="max-w-[55%] text-right">
          <div className={`font-bold leading-tight ${compact ? 'text-[17px]' : 'text-[19px]'}`}>{card.storeName}</div>
          <div className="text-[11px]" style={{ color: t.muted }}>{card.category}</div>
        </div>
      </div>

      <div className={`absolute inset-x-0 text-center ${compact ? 'top-[72px]' : 'top-[88px]'}`}>
        <div className="flex items-baseline justify-center gap-1.5 font-extrabold leading-none tracking-[-0.03em]">
          <span className={compact ? 'text-[42px]' : 'text-[52px]'}>{formatNumber(card.balance)}</span>
          <span className={compact ? 'text-[22px]' : 'text-[27px]'}>Б</span>
        </div>
      </div>

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
