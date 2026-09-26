import { NavLink, useLocation } from 'react-router-dom'
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

const matches = (pathname: string, tab: Tab) =>
  tab.end ? pathname === tab.to : pathname === tab.to || pathname.startsWith(`${tab.to}/`)

export function TabBar() {
  const t = useT()
  const { pathname } = useLocation()
  const unread = useUnreadCount()
  const hasUnread = (unread.data ?? 0) > 0

  const activeIndex = Math.max(0, tabs.findIndex((tab) => matches(pathname, tab)))
  // Доға мен көк дөңгелектің ортасы — сол бөлімнің тұрған жері.
  const center = `${((activeIndex + 0.5) * 100) / tabs.length}%`

  return (
    <nav className="fixed inset-x-0 bottom-0 z-10 mx-auto max-w-[480px] px-3 pb-[max(10px,env(safe-area-inset-bottom))]">
      <div className="relative">
        {/* Мәзір панелі */}
        <div className="absolute inset-0 rounded-[26px] bg-surface shadow-[0_6px_28px_rgba(15,23,42,0.12)]" />

        {/* Панельдің шетінен шығып тұратын доға — таңдағанда жылжиды */}
        <span
          aria-hidden="true"
          className="absolute -top-[17px] h-[48px] w-[94px] -translate-x-1/2 rounded-[100%] bg-surface transition-[left] duration-300 ease-out"
          style={{ left: center }}
        />

        {/* Таңдалған бөлімнің көк дөңгелегі */}
        <span
          aria-hidden="true"
          className="absolute -top-[9px] size-[42px] -translate-x-1/2 rounded-full bg-brand shadow-[0_6px_14px_rgba(10,132,248,0.45)] transition-[left] duration-300 ease-out"
          style={{ left: center }}
        />

        <ul className="relative flex items-center px-1.5 pb-2.5 pt-3">
          {tabs.map((tab, i) => {
            const { to, label, icon: Icon, end, badge } = tab
            const isActive = i === activeIndex
            return (
              <li key={to} className="min-w-0 flex-1">
                <NavLink
                  to={to}
                  end={end}
                  className={`flex flex-col items-center gap-1 text-[10px] font-semibold transition-colors ${
                    isActive ? 'text-brand' : 'text-ink-2'
                  }`}
                >
                  <span
                    className={`relative flex size-10 items-center justify-center transition-transform duration-300 ease-out ${
                      isActive ? '-translate-y-3 text-white' : ''
                    }`}
                  >
                    <Icon size={23} strokeWidth={isActive ? 2.1 : 1.9} />
                    {badge && hasUnread && (
                      <span className="absolute right-1.5 top-1.5 size-2 rounded-full bg-danger ring-2 ring-surface" />
                    )}
                  </span>
                  <span className="max-w-full truncate">{t(label)}</span>
                </NavLink>
              </li>
            )
          })}
        </ul>
      </div>
    </nav>
  )
}
