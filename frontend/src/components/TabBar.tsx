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
  /** Ортадағы негізгі бөлім — иконкасы көк дөңгелектің ішінде тұрады. */
  primary?: boolean
}

const tabs: Tab[] = [
  { to: '/', label: 'tab.home', icon: House, end: true },
  { to: '/stores', label: 'tab.stores', icon: ShoppingBag },
  { to: '/cards', label: 'tab.cards', icon: CreditCard, primary: true },
  { to: '/notifications', label: 'tab.notifications', icon: MessageCircleMore, badge: true },
  { to: '/profile', label: 'tab.profile', icon: User },
]

export function TabBar() {
  const t = useT()
  const unread = useUnreadCount()
  const hasUnread = (unread.data ?? 0) > 0

  return (
    <nav className="fixed inset-x-0 bottom-0 z-10 mx-auto max-w-[480px] px-3 pb-[max(10px,env(safe-area-inset-bottom))]">
      <ul className="flex items-center rounded-[26px] bg-surface px-1.5 py-2.5 shadow-[0_6px_28px_rgba(15,23,42,0.12)]">
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
                    className={`relative flex size-10 items-center justify-center rounded-full transition-colors ${
                      primary ? 'bg-brand text-white' : isActive ? 'bg-brand-soft' : ''
                    }`}
                  >
                    <Icon size={23} strokeWidth={1.9} />
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
