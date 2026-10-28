import { useState } from 'react'
import { admin, type ServiceRequest, type ServiceRequestStatus } from '../../api/admin'
import { ErrorMessage } from '../../components/ErrorMessage'
import { Pager } from '../../components/Pager'
import { formatDateTime } from '../../lib/format'
import { useReload } from '../../lib/reload'
import { useAsync } from '../../lib/useAsync'

const PAGE_SIZE = 10
const LABELS: Record<ServiceRequestStatus, string> = { PENDING: 'Bekleyen', APPROVED: 'Onaylı', REJECTED: 'Reddedilen' }

function Row({ item, onChanged }: { item: ServiceRequest; onChanged: () => void }) {
  const [busy, setBusy] = useState<'approve' | 'reject' | null>(null)
  const [error, setError] = useState<unknown>()

  async function decide(kind: 'approve' | 'reject') {
    setBusy(kind)
    setError(undefined)
    try {
      await (kind === 'approve' ? admin.approveServiceRequest(item.doctorId, item.serviceId) : admin.rejectServiceRequest(item.doctorId, item.serviceId))
      onChanged()
    } catch (e) {
      setError(e)
      setBusy(null)
    }
  }

  return (
    <li className="appointment">
      <div className="appointment-head">
        <strong>{item.doctorName}</strong>
        <span className={`badge badge-${item.status === 'APPROVED' ? 'approved' : item.status === 'REJECTED' ? 'rejected' : 'pending'}`}>
          {LABELS[item.status as ServiceRequestStatus] ?? item.status}
        </span>
      </div>
      <p>
        Hizmet: <strong>{item.serviceName}</strong>
      </p>
      <p className="page-note">Talep: {formatDateTime(item.requestedAt)}</p>
      <ErrorMessage error={error} />
      {item.status === 'PENDING' && (
        <div className="row">
          <button type="button" className="btn btn-primary" disabled={busy !== null} onClick={() => void decide('approve')}>
            {busy === 'approve' ? 'Onaylanıyor…' : 'Onayla'}
          </button>
          <button type="button" className="btn" disabled={busy !== null} onClick={() => void decide('reject')}>
            {busy === 'reject' ? 'Reddediliyor…' : 'Reddet'}
          </button>
        </div>
      )}
    </li>
  )
}

export default function AdminServiceRequests() {
  const [status, setStatus] = useState<ServiceRequestStatus>('PENDING')
  const [page, setPage] = useState(1)
  const [version, reload] = useReload()

  const result = useAsync((signal) => admin.serviceRequests({ status, page, pageSize: PAGE_SIZE }, signal), [status, page, version])
  const list = result.data?.data

  return (
    <>
      <h2>Doktor hizmet talepleri</h2>
      <p className="page-note">Onaylanan hizmeti doktor verebilir ve hastalar o doktora o hizmet için randevu alabilir. Kapsamınız dışındaki şubelerin talepleri görünmez.</p>
      <label className="inline-field">
        Durum
        <select
          value={status}
          onChange={(e) => {
            setStatus(e.target.value as ServiceRequestStatus)
            setPage(1)
          }}
        >
          {Object.entries(LABELS).map(([value, label]) => (
            <option key={value} value={value}>
              {label}
            </option>
          ))}
        </select>
      </label>

      {result.loading && (
        <p className="page-note" role="status">
          Talepler yükleniyor…
        </p>
      )}
      <ErrorMessage error={result.error} />
      {list?.length === 0 && <p className="page-note">{status === 'PENDING' ? 'Onay bekleyen talep yok.' : 'Gösterilecek talep yok.'}</p>}
      <ul className="appointment-list">
        {list?.map((item) => (
          <Row key={`${item.doctorId}-${item.serviceId}`} item={item} onChanged={reload} />
        ))}
      </ul>
      <Pager page={page} total={result.data?.total ?? 0} pageSize={PAGE_SIZE} onPage={setPage} noun="talep" />
    </>
  )
}
