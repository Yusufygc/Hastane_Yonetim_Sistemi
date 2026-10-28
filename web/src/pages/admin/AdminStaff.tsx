import { useState, type FormEvent } from 'react'
import { admin } from '../../api/admin'
import { ConfirmAction } from '../../components/ConfirmAction'
import { ErrorMessage } from '../../components/ErrorMessage'
import { Pager } from '../../components/Pager'
import { TextField } from '../../components/TextField'
import { fieldError } from '../../lib/errors'
import { useReload } from '../../lib/reload'
import { useAsync } from '../../lib/useAsync'
import { useAdmin } from './AdminContext'

const PAGE_SIZE = 10
const EMPTY = { firstName: '', lastName: '', email: '', password: '', title: '', departmentId: '' }

function StaffForm({ hospitalId, onDone, onCancel }: { hospitalId: string; onDone: () => void; onCancel: () => void }) {
  const departments = useAsync((signal) => admin.departments(hospitalId, signal).then((r) => r.data.filter((d) => d.isActive)), [hospitalId])
  const [form, setForm] = useState(EMPTY)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<unknown>()
  const set = (key: keyof typeof EMPTY) => (value: string) => setForm((f) => ({ ...f, [key]: value }))
  const err = (key: string) => fieldError(error, key)

  async function submit(event: FormEvent) {
    event.preventDefault()
    setBusy(true)
    setError(undefined)
    try {
      await admin.createStaff({
        hospitalId,
        departmentId: form.departmentId || null,
        email: form.email.trim(),
        password: form.password,
        firstName: form.firstName.trim(),
        lastName: form.lastName.trim(),
        title: form.title.trim(),
      })
      onDone()
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
      <div className="grid-2">
        <TextField label="E-posta" name="email" type="email" value={form.email} onChange={set('email')} error={err('email')} required />
        <TextField label="Başlangıç parolası" name="password" type="password" autoComplete="new-password" value={form.password} onChange={set('password')} error={err('password')} required />
      </div>
      <div className="grid-2">
        <TextField label="Unvan" name="title" value={form.title} onChange={set('title')} error={err('title')} required />
        <label>
          Bölüm (isteğe bağlı)
          <select value={form.departmentId} onChange={(e) => set('departmentId')(e.target.value)}>
            <option value="">Bölümsüz</option>
            {departments.data?.map((d) => (
              <option key={d.id} value={d.id}>
                {d.name}
              </option>
            ))}
          </select>
        </label>
      </div>
      <div className="row">
        <button type="submit" className="btn btn-primary" disabled={busy}>
          {busy ? 'Kaydediliyor…' : 'Personeli oluştur'}
        </button>
        <button type="button" className="btn btn-ghost" onClick={onCancel} disabled={busy}>
          Vazgeç
        </button>
      </div>
    </form>
  )
}

export default function AdminStaff() {
  const { hospitalId } = useAdmin()
  const [page, setPage] = useState(1)
  const [version, reload] = useReload()
  const [creating, setCreating] = useState(false)

  const result = useAsync(hospitalId ? (signal) => admin.staff({ hospitalId, page, pageSize: PAGE_SIZE }, signal) : null, [hospitalId, page, version])
  const list = result.data?.data

  return (
    <>
      <h2>Personel</h2>
      {!creating && hospitalId && (
        <button type="button" className="btn btn-primary" onClick={() => setCreating(true)}>
          Yeni personel
        </button>
      )}
      {creating && hospitalId && (
        <StaffForm
          hospitalId={hospitalId}
          onCancel={() => setCreating(false)}
          onDone={() => {
            setCreating(false)
            reload()
          }}
        />
      )}

      {result.loading && (
        <p className="page-note" role="status">
          Personel yükleniyor…
        </p>
      )}
      <ErrorMessage error={result.error} />
      {list?.length === 0 && <p className="page-note">Bu şubede personel yok.</p>}
      <ul className="appointment-list">
        {list?.map((s) => (
          <li key={s.id} className="appointment">
            <div className="appointment-head">
              <strong>
                {s.firstName} {s.lastName}
              </strong>
              <span className={`badge ${s.isActive ? 'badge-approved' : 'badge-cancelled'}`}>{s.isActive ? 'Aktif' : 'Pasif'}</span>
            </div>
            <p>{s.title}</p>
            <p className="page-note">{s.email}</p>
            <ConfirmAction
              label={s.isActive ? 'Pasifleştir' : 'Aktifleştir'}
              danger={s.isActive}
              onConfirm={async () => {
                await (s.isActive ? admin.deactivateUser(s.id) : admin.activateUser(s.id))
                reload()
              }}
            />
          </li>
        ))}
      </ul>
      <Pager page={page} total={result.data?.total ?? 0} pageSize={PAGE_SIZE} onPage={setPage} noun="personel" />
    </>
  )
}
