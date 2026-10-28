import { useState, type FormEvent } from 'react'
import { admin, type AdminHospital, type WorkingHoursItem } from '../../api/admin'
import { ConfirmAction } from '../../components/ConfirmAction'
import { ErrorMessage } from '../../components/ErrorMessage'
import { TextField } from '../../components/TextField'
import { fieldError } from '../../lib/errors'
import { DAY_NAMES, formatTime } from '../../lib/format'
import { useAsync } from '../../lib/useAsync'
import { useAdmin } from './AdminContext'

const DAY_ORDER = [1, 2, 3, 4, 5, 6, 0]

function HospitalForm({
  initial,
  submitLabel,
  onSubmit,
  onCancel,
}: {
  initial: { name: string; address: string; phone: string; timeZoneId: string }
  submitLabel: string
  onSubmit: (body: { name: string; address: string | null; phone: string | null; timeZoneId: string }) => Promise<void>
  onCancel: () => void
}) {
  const [form, setForm] = useState(initial)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<unknown>()
  const set = (key: keyof typeof initial) => (value: string) => setForm((f) => ({ ...f, [key]: value }))
  const err = (key: string) => fieldError(error, key)

  async function submit(event: FormEvent) {
    event.preventDefault()
    setBusy(true)
    setError(undefined)
    try {
      await onSubmit({ name: form.name.trim(), address: form.address.trim() || null, phone: form.phone.trim() || null, timeZoneId: form.timeZoneId.trim() })
    } catch (e) {
      setError(e)
      setBusy(false)
    }
  }

  return (
    <form onSubmit={submit} noValidate className="panel-form">
      <ErrorMessage error={error} />
      <TextField label="Şube adı" name="name" value={form.name} onChange={set('name')} error={err('name')} required />
      <TextField label="Adres (isteğe bağlı)" name="address" value={form.address} onChange={set('address')} error={err('address')} />
      <div className="grid-2">
        <TextField label="Telefon (isteğe bağlı)" name="phone" type="tel" value={form.phone} onChange={set('phone')} error={err('phone')} />
        <TextField label="Saat dilimi (IANA)" name="timeZoneId" placeholder="Europe/Istanbul" value={form.timeZoneId} onChange={set('timeZoneId')} error={err('timeZoneId')} required />
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

interface DayRow {
  open: boolean
  openTime: string
  closeTime: string
}

/** Haftalık çalışma saatleri: kayıt olmayan gün kapalıdır; gün başına tek aralık. Kaydet tüm listeyi değiştirir (PUT). */
function WorkingHoursEditor({ hospital, onDone }: { hospital: AdminHospital; onDone: () => void }) {
  const detail = useAsync((signal) => admin.hospital(hospital.id, signal).then((r) => r.data), [hospital.id])

  if (detail.loading) {
    return (
      <p className="page-note" role="status">
        Çalışma saatleri yükleniyor…
      </p>
    )
  }
  if (detail.error) return <ErrorMessage error={detail.error} />
  if (!detail.data) return null

  const initial: DayRow[] = Array.from({ length: 7 }, (_, day) => {
    const found = detail.data!.workingHours.find((h) => h.day === day)
    return found ? { open: true, openTime: formatTime(found.openTime), closeTime: formatTime(found.closeTime) } : { open: false, openTime: '08:00', closeTime: '17:00' }
  })
  return <HoursForm hospitalId={hospital.id} initial={initial} onDone={onDone} />
}

function HoursForm({ hospitalId, initial, onDone }: { hospitalId: string; initial: DayRow[]; onDone: () => void }) {
  const [rows, setRows] = useState(initial)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<unknown>()
  const [saved, setSaved] = useState(false)

  const update = (day: number, patch: Partial<DayRow>) => {
    setSaved(false)
    setRows((r) => r.map((row, i) => (i === day ? { ...row, ...patch } : row)))
  }

  async function submit(event: FormEvent) {
    event.preventDefault()
    setBusy(true)
    setError(undefined)
    setSaved(false)
    try {
      const hours: WorkingHoursItem[] = rows.flatMap((row, day) => (row.open ? [{ day, openTime: row.openTime, closeTime: row.closeTime }] : []))
      await admin.setWorkingHours(hospitalId, hours)
      setSaved(true)
      onDone()
    } catch (e) {
      setError(e)
    } finally {
      setBusy(false)
    }
  }

  return (
    <form onSubmit={submit} noValidate className="panel-form">
      <p className="page-note">Hasta ve doktor uygunluğu bu saatlerin içinde olmalıdır. İşaretsiz gün kapalıdır.</p>
      <ErrorMessage error={error} />
      <table className="hours">
        <caption className="sr-only">Haftalık çalışma saatleri</caption>
        <thead>
          <tr>
            <th scope="col">Gün</th>
            <th scope="col">Açık</th>
            <th scope="col">Açılış</th>
            <th scope="col">Kapanış</th>
          </tr>
        </thead>
        <tbody>
          {DAY_ORDER.map((day) => (
            <tr key={day}>
              <th scope="row">{DAY_NAMES[day]}</th>
              <td>
                <input type="checkbox" aria-label={`${DAY_NAMES[day]} açık`} checked={rows[day].open} onChange={(e) => update(day, { open: e.target.checked })} />
              </td>
              <td>
                <input type="time" aria-label={`${DAY_NAMES[day]} açılış`} disabled={!rows[day].open} value={rows[day].openTime} onChange={(e) => update(day, { openTime: e.target.value })} />
              </td>
              <td>
                <input type="time" aria-label={`${DAY_NAMES[day]} kapanış`} disabled={!rows[day].open} value={rows[day].closeTime} onChange={(e) => update(day, { closeTime: e.target.value })} />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      <div className="row">
        <button type="submit" className="btn btn-primary" disabled={busy}>
          {busy ? 'Kaydediliyor…' : 'Çalışma saatlerini kaydet'}
        </button>
        {saved && (
          <span className="notice" role="status">
            Kaydedildi.
          </span>
        )}
      </div>
    </form>
  )
}

type Panel = { id: string; kind: 'edit' | 'hours' } | 'create' | null

function OrganizationForm() {
  const { organizationName, reloadHospitals } = useAdmin()
  const [name, setName] = useState(organizationName ?? '')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<unknown>()
  const [saved, setSaved] = useState(false)

  async function submit(event: FormEvent) {
    event.preventDefault()
    setBusy(true)
    setError(undefined)
    setSaved(false)
    try {
      await admin.updateOrganization(name.trim())
      setSaved(true)
      reloadHospitals()
    } catch (e) {
      setError(e)
    } finally {
      setBusy(false)
    }
  }

  return (
    <form onSubmit={submit} noValidate className="inline-form">
      <TextField label="Kurum adı" name="organization" value={name} onChange={setName} error={fieldError(error, 'name')} required />
      <button type="submit" className="btn" disabled={busy || name.trim() === ''}>
        {busy ? 'Kaydediliyor…' : 'Kurum adını kaydet'}
      </button>
      {saved && (
        <span className="notice" role="status">
          Kaydedildi.
        </span>
      )}
      <ErrorMessage error={fieldError(error, 'name') ? undefined : error} />
    </form>
  )
}

export default function AdminHospitals() {
  const { hospitals, isSuper, reloadHospitals, organizationName } = useAdmin()
  const [panel, setPanel] = useState<Panel>(null)

  const done = () => {
    setPanel(null)
    reloadHospitals()
  }

  return (
    <>
      <h2>Şubeler</h2>
      {isSuper && organizationName && <OrganizationForm key={organizationName} />}
      {isSuper && panel !== 'create' && (
        <button type="button" className="btn btn-primary" onClick={() => setPanel('create')}>
          Yeni şube
        </button>
      )}
      {!isSuper && <p className="page-note">Şube ekleme ve kurum adı yalnız süper yöneticiye açıktır; kendi şubenizin bilgilerini ve saatlerini düzenleyebilirsiniz.</p>}
      {panel === 'create' && (
        <HospitalForm
          initial={{ name: '', address: '', phone: '', timeZoneId: 'Europe/Istanbul' }}
          submitLabel="Şubeyi oluştur"
          onCancel={() => setPanel(null)}
          onSubmit={async (body) => {
            await admin.createHospital(body)
            done()
          }}
        />
      )}

      <ul className="appointment-list">
        {hospitals.map((h) => {
          const active = typeof panel === 'object' && panel?.id === h.id ? panel.kind : null
          return (
            <li key={h.id} className="appointment">
              <div className="appointment-head">
                <strong>{h.name}</strong>
                <span className={`badge ${h.isActive ? 'badge-approved' : 'badge-cancelled'}`}>{h.isActive ? 'Aktif' : 'Pasif'}</span>
              </div>
              <p className="page-note">
                {[h.address, h.phone, h.timeZoneId].filter(Boolean).join(' · ')}
              </p>
              {active === 'edit' && (
                <HospitalForm
                  initial={{ name: h.name, address: h.address ?? '', phone: h.phone ?? '', timeZoneId: h.timeZoneId }}
                  submitLabel="Kaydet"
                  onCancel={() => setPanel(null)}
                  onSubmit={async (body) => {
                    await admin.updateHospital(h.id, body)
                    done()
                  }}
                />
              )}
              {active === 'hours' && <WorkingHoursEditor hospital={h} onDone={reloadHospitals} />}
              {h.isActive && (
                <div className="row">
                  <button type="button" className="btn btn-ghost" onClick={() => setPanel(active === 'edit' ? null : { id: h.id, kind: 'edit' })}>
                    Bilgileri düzenle
                  </button>
                  <button type="button" className="btn btn-ghost" onClick={() => setPanel(active === 'hours' ? null : { id: h.id, kind: 'hours' })}>
                    Çalışma saatleri
                  </button>
                  <ConfirmAction
                    label="Şubeyi pasifleştir"
                    danger
                    onConfirm={async () => {
                      await admin.deactivateHospital(h.id)
                      done()
                    }}
                  />
                </div>
              )}
            </li>
          )
        })}
      </ul>
    </>
  )
}
