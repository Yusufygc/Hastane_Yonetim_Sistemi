import { useState } from 'react'
import { appointments, type Appointment } from '../../api/endpoints'
import { ErrorMessage } from '../../components/ErrorMessage'
import { ReasonForm } from '../../components/ReasonForm'
import { StatusBadge } from '../../components/StatusBadge'
import { formatDateTime, formatMoney, hasStarted } from '../../lib/format'

type Mode = 'idle' | 'reject' | 'cancel'

/** Randevu kartındaki doktor işlemleri: bekleyen → onayla/reddet, onaylı → tamamla/iptal. */
function AppointmentActions({ appointment, onChanged }: { appointment: Appointment; onChanged: () => void }) {
  const [mode, setMode] = useState<Mode>('idle')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<unknown>()

  async function run(action: () => Promise<unknown>) {
    setBusy(true)
    setError(undefined)
    try {
      await action()
      setMode('idle')
      onChanged()
    } catch (e) {
      setError(e)
    } finally {
      setBusy(false)
    }
  }

  if (mode === 'reject' || mode === 'cancel') {
    const rejecting = mode === 'reject'
    return (
      <ReasonForm
        label={rejecting ? 'Red nedeni (hastaya iletilir)' : 'İptal nedeni (hastaya iletilir)'}
        submitLabel={rejecting ? 'Talebi reddet' : 'Randevuyu iptal et'}
        busyLabel={rejecting ? 'Reddediliyor…' : 'İptal ediliyor…'}
        onCancel={() => setMode('idle')}
        onSubmit={async (reason) => {
          await (rejecting ? appointments.reject(appointment.id, reason) : appointments.cancel(appointment.id, reason))
          setMode('idle')
          onChanged()
        }}
      />
    )
  }

  const started = hasStarted(appointment.startAt)

  return (
    <div className="actions">
      <ErrorMessage error={error} />
      <div className="row">
        {appointment.status === 'PENDING' && (
          <>
            <button type="button" className="btn btn-primary" disabled={busy} onClick={() => void run(() => appointments.approve(appointment.id))}>
              {busy ? 'Onaylanıyor…' : 'Onayla'}
            </button>
            <button type="button" className="btn" disabled={busy} onClick={() => setMode('reject')}>
              Reddet
            </button>
          </>
        )}
        {appointment.status === 'APPROVED' && (
          <>
            <button
              type="button"
              className="btn btn-primary"
              disabled={busy || !started}
              title={started ? undefined : 'Randevu başlamadan tamamlanamaz'}
              onClick={() => void run(() => appointments.complete(appointment.id))}
            >
              Tamamlandı olarak işaretle
            </button>
            <button type="button" className="btn btn-ghost" disabled={busy} onClick={() => setMode('cancel')}>
              İptal et
            </button>
          </>
        )}
      </div>
    </div>
  )
}

export function AppointmentCard({ appointment, onChanged }: { appointment: Appointment; onChanged: () => void }) {
  return (
    <li className="appointment">
      <div className="appointment-head">
        <strong>{appointment.serviceName}</strong>
        <StatusBadge status={appointment.status} />
      </div>
      <p>
        <strong>{appointment.patientName}</strong> · {formatDateTime(appointment.startAt)}
      </p>
      <p className="page-note">
        {appointment.durationMinutes} dk · {formatMoney(appointment.price, appointment.currency)}
      </p>
      {appointment.patientNote && <p className="note">Hasta notu: {appointment.patientNote}</p>}
      {appointment.decisionReason && <p className="page-note">Karar notu: {appointment.decisionReason}</p>}
      <AppointmentActions appointment={appointment} onChanged={onChanged} />
    </li>
  )
}
