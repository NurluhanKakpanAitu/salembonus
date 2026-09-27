import { NavLink } from 'react-router-dom'
import { ChevronsLeft, ChevronsRight, X } from 'lucide-react'
import { Logo } from '../components/Logo'
import { activeStore, useAuth } from '../lib/auth'
import { useT } from '../lib/i18n'
import { canSee, NAV } from './nav'

/** Қара сол жақ мәзір. Тек рұқсат етілген бөлімдер көрінеді; мобильдіде шығып тұратын панель. */
export function Sidebar({ collapsed, onToggle, mobileOpen, onClose }: {
  collapsed: boolean
  onToggle: () => void
  mobileOpen: boolean
  onClose: () => void
}) {
  const t = useT()
  const store = useAuth(activeStore)
  const items = NAV.filter((i) => canSee(i, store))

  return (
    <>
      {mobileOpen && <div className="fixed inset-0 z-30 bg-black/40 lg:hidden" onClick={onClose} aria-hidden="true" />}
      <aside
        className={`fixed inset-y-0 left-0 z-40 flex flex-col bg-nav text-nav-ink transition-[width,transform] duration-200 lg:static lg:translate-x-0 ${
          mobileOpen ? 'translate-x-0' : '-translate-x-full'
        } ${collapsed ? 'lg:w-[76px]' : 'lg:w-[248px]'} w-[260px]`}
      >
        <div className={`flex h-[72px] shrink-0 items-center ${collapsed ? 'lg:justify-center' : ''} px-5`}>
          {collapsed ? (
            <span className="hidden text-[22px] font-extrabold text-white lg:inline">
              S<span className="text-brand">P</span>
            </span>
          ) : null}
          <span className={collapsed ? 'lg:hidden' : ''}>
            <Logo size={27} />
          </span>
          <button type="button" onClick={onClose} className="ml-auto text-nav-ink lg:hidden" aria-label="Close">
            <X size={22} />
          </button>
        </div>

        <nav className="flex-1 overflow-y-auto px-3 py-2">
          <ul className="flex flex-col gap-1">
            {items.map((item) => (
              <li key={item.path}>
                <NavLink
                  to={item.path}
                  onClick={onClose}
                  title={collapsed ? t(item.label) : undefined}
                  className={({ isActive }) =>
                    `flex h-11 items-center gap-3 rounded-xl px-3 text-[15px] font-medium transition-colors ${
                      collapsed ? 'lg:justify-center lg:px-0' : ''
                    } ${isActive ? 'bg-brand text-white' : 'hover:bg-nav-2 hover:text-white'}`
                  }
                >
                  <item.icon size={20} className="shrink-0" />
                  <span className={`truncate ${collapsed ? 'lg:hidden' : ''}`}>{t(item.label)}</span>
                </NavLink>
              </li>
            ))}
          </ul>
        </nav>

        <button
          type="button"
          onClick={onToggle}
          className={`mx-3 mb-4 hidden h-11 items-center gap-3 rounded-xl px-3 text-[14px] hover:bg-nav-2 hover:text-white lg:flex ${
            collapsed ? 'justify-center px-0' : ''
          }`}
        >
          {collapsed ? <ChevronsRight size={19} /> : <ChevronsLeft size={19} />}
          {!collapsed && t('nav.collapse')}
        </button>
      </aside>
    </>
  )
}
