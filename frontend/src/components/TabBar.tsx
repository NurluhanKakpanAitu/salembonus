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

const RADIUS = 20
/** Доғаның ені мен биіктігі — панельдің жиегінен шығып тұратын томпақ. */
const DOME_W = 56
const DOME_H = 13
const SLIDE_MS = 280

const matches = (pathname: string, tab: Tab) =>
  tab.end ? pathname === tab.to : pathname === tab.to || pathname.startsWith(`${tab.to}/`)

/**
 * Панельдің контуры: дөңгелектелген тіктөртбұрыш, үстіңгі жиегінде cx нүктесінде доға.
 * Доғаның шеттері бұрыштарға жетпейді, сондықтан шеткі бөлімде де пішін бұзылмайды.
 */
function barPath(w: number, h: number, cx: number): string {
  const half = DOME_W / 2
  // Табаны бұрыштарға жетпейді, ал шыңы әрқашан дәл таңдалған иконканың үстінде.
  const left = Math.max(RADIUS, cx - half)
  const right = Math.min(w - RADIUS, cx + half)
  const easeLeft = (cx - left) * 0.55
  const easeRight = (right - cx) * 0.55
  return [
    `M ${RADIUS} 0`,
    `L ${left} 0`,
    `C ${left + easeLeft} 0 ${cx - easeLeft} ${-DOME_H} ${cx} ${-DOME_H}`,
    `C ${cx + easeRight} ${-DOME_H} ${right - easeRight} 0 ${right} 0`,
    `L ${w - RADIUS} 0`,
    `A ${RADIUS} ${RADIUS} 0 0 1 ${w} ${RADIUS}`,
    `L ${w} ${h - RADIUS}`,
    `A ${RADIUS} ${RADIUS} 0 0 1 ${w - RADIUS} ${h}`,
    `L ${RADIUS} ${h}`,
    `A ${RADIUS} ${RADIUS} 0 0 1 0 ${h - RADIUS}`,
    `L 0 ${RADIUS}`,
    `A ${RADIUS} ${RADIUS} 0 0 1 ${RADIUS} 0`,
    'Z',
  ].join(' ')
}

export function TabBar() {
  const t = useT()
  const { pathname } = useLocation()
  const unread = useUnreadCount()
  const hasUnread = (unread.data ?? 0) > 0

  const barRef = useRef<HTMLUListElement>(null)
  const [size, setSize] = useState({ w: 0, h: 0 })
  const activeIndex = Math.max(0, tabs.findIndex((tab) => matches(pathname, tab)))
  const target = size.w ? ((activeIndex + 0.5) * size.w) / tabs.length : 0

  const [cx, setCx] = useState(target)
  const fromRef = useRef(target)

  useEffect(() => {
    const el = barRef.current
    if (!el) return
    const measure = () => setSize({ w: el.offsetWidth, h: el.offsetHeight })
    measure()
    const observer = new ResizeObserver(measure)
    observer.observe(el)
    return () => observer.disconnect()
  }, [])

  // Доға жаңа орнына жұмсақ сырғиды.
  useEffect(() => {
    if (!size.w) return
    const from = fromRef.current || target
    if (from === target) {
      setCx(target)
      return
    }
    let frame = 0
    const start = performance.now()
    const step = (now: number) => {
      const p = Math.min(1, (now - start) / SLIDE_MS)
      const eased = 1 - (1 - p) ** 3
      const value = from + (target - from) * eased
      setCx(value)
      fromRef.current = value
      if (p < 1) frame = requestAnimationFrame(step)
    }
    frame = requestAnimationFrame(step)
    return () => cancelAnimationFrame(frame)
  }, [target, size.w])

  return (
    <nav className="fixed inset-x-0 bottom-0 z-10 mx-auto max-w-[480px] px-3 pb-[max(10px,env(safe-area-inset-bottom))]">
      <div className="relative">
        {size.w > 0 && (
          <svg
            aria-hidden="true"
            className="pointer-events-none absolute left-0 drop-shadow-[0_4px_18px_rgba(15,23,42,0.12)]"
            style={{ top: -DOME_H, width: size.w, height: size.h + DOME_H }}
            viewBox={`0 ${-DOME_H} ${size.w} ${size.h + DOME_H}`}
          >
            <path d={barPath(size.w, size.h, cx)} fill="var(--color-surface)" />
          </svg>
        )}

        {/* Таңдалған бөлімнің көк дөңгелегі — доғамен бірге жылжиды. */}
        {size.w > 0 && (
          <span
            aria-hidden="true"
            className="absolute -top-[3px] size-10 -translate-x-1/2 rounded-full bg-brand shadow-[0_5px_12px_rgba(10,132,248,0.4)]"
            style={{ left: cx }}
          />
        )}

        <ul ref={barRef} className="relative flex items-center px-2.5 pb-2.5 pt-3">
          {tabs.map((tab, i) => {
            const { to, label, icon: Icon, end, badge } = tab
            const isActive = i === activeIndex
            return (
              <li key={to} className="min-w-0 flex-1">
                <NavLink
                  to={to}
                  end={end}
                  className={`flex flex-col items-center gap-1 px-1 text-[10px] font-semibold leading-none transition-colors ${
                    isActive ? 'text-brand' : 'text-ink-2'
                  }`}
                >
                  <span
                    className={`relative flex size-10 items-center justify-center transition-transform duration-300 ease-out ${
                      isActive ? '-translate-y-[13px] text-white' : ''
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
