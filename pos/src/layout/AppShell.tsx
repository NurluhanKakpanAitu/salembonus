import { Suspense, useEffect, useState } from 'react'
import { Outlet, useLocation } from 'react-router-dom'
import { Loader2 } from 'lucide-react'
import { useT } from '../lib/i18n'
import { Toaster } from '../components/ui/Toast'
import { NAV } from './nav'
import { Sidebar } from './Sidebar'
import { Topbar } from './Topbar'

const COLLAPSED_KEY = 'salempos.sidebar.collapsed'

function readCollapsed() {
  try {
    return localStorage.getItem(COLLAPSED_KEY) === '1'
  } catch {
    return false
  }
}

/**
 * Кабинеттің қабығы: сол жақта мәзір, жоғарыда панель, ортада бөлім. Әрекетсіздік бұғаты мұнда жоқ — ол тек касса бетінде
 * (CashierShell): касса бұғатталса да, сол құрылғыдағы кабинет (тауар, қойма, есептер) жұмыс істей береді.
 */
export function AppShell() {
  const t = useT()
  const location = useLocation()
  const [collapsed, setCollapsed] = useState(readCollapsed)
  const [mobileOpen, setMobileOpen] = useState(false)

  const current = NAV.find((i) => location.pathname.startsWith(i.path))

  // Мобильді мәзір Escape-пен жабылады.
  useEffect(() => {
    if (!mobileOpen) return
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && setMobileOpen(false)
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [mobileOpen])

  const toggle = () =>
    setCollapsed((c) => {
      try {
        localStorage.setItem(COLLAPSED_KEY, c ? '0' : '1')
      } catch {
        /* сақталмаса — келесі жолы ашық тұрады */
      }
      return !c
    })

  return (
    <div className="flex h-full">
      <Sidebar collapsed={collapsed} onToggle={toggle} mobileOpen={mobileOpen} onClose={() => setMobileOpen(false)} />
      <div className="flex min-w-0 flex-1 flex-col">
        <Topbar title={current ? t(current.label) : ''} onMenu={() => setMobileOpen(true)} />
        <main className="min-h-0 flex-1 overflow-y-auto p-4 lg:p-6">
          <Suspense fallback={<div className="flex h-full items-center justify-center text-ink-3"><Loader2 className="animate-spin" size={28} /></div>}>
            <Outlet />
          </Suspense>
        </main>
      </div>
      <Toaster />
    </div>
  )
}
