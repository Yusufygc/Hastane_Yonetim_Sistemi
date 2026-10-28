import { useState, type FormEvent } from 'react'
import { admin, type AdminService, type Department } from '../../api/admin'
import { ConfirmAction } from '../../components/ConfirmAction'
import { ErrorMessage } from '../../components/ErrorMessage'
import { TextField } from '../../components/TextField'
import { fieldError } from '../../lib/errors'
import { formatMoney } from '../../lib/format'
import { useReload } from '../../lib/reload'
import { useAsync } from '../../lib/useAsync'
import { useAdmin } from './AdminContext'

interface FormState {
  name: string
  departmentId: string
  durationMinutes: string
  price: string
  currency: string
}

const EMPTY: FormState = { name: '', departmentId: '', durationMinutes: '15', price: '0', currency: 'TRY' }

function ServiceForm({
  departments,
  initial,
  submitLabel,
  onSubmit,
  onCancel,
}: {
  departments: Department[]
  initial: FormState
  submitLabel: string
  onSubmit: (body: { name: string; departmentId: string | null; durationMinutes: number; price: number; currency: string }) => Promise<void>
  onCancel: () => void
}) {
  const [form, setForm] = useState(initial)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<unknown>()
  const set = (key: keyof FormState) => (value: string) => setForm((f) => ({ ...f, [key]: value }))
  const err = (key: string) => fieldError(error, key)

  async function submit(event: FormEvent) {
    event.preventDefault()
    setBusy(true)
    setError(undefined)
    try {
      await onSubmit({
        name: form.name.trim(),
        departmentId: form.departmentId || null,
        durationMinutes: Number(form.durationMinutes),
        price: Number(form.price.replace(',', '.')),
        currency: form.currency.trim().toUpperCase(),
      })
    } catch (e) {
      setError(e)
      setBusy(false)
    }
  }

  return (
    <form onSubmit={submit} noValidate className="panel-form">
      <ErrorMessage error={error} />
      <TextField label="Hizmet adı" name="name" value={form.name} onChange={set('name')} error={err('name')} required />
      <div className="grid-2">
        <label>
          Bölüm (isteğe bağlı)
          <select value={form.departmentId} onChange={(e) => set('departmentId')(e.target.value)}>
            <option value="">Bölümsüz</option>
            {departments.map((d) => (
              <option key={d.id} value={d.id}>
                {d.name}
              </option>
            ))}
          </select>
        </label>
        <TextField label="Süre (dakika)" name="durationMinutes" type="number" min={1} max={480} value={form.durationMinutes} onChange={set('durationMinutes')} error={err('durationMinutes')} required />
      </div>
      <div className="grid-2">
        <TextField label="Ücret" name="price" inputMode="decimal" value={form.price} onChange={set('price')} error={err('price')} required />
        <TextField label="Para birimi" name="currency" maxLength={3} value={form.currency} onChange={set('currency')} error={err('currency')} required />
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

const toForm = (s: AdminService): FormState => ({ name: s.name, departmentId: s.departmentId ?? '', durationMinutes: String(s.durationMinutes), price: String(s.price), currency: s.currency })

export default function AdminServices() {
  const { hospitalId, hospital } = useAdmin()
  const [version, reload] = useReload()
  const [creating, setCreating] = useState(false)
  const [editing, setEditing] = useState<string | null>(null)

  const departments = useAsync(hospitalId ? (signal) => admin.departments(hospitalId, signal).then((r) => r.data) : null, [hospitalId])
  const result = useAsync(hospitalId ? (signal) => admin.services(hospitalId, signal).then((r) => r.data) : null, [hospitalId, version])
  const activeDepartments = (departments.data ?? []).filter((d) => d.isActive)
  const departmentName = (id: string | null) => departments.data?.find((d) => d.id === id)?.name

  return (
    <>
      <h2>Hizmetler{hospital ? ` — ${hospital.name}` : ''}</h2>
      <p className="page-note">Hasta hizmetin süresini ve ücretini randevuda görür; randevu anında bu değerlerin kopyası saklanır, sonradan değişiklik geçmiş randevuları etkilemez.</p>
      {!creating && hospitalId && (
        <button type="button" className="btn btn-primary" onClick={() => setCreating(true)} disabled={editing !== null}>
          Yeni hizmet
        </button>
      )}
      {creating && hospitalId && (
        <ServiceForm
          departments={activeDepartments}
          initial={EMPTY}
          submitLabel="Hizmeti oluştur"
          onCancel={() => setCreating(false)}
          onSubmit={async (body) => {
            await admin.createService({ ...body, hospitalId })
            setCreating(false)
            reload()
          }}
        />
      )}

      {result.loading && (
        <p className="page-note" role="status">
          Hizmetler yükleniyor…
        </p>
      )}
      <ErrorMessage error={result.error} />
      {result.data?.length === 0 && <p className="page-note">Bu şubede hizmet yok.</p>}
      <ul className="appointment-list">
        {result.data?.map((s) => (
          <li key={s.id} className="appointment">
            <div className="appointment-head">
              <strong>{s.name}</strong>
              <span className={`badge ${s.isActive ? 'badge-approved' : 'badge-cancelled'}`}>{s.isActive ? 'Aktif' : 'Pasif'}</span>
            </div>
            <p>
              {s.durationMinutes} dk · {formatMoney(s.price, s.currency)}
              {departmentName(s.departmentId) && ` · ${departmentName(s.departmentId)}`}
            </p>
            {editing === s.id ? (
              <ServiceForm
                departments={activeDepartments}
                initial={toForm(s)}
                submitLabel="Kaydet"
                onCancel={() => setEditing(null)}
                onSubmit={async (body) => {
                  await admin.updateService(s.id, body)
                  setEditing(null)
                  reload()
                }}
              />
            ) : (
              s.isActive && (
                <div className="row">
                  <button type="button" className="btn btn-ghost" onClick={() => setEditing(s.id)} disabled={creating}>
                    Düzenle
                  </button>
                  <ConfirmAction
                    label="Pasifleştir"
                    danger
                    onConfirm={async () => {
                      await admin.deactivateService(s.id)
                      reload()
                    }}
                  />
                </div>
              )
            )}
          </li>
        ))}
      </ul>
    </>
  )
}
