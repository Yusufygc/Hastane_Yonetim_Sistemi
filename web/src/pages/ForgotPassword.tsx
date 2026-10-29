import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { auth } from '../api/endpoints'
import { ErrorMessage } from '../components/ErrorMessage'
import { TextField } from '../components/TextField'
import { fieldError } from '../lib/errors'

export default function ForgotPassword() {
  const [email, setEmail] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<unknown>()
  const [sentTo, setSentTo] = useState<string | null>(null)

  async function submit(event: FormEvent) {
    event.preventDefault()
    setBusy(true)
    setError(undefined)
    try {
      await auth.forgotPassword(email.trim())
      setSentTo(email.trim())
    } catch (e) {
      setError(e)
    } finally {
      setBusy(false)
    }
  }

  // Güvenlik: yanıt hesap var olsun olmasın aynıdır; metin de "kayıtlıysa" diyerek hesap varlığını sızdırmaz.
  if (sentTo) {
    return (
      <section className="page narrow">
        <h1>E-postanızı kontrol edin</h1>
        <div className="notice" role="status">
          <strong>{sentTo}</strong> adresi sistemde kayıtlıysa parola sıfırlama bağlantısı gönderdik.
        </div>
        <p>Bağlantı kısa süre geçerlidir ve yalnızca bir kez kullanılabilir. E-posta birkaç dakika içinde gelmezse spam klasörüne bakın veya tekrar deneyin.</p>
        <div className="row">
          <button type="button" className="btn" onClick={() => setSentTo(null)}>
            Başka bir adres dene
          </button>
          <Link className="btn btn-ghost" to="/giris">
            Girişe dön
          </Link>
        </div>
      </section>
    )
  }

  const emailError = fieldError(error, 'email')

  return (
    <section className="page narrow">
      <h1>Şifremi unuttum</h1>
      <p className="lead">Hesabınızın e-posta adresini yazın; parolanızı sıfırlamanız için bir bağlantı gönderelim.</p>
      <form onSubmit={submit} noValidate>
        <ErrorMessage error={emailError ? undefined : error} />
        <TextField label="E-posta" name="email" type="email" autoComplete="username" required value={email} onChange={setEmail} error={emailError} />
        <button className="btn btn-primary" type="submit" disabled={busy || email.trim() === ''}>
          {busy ? 'Gönderiliyor…' : 'Sıfırlama bağlantısı gönder'}
        </button>
      </form>
      <p>
        <Link to="/giris">Girişe dön</Link>
      </p>
    </section>
  )
}
