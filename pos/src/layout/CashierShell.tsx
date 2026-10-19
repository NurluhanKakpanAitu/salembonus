import { Suspense } from 'react'
import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { Loader2 } from 'lucide-react'
import { useT } from '../lib/i18n'
import { enterKiosk, isKiosk } from '../lib/kiosk'
import { useRegister } from '../lib/register'
import { LockScreen } from '../components/LockScreen'
import { Toaster } from '../components/ui/Toast'
import { useIdleLock } from './useIdleLock'
import { Topbar } from './Topbar'

/**
 * Кассаның қабығы: мәзірсіз, тек жоғарғы панель (кассир, хабарламалар, бұғаттау, ауысу, шығу).
 * Кассир ауысса да вкладка касса режимінде қалады — келесі кассир де тек кассаны көреді.
 */
export function CashierShell() {
  const t = useT()
  const locked = useRegister((s) => s.locked && !!s.register)
  useIdleLock()
  enterKiosk()

  return (
    <div className="flex h-full flex-col">
      <Topbar title={t('nav.cashier')} kiosk />
      <main className="min-h-0 flex-1 overflow-y-auto p-4 lg:p-6">
        <Suspense fallback={<div className="flex h-full items-center justify-center text-ink-3"><Loader2 className="animate-spin" size={28} /></div>}>
          <Outlet />
        </Suspense>
      </main>
      {locked && <LockScreen />}
      <Toaster />
    </div>
  )
}

/** Касса вкладкасында басқа бөлімнің мекенжайы ашылмайды — кассаға қайтарады. */
export function KioskGuard() {
  const { pathname } = useLocation()
  if (isKiosk() && !pathname.startsWith('/cashier')) return <Navigate to="/cashier" replace />
  return <Outlet />
}
