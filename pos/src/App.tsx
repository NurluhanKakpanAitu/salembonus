import { useEffect } from 'react'
import { Navigate, Outlet, Route, Routes } from 'react-router-dom'
import { Loader2 } from 'lucide-react'
import { AppShell } from './layout/AppShell'
import { NAV } from './layout/nav'
import { LoginPage } from './pages/LoginPage'
import { ForgotPasswordPage } from './pages/ForgotPasswordPage'
import { SectionPage } from './pages/SectionPage'
import { refreshSession, useAuth } from './lib/auth'
import { loadRegister } from './lib/register'
import { CashierPage } from './pages/CashierPage'
import { ProductsPage } from './pages/products/ProductsPage'
import { StatisticsPage } from './pages/StatisticsPage'
import type { TranslationKey } from './lib/i18n'

const HINTS: Record<string, TranslationKey> = {
  '/statistics': 'section.statisticsHint',
}

/** Кірмеген пайдаланушы кіру бетіне жіберіледі. Шешімді сервер қабылдайды (ТЗ §11.8). */
function RequireAuth() {
  const status = useAuth((s) => s.status)
  if (status === 'loading')
    return (
      <div className="flex h-full items-center justify-center text-ink-3">
        <Loader2 className="animate-spin" size={28} />
      </div>
    )
  if (status === 'guest') return <Navigate to="/login" replace />
  return <Outlet />
}

function StartPage() {
  const startPage = useAuth((s) => s.me?.startPage ?? 'cashier')
  return <Navigate to={`/${startPage}`} replace />
}

export default function App() {
  // Бет ашылғанда сеансты cookie-дегі refresh арқылы қалпына келтіреміз.
  useEffect(() => {
    if (useAuth.getState().status === 'loading') void refreshSession()
    // Бұл құрылғы касса ма — кірмей тұрып та керек (кіру бетінде кассирлер тізімі).
    void loadRegister()
  }, [])

  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/forgot-password" element={<ForgotPasswordPage />} />
      <Route element={<RequireAuth />}>
        <Route element={<AppShell />}>
          <Route index element={<StartPage />} />
          {NAV.map((item) => (
            <Route
              key={item.path}
              path={item.path === '/products' ? '/products/*' : item.path}
              element={
                item.path === '/cashier' ? <CashierPage />
                : item.path === '/products' ? <ProductsPage />
                : item.path === '/statistics' ? <StatisticsPage />
                : <SectionPage item={item} hint={HINTS[item.path]} />
              }
            />
          ))}
          <Route path="*" element={<StartPage />} />
        </Route>
      </Route>
    </Routes>
  )
}
