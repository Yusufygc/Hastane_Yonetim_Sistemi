import { useState, type FormEvent } from 'react'
import { doctor, type DoctorProfile as Profile } from '../../api/endpoints'
import { ErrorMessage } from '../../components/ErrorMessage'
import { fieldError } from '../../lib/errors'
import { useAsync } from '../../lib/useAsync'

/** Profil yüklendikten sonra çizilir; form başlangıç değerleri doğrudan profilden gelir (effect ile senkron gerekmez). */
function ProfileForm({ data }: { data: Profile }) {
  const [specialty, setSpecialty] = useState(data.specialty)
  const [phone, setPhone] = useState(data.phone ?? '')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<unknown>()
  const [saved, setSaved] = useState(false)

  async function save(event: FormEvent) {
    event.preventDefault()
    setBusy(true)
    setError(undefined)
    setSaved(false)
    try {
      await doctor.updateProfile(specialty.trim(), phone.trim() || null)
      setSaved(true)
    } catch (e) {
      setError(e)
    } finally {
      setBusy(false)
    }
  }

  const specialtyError = fieldError(error, 'specialty')
  const phoneError = fieldError(error, 'phone')

  return (
    <form onSubmit={save} noValidate className="narrow-form">
      <ErrorMessage error={error} />
      <label>
        Uzmanlık
        <input value={specialty} onChange={(e) => setSpecialty(e.target.value)} required aria-invalid={specialtyError ? true : undefined} />
        {specialtyError && <span className="field-error">{specialtyError}</span>}
      </label>
      <label>
        Telefon
        <input type="tel" value={phone} onChange={(e) => setPhone(e.target.value)} aria-invalid={phoneError ? true : undefined} />
        {phoneError && <span className="field-error">{phoneError}</span>}
      </label>
      <button type="submit" className="btn btn-primary" disabled={busy}>
        {busy ? 'Kaydediliyor…' : 'Kaydet'}
      </button>
      {saved && (
        <div className="notice" role="status">
          Profil güncellendi.
        </div>
      )}
    </form>
  )
}

export default function DoctorProfile() {
  const profile = useAsync((signal) => doctor.profile(signal).then((r) => r.data), [])
  const data = profile.data

  return (
    <>
      <h2>Profil</h2>
      {profile.loading && (
        <p className="page-note" role="status">
          Profil yükleniyor…
        </p>
      )}
      <ErrorMessage error={profile.error} />
      {data && (
        <>
          <dl className="summary">
            <dt>Ad soyad</dt>
            <dd>
              {data.firstName} {data.lastName}
            </dd>
            <dt>E-posta</dt>
            <dd>{data.email}</dd>
            <dt>Bölüm</dt>
            <dd>{data.departmentName}</dd>
            <dt>Diploma no</dt>
            <dd>{data.diplomaNo}</dd>
          </dl>
          <p className="page-note">Ad, diploma ve bölüm bilgisini yalnız yönetici değiştirebilir.</p>
          <ProfileForm data={data} />
        </>
      )}
    </>
  )
}
