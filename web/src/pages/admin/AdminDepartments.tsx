import { useState, type FormEvent } from 'react'
import { admin, type Department } from '../../api/admin'
import { ConfirmAction } from '../../components/ConfirmAction'
import { ErrorMessage } from '../../components/ErrorMessage'
import { TextField } from '../../components/TextField'
import { fieldError } from '../../lib/errors'
import { useReload } from '../../lib/reload'
import { useAsync } from '../../lib/useAsync'
import { useAdmin } from './AdminContext'

function RenameForm({ department, onDone, onCancel }: { department: Department; onDone: () => void; onCancel: () => void }) {
  const [name, setName] = useState(department.name)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<unknown>()

  async function submit(event: FormEvent) {
    event.preventDefault()
    setBusy(true)
    setError(undefined)
    try {
      await admin.renameDepartment(department.id, name.trim())
      onDone()
    } catch (e) {
      setError(e)
      setBusy(false)
    }
  }

  return (
    <form onSubmit={submit} noValidate className="inline-form">
      <TextField label="Bölüm adı" name="name" value={name} onChange={setName} error={fieldError(error, 'name')} required />
      <button type="submit" className="btn btn-primary" disabled={busy || name.trim() === ''}>
        {busy ? 'Kaydediliyor…' : 'Kaydet'}
      </button>
      <button type="button" className="btn btn-ghost" onClick={onCancel} disabled={busy}>
        Vazgeç
      </button>
      <ErrorMessage error={fieldError(error, 'name') ? undefined : error} />
    </form>
  )
}

export default function AdminDepartments() {
  const { hospitalId, hospital } = useAdmin()
  const [version, reload] = useReload()
  const [name, setName] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<unknown>()
  const [editing, setEditing] = useState<string | null>(null)

  const result = useAsync(hospitalId ? (signal) => admin.departments(hospitalId, signal).then((r) => r.data) : null, [hospitalId, version])

  async function add(event: FormEvent) {
    event.preventDefault()
    if (!hospitalId) return
    setBusy(true)
    setError(undefined)
    try {
      await admin.createDepartment(hospitalId, name.trim())
      setName('')
      reload()
    } catch (e) {
      setError(e)
    } finally {
      setBusy(false)
    }
  }

  return (
    <>
      <h2>Bölümler{hospital ? ` — ${hospital.name}` : ''}</h2>
      <form onSubmit={add} noValidate className="inline-form">
        <TextField label="Yeni bölüm adı" name="newName" value={name} onChange={setName} error={fieldError(error, 'name')} required />
        <button type="submit" className="btn btn-primary" disabled={busy || name.trim() === '' || !hospitalId}>
          {busy ? 'Ekleniyor…' : 'Bölüm ekle'}
        </button>
      </form>
      <ErrorMessage error={fieldError(error, 'name') ? undefined : error} />

      {result.loading && (
        <p className="page-note" role="status">
          Bölümler yükleniyor…
        </p>
      )}
      <ErrorMessage error={result.error} />
      {result.data?.length === 0 && <p className="page-note">Bu şubede bölüm yok.</p>}
      <ul className="appointment-list">
        {result.data?.map((d) => (
          <li key={d.id} className="appointment">
            <div className="appointment-head">
              <strong>{d.name}</strong>
              <span className={`badge ${d.isActive ? 'badge-approved' : 'badge-cancelled'}`}>{d.isActive ? 'Aktif' : 'Pasif'}</span>
            </div>
            {editing === d.id ? (
              <RenameForm
                department={d}
                onCancel={() => setEditing(null)}
                onDone={() => {
                  setEditing(null)
                  reload()
                }}
              />
            ) : (
              d.isActive && (
                <div className="row">
                  <button type="button" className="btn btn-ghost" onClick={() => setEditing(d.id)}>
                    Yeniden adlandır
                  </button>
                  <ConfirmAction
                    label="Pasifleştir"
                    danger
                    onConfirm={async () => {
                      await admin.deactivateDepartment(d.id)
                      reload()
                    }}
                  />
                </div>
              )
            )}
          </li>
        ))}
      </ul>
      <p className="page-note">Aktif hizmeti olan bölüm pasifleştirilemez; önce o bölümün hizmetlerini pasifleştirin.</p>
    </>
  )
}
