import { Crown, Info, QrCode } from 'lucide-react'
import { QRCodeSVG } from 'qrcode.react'
import type { BonusCard } from '../../lib/api'
import { formatNumber } from '../../lib/format'
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
      className={`relative flex w-full flex-col justify-between overflow-hidden rounded-[22px] px-5 py-4 ${
        expanded ? 'min-h-[216px]' : compact ? 'h-[196px]' : 'h-[216px]'
      }`}
      style={{ background: theme.bg, color: theme.text }}
    >
      {/* Әшекей: мөлдір карта пішіні мен бренд күлкісі */}
      <div aria-hidden="true" className="pointer-events-none absolute inset-0 overflow-hidden rounded-[22px]">
        <div
          className="absolute -right-10 top-[58px] h-[116px] w-[186px] rotate-[-10deg] rounded-[22px]"
          style={{ background: theme.box }}
        />
        <svg viewBox="0 0 100 30" className="absolute right-[44px] top-[106px] h-7 w-[74px] opacity-90">
          <path d="M8 6c15 22 69 22 84 0" fill="none" stroke={theme.text} strokeWidth="11" strokeLinecap="round" />
        </svg>
      </div>

      <div className="relative flex items-start justify-between gap-3">
        <div className="min-w-0 truncate pt-0.5 text-[17px] font-semibold tracking-[0.02em]" style={{ color: theme.text }}>
          {card.storeName}
        </div>
        <div
          className="flex shrink-0 items-center gap-1.5 rounded-full px-3 py-1.5 text-[12px] font-bold"
          style={{ background: theme.text, color: theme.solid }}
        >
          <Crown size={14} className="text-brand" fill="currentColor" />
          {t('cards.levelPill', {
            level: t(`level.${card.level}` as TranslationKey),
            percent: card.cashbackPercent,
          })}
        </div>
      </div>

      <div className="relative">
        <div className="text-[13px]" style={{ color: theme.muted }}>{t('cards.balanceLabel')}</div>
        <div className="mt-0.5 flex items-baseline gap-1.5 font-extrabold leading-none tracking-[-0.03em]">
          <span className={compact ? 'text-[44px]' : 'text-[52px]'}>{formatNumber(card.balance)}</span>
          <span className={compact ? 'text-[23px]' : 'text-[27px]'}>{t('common.bonusUnit')}</span>
        </div>
      </div>


      {expanded && (
        <div className="relative mt-4 flex flex-col items-center">
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

      <div className="relative mt-4 flex items-end justify-between gap-3">
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
