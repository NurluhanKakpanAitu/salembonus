import { NavLink } from 'react-router-dom'
import { CreditCard, House, MessageCircleMore, ShoppingBag, User } from 'lucide-react'
import { useUnreadCount } from '../lib/queries'
import { useT, type TranslationKey } from '../lib/i18n'

interface Tab {
  to: string
  label: TranslationKey
  icon: typeof House
  end?: boolean
  badge?: boolean
}

const tabs: Tab[] = [
  { to: '/', label: 'tab.home', icon: House, end: true },
  { to: '/stores', label: 'tab.stores', icon: ShoppingBag },
  { to: '/cards', label: 'tab.cards', icon: CreditCard },
  { to: '/notifications', label: 'tab.notifications', icon: MessageCircleMore, badge: true },
  { to: '/profile', label: 'tab.profile', icon: User },
]

export function TabBar() {
  const t = useT()
  const unread = useUnreadCount()
  const hasUnread = (unread.data ?? 0) > 0

  return (
    <nav className="fixed inset-x-0 bottom-0 z-10 mx-auto max-w-[480px] px-3 pb-[max(10px,env(safe-area-inset-bottom))]">
      <ul className="flex items-center rounded-[26px] bg-surface px-1.5 pb-2.5 pt-3 shadow-[0_6px_28px_rgba(15,23,42,0.12)]">
        {tabs.map(({ to, label, icon: Icon, end, badge }) => (
          <li key={to} className="min-w-0 flex-1">
            <NavLink
              to={to}
              end={end}
              className={({ isActive }) =>
                `flex flex-col items-center gap-1 text-[10px] font-semibold transition-colors ${
                  isActive ? 'text-brand' : 'text-ink-2'
                }`
              }
            >
              {({ isActive }) => (
                <>
                  {/*
                    Таңдалған бөлім көк дөңгелекке айналып, мәзірден сәл жоғары көтеріледі.
                    Айналасындағы ақ жиек панельді доға сияқты ойып тұрғандай көрсетеді.
                  */}
                  <span
                    className={`relative flex size-10 items-center justify-center rounded-full transition-all duration-300 ease-out ${
                      isActive
                        ? '-translate-y-3 bg-brand text-white shadow-[0_6px_14px_rgba(10,132,248,0.45)] ring-4 ring-surface'
                        : 'translate-y-0'
                    }`}
                  >
                    <Icon size={23} strokeWidth={isActive ? 2.1 : 1.9} />
                    {badge && hasUnread && (
                      <span className="absolute right-1.5 top-1.5 size-2 rounded-full bg-danger ring-2 ring-surface" />
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
