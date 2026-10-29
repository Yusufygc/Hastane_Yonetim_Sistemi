import { useEffect, useState, type FormEvent } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { auth } from '../api/endpoints'
import { ApiError } from '../api/http'
import { ErrorMessage } from '../components/ErrorMessage'
import { TextField } from '../components/TextField'
import { fieldError } from '../lib/errors'

const INVALID_TOKEN_CODE = 'auth.invalid_reset_token'

export default function ResetPassword() {
  const location = useLocation()
  const navigate = useNavigate()

  // Belirteç e-postadaki bağlantıdan ilk açılışta okunur, sonra adres çubuğundan ve geçmişten silinir:
  // ekran görüntüsü, Referer veya tarayıcı geçmişi üzerinden sızmasın.
  const [token] = useState(() => new URLSearchParams(location.search).get('token'))
  useEffect(() => {
    if (location.search) navigate(location.pathname, { replace: true })
  }, [location.search, location.pathname, navigate])

  const [password, setPassword] = useState('')
  const [confirm, setConfirm] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<unknown>()
  const [done, setDone] = useState(false)

  const mismatch = confirm !== '' && password !== confirm

  async function submit(event: FormEvent) {
    event.preventDefault()
    if (!token || password !== confirm) return
    setBusy(true)
    setError(undefined)
    try {
      await auth.resetPassword(token, password)
      setDone(true)
    } catch (e) {
      setError(e)
    } finally {
      setBusy(false)
    }
  }

  if (done) {
    return (
      <section className="page narrow">
        <h1>Parolanız değiştirildi</h1>
        <div className="notice" role="status">
          Yeni parolanız kaydedildi. Güvenliğiniz için açık tüm oturumlarınız kapatıldı.
        </div>
        <Link className="btn btn-primary" to="/giris">
          Giriş yap
        </Link>
      </section>
    )
  }

  const invalidToken = !token || (error instanceof ApiError && error.code === INVALID_TOKEN_CODE)
  if (invalidToken) {
    return (
      <section className="page narrow">
        <h1>Bağlantı geçersiz</h1>
        <div className="alert" role="alert">
          <p>Parola sıfırlama bağlantısı geçersiz, süresi dolmuş veya daha önce kullanılmış.</p>
        </div>
        <p>
          <Link className="btn btn-primary" to="/sifremi-unuttum">
            Yeni bağlantı iste
          </Link>
        </p>
      </section>
    )
  }

  const passwordError = fieldError(error, 'newPassword')

  return (
    <section className="page narrow">
      <h1>Yeni parola belirle</h1>
      <p className="lead">En az 8 karakter; en az bir harf ve bir rakam içermelidir.</p>
      <form onSubmit={submit} noValidate>
        <ErrorMessage error={passwordError ? undefined : error} />
        <TextField
          label="Yeni parola"
          name="newPassword"
          type="password"
          autoComplete="new-password"
          required
          value={password}
          onChange={setPassword}
          error={passwordError}
        />
        <TextField
          label="Yeni parola (tekrar)"
          name="confirm"
          type="password"
          autoComplete="new-password"
          required
          value={confirm}
          onChange={setConfirm}
          error={mismatch ? 'Parolalar eşleşmiyor.' : undefined}
        />
        <button className="btn btn-primary" type="submit" disabled={busy || password === '' || mismatch || confirm === ''}>
          {busy ? 'Kaydediliyor…' : 'Parolayı kaydet'}
        </button>
      </form>
    </section>
  )
}
