import { useState, type FormEvent } from 'react'
import { admin, type AdminDoctor, type Department } from '../../api/admin'
import { ConfirmAction } from '../../components/ConfirmAction'
import { ErrorMessage } from '../../components/ErrorMessage'
import { Pager } from '../../components/Pager'
import { TextField } from '../../components/TextField'
import { fieldError } from '../../lib/errors'
import { useReload } from '../../lib/reload'
import { useAsync } from '../../lib/useAsync'
import { useAdmin } from './AdminContext'

const PAGE_SIZE = 10

interface FormState {
  firstName: string
  lastName: string
  email: string
  password: string
  specialty: string
  diplomaNo: string
  phone: string
  departmentId: string
}

const EMPTY: FormState = { firstName: '', lastName: '', email: '', password: '', specialty: '', diplomaNo: '', phone: '', departmentId: '' }

/** Doktor formu: oluşturmada hesap alanları (e-posta, parola) da vardır; düzenlemede yoktur. Kaydetme çağıran tarafın `onSubmit`'indedir. */
function DoctorForm({
  departments,
  initial,
  withAccount,
  submitLabel,
  onSubmit,
  onCancel,
}: {
  departments: Department[]
  initial: FormState
  withAccount: boolean
  submitLabel: string
  onSubmit: (form: FormState) => Promise<void>
  onCancel: () => void
}) {
  const [form, setForm] = useState<FormState>(initial)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<unknown>()
  const set = (key: keyof FormState) => (value: string) => setForm((f) => ({ ...f, [key]: value }))
  const err = (key: string) => fieldError(error, key)

  async function submit(event: FormEvent) {
    event.preventDefault()
    setBusy(true)
    setError(undefined)
    try {
      await onSubmit(form)
    } catch (e) {
      setError(e)
      setBusy(false)
    }
  }

  return (
    <form onSubmit={submit} noValidate className="panel-form">
      <ErrorMessage error={error} />
      <div className="grid-2">
        <TextField label="Ad" name="firstName" value={form.firstName} onChange={set('firstName')} error={err('firstName')} required />
        <TextField label="Soyad" name="lastName" value={form.lastName} onChange={set('lastName')} error={err('lastName')} required />
      </div>
      {withAccount && (
        <div className="grid-2">
          <TextField label="E-posta" name="email" type="email" value={form.email} onChange={set('email')} error={err('email')} required />
          <TextField label="Başlangıç parolası" name="password" type="password" autoComplete="new-password" value={form.password} onChange={set('password')} error={err('password')} required />
        </div>
      )}
      <div className="grid-2">
        <TextField label="Uzmanlık" name="specialty" value={form.specialty} onChange={set('specialty')} error={err('specialty')} required />
        <TextField label="Diploma no" name="diplomaNo" value={form.diplomaNo} onChange={set('diplomaNo')} error={err('diplomaNo')} required />
      </div>
      <div className="grid-2">
        <TextField label="Telefon (isteğe bağlı)" name="phone" type="tel" value={form.phone} onChange={set('phone')} error={err('phone')} />
        <label>
          Bölüm
          <select value={form.departmentId} onChange={(e) => set('departmentId')(e.target.value)} required aria-invalid={err('departmentId') ? true : undefined}>
            <option value="">Bölüm seçin</option>
            {departments.map((d) => (
              <option key={d.id} value={d.id}>
                {d.name}
              </option>
            ))}
          </select>
          {err('departmentId') && <span className="field-error">{err('departmentId')}</span>}
        </label>
      </div>
      <div className="row">
        <button type="submit" className="btn btn-primary" disabled={busy}>
          {busy ? 'Kaydediliyor…' : submitLabel}
        </button>
        <button type="button" className="btn btn-ghost" onClick={onCancel} disabled={busy}>
          Vazgeç
        </button>
      </div>
    </form>
  )
}

const common = (f: FormState) => ({
  firstName: f.firstName.trim(),
  lastName: f.lastName.trim(),
  specialty: f.specialty.trim(),
  diplomaNo: f.diplomaNo.trim(),
  phone: f.phone.trim() || null,
  departmentId: f.departmentId,
})

export default function AdminDoctors() {
  const { hospitalId } = useAdmin()
  const [page, setPage] = useState(1)
  const [version, reload] = useReload()
  const [creating, setCreating] = useState(false)
  const [editing, setEditing] = useState<AdminDoctor | null>(null)

  const departments = useAsync(hospitalId ? (signal) => admin.departments(hospitalId, signal).then((r) => r.data.filter((d) => d.isActive)) : null, [hospitalId])
  const result = useAsync(hospitalId ? (signal) => admin.doctors({ hospitalId, page, pageSize: PAGE_SIZE }, signal) : null, [hospitalId, page, version])
  const list = result.data?.data

  return (
    <>
      <h2>Doktorlar</h2>
      <p className="page-note">Hesabı yönetici açar ve başlangıç parolasını verir; doktor girişten sonra parolasını değiştirir. Pasifleştirilen doktor giriş yapamaz ve yeni randevu alamaz.</p>
      {!creating && !editing && (
        <button type="button" className="btn btn-primary" onClick={() => setCreating(true)}>
          Yeni doktor
        </button>
      )}
      {creating && (
        <DoctorForm
          departments={departments.data ?? []}
          initial={EMPTY}
          withAccount
          submitLabel="Doktoru oluştur"
          onCancel={() => setCreating(false)}
          onSubmit={async (f) => {
            await admin.createDoctor({ ...common(f), email: f.email.trim(), password: f.password })
            setCreating(false)
            reload()
          }}
        />
      )}
      <ErrorMessage error={departments.error} />

      {result.loading && (
        <p className="page-note" role="status">
          Doktorlar yükleniyor…
        </p>
      )}
      <ErrorMessage error={result.error} />
      {list?.length === 0 && <p className="page-note">Bu şubede doktor yok.</p>}
      <ul className="appointment-list">
        {list?.map((d) => (
          <li key={d.id} className="appointment">
            <div className="appointment-head">
              <strong>
                {d.firstName} {d.lastName}
              </strong>
              <span className={`badge ${d.isActive ? 'badge-approved' : 'badge-cancelled'}`}>{d.isActive ? 'Aktif' : 'Pasif'}</span>
            </div>
            <p>
              {d.specialty} · {d.departmentName}
            </p>
            <p className="page-note">
              {d.email} · Diploma {d.diplomaNo}
              {d.phone && ` · ${d.phone}`}
            </p>
            {editing?.id === d.id ? (
              <DoctorForm
                departments={departments.data ?? []}
                initial={{ ...EMPTY, firstName: d.firstName, lastName: d.lastName, specialty: d.specialty, diplomaNo: d.diplomaNo, phone: d.phone ?? '', departmentId: d.departmentId }}
                withAccount={false}
                submitLabel="Kaydet"
                onCancel={() => setEditing(null)}
                onSubmit={async (f) => {
                  await admin.updateDoctor(d.id, common(f))
                  setEditing(null)
                  reload()
                }}
              />
            ) : (
              <div className="row">
                <button type="button" className="btn btn-ghost" onClick={() => setEditing(d)} disabled={creating}>
                  Düzenle
                </button>
                <ConfirmAction
                  label={d.isActive ? 'Pasifleştir' : 'Aktifleştir'}
                  danger={d.isActive}
                  onConfirm={async () => {
                    await (d.isActive ? admin.deactivateUser(d.id) : admin.activateUser(d.id))
                    reload()
                  }}
                />
              </div>
            )}
          </li>
        ))}
      </ul>
      <Pager page={page} total={result.data?.total ?? 0} pageSize={PAGE_SIZE} onPage={setPage} noun="doktor" />
    </>
  )
}
