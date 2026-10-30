import { Link, NavLink, Outlet } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { useMotion } from '../lib/motion'
import { useTheme, type ThemeMode } from '../lib/theme'

const THEME_OPTIONS: { value: ThemeMode; label: string; title: string; icon: string }[] = [
  { value: 'light', label: 'Açık', title: 'Açık tema', icon: '☀' },
  { value: 'dark', label: 'Koyu', title: 'Koyu tema', icon: '☾' },
  { value: 'system', label: 'Sistem', title: 'Sistem teması', icon: '◐' },
]

export default function Layout() {
  const { state, logout } = useAuth()
  const { simple, mode, setMode } = useMotion()
  const { mode: themeMode, setMode: setThemeMode } = useTheme()
  const isDoctor = state.status === 'authenticated' && state.user.role === 'DOCTOR'
  const isAdmin = state.status === 'authenticated' && state.user.role === 'ADMIN'

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
          <span className="brand-text">
            Hastane Randevu
            <small className="brand-sub">Dijital Klinik Sistemi</small>
          </span>
        </Link>
        <nav aria-label="Ana menü" className="nav">
          {isDoctor ? (
            <NavLink to="/doktor">Doktor paneli</NavLink>
          ) : isAdmin ? (
            <NavLink to="/yonetim">Yönetim paneli</NavLink>
          ) : (
            <>
              <NavLink to="/randevu-al">Randevu al</NavLink>
              {state.status === 'authenticated' && <NavLink to="/randevularim">Randevularım</NavLink>}
            </>
          )}
        </nav>
        <div className="header-actions">
          <div className="theme-switch" role="group" aria-label="Tema seçimi">
            {THEME_OPTIONS.map((opt) => {
              const active = themeMode === opt.value
              return (
                <button
                  key={opt.value}
                  type="button"
                  className={`theme-btn ${active ? 'is-active' : ''}`}
                  aria-pressed={active}
                  title={opt.title}
                  onClick={() => setThemeMode(opt.value)}
                >
                  <span className="theme-icon" aria-hidden="true">
                    {opt.icon}
                  </span>
                  <span className="theme-label">{opt.label}</span>
                </button>
              )
            })}
          </div>
          <button
            type="button"
            className="btn btn-ghost btn-sm"
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
              <button type="button" className="btn btn-sm" onClick={() => void logout()}>
                Çıkış
              </button>
            </>
          ) : (
            state.status === 'anonymous' && (
              <>
                <Link className="btn btn-ghost btn-sm" to="/giris">
                  Giriş
                </Link>
                <Link className="btn btn-primary btn-sm" to="/kayit">
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
