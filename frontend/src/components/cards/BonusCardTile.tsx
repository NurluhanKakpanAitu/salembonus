import { QrCode } from 'lucide-react'
import type { BonusCard } from '../../lib/api'
import { formatNumber } from '../../lib/format'
import { storeTheme } from '../../lib/theme'
import { useT, type TranslationKey } from '../../lib/i18n'

/**
 * Бонус картасы: жоғарыда дүкен атауы мен мәртебе белгісі, ортада баланс,
 * төменде бренд жолы және QR батырмасы.
 */
export function BonusCardTile({
  card,
  size = 'full',
  onAction,
}: {
  card: BonusCard
  size?: 'full' | 'compact'
  onAction?: () => void
}) {
  const t = useT()
  const theme = storeTheme(card.themeColor)
  const compact = size === 'compact'

  return (
    <div
      className={`flex w-full flex-col justify-between rounded-[22px] px-5 py-4 ${compact ? 'h-[196px]' : 'h-[216px]'}`}
      style={{ background: theme.bg, color: theme.text }}
    >
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0 pt-1 text-[12px] font-bold uppercase tracking-[0.08em]" style={{ color: theme.muted }}>
          {card.storeName}
        </div>
        <div
          className="shrink-0 rounded-full px-3 py-1.5 text-[12px] font-bold"
          style={{ background: theme.text, color: theme.bg }}
        >
          {t('cards.levelPill', {
            level: t(`level.${card.level}` as TranslationKey),
            percent: card.cashbackPercent,
          })}
        </div>
      </div>

      <div>
        <div className="text-[13px]" style={{ color: theme.muted }}>{t('cards.balanceLabel')}</div>
        <div className="mt-0.5 flex items-baseline gap-1.5 font-extrabold leading-none tracking-[-0.03em]">
          <span className={compact ? 'text-[44px]' : 'text-[52px]'}>{formatNumber(card.balance)}</span>
          <span className={compact ? 'text-[23px]' : 'text-[27px]'}>{t('common.bonusUnit')}</span>
        </div>
      </div>

      <div className="flex items-end justify-between gap-3">
        <div className="min-w-0 truncate text-[11px] font-semibold tracking-[0.06em]" style={{ color: theme.muted }}>
          {t('cards.brandLine')}
        </div>
        {onAction && (
          <button
            type="button"
            onClick={(e) => {
              e.preventDefault()
              e.stopPropagation()
              onAction()
            }}
            className="flex shrink-0 items-center gap-1.5 rounded-full px-4 py-2.5 text-[13px] font-bold active:scale-[0.98]"
            style={{ background: theme.text, color: theme.bg }}
          >
            <QrCode size={15} /> {t('cards.qrShort')}
          </button>
        )}
      </div>
    </div>
  )
}
