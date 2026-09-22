import { Bell, Gift, Percent, Settings, Store, type LucideIcon } from 'lucide-react'
import type { NotificationCategory } from '../../lib/api'

const CHIPS: { key: NotificationCategory | null; label: string; icon: LucideIcon }[] = [
  { key: null, label: 'Барлығы', icon: Bell },
  { key: 'Bonus', label: 'Бонустар', icon: Gift },
  { key: 'Promo', label: 'Акциялар', icon: Percent },
  { key: 'System', label: 'Жүйелік', icon: Settings },
  { key: 'Store', label: 'Дүкендер', icon: Store },
]

export function FilterChips({ value, onChange }: {
  value: NotificationCategory | null
  onChange: (v: NotificationCategory | null) => void
}) {
  return (
    <div className="-mx-4 flex gap-2 overflow-x-auto px-4 [scrollbar-width:none] [&::-webkit-scrollbar]:hidden">
      {CHIPS.map(({ key, label, icon: Icon }) => {
        const active = key === value
        return (
          <button
            key={label}
            type="button"
            onClick={() => onChange(key)}
            className={`flex shrink-0 items-center gap-1.5 rounded-full px-3 py-2 text-xs font-semibold transition-colors ${
              active ? 'bg-brand text-white' : 'border border-line bg-surface text-ink'
            }`}
          >
            <Icon size={14} className={active ? 'text-white' : 'text-ink-2'} /> {label}
          </button>
        )
      })}
    </div>
  )
}
