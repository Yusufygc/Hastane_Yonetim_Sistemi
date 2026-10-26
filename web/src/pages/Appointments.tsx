import { useCallback, useState } from 'react'
import { useLocation } from 'react-router-dom'
import { appointments, type Appointment, type AppointmentStatus } from '../api/endpoints'
import { ErrorMessage } from '../components/ErrorMessage'
import { StatusBadge } from '../components/StatusBadge'
import { STATUS_LABELS, formatDateTime, formatMoney, isCancellable } from '../lib/format'
import { useAsync } from '../lib/useAsync'

const PAGE_SIZE = 10

function CancelForm({ appointment, onDone }: { appointment: Appointment; onDone: () => void }) {
  const [reason, setReason] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<unknown>()

  async function submit(event: React.FormEvent) {
    event.preventDefault()
    setBusy(true)
    setError(undefined)
    try {
      await appointments.cancel(appointment.id, reason.trim())
      onDone()
    } catch (e) {
      setError(e)
    } finally {
      setBusy(false)
    }
  }

  return (
    <form className="cancel-form" onSubmit={submit}>
      <ErrorMessage error={error} />
      <label>
        İptal nedeni
        <input required maxLength={500} value={reason} onChange={(e) => setReason(e.target.value)} />
      </label>
      <button className="btn btn-danger" type="submit" disabled={busy || reason.trim() === ''}>
        {busy ? 'İptal ediliyor…' : 'Randevuyu iptal et'}
      </button>
    </form>
  )
}

export default function Appointments() {
  const location = useLocation()
  const created = (location.state as { created?: boolean } | null)?.created
  const [status, setStatus] = useState<AppointmentStatus | ''>('')
  const [page, setPage] = useState(1)
  const [version, setVersion] = useState(0)
  const [cancelling, setCancelling] = useState<string | null>(null)

  const load = useCallback((signal: AbortSignal) => appointments.mine({ status: status || undefined, page, pageSize: PAGE_SIZE }, signal), [status, page])
  const result = useAsync(load, [status, page, version])

  const total = result.data?.total ?? 0
  const pageCount = Math.max(1, Math.ceil(total / PAGE_SIZE))
  const list = result.data?.data

  return (
    <section className="page">
      <h1>Randevularım</h1>
      {created && (
        <div className="notice" role="status">
          Randevu talebiniz alındı. Doktor onayladığında e-posta ile bilgilendirileceksiniz.
        </div>
      )}

      <label className="inline-field">
        Duruma göre
        <select
          value={status}
          onChange={(e) => {
            setStatus(e.target.value as AppointmentStatus | '')
            setPage(1)
          }}
        >
          <option value="">Tümü</option>
          {Object.entries(STATUS_LABELS).map(([value, label]) => (
            <option key={value} value={value}>
              {label}
            </option>
          ))}
        </select>
      </label>

      {result.loading && (
        <p className="page-note" role="status">
          Randevular yükleniyor…
        </p>
      )}
      <ErrorMessage error={result.error} />
      {list?.length === 0 && <p className="page-note">Gösterilecek randevu yok.</p>}

      <ul className="appointment-list">
        {list?.map((a) => (
          <li key={a.id} className="appointment">
            <div className="appointment-head">
              <strong>{a.serviceName}</strong>
              <StatusBadge status={a.status} />
            </div>
            <p>
              {a.doctorName} · {a.hospitalName}
            </p>
            <p>{formatDateTime(a.startAt)}</p>
            <p className="page-note">
              {a.durationMinutes} dk · {formatMoney(a.price, a.currency)}
            </p>
            {a.decisionReason && <p className="page-note">Not: {a.decisionReason}</p>}
            {isCancellable(a.status) &&
              (cancelling === a.id ? (
                <CancelForm
                  appointment={a}
                  onDone={() => {
                    setCancelling(null)
                    setVersion((v) => v + 1)
                  }}
                />
              ) : (
                <button type="button" className="btn btn-ghost" onClick={() => setCancelling(a.id)}>
                  İptal et
                </button>
              ))}
          </li>
        ))}
      </ul>

      {pageCount > 1 && (
        <nav className="pager" aria-label="Sayfalama">
          <button type="button" className="btn" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
            Önceki
          </button>
          <span>
            Sayfa {page} / {pageCount} ({total} randevu)
          </span>
          <button type="button" className="btn" disabled={page >= pageCount} onClick={() => setPage((p) => p + 1)}>
            Sonraki
          </button>
        </nav>
      )}
    </section>
  )
}
