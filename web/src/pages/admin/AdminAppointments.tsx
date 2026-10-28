import { useState } from 'react'
import { admin } from '../../api/admin'
import type { Appointment, AppointmentStatus } from '../../api/endpoints'
import { ErrorMessage } from '../../components/ErrorMessage'
import { Pager } from '../../components/Pager'
import { ReasonForm } from '../../components/ReasonForm'
import { StatusBadge } from '../../components/StatusBadge'
import { STATUS_LABELS, formatDateTime, formatMoney, isCancellable } from '../../lib/format'
import { useReload } from '../../lib/reload'
import { useAsync } from '../../lib/useAsync'
import { useAdmin } from './AdminContext'

const PAGE_SIZE = 10

function Row({ appointment, onChanged }: { appointment: Appointment; onChanged: () => void }) {
  const [cancelling, setCancelling] = useState(false)

  return (
    <li className="appointment">
      <div className="appointment-head">
        <strong>{appointment.serviceName}</strong>
        <StatusBadge status={appointment.status} />
      </div>
      <p>
        <strong>{appointment.patientName}</strong> → {appointment.doctorName}
      </p>
      <p>{formatDateTime(appointment.startAt)}</p>
      <p className="page-note">
        {appointment.durationMinutes} dk · {formatMoney(appointment.price, appointment.currency)}
      </p>
      {appointment.decisionReason && <p className="page-note">Karar notu: {appointment.decisionReason}</p>}
      {isCancellable(appointment.status) &&
        (cancelling ? (
          <ReasonForm
            label="İptal nedeni (hasta ve doktora iletilir)"
            submitLabel="Randevuyu iptal et"
            busyLabel="İptal ediliyor…"
            onCancel={() => setCancelling(false)}
            onSubmit={async (reason) => {
              await admin.cancelAppointment(appointment.id, reason)
              setCancelling(false)
              onChanged()
            }}
          />
        ) : (
          <button type="button" className="btn btn-ghost" onClick={() => setCancelling(true)}>
            İptal et
          </button>
        ))}
    </li>
  )
}

export default function AdminAppointments() {
  const { hospitalId } = useAdmin()
  const [status, setStatus] = useState<AppointmentStatus | ''>('')
  const [page, setPage] = useState(1)
  const [version, reload] = useReload()

  const result = useAsync(
    hospitalId ? (signal) => admin.appointments({ hospitalId, status: status || undefined, page, pageSize: PAGE_SIZE }, signal) : null,
    [hospitalId, status, page, version],
  )
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
      {list?.length === 0 && <p className="page-note">Gösterilecek randevu yok.</p>}
      <ul className="appointment-list">
        {list?.map((a) => (
          <Row key={a.id} appointment={a} onChanged={reload} />
        ))}
      </ul>
      <Pager page={page} total={result.data?.total ?? 0} pageSize={PAGE_SIZE} onPage={setPage} noun="randevu" />
    </>
  )
}
