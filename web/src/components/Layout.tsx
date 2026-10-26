import { Link, NavLink, Outlet } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { useMotion } from '../lib/motion'

export default function Layout() {
  const { state, logout } = useAuth()
  const { simple, mode, setMode } = useMotion()

  return (
    <>
      <a className="skip-link" href="#icerik">
        İçeriğe geç
      </a>
      <header className="site-header">
        <Link to="/" className="brand" aria-label="Hastane Randevu, ana sayfa">
          <span className="brand-mark" aria-hidden="true">
            +
          </span>
          Hastane Randevu
        </Link>
        <nav aria-label="Ana menü" className="nav">
          <NavLink to="/randevu-al">Randevu al</NavLink>
          {state.status === 'authenticated' && <NavLink to="/randevularim">Randevularım</NavLink>}
        </nav>
        <div className="header-actions">
          <button
            type="button"
            className="btn btn-ghost"
            aria-pressed={simple}
            title="3B animasyonları kapatır; düşük başarımlı cihazlar için"
            onClick={() => setMode(simple ? 'full' : 'simple')}
          >
            Sade mod: {simple ? 'açık' : 'kapalı'}
            {mode === 'auto' && <span className="sr-only"> (otomatik)</span>}
          </button>
          {state.status === 'authenticated' ? (
            <>
              <span className="user-chip" title={state.user.email}>
                {state.user.email}
              </span>
              <button type="button" className="btn" onClick={() => void logout()}>
                Çıkış
              </button>
            </>
          ) : (
            state.status === 'anonymous' && (
              <>
                <Link className="btn btn-ghost" to="/giris">
                  Giriş
                </Link>
                <Link className="btn btn-primary" to="/kayit">
                  Kayıt ol
                </Link>
              </>
            )
          )}
        </div>
      </header>
      <main id="icerik" tabIndex={-1}>
        <Outlet />
      </main>
    </>
  )
}
