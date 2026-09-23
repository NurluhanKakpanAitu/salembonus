import { QrCode, Store } from 'lucide-react'
import type { Customer } from '../../lib/api'
import { formatNumber } from '../../lib/format'

/** Басты беттің басты картасы: жалпы бонус және QR батырмасы. */
export function BalanceCard({ me, onShowQr }: { me: Customer; onShowQr: () => void }) {
  return (
    <section className="rounded-[24px] bg-surface p-5">
      <div className="flex items-start justify-between">
        <div>
          <div className="text-[13px] font-medium text-ink-2">Барлық бонус</div>
          <div className="mt-1 flex items-baseline gap-1.5">
            <span className="text-[40px] font-extrabold leading-none tracking-[-0.03em]">{formatNumber(me.totalBalance)}</span>
            <span className="text-xl font-bold text-ink-2">Б</span>
          </div>
        </div>
        <span className="flex items-center gap-1.5 rounded-full bg-bg px-3 py-1.5 text-xs font-semibold text-ink-2">
          <Store size={14} /> {me.storeCount} дүкен
        </span>
      </div>
      <button
        type="button"
        onClick={onShowQr}
        className="mt-5 flex h-13 w-full items-center justify-center gap-2 rounded-2xl bg-brand text-[15px] font-semibold text-white active:scale-[0.99]"
      >
        <QrCode size={20} /> QR-код көрсету
      </button>
    </section>
  )
}
