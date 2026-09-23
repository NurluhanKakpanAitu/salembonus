import { ChevronRight, Plus } from 'lucide-react'
import { Link } from 'react-router-dom'
import type { BonusCard } from '../../lib/api'
import { formatNumber } from '../../lib/format'
import { storeIcon } from '../../lib/theme'

/** Дүкендердің жинақы карталары, көлденең сырғиды. */
export function StoreStrip({ cards }: { cards: BonusCard[] }) {
  return (
    <section>
      <div className="mb-3 flex items-center justify-between">
        <h2 className="text-[17px] font-bold">Менің дүкендерім</h2>
        <Link to="/cards" className="flex items-center text-[13px] font-semibold text-brand">
          Барлығы <ChevronRight size={16} />
        </Link>
      </div>
      <div className="-mx-4 flex gap-3 overflow-x-auto px-4 pb-1 [scrollbar-width:none] [&::-webkit-scrollbar]:hidden">
        {cards.map((c) => {
          const Icon = storeIcon(c.icon)
          return (
            <Link
              key={c.storeId}
              to={`/cards/${c.storeId}`}
              className="flex w-[148px] shrink-0 flex-col rounded-[20px] bg-surface p-4 active:scale-[0.98]"
            >
              <div
                className="flex size-11 items-center justify-center rounded-2xl"
                style={{ background: `${c.themeColor}1F`, color: c.themeColor === '#111113' ? '#0A84F8' : c.themeColor }}
              >
                <Icon size={22} />
              </div>
              <div className="mt-4 truncate text-[13px] font-semibold text-ink-2">{c.storeName}</div>
              <div className="mt-0.5 text-[20px] font-extrabold leading-tight tracking-tight">
                {formatNumber(c.balance)} <span className="text-sm font-bold text-ink-2">Б</span>
              </div>
              <div className="mt-2 w-fit rounded-full bg-bg px-2 py-0.5 text-[10px] font-semibold text-ink-2">{c.cashbackPercent}% бонус</div>
            </Link>
          )
        })}
        <Link
          to="/stores"
          className="flex w-[120px] shrink-0 flex-col items-center justify-center gap-2 rounded-[20px] border-2 border-dashed border-line text-ink-2 active:scale-[0.98]"
        >
          <span className="flex size-10 items-center justify-center rounded-full bg-surface"><Plus size={20} /></span>
          <span className="text-xs font-semibold">Дүкен қосу</span>
        </Link>
      </div>
    </section>
  )
}
