import { useState } from 'react'
import { catalog, doctor, type DoctorService } from '../../api/endpoints'
import { ErrorMessage } from '../../components/ErrorMessage'
import { formatDateTime, formatMoney } from '../../lib/format'
import { useReload } from '../../lib/reload'
import { useAsync } from '../../lib/useAsync'

const STATUS_TEXT: Record<string, string> = { PENDING: 'Yönetici onayı bekliyor', APPROVED: 'Onaylı', REJECTED: 'Reddedildi' }

function ServiceRow({ item, onChanged }: { item: DoctorService; onChanged: () => void }) {
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<unknown>()
  // Onaylı hizmeti yalnız yönetici kaldırır; doktor yalnız bekleyen/reddedilen talebi geri çeker.
  const withdrawable = item.status !== 'APPROVED'

  async function withdraw() {
    setBusy(true)
    setError(undefined)
    try {
      await doctor.removeService(item.serviceId)
      onChanged()
    } catch (e) {
      setError(e)
      setBusy(false)
    }
  }

  return (
    <li className="appointment">
      <div className="appointment-head">
        <strong>{item.serviceName}</strong>
        <span className={`badge badge-${item.status === 'APPROVED' ? 'approved' : item.status === 'REJECTED' ? 'rejected' : 'pending'}`}>{STATUS_TEXT[item.status] ?? item.status}</span>
      </div>
      <p className="page-note">
        Talep: {formatDateTime(item.requestedAt)}
        {item.decidedAt && ` · Karar: ${formatDateTime(item.decidedAt)}`}
      </p>
      <ErrorMessage error={error} />
      {withdrawable ? (
        <button type="button" className="btn btn-ghost" disabled={busy} onClick={() => void withdraw()}>
          {busy ? 'Geri çekiliyor…' : item.status === 'PENDING' ? 'Talebi geri çek' : 'Kaydı kaldır'}
        </button>
      ) : (
        <p className="page-note">Onaylı hizmeti yalnız yönetici kaldırabilir.</p>
      )}
    </li>
  )
}

export default function DoctorServices() {
  const [version, reload] = useReload()
  const profile = useAsync((signal) => doctor.profile(signal).then((r) => r.data), [])
  const mine = useAsync((signal) => doctor.services(signal).then((r) => r.data), [version])
  const hospitalId = profile.data?.hospitalId
  const offered = useAsync(hospitalId ? (signal) => catalog.services(hospitalId, signal).then((r) => r.data) : null, [hospitalId])

  const [selected, setSelected] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<unknown>()
  const [sent, setSent] = useState(false)

  const taken = new Set((mine.data ?? []).map((s) => s.serviceId))
  const candidates = (offered.data ?? []).filter((s) => !taken.has(s.id))

  async function request() {
    if (!selected) return
    setBusy(true)
    setError(undefined)
    setSent(false)
    try {
      await doctor.requestService(selected)
      setSelected('')
      setSent(true)
      reload()
    } catch (e) {
      setError(e)
    } finally {
      setBusy(false)
    }
  }

  return (
    <>
      <h2>Hizmetlerim</h2>
      <p className="page-note">Şubenizdeki bir hizmeti vermek için talep edin; yönetici onaylayınca hastalar size bu hizmet için randevu alabilir.</p>

      <div className="inline-form">
        <label>
          Yeni hizmet talebi
          <select value={selected} onChange={(e) => setSelected(e.target.value)} disabled={candidates.length === 0}>
            <option value="">{candidates.length === 0 ? 'Talep edilebilecek hizmet yok' : 'Hizmet seçin'}</option>
            {candidates.map((s) => (
              <option key={s.id} value={s.id}>
                {s.name} ({s.durationMinutes} dk, {formatMoney(s.price, s.currency)})
              </option>
            ))}
          </select>
        </label>
        <button type="button" className="btn btn-primary" disabled={!selected || busy} onClick={() => void request()}>
          {busy ? 'Gönderiliyor…' : 'Talep et'}
        </button>
      </div>
      <ErrorMessage error={error ?? offered.error ?? profile.error} />
      {sent && (
        <div className="notice" role="status">
          Talebiniz yöneticiye iletildi.
        </div>
      )}

      {mine.loading && (
        <p className="page-note" role="status">
          Hizmetler yükleniyor…
        </p>
      )}
      <ErrorMessage error={mine.error} />
      {mine.data?.length === 0 && <p className="page-note">Henüz hizmet talebiniz yok.</p>}
      <ul className="appointment-list">
        {mine.data?.map((item) => (
          <ServiceRow key={item.serviceId} item={item} onChanged={reload} />
        ))}
      </ul>
    </>
  )
}
