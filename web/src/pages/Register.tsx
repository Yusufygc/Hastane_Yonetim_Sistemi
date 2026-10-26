import { useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { ErrorMessage } from '../components/ErrorMessage'
import { fieldError } from '../lib/errors'
import { toDateInput } from '../lib/format'

const TODAY = toDateInput(new Date())

export default function Register() {
  const { register } = useAuth()
  const navigate = useNavigate()
  const [form, setForm] = useState({ firstName: '', lastName: '', email: '', password: '', identityNumber: '', birthDate: '', phone: '' })
  const [error, setError] = useState<unknown>()
  const [busy, setBusy] = useState(false)

  const set = (key: keyof typeof form) => (e: { target: { value: string } }) => setForm((f) => ({ ...f, [key]: e.target.value }))

  async function submit(event: FormEvent) {
    event.preventDefault()
    setBusy(true)
    setError(undefined)
    try {
      await register({ ...form, email: form.email.trim(), phone: form.phone.trim() || null })
      navigate('/randevu-al', { replace: true })
    } catch (e) {
      setError(e)
    } finally {
      setBusy(false)
    }
  }

  const field = (key: keyof typeof form, label: string, props: React.InputHTMLAttributes<HTMLInputElement> = {}) => {
    const message = fieldError(error, key)
    return (
      <label>
        {label}
        <input value={form[key]} onChange={set(key)} aria-invalid={message ? true : undefined} aria-describedby={message ? `${key}-hata` : undefined} {...props} />
        {message && (
          <span id={`${key}-hata`} className="field-error">
            {message}
          </span>
        )}
      </label>
    )
  }

  return (
    <section className="page narrow">
      <h1>Kayıt ol</h1>
      <form onSubmit={submit} noValidate>
        <ErrorMessage error={error} />
        <div className="grid-2">
          {field('firstName', 'Ad', { autoComplete: 'given-name', required: true })}
          {field('lastName', 'Soyad', { autoComplete: 'family-name', required: true })}
        </div>
        {field('email', 'E-posta', { type: 'email', autoComplete: 'email', required: true })}
        {field('password', 'Parola', { type: 'password', autoComplete: 'new-password', required: true })}
        {field('identityNumber', 'T.C. kimlik no', { inputMode: 'numeric', maxLength: 11, autoComplete: 'off', required: true })}
        {field('birthDate', 'Doğum tarihi', { type: 'date', max: TODAY, autoComplete: 'bday', required: true })}
        {field('phone', 'Telefon (isteğe bağlı)', { type: 'tel', autoComplete: 'tel' })}
        <button className="btn btn-primary" type="submit" disabled={busy}>
          {busy ? 'Kaydediliyor…' : 'Kayıt ol'}
        </button>
      </form>
      <p>
        Zaten hesabınız var mı? <Link to="/giris">Giriş yapın</Link>
      </p>
    </section>
  )
}
