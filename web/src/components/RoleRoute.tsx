import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'

/**
 * Yalnız görsel rol kilidi: giriş yoksa girişe, yanlış rolse açıklayıcı mesaja yönlendirir.
 * Gerçek yetki backend'dedir (yanlış rolde API 403 döner); bu bileşen yalnızca kullanıcıyı boş ekrandan kurtarır.
 */
export default function RoleRoute({ role, label }: { role: string; label: string }) {
  const { state } = useAuth()
  const location = useLocation()

  if (state.status === 'loading') {
    return (
      <p className="page-note" role="status">
        Oturum denetleniyor…
      </p>
    )
  }
  if (state.status === 'anonymous') return <Navigate to="/giris" replace state={{ from: location.pathname }} />
  if (state.user.role !== role) {
    return (
      <section className="page narrow">
        <h1>Bu sayfaya erişiminiz yok</h1>
        <p>{label} yalnız ilgili rolle giriş yapanlara açıktır.</p>
      </section>
    )
  }
  return <Outlet />
}
