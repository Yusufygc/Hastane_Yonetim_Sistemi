import { NavLink, Outlet } from 'react-router-dom'
import { AdminProvider, useAdmin } from './AdminContext'

const LINKS = [
  { to: '/yonetim', label: 'Randevular', end: true },
  { to: '/yonetim/hizmet-talepleri', label: 'Hizmet talepleri' },
  { to: '/yonetim/doktorlar', label: 'Doktorlar' },
  { to: '/yonetim/personel', label: 'Personel' },
  { to: '/yonetim/subeler', label: 'Şubeler' },
  { to: '/yonetim/bolumler', label: 'Bölümler' },
  { to: '/yonetim/hizmetler', label: 'Hizmetler' },
]

function HospitalPicker() {
  const { hospitals, hospitalId, selectHospital, isSuper, organizationName } = useAdmin()

  return (
    <div className="admin-bar">
      {organizationName && <span className="page-note">{organizationName}</span>}
      <label className="inline-field">
        Şube
        <select value={hospitalId ?? ''} onChange={(e) => selectHospital(e.target.value)} disabled={hospitals.length <= 1}>
          {hospitals.map((h) => (
            <option key={h.id} value={h.id}>
              {h.name}
              {h.isActive ? '' : ' (pasif)'}
            </option>
          ))}
        </select>
      </label>
      <span className="badge">{isSuper ? 'Süper yönetici' : 'Şube yöneticisi'}</span>
    </div>
  )
}

export default function AdminLayout() {
  return (
    <section className="page wide">
      <h1>Yönetim paneli</h1>
      <AdminProvider>
        <HospitalPicker />
        <nav className="subnav" aria-label="Yönetim paneli menüsü">
          {LINKS.map((link) => (
            <NavLink key={link.to} to={link.to} end={link.end}>
              {link.label}
            </NavLink>
          ))}
        </nav>
        <Outlet />
      </AdminProvider>
    </section>
  )
}
