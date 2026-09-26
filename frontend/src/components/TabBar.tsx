import { NavLink } from 'react-router-dom'
import { House, MessageCircle, Store, User, WalletCards } from 'lucide-react'
import { useUnreadCount } from '../lib/queries'
import { useT, type TranslationKey } from '../lib/i18n'

interface Tab {
  to: string
  label: TranslationKey
  icon: typeof House
  end?: boolean
  badge?: boolean
  /** Ортадағы көтеріңкі батырма — ең жиі ашылатын бөлім. */
  primary?: boolean
}

const tabs: Tab[] = [
  { to: '/', label: 'tab.home', icon: House, end: true },
  { to: '/stores', label: 'tab.stores', icon: Store },
  { to: '/cards', label: 'tab.cards', icon: WalletCards, primary: true },
  { to: '/notifications', label: 'tab.notifications', icon: MessageCircle, badge: true },
  { to: '/profile', label: 'tab.profile', icon: User },
]

export function TabBar() {
  const t = useT()
  const unread = useUnreadCount()
  const hasUnread = (unread.data ?? 0) > 0

  return (
    <nav className="fixed inset-x-0 bottom-0 z-10 mx-auto max-w-[480px] border-t border-line bg-surface px-1.5 pb-[max(10px,env(safe-area-inset-bottom))] pt-2">
      <ul className="flex items-end">
        {tabs.map(({ to, label, icon: Icon, end, badge, primary }) => (
          <li key={to} className="min-w-0 flex-1">
            <NavLink
              to={to}
              end={end}
              className={({ isActive }) =>
                `flex flex-col items-center gap-1 text-[10px] font-semibold ${
                  isActive || primary ? 'text-brand' : 'text-ink-2'
                }`
              }
            >
              {({ isActive }) => (
                <>
                  <span
                    className={
                      primary
                        ? 'relative -mt-6 flex size-[52px] items-center justify-center rounded-full bg-brand text-white shadow-[0_6px_16px_rgba(10,132,248,0.4)]'
                        : `relative flex h-9 w-14 items-center justify-center rounded-2xl transition-colors ${
                            isActive ? 'bg-brand-soft' : ''
                          }`
                    }
                  >
                    <Icon size={primary ? 25 : 23} strokeWidth={isActive || primary ? 2.1 : 1.8} />
                    {badge && hasUnread && (
                      <span className="absolute right-3 top-1 size-2 rounded-full bg-danger ring-2 ring-surface" />
                    )}
                  </span>
                  <span className="max-w-full truncate">{t(label)}</span>
                </>
              )}
            </NavLink>
          </li>
        ))}
      </ul>
    </nav>
  )
}
