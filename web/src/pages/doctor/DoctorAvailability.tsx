import { useState, type FormEvent } from 'react'
import { doctor, type Availability, type TimeOff } from '../../api/endpoints'
import { ErrorMessage } from '../../components/ErrorMessage'
import { DAY_NAMES, formatDateShort, formatDateTime, formatTime, localInputToIso, toDateInput } from '../../lib/format'
import { useReload } from '../../lib/reload'
import { useAsync } from '../../lib/useAsync'

/** Pazartesi'den başlayan görüntüleme sırası (API günü: 0 = Pazar). */
const DAY_ORDER = [1, 2, 3, 4, 5, 6, 0]

function RemoveButton({ label, onRemove }: { label: string; onRemove: () => Promise<unknown> }) {
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<unknown>()

  return (
    <>
      <ErrorMessage error={error} />
      <button
        type="button"
        className="btn btn-ghost"
        disabled={busy}
        onClick={async () => {
          setBusy(true)
          setError(undefined)
          try {
            await onRemove()
          } catch (e) {
            setError(e)
            setBusy(false)
          }
        }}
      >
        {busy ? 'Siliniyor…' : label}
      </button>
    </>
  )
}

function AvailabilitySection() {
  const [version, reload] = useReload()
  const list = useAsync((signal) => doctor.availability(signal).then((r) => r.data), [version])
  const [form, setForm] = useState(() => ({ day: '1', startTime: '09:00', endTime: '12:00', validFrom: toDateInput(new Date()), validTo: '' }))
  const [error, setError] = useState<unknown>()
  const [busy, setBusy] = useState(false)

  async function add(event: FormEvent) {
    event.preventDefault()
    setBusy(true)
    setError(undefined)
    try {
      await doctor.addAvailability({ day: Number(form.day), startTime: form.startTime, endTime: form.endTime, validFrom: form.validFrom, validTo: form.validTo || null })
      reload()
    } catch (e) {
      setError(e)
    } finally {
      setBusy(false)
    }
  }

  const sorted: Availability[] = [...(list.data ?? [])].sort(
    (a, b) => DAY_ORDER.indexOf(a.day) - DAY_ORDER.indexOf(b.day) || a.startTime.localeCompare(b.startTime),
  )
  const set = (key: keyof typeof form) => (e: { target: { value: string } }) => setForm((f) => ({ ...f, [key]: e.target.value }))

  return (
    <section aria-labelledby="uygunluk-baslik">
      <h2 id="uygunluk-baslik">Haftalık uygunluk</h2>
      <p className="page-note">Hastalar yalnız bu pencerelerde (şubenin çalışma saatleri içinde) randevu alabilir. Pencereler birbirine çakışamaz.</p>

      <form onSubmit={add} className="inline-form" noValidate>
        <label>
          Gün
          <select value={form.day} onChange={set('day')}>
            {DAY_ORDER.map((d) => (
              <option key={d} value={d}>
                {DAY_NAMES[d]}
              </option>
            ))}
          </select>
        </label>
        <label>
          Başlangıç
          <input type="time" required value={form.startTime} onChange={set('startTime')} />
        </label>
        <label>
          Bitiş
          <input type="time" required value={form.endTime} onChange={set('endTime')} />
        </label>
        <label>
          Geçerli (başlangıç)
          <input type="date" required value={form.validFrom} onChange={set('validFrom')} />
        </label>
        <label>
          Geçerli (bitiş, isteğe bağlı)
          <input type="date" value={form.validTo} onChange={set('validTo')} />
        </label>
        <button type="submit" className="btn btn-primary" disabled={busy}>
          {busy ? 'Ekleniyor…' : 'Pencere ekle'}
        </button>
      </form>
      <ErrorMessage error={error} />

      {list.loading && (
        <p className="page-note" role="status">
          Uygunluk yükleniyor…
        </p>
      )}
      <ErrorMessage error={list.error} />
      {list.data?.length === 0 && <p className="page-note">Henüz uygunluk penceresi tanımlamadınız; hasta randevu alamaz.</p>}
      <ul className="appointment-list">
        {sorted.map((a) => (
          <li key={a.id} className="appointment">
            <div className="appointment-head">
              <strong>{DAY_NAMES[a.day]}</strong>
              <span>
                {formatTime(a.startTime)} – {formatTime(a.endTime)}
              </span>
            </div>
            <p className="page-note">
              {formatDateShort(a.validFrom)} tarihinden {a.validTo ? `${formatDateShort(a.validTo)} tarihine kadar` : 'itibaren süresiz'}
            </p>
            <RemoveButton
              label="Pencereyi sil"
              onRemove={async () => {
                await doctor.removeAvailability(a.id)
                reload()
              }}
            />
          </li>
        ))}
      </ul>
    </section>
  )
}

function TimeOffSection() {
  const [version, reload] = useReload()
  const list = useAsync((signal) => doctor.timeOff(signal).then((r) => r.data), [version])
  const [form, setForm] = useState({ startAt: '', endAt: '', reason: '' })
  const [error, setError] = useState<unknown>()
  const [busy, setBusy] = useState(false)

  async function add(event: FormEvent) {
    event.preventDefault()
    setBusy(true)
    setError(undefined)
    try {
      await doctor.addTimeOff({ startAt: localInputToIso(form.startAt), endAt: localInputToIso(form.endAt), reason: form.reason.trim() || null })
      setForm({ startAt: '', endAt: '', reason: '' })
      reload()
    } catch (e) {
      setError(e)
    } finally {
      setBusy(false)
    }
  }

  const sorted: TimeOff[] = [...(list.data ?? [])].sort((a, b) => a.startAt.localeCompare(b.startAt))
  const set = (key: keyof typeof form) => (e: { target: { value: string } }) => setForm((f) => ({ ...f, [key]: e.target.value }))

  return (
    <section aria-labelledby="izin-baslik">
      <h2 id="izin-baslik">İzinler</h2>
      <p className="page-note">İzin aralığında randevu alınamaz. Bu aralıkta onaylı/bekleyen randevunuz varsa izin eklenemez. Saatler tarayıcınızın saat dilimindedir.</p>

      <form onSubmit={add} className="inline-form" noValidate>
        <label>
          Başlangıç
          <input type="datetime-local" required value={form.startAt} onChange={set('startAt')} />
        </label>
        <label>
          Bitiş
          <input type="datetime-local" required value={form.endAt} onChange={set('endAt')} />
        </label>
        <label>
          Neden (isteğe bağlı)
          <input maxLength={200} value={form.reason} onChange={set('reason')} />
        </label>
        <button type="submit" className="btn btn-primary" disabled={busy || !form.startAt || !form.endAt}>
          {busy ? 'Ekleniyor…' : 'İzin ekle'}
        </button>
      </form>
      <ErrorMessage error={error} />

      <ErrorMessage error={list.error} />
      {list.data?.length === 0 && <p className="page-note">Tanımlı izniniz yok.</p>}
      <ul className="appointment-list">
        {sorted.map((t) => (
          <li key={t.id} className="appointment">
            <p>
              <strong>{formatDateTime(t.startAt)}</strong> → <strong>{formatDateTime(t.endAt)}</strong>
            </p>
            {t.reason && <p className="page-note">{t.reason}</p>}
            <RemoveButton
              label="İzni sil"
              onRemove={async () => {
                await doctor.removeTimeOff(t.id)
                reload()
              }}
            />
          </li>
        ))}
      </ul>
    </section>
  )
}

export default function DoctorAvailability() {
  return (
    <>
      <AvailabilitySection />
      <TimeOffSection />
    </>
  )
}
