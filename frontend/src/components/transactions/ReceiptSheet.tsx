import { useEffect } from 'react'
import { X } from 'lucide-react'
import { formatBonus, formatDateTime, formatTenge, formatDate } from '../../lib/format'
import { useT } from '../../lib/i18n'
import { useReceipt } from '../../lib/queries'
import { storeTheme } from '../../lib/theme'
import { ErrorBox, Skeleton } from '../Skeleton'

/** Чектің бір жолы: сол жақта атауы, оң жақта сомасы. */
function Row({ label, value, strong, tone }: {
  label: string
  value: string
  strong?: boolean
  tone?: 'green' | 'danger'
}) {
  const color = tone === 'green' ? 'text-green' : tone === 'danger' ? 'text-danger' : ''
  return (
    <div className="flex items-baseline justify-between gap-3 py-2.5">
      <span className="text-[14px] text-ink-2">{label}</span>
      <span className={`shrink-0 text-right text-[15px] ${strong ? 'font-extrabold' : 'font-semibold'} ${color}`}>
        {value}
      </span>
    </div>
  )
}

/** Бір сатып алудың чегі: төменнен шығатын парақ. */
export function ReceiptSheet({ receiptId, onClose }: { receiptId: string | null; onClose: () => void }) {
  const t = useT()
  const receipt = useReceipt(receiptId)

  useEffect(() => {
    if (receiptId === null) return
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && onClose()
    window.addEventListener('keydown', onKey)
    document.body.style.overflow = 'hidden'
    return () => {
      window.removeEventListener('keydown', onKey)
      document.body.style.overflow = ''
    }
  }, [receiptId, onClose])

  if (receiptId === null) return null

  const r = receipt.data
  const theme = r ? storeTheme(r.themeColor) : null

  return (
    <div
      className="fixed inset-0 z-50 flex items-end justify-center bg-black/50"
      onClick={onClose}
      role="dialog"
      aria-modal="true"
    >
      <div
        className="max-h-[92vh] w-full max-w-[480px] overflow-y-auto rounded-t-[28px] bg-surface pb-[max(20px,env(safe-area-inset-bottom))]"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Дүкен түсімен боялған бас жағы */}
        <div
          className="relative rounded-t-[28px] px-5 pb-5 pt-4"
          style={theme ? { background: theme.bg, color: theme.text } : undefined}
        >
          <div className="mx-auto mb-3 h-1 w-10 rounded-full" style={{ background: theme?.box ?? 'var(--color-muted)' }} />
          <button
            type="button"
            aria-label={t('common.close')}
            onClick={onClose}
            className="absolute right-4 top-4 flex size-8 items-center justify-center rounded-full active:scale-95"
            style={theme ? { background: theme.box, color: theme.text } : undefined}
          >
            <X size={17} />
          </button>
          <div className="text-[13px] opacity-80">{t('receipt.title')}</div>
          <div className="mt-0.5 truncate pr-10 text-[19px] font-extrabold">{r?.storeName ?? '…'}</div>
          {r && (
            <div className="mt-1 text-[12px] opacity-80">
              {formatDateTime(r.createdAt)} · {t('receipt.number', { number: r.number })}
            </div>
          )}
        </div>

        <div className="px-5 pt-1">
          {receipt.isPending && <Skeleton className="mt-4 h-52" />}
          {receipt.isError && <ErrorBox message={t('receipt.notFound')} onRetry={() => receipt.refetch()} />}

          {r && (
            <>
              <div className="divide-y divide-line">
                <Row label={t('receipt.purchase')} value={formatTenge(r.purchaseAmount)} strong />
                {r.redeemed > 0 && (
                  <Row label={t('receipt.redeemed')} value={`− ${formatBonus(r.redeemed)}`} tone="danger" />
                )}
                {r.redeemed > 0 && <Row label={t('receipt.paid')} value={formatTenge(r.paid)} />}
                {r.accrued > 0 && (
                  <Row
                    label={`${t('receipt.accrued')} · ${t('receipt.percent', { percent: r.cashbackPercent })}`}
                    value={`+ ${formatBonus(r.accrued)}`}
                    tone="green"
                  />
                )}
                <Row label={t('receipt.balanceAfter')} value={formatBonus(r.balanceAfter)} strong />
              </div>

              {r.expiresAt && (
                <div className="mt-3 rounded-2xl bg-muted px-4 py-3 text-[13px] text-ink-2">
                  {t('receipt.expires')}: <span className="font-semibold text-ink">{formatDate(r.expiresAt.slice(0, 10))}</span>
                </div>
              )}

              {r.comment && (
                <div className="mt-3">
                  <div className="text-[12px] text-ink-3">{t('receipt.comment')}</div>
                  <div className="mt-0.5 text-[14px]">{r.comment}</div>
                </div>
              )}

              <p className="mt-4 text-[12px] leading-snug text-ink-3">{t('receipt.note')}</p>
            </>
          )}
        </div>
      </div>
    </div>
  )
}
