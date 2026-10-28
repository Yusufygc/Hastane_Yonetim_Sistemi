import { useState, type FormEvent } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { ErrorMessage } from '../components/ErrorMessage'

// Girişten sonra rolün ana ekranı (gidilmek istenen sayfa yoksa).
const HOME_BY_ROLE: Record<string, string> = { DOCTOR: '/doktor', ADMIN: '/yonetim' }

export default function Login() {
  const { login } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const from = (location.state as { from?: string } | null)?.from

  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<unknown>()
  const [busy, setBusy] = useState(false)

  async function submit(event: FormEvent) {
    event.preventDefault()
    setBusy(true)
    setError(undefined)
    try {
      const user = await login(email.trim(), password)
      // Gidilmek istenen sayfa varsa oraya; yoksa role göre ana ekran (doktor/yönetici → panel, hasta → randevularım).
      navigate(from ?? HOME_BY_ROLE[user.role] ?? '/randevularim', { replace: true })
    } catch (e) {
      setError(e)
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="page narrow">
      <h1>Giriş yap</h1>
      <form onSubmit={submit} noValidate>
        <ErrorMessage error={error} />
        <label>
          E-posta
          <input type="email" autoComplete="username" required value={email} onChange={(e) => setEmail(e.target.value)} />
        </label>
        <label>
          Parola
          <input type="password" autoComplete="current-password" required value={password} onChange={(e) => setPassword(e.target.value)} />
        </label>
        <button className="btn btn-primary" type="submit" disabled={busy}>
          {busy ? 'Giriş yapılıyor…' : 'Giriş yap'}
        </button>
      </form>
      <p>
        Hesabınız yok mu? <Link to="/kayit">Kayıt olun</Link>
      </p>
    </section>
  )
}
