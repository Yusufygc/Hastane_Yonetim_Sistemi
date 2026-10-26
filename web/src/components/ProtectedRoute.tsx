import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'

/** Yalnız görsel kilit: giriş yoksa giriş sayfasına yönlendirir. Gerçek koruma backend'dedir (401/403/404). */
export default function ProtectedRoute() {
  const { state } = useAuth()
  const location = useLocation()

  if (state.status === 'loading') return <p className="page-note" role="status">Oturum denetleniyor…</p>
  if (state.status === 'anonymous') return <Navigate to="/giris" replace state={{ from: location.pathname }} />
  return <Outlet />
}
