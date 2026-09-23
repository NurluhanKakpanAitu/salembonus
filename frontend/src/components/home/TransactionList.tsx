import { ChevronRight, CircleMinus, CirclePlus } from 'lucide-react'
import { Link } from 'react-router-dom'
import type { BonusTransaction } from '../../lib/api'
import { formatDateTime, formatSigned, formatTenge, transactionTitle } from '../../lib/format'

export function TransactionList({ items, title = 'Соңғы операциялар', allHref }: {
  items: BonusTransaction[]
  title?: string
  allHref?: string
}) {
  return (
    <section>
      <div className="mb-3 flex items-center justify-between">
        <h2 className="text-lg font-bold">{title}</h2>
        {allHref && (
          <Link to={allHref} className="flex items-center text-[13px] font-medium text-blue-600">
            Барлығы <ChevronRight size={14} />
          </Link>
        )}
      </div>
      <ul className="rounded-card bg-surface px-4">
        {items.length === 0 && <li className="py-6 text-center text-sm text-ink-2">Операциялар әлі жоқ</li>}
        {items.map((t, i) => {
          const plus = t.amount >= 0
          return (
            <li
              key={t.id}
              className={`flex items-center gap-3 py-3.5 ${i < items.length - 1 ? 'border-b border-line' : ''}`}
            >
              <div className={`flex size-11 shrink-0 items-center justify-center rounded-full ${plus ? 'bg-green-soft text-green' : 'bg-danger-soft text-danger'}`}>
                {plus ? <CirclePlus size={22} /> : <CircleMinus size={22} />}
              </div>
              <div className="min-w-0 flex-1">
                <div className="truncate text-[15px] font-semibold">{transactionTitle[t.type] ?? t.type}</div>
                <div className="text-[13px] text-ink-2">{t.storeName}</div>
                <div className="text-xs text-ink-3">{formatDateTime(t.createdAt)}</div>
              </div>
              <div className="text-right">
                <div className={`text-base font-bold ${plus ? 'text-green' : 'text-danger'}`}>{formatSigned(t.amount, 'Б')}</div>
                {t.purchaseAmount != null && <div className="text-xs text-ink-3">Чек: {formatTenge(t.purchaseAmount)}</div>}
              </div>
            </li>
          )
        })}
      </ul>
    </section>
  )
}
