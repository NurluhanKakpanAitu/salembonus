import { ChevronRight, type LucideIcon } from 'lucide-react'
import { Link } from 'react-router-dom'

export interface MenuItem {
  icon: LucideIcon
  title: string
  subtitle: string
  to?: string
  onClick?: () => void
  danger?: boolean
}

export function MenuList({ items }: { items: MenuItem[] }) {
  return (
    <ul className="rounded-card bg-surface px-4">
      {items.map((it, i) => {
        const Icon = it.icon
        const inner = (
          <>
            <Icon size={22} strokeWidth={1.8} className={it.danger ? 'text-danger' : 'text-ink-2'} />
            <div className="min-w-0 flex-1">
              <div className={`text-[15px] font-semibold ${it.danger ? 'text-danger' : ''}`}>{it.title}</div>
              <div className="text-xs text-ink-2">{it.subtitle}</div>
            </div>
            <ChevronRight size={18} className="text-ink-3" />
          </>
        )
        const cls = `flex w-full items-center gap-3.5 py-3.5 text-left ${i < items.length - 1 ? 'border-b border-line' : ''}`
        return (
          <li key={it.title}>
            {it.to ? (
              <Link to={it.to} className={cls}>{inner}</Link>
            ) : (
              <button type="button" onClick={it.onClick} className={cls}>{inner}</button>
            )}
          </li>
        )
      })}
    </ul>
  )
}
