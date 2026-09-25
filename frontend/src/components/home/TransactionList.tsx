import { ChevronRight, Minus, Plus } from 'lucide-react'
import { Link } from 'react-router-dom'
import type { BonusTransaction } from '../../lib/api'
import { formatDateTime, formatSigned, formatTenge, transactionTitle } from '../../lib/format'
import { useT } from '../../lib/i18n'

export function TransactionList({ items, title, allHref }: {
  items: BonusTransaction[]
  title?: string
  allHref?: string
}) {
  const t = useT()
  return (
    <section>
      <div className="mb-3 flex items-center justify-between">
        <h2 className="text-[17px] font-bold">{title ?? t('tx.recent')}</h2>
        {allHref && (
          <Link to={allHref} className="flex items-center text-[13px] font-semibold text-brand">
            {t('common.all')} <ChevronRight size={16} />
          </Link>
        )}
      </div>
      <ul className="rounded-card bg-surface px-4">
        {items.length === 0 && <li className="py-6 text-center text-sm text-ink-2">{t('tx.empty')}</li>}
        {items.map((tx, i) => {
          const plus = tx.amount >= 0
          return (
            <li
              key={tx.id}
              className={`flex items-center gap-3 py-3.5 ${i < items.length - 1 ? 'border-b border-line' : ''}`}
            >
              <div className="flex size-11 shrink-0 items-center justify-center rounded-full bg-bg text-ink-2">
                {plus ? <Plus size={20} strokeWidth={2.5} /> : <Minus size={20} strokeWidth={2.5} />}
              </div>
              <div className="min-w-0 flex-1">
                <div className="truncate text-[15px] font-bold leading-tight">{transactionTitle(tx.type)}</div>
                <div className="mt-0.5 truncate text-xs text-ink-3">
                  {tx.storeName} · {formatDateTime(tx.createdAt)}
                </div>
              </div>
              <div className="shrink-0 text-right">
                <div className={`text-[15px] font-extrabold ${plus ? 'text-green' : 'text-danger'}`}>
                  {formatSigned(tx.amount, 'Б')}
                </div>
                {tx.purchaseAmount != null && (
                  <div className="text-[11px] text-ink-3">{t('tx.receipt', { amount: formatTenge(tx.purchaseAmount) })}</div>
                )}
              </div>
            </li>
          )
        })}
      </ul>
    </section>
  )
}
