import { Navigate, Route, Routes } from 'react-router-dom'
import { AppShell } from './components/AppShell'
import { RequireAuth } from './components/auth/RequireAuth'
import { InstallGate } from './components/InstallGate'
import { HomePage } from './pages/HomePage'
import { CardsPage } from './pages/CardsPage'
import { CardDetailPage } from './pages/CardDetailPage'
import { StoresPage } from './pages/StoresPage'
import { StorePage } from './pages/StorePage'
import { NotificationsPage } from './pages/NotificationsPage'
import { ProfilePage } from './pages/ProfilePage'
import { ProfileEditPage } from './pages/ProfileEditPage'
import { TransactionsPage } from './pages/TransactionsPage'
import { LoginPage } from './pages/auth/LoginPage'
import { VerifyPage } from './pages/auth/VerifyPage'
import { WelcomePage } from './pages/auth/WelcomePage'

export default function App() {
  return (
    <InstallGate>
    <Routes>
      <Route path="login" element={<LoginPage />} />
      <Route path="verify" element={<VerifyPage />} />
      <Route element={<RequireAuth />}>
        <Route path="welcome" element={<WelcomePage />} />
        <Route element={<AppShell />}>
          <Route index element={<HomePage />} />
          <Route path="cards" element={<CardsPage />} />
          <Route path="cards/:storeId" element={<CardDetailPage />} />
          <Route path="transactions" element={<TransactionsPage />} />
          <Route path="stores" element={<StoresPage />} />
          <Route path="stores/:storeId" element={<StorePage />} />
          <Route path="notifications" element={<NotificationsPage />} />
          <Route path="profile" element={<ProfilePage />} />
          <Route path="profile/edit" element={<ProfileEditPage />} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Route>
      </Route>
    </Routes>
    </InstallGate>
  )
}
