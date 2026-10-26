import { useState, type ReactNode } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { appointments, catalog } from '../api/endpoints'
import { ApiError } from '../api/http'
import { useAuth } from '../auth/AuthContext'
import { ErrorMessage } from '../components/ErrorMessage'
import { bookingRange, formatDateLong, formatMoney } from '../lib/format'
import { useBookingDraft } from '../lib/draft'
import { useAsync } from '../lib/useAsync'

const NOTE_MAX = 500

function Step({ number, title, done, children }: { number: number; title: string; done?: boolean; children: ReactNode }) {
  return (
    <section className={`step ${done ? 'is-done' : ''}`} aria-labelledby={`adim-${number}`}>
      <h2 id={`adim-${number}`}>
        <span className="step-no" aria-hidden="true">
          {number}
        </span>
        {title}
      </h2>
      {children}
    </section>
  )
}

function Loading({ what }: { what: string }) {
  return (
    <p className="page-note" role="status">
      {what} yükleniyor…
    </p>
  )
}

export default function Book() {
  const { state } = useAuth()
  const navigate = useNavigate()
  const { draft, select, reset } = useBookingDraft()
  const [note, setNote] = useState('')
  const [busy, setBusy] = useState(false)
  const [submitError, setSubmitError] = useState<unknown>()
  const range = bookingRange()

  const hospitals = useAsync((signal) => catalog.hospitals(signal).then((r) => r.data), [])
  const services = useAsync(draft.hospitalId ? (signal) => catalog.services(draft.hospitalId!, signal).then((r) => r.data) : null, [draft.hospitalId])
  const doctors = useAsync(
    draft.hospitalId && draft.serviceId ? (signal) => catalog.doctors(draft.hospitalId!, draft.serviceId!, signal).then((r) => r.data) : null,
    [draft.hospitalId, draft.serviceId],
  )
  const slots = useAsync(
    draft.doctorId && draft.serviceId && draft.date ? (signal) => catalog.slots(draft.doctorId!, draft.serviceId!, draft.date!, signal).then((r) => r.data) : null,
    [draft.doctorId, draft.serviceId, draft.date],
  )

  const hospital = hospitals.data?.find((h) => h.id === draft.hospitalId)
  const service = services.data?.find((s) => s.id === draft.serviceId)
  const doctor = doctors.data?.find((d) => d.id === draft.doctorId)
  const slot = slots.data?.slots.find((s) => s.startAt === draft.startAt)
  const timeZone = slots.data?.timeZoneId ?? hospital?.timeZoneId

  async function submit() {
    if (!draft.doctorId || !draft.serviceId || !draft.startAt) return
    setBusy(true)
    setSubmitError(undefined)
    try {
      await appointments.create(draft.doctorId, draft.serviceId, draft.startAt, note.trim() || null)
      reset()
      navigate('/randevularim', { state: { created: true } })
    } catch (e) {
      setSubmitError(e)
      // Başkası aynı saati almış olabilir: seçimi bırak, kullanıcı başka saat seçsin.
      if (e instanceof ApiError && e.status === 409) select('startAt', undefined)
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="page">
      <h1>Randevu al</h1>
      <p className="lead">Seçimleriniz sırayla ilerler; bir adımı değiştirirseniz sonraki seçimler sıfırlanır.</p>

      <Step number={1} title="Şube seçin" done={!!draft.hospitalId}>
        {hospitals.loading && <Loading what="Şubeler" />}
        <ErrorMessage error={hospitals.error} />
        <div className="choice-grid" role="radiogroup" aria-label="Şube">
          {hospitals.data?.map((h) => (
            <button key={h.id} type="button" role="radio" aria-checked={draft.hospitalId === h.id} className="choice" onClick={() => select('hospitalId', h.id)}>
              <strong>{h.name}</strong>
              {h.address && <span>{h.address}</span>}
            </button>
          ))}
        </div>
      </Step>

      {draft.hospitalId && (
        <Step number={2} title="Hizmet seçin" done={!!draft.serviceId}>
          {services.loading && <Loading what="Hizmetler" />}
          <ErrorMessage error={services.error} />
          {services.data?.length === 0 && <p className="page-note">Bu şubede henüz hizmet tanımlı değil.</p>}
          <div className="choice-grid" role="radiogroup" aria-label="Hizmet">
            {services.data?.map((s) => (
              <button key={s.id} type="button" role="radio" aria-checked={draft.serviceId === s.id} className="choice" onClick={() => select('serviceId', s.id)}>
                <strong>{s.name}</strong>
                <span>
                  {s.durationMinutes} dk · {formatMoney(s.price, s.currency)}
                </span>
              </button>
            ))}
          </div>
        </Step>
      )}

      {draft.serviceId && (
        <Step number={3} title="Doktor seçin" done={!!draft.doctorId}>
          {doctors.loading && <Loading what="Doktorlar" />}
          <ErrorMessage error={doctors.error} />
          {doctors.data?.length === 0 && <p className="page-note">Bu hizmeti veren doktor yok. Başka bir hizmet deneyin.</p>}
          <div className="choice-grid" role="radiogroup" aria-label="Doktor">
            {doctors.data?.map((d) => (
              <button key={d.id} type="button" role="radio" aria-checked={draft.doctorId === d.id} className="choice" onClick={() => select('doctorId', d.id)}>
                <strong>
                  {d.firstName} {d.lastName}
                </strong>
                <span>{[d.specialty, d.departmentName].filter(Boolean).join(' · ')}</span>
              </button>
            ))}
          </div>
        </Step>
      )}

      {draft.doctorId && (
        <Step number={4} title="Tarih ve saat seçin" done={!!draft.startAt}>
          <label className="inline-field">
            Tarih
            <input type="date" min={range.min} max={range.max} value={draft.date ?? ''} onChange={(e) => select('date', e.target.value || undefined)} />
          </label>
          {slots.loading && <Loading what="Saatler" />}
          <ErrorMessage error={slots.error} />
          {slots.data && slots.data.slots.length === 0 && <p className="page-note">Bu tarihte boş saat yok. Başka bir gün deneyin.</p>}
          {slots.data && slots.data.slots.length > 0 && (
            <>
              <p className="page-note">Saatler {slots.data.timeZoneId} saat dilimindedir.</p>
              <div className="slot-grid" role="radiogroup" aria-label="Saat">
                {slots.data.slots.map((s) => (
                  <button key={s.startAt} type="button" role="radio" aria-checked={draft.startAt === s.startAt} className="slot" onClick={() => select('startAt', s.startAt)}>
                    {s.localStart.slice(0, 5)}
                  </button>
                ))}
              </div>
            </>
          )}
        </Step>
      )}

      {draft.startAt && service && doctor && slot && (
        <Step number={5} title="Özet ve onay">
          <dl className="summary">
            <dt>Şube</dt>
            <dd>{hospital?.name}</dd>
            <dt>Hizmet</dt>
            <dd>
              {service.name} ({service.durationMinutes} dk, {formatMoney(service.price, service.currency)})
            </dd>
            <dt>Doktor</dt>
            <dd>
              {doctor.firstName} {doctor.lastName}
            </dd>
            <dt>Zaman</dt>
            <dd>
              {draft.date && formatDateLong(draft.date)}, {slot.localStart.slice(0, 5)} {timeZone && `(${timeZone})`}
            </dd>
          </dl>
          <label>
            Doktora notunuz (isteğe bağlı)
            <textarea rows={3} maxLength={NOTE_MAX} value={note} onChange={(e) => setNote(e.target.value)} />
            <span className="hint">
              {note.length}/{NOTE_MAX}
            </span>
          </label>
          <ErrorMessage error={submitError} />
          {state.status === 'authenticated' ? (
            <button type="button" className="btn btn-primary btn-lg" disabled={busy} onClick={() => void submit()}>
              {busy ? 'Gönderiliyor…' : 'Randevu talep et'}
            </button>
          ) : (
            <p>
              Talebi göndermek için{' '}
              <Link to="/giris" state={{ from: '/randevu-al' }}>
                giriş yapın
              </Link>{' '}
              veya <Link to="/kayit">kayıt olun</Link>; seçimleriniz korunur.
            </p>
          )}
          <p className="page-note">Talebiniz doktor onayına gider; durumu “Randevularım” sayfasından ve e-posta ile takip edebilirsiniz.</p>
        </Step>
      )}
    </section>
  )
}
