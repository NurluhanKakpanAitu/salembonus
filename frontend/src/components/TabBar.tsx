import { useEffect, useRef, useState } from 'react'
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

/** Таңдау белгісінің бөлім ұясынан қаншаға тар екені. */
const PILL_INSET = 12
/** Серпімді жылжу: соңында аздап асып барып орнына түседі. */
const SPRING = 'cubic-bezier(0.34, 1.4, 0.64, 1)'

const matches = (pathname: string, tab: Tab) =>
  tab.end ? pathname === tab.to : pathname === tab.to || pathname.startsWith(`${tab.to}/`)

/**
 * Төменгі мәзір: таңдалған бөлімнің астында көк белгі сырғып барады,
 * жазуы жоғалып, иконкасы үлкейіп белгінің ортасына түседі.
 * Идея react-native-motion-tabs (MIT) жобасынан алынып, вебке бейімделген.
 */
export function TabBar() {
  const t = useT()
  const { pathname } = useLocation()
  const unread = useUnreadCount()
  const hasUnread = (unread.data ?? 0) > 0

  const barRef = useRef<HTMLUListElement>(null)
  const [width, setWidth] = useState(0)
  const activeIndex = Math.max(0, tabs.findIndex((tab) => matches(pathname, tab)))
  const slot = width / tabs.length

  useEffect(() => {
    const el = barRef.current
    if (!el) return
    const measure = () => setWidth(el.offsetWidth)
    measure()
    const observer = new ResizeObserver(measure)
    observer.observe(el)
    return () => observer.disconnect()
  }, [])

  return (
    <nav className="fixed inset-x-0 bottom-0 z-10 mx-auto max-w-[480px] px-4 pb-[max(12px,env(safe-area-inset-bottom))]">
      <div className="relative rounded-[32px] bg-surface shadow-[0_8px_28px_rgba(15,23,42,0.14)]">
        {slot > 0 && (
          <span
            aria-hidden="true"
            className="absolute inset-y-[7px] rounded-[26px] bg-brand shadow-[0_4px_12px_rgba(10,132,248,0.38)]"
            style={{
              left: PILL_INSET / 2,
              width: slot - PILL_INSET,
              transform: `translateX(${slot * activeIndex}px)`,
              transition: `transform 450ms ${SPRING}`,
            }}
          />
        )}

        <ul ref={barRef} className="relative flex items-center py-3">
          {tabs.map((tab, i) => {
            const { to, label, icon: Icon, end, badge } = tab
            const isActive = i === activeIndex
            return (
              <li key={to} className="min-w-0 flex-1">
                <NavLink
                  to={to}
                  end={end}
                  className="flex flex-col items-center px-1"
                  aria-label={t(label)}
                >
                  <span
                    className={`relative flex size-7 items-center justify-center transition-[transform,color] duration-[450ms] ${
                      isActive ? 'translate-y-[6px] scale-[1.15] text-white' : 'text-ink-2'
                    }`}
                    style={{ transitionTimingFunction: SPRING }}
                  >
                    <Icon size={23} strokeWidth={isActive ? 2.1 : 1.9} />
                    {badge && hasUnread && (
                      <span
                        className={`absolute -right-0.5 top-0 size-2 rounded-full ring-2 transition-colors ${
                          isActive ? 'bg-white ring-brand' : 'bg-danger ring-surface'
                        }`}
                      />
                    )}
                  </span>
                  <span
                    className={`mt-0.5 max-w-full truncate text-[10px] font-semibold leading-none text-ink-2 transition-opacity duration-300 ${
                      isActive ? 'opacity-0' : 'opacity-100'
                    }`}
                  >
                    {t(label)}
                  </span>
                </NavLink>
              </li>
            )
          })}
        </ul>
      </div>
    </nav>
  )
}
