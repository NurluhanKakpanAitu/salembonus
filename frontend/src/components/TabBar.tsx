import { NavLink } from 'react-router-dom'
import { Bell, CirclePlus, CreditCard, House, User } from 'lucide-react'
import { useUnreadCount } from '../lib/queries'
import { useT, type TranslationKey } from '../lib/i18n'

const tabs: { to: string; label: TranslationKey; icon: typeof House; end?: boolean; badge?: boolean }[] = [
  { to: '/', label: 'tab.home', icon: House, end: true },
  { to: '/cards', label: 'tab.cards', icon: CreditCard },
  { to: '/stores', label: 'tab.stores', icon: CirclePlus },
  { to: '/notifications', label: 'tab.notifications', icon: Bell, badge: true },
  { to: '/profile', label: 'tab.profile', icon: User },
]

export function TabBar() {
  const t = useT()
  const unread = useUnreadCount()
  const hasUnread = (unread.data ?? 0) > 0

  return (
    <nav className="fixed inset-x-0 bottom-0 z-10 mx-auto max-w-[480px] border-t border-line bg-surface px-2 pb-[max(12px,env(safe-area-inset-bottom))] pt-2.5">
      <ul className="flex justify-between">
        {tabs.map(({ to, label, icon: Icon, end, badge }) => (
          <li key={to} className="flex-1">
            <NavLink
              to={to}
              end={end}
              className={({ isActive }) =>
                `flex flex-col items-center gap-1 text-[10px] font-medium ${
                  isActive ? 'text-brand font-semibold' : 'text-ink-2'
                }`
              }
            >
              <span className="relative">
                <Icon size={24} strokeWidth={1.8} />
                {badge && hasUnread && <span className="absolute -right-0.5 -top-0.5 size-2 rounded-full bg-brand" />}
              </span>
              {t(label)}
            </NavLink>
          </li>
        ))}
      </ul>
    </nav>
  )
}
