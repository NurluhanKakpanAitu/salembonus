import { Navigate, Route, Routes } from 'react-router-dom'
import { AppShell } from './components/AppShell'
import { HomePage } from './pages/HomePage'
import { CardsPage } from './pages/CardsPage'
import { CardDetailPage } from './pages/CardDetailPage'
import { StoresPage } from './pages/StoresPage'
import { NotificationsPage } from './pages/NotificationsPage'
import { ProfilePage } from './pages/ProfilePage'
import { TransactionsPage } from './pages/TransactionsPage'

export default function App() {
  return (
    <Routes>
      <Route element={<AppShell />}>
        <Route index element={<HomePage />} />
        <Route path="cards" element={<CardsPage />} />
        <Route path="cards/:storeId" element={<CardDetailPage />} />
        <Route path="transactions" element={<TransactionsPage />} />
        <Route path="stores" element={<StoresPage />} />
        <Route path="notifications" element={<NotificationsPage />} />
        <Route path="profile" element={<ProfilePage />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Route>
    </Routes>
  )
}
