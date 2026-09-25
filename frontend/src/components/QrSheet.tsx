import { useEffect } from 'react'
import { QRCodeSVG } from 'qrcode.react'
import { X } from 'lucide-react'
import { useMe, useQr } from '../lib/queries'
import { LogoMark } from './Brand'
import { useT } from '../lib/i18n'

/** Кассада көрсетілетін QR коды. Төменнен шығатын парақ. */
export function QrSheet({ open, onClose, storeName }: { open: boolean; onClose: () => void; storeName?: string }) {
  const t = useT()
  const qr = useQr()
  const me = useMe()

  useEffect(() => {
    if (!open) return
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && onClose()
    window.addEventListener('keydown', onKey)
    document.body.style.overflow = 'hidden'
    return () => {
      window.removeEventListener('keydown', onKey)
      document.body.style.overflow = ''
    }
  }, [open, onClose])

  if (!open) return null

  return (
    <div className="fixed inset-0 z-50 flex items-end justify-center bg-black/50" onClick={onClose} role="dialog" aria-modal="true">
      <div
        className="w-full max-w-[480px] rounded-t-[28px] bg-surface px-6 pb-[max(24px,env(safe-area-inset-bottom))] pt-3"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="mx-auto mb-4 h-1 w-10 rounded-full bg-gray-300" />
        <div className="flex items-center justify-between">
          <div>
            <div className="text-lg font-bold">{t('qr.myTitle')}</div>
            <div className="text-xs text-ink-2">{storeName ? t('qr.showAt', { store: storeName }) : t('qr.showAnywhere')}</div>
          </div>
          <button type="button" aria-label={t('common.close')} onClick={onClose} className="flex size-9 items-center justify-center rounded-full bg-bg text-ink-2">
            <X size={18} />
          </button>
        </div>

        <div className="mt-6 flex flex-col items-center">
          <div className="relative rounded-3xl border border-line bg-white p-5">
            {qr.data ? (
              <QRCodeSVG value={qr.data.payload} size={220} level="H" includeMargin={false} />
            ) : (
              <div className="size-[220px] animate-pulse rounded-xl bg-gray-100" />
            )}
            <div className="absolute left-1/2 top-1/2 -translate-x-1/2 -translate-y-1/2 rounded-full bg-white p-1.5">
              <LogoMark size={40} />
            </div>
          </div>
          <div className="mt-4 text-lg font-semibold tracking-[0.3em]">{qr.data?.code ?? '…'}</div>
          <div className="mt-1 text-xs text-ink-2">{me.data?.fullName}</div>
          <p className="mt-4 text-center text-xs text-ink-3">
            {t('qr.fallback')}
          </p>
        </div>
      </div>
    </div>
  )
}
