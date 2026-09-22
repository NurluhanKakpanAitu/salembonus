import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from '../../lib/auth'

export function RequireAuth() {
  const loggedIn = useAuth((s) => !!s.accessToken)
  const location = useLocation()
  if (!loggedIn) return <Navigate to="/login" replace state={{ from: location.pathname }} />
  return <Outlet />
}
