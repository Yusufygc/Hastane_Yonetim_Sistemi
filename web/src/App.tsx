import { BrowserRouter, Route, Routes } from 'react-router-dom'
import { AuthProvider } from './auth/AuthContext'
import Layout from './components/Layout'
import ProtectedRoute from './components/ProtectedRoute'
import RoleRoute from './components/RoleRoute'
import { MotionProvider } from './lib/MotionProvider'
import Appointments from './pages/Appointments'
import Book from './pages/Book'
import Landing from './pages/Landing'
import Login from './pages/Login'
import NotFound from './pages/NotFound'
import Register from './pages/Register'
import DoctorAppointments from './pages/doctor/DoctorAppointments'
import DoctorAvailability from './pages/doctor/DoctorAvailability'
import DoctorLayout from './pages/doctor/DoctorLayout'
import DoctorProfile from './pages/doctor/DoctorProfile'
import DoctorSchedule from './pages/doctor/DoctorSchedule'
import DoctorServices from './pages/doctor/DoctorServices'

export default function App() {
  return (
    <BrowserRouter>
      <MotionProvider>
        <AuthProvider>
          <Routes>
            <Route element={<Layout />}>
              <Route index element={<Landing />} />
              <Route path="giris" element={<Login />} />
              <Route path="kayit" element={<Register />} />
              {/* Katalog anonim gezilir; talep gönderirken giriş istenir (seçimler korunur). */}
              <Route path="randevu-al" element={<Book />} />
              <Route element={<ProtectedRoute />}>
                <Route path="randevularim" element={<Appointments />} />
              </Route>
              <Route element={<RoleRoute role="DOCTOR" label="Doktor paneli" />}>
                <Route path="doktor" element={<DoctorLayout />}>
                  <Route index element={<DoctorAppointments />} />
                  <Route path="takvim" element={<DoctorSchedule />} />
                  <Route path="hizmetlerim" element={<DoctorServices />} />
                  <Route path="uygunluk" element={<DoctorAvailability />} />
                  <Route path="profil" element={<DoctorProfile />} />
                </Route>
              </Route>
              <Route path="*" element={<NotFound />} />
            </Route>
          </Routes>
        </AuthProvider>
      </MotionProvider>
    </BrowserRouter>
  )
}
