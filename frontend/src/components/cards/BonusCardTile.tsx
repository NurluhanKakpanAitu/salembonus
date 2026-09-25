import { Info, QrCode } from 'lucide-react'
import { QRCodeSVG } from 'qrcode.react'
import type { BonusCard } from '../../lib/api'
import { formatBonus, formatDate, formatNumber } from '../../lib/format'
import { storeTheme } from '../../lib/theme'
import { useT, type TranslationKey } from '../../lib/i18n'
import { useQr } from '../../lib/queries'

/**
 * Бонус картасы: жоғарыда дүкен атауы мен мәртебе белгісі, ортада баланс,
 * төменде бренд жолы және QR батырмасы.
 */
export function BonusCardTile({
  card,
  size = 'full',
  onAction,
  expanded = false,
  onInfo,
}: {
  card: BonusCard
  size?: 'full' | 'compact'
  onAction?: () => void
  /** Ашық күйде QR коды картаның ішінде көрінеді. */
  expanded?: boolean
  onInfo?: () => void
}) {
  const t = useT()
  const theme = storeTheme(card.themeColor)
  const compact = size === 'compact'
  const qr = useQr()

  return (
    <div
      className={`flex w-full flex-col justify-between rounded-[22px] px-5 py-4 ${
        expanded ? 'min-h-[216px]' : compact ? 'h-[196px]' : 'h-[216px]'
      }`}
      style={{ background: theme.bg, color: theme.text }}
    >
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0 pt-1 text-[12px] font-bold uppercase tracking-[0.08em]" style={{ color: theme.muted }}>
          {card.storeName}
        </div>
        <div
          className="shrink-0 rounded-full px-3 py-1.5 text-[12px] font-bold"
          style={{ background: theme.text, color: theme.solid }}
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

      {card.expiringAt && card.expiringAmount ? (
        <div className="mt-2 text-[12px]" style={{ color: theme.muted }}>
          {t('cards.expiring', {
            amount: formatBonus(card.expiringAmount),
            date: formatDate(card.expiringAt.slice(0, 10)),
          })}
        </div>
      ) : null}

      {expanded && (
        <div className="mt-4 flex flex-col items-center">
          <div className="rounded-2xl bg-white p-3">
            {qr.data ? (
              <QRCodeSVG value={qr.data.payload} size={168} level="H" includeMargin={false} />
            ) : (
              <div className="size-[168px] animate-pulse rounded-lg bg-gray-100" />
            )}
          </div>
          <div className="mt-2 text-[15px] font-bold tracking-[0.2em]">{qr.data?.code ?? '…'}</div>
        </div>
      )}

      <div className="mt-4 flex items-end justify-between gap-3">
        <div className="min-w-0 truncate text-[11px] font-semibold tracking-[0.06em]" style={{ color: theme.muted }}>
          {t('cards.brandLine')}
        </div>
        {expanded && onInfo && (
          <button
            type="button"
            aria-label={t('cards.openCard')}
            onClick={(e) => {
              e.preventDefault()
              e.stopPropagation()
              onInfo()
            }}
            className="flex size-9 shrink-0 items-center justify-center rounded-full active:scale-95"
            style={{ background: theme.box, color: theme.text }}
          >
            <Info size={18} />
          </button>
        )}
        {!expanded && onAction && (
          <button
            type="button"
            onClick={(e) => {
              e.preventDefault()
              e.stopPropagation()
              onAction()
            }}
            className="flex shrink-0 items-center gap-1.5 rounded-full px-4 py-2.5 text-[13px] font-bold active:scale-[0.98]"
            style={{ background: theme.text, color: theme.solid }}
          >
            <QrCode size={15} /> {t('cards.qrShort')}
          </button>
        )}
      </div>
    </div>
  )
}
