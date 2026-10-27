import { useState } from 'react'
import { doctor, type AppointmentStatus } from '../../api/endpoints'
import { ErrorMessage } from '../../components/ErrorMessage'
import { STATUS_LABELS } from '../../lib/format'
import { useReload } from '../../lib/reload'
import { useAsync } from '../../lib/useAsync'
import { AppointmentCard } from './AppointmentCard'

const PAGE_SIZE = 10

export default function DoctorAppointments() {
  const [status, setStatus] = useState<AppointmentStatus | ''>('PENDING')
  const [page, setPage] = useState(1)
  const [version, reload] = useReload()

  const result = useAsync(
    (signal) => doctor.appointments({ status: status || undefined, page, pageSize: PAGE_SIZE }, signal),
    [status, page, version],
  )

  const total = result.data?.total ?? 0
  const pageCount = Math.max(1, Math.ceil(total / PAGE_SIZE))
  const list = result.data?.data

  return (
    <>
      <h2>Randevular</h2>
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
      {list?.length === 0 && <p className="page-note">{status === 'PENDING' ? 'Onay bekleyen talep yok.' : 'Gösterilecek randevu yok.'}</p>}

      <ul className="appointment-list">
        {list?.map((a) => (
          <AppointmentCard key={a.id} appointment={a} onChanged={reload} />
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
    </>
  )
}
