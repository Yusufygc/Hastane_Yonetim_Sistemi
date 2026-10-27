import { NavLink, Outlet } from 'react-router-dom'

const LINKS = [
  { to: '/doktor', label: 'Randevular', end: true },
  { to: '/doktor/takvim', label: 'Takvim' },
  { to: '/doktor/hizmetlerim', label: 'Hizmetlerim' },
  { to: '/doktor/uygunluk', label: 'Uygunluk ve izin' },
  { to: '/doktor/profil', label: 'Profil' },
]

export default function DoctorLayout() {
  return (
    <section className="page">
      <h1>Doktor paneli</h1>
      <nav className="subnav" aria-label="Doktor paneli menüsü">
        {LINKS.map((link) => (
          <NavLink key={link.to} to={link.to} end={link.end}>
            {link.label}
          </NavLink>
        ))}
      </nav>
      <Outlet />
    </section>
  )
}
