import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { session } from './api/http'
import { bodyOf, calls, fetchMock, json, loginHandler, paged, serve, signIn, type Handler } from './test/helpers'

const hospital = { id: 'h1', organizationId: 'o1', name: 'Merkez Şube', address: null, phone: null, timeZoneId: 'Europe/Istanbul', isActive: true }
const department = { id: 'dep1', hospitalId: 'h1', name: 'Dahiliye', isActive: true }

/** Süper yönetici: kurum okunabilir. Şube yöneticisi: aynı uç 403. */
const hospitalsHandler: Handler = (url) => (url === '/api/hospitals' ? json([hospital]) : undefined)
const superHandler: Handler = (url) => (url === '/api/organization' ? json({ id: 'o1', name: 'Test Kurumu', isActive: true }) : undefined)
const branchManagerHandler: Handler = (url) =>
  url === '/api/organization' ? json({ title: 'Yetkiniz yok', detail: 'Bu işlem için yetkiniz yok.', code: 'auth.forbidden' }, { status: 403 }) : undefined
const departmentsHandler: Handler = (url) => (url === '/api/hospitals/h1/departments' ? json([department]) : undefined)

beforeEach(() => {
  vi.stubGlobal('fetch', fetchMock)
  session.clear()
  localStorage.setItem('hospital.motionMode', 'simple')
})

afterEach(() => {
  fetchMock.mockReset()
  vi.unstubAllGlobals()
  window.history.pushState({}, '', '/')
})

const goTo = async (name: string) => userEvent.click(await screen.findByRole('link', { name }))

describe('yönetim paneli', () => {
  it('yönetici girişi panele yönlendirir; menüde Yönetim paneli var, hasta menüsü yok', async () => {
    serve(loginHandler('ADMIN'), hospitalsHandler, superHandler)
    await signIn('ADMIN')

    expect(await screen.findByRole('heading', { name: 'Yönetim paneli' })).toBeInTheDocument()
    expect(window.location.pathname).toBe('/yonetim')
    const nav = screen.getByRole('navigation', { name: 'Ana menü' })
    expect(within(nav).getByRole('link', { name: 'Yönetim paneli' })).toBeInTheDocument()
    expect(within(nav).queryByRole('link', { name: 'Randevu al' })).not.toBeInTheDocument()
    expect(await screen.findByText('Süper yönetici')).toBeInTheDocument()
  })

  it('hasta panele giremez ve yönetici uçlarına istek atılmaz', async () => {
    serve(loginHandler('PATIENT'))
    await signIn('PATIENT')
    await screen.findByRole('heading', { name: 'Randevularım' })

    window.history.pushState({}, '', '/yonetim')
    window.dispatchEvent(new PopStateEvent('popstate'))

    expect(await screen.findByRole('heading', { name: 'Bu sayfaya erişiminiz yok' })).toBeInTheDocument()
    expect(calls('GET', '/api/hospitals').length).toBe(0)
  })

  it('şube yöneticisi: rozet "Şube yöneticisi", yeni şube/kurum adı yok', async () => {
    serve(loginHandler('ADMIN'), hospitalsHandler, branchManagerHandler)
    await signIn('ADMIN')
    await goTo('Şubeler')

    expect(await screen.findByText('Şube yöneticisi')).toBeInTheDocument()
    expect(await screen.findByText('Merkez Şube', { selector: 'strong' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Yeni şube' })).not.toBeInTheDocument()
    expect(screen.queryByLabelText('Kurum adı')).not.toBeInTheDocument()
  })

  it('süper yönetici yeni şube oluşturur: gövde doğru gider', async () => {
    serve(
      loginHandler('ADMIN'),
      hospitalsHandler,
      superHandler,
      (url, init) => (url === '/api/hospitals' && init?.method === 'POST' ? json({ id: 'h2' }, { status: 201 }) : undefined),
    )
    await signIn('ADMIN')
    await goTo('Şubeler')

    await userEvent.click(await screen.findByRole('button', { name: 'Yeni şube' }))
    await userEvent.type(screen.getByLabelText('Şube adı'), 'Kadıköy Şubesi')
    await userEvent.click(screen.getByRole('button', { name: 'Şubeyi oluştur' }))

    await waitFor(() => expect(calls('POST', '/api/hospitals').length).toBe(1))
    expect(bodyOf(calls('POST', '/api/hospitals')[0] as Parameters<typeof fetch>)).toEqual({ name: 'Kadıköy Şubesi', address: null, phone: null, timeZoneId: 'Europe/Istanbul' })
  })

  it('çalışma saatleri: yalnız işaretli günler PUT ile gider, işaretsiz gün kapalıdır', async () => {
    serve(
      loginHandler('ADMIN'),
      hospitalsHandler,
      superHandler,
      (url) =>
        url === '/api/hospitals/h1'
          ? json({ ...hospital, workingHours: [{ day: 1, openTime: '08:00:00', closeTime: '17:00:00' }, { day: 2, openTime: '08:00:00', closeTime: '12:00:00' }] })
          : undefined,
      (url, init) => (url === '/api/hospitals/h1/working-hours' && init?.method === 'PUT' ? new Response(null, { status: 204 }) : undefined),
    )
    await signIn('ADMIN')
    await goTo('Şubeler')

    await userEvent.click(await screen.findByRole('button', { name: 'Çalışma saatleri' }))
    const wed = await screen.findByLabelText('Çarşamba açık')
    expect(screen.getByLabelText('Pazartesi açık')).toBeChecked()
    expect(wed).not.toBeChecked()
    await userEvent.click(wed) // çarşambayı aç (varsayılan 08:00–17:00)
    await userEvent.click(screen.getByRole('button', { name: 'Çalışma saatlerini kaydet' }))

    await waitFor(() => expect(calls('PUT', '/working-hours').length).toBe(1))
    expect(bodyOf(calls('PUT', '/working-hours')[0] as Parameters<typeof fetch>)).toEqual({
      hours: [
        { day: 1, openTime: '08:00', closeTime: '17:00' },
        { day: 2, openTime: '08:00', closeTime: '12:00' },
        { day: 3, openTime: '08:00', closeTime: '17:00' },
      ],
    })
  })

  it('bölüm eklenir: seçili şubeye POST atılır', async () => {
    serve(
      loginHandler('ADMIN'),
      hospitalsHandler,
      superHandler,
      departmentsHandler,
      (url, init) => (url === '/api/hospitals/h1/departments' && init?.method === 'POST' ? json({ id: 'dep2' }, { status: 201 }) : undefined),
    )
    await signIn('ADMIN')
    await goTo('Bölümler')

    await userEvent.type(await screen.findByLabelText('Yeni bölüm adı'), 'Kardiyoloji')
    await userEvent.click(screen.getByRole('button', { name: 'Bölüm ekle' }))

    await waitFor(() => expect(calls('POST', '/api/hospitals/h1/departments').length).toBe(1))
    expect(bodyOf(calls('POST', '/api/hospitals/h1/departments')[0] as Parameters<typeof fetch>)).toEqual({ name: 'Kardiyoloji' })
  })

  it('bölüm pasifleştirme iki adımlıdır; aktif hizmet varsa sunucu mesajı gösterilir', async () => {
    serve(
      loginHandler('ADMIN'),
      hospitalsHandler,
      superHandler,
      departmentsHandler,
      (url, init) =>
        url === '/api/departments/dep1/deactivate' && init?.method === 'POST'
          ? json({ title: 'Çakışma', detail: 'Bölümde aktif hizmet var.', code: 'department.has_active_services', traceId: 't-1' }, { status: 409 })
          : undefined,
    )
    await signIn('ADMIN')
    await goTo('Bölümler')

    await userEvent.click(await screen.findByRole('button', { name: 'Pasifleştir' }))
    expect(calls('POST', '/deactivate').length).toBe(0) // onaydan önce istek yok

    await userEvent.click(screen.getByRole('button', { name: 'Evet, onayla' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Bölümde aktif hizmet var.')
    expect(calls('POST', '/deactivate').length).toBe(1)
  })

  it('doktor oluşturma: gövde doğru gider, 400 alan hataları alanın altında görünür', async () => {
    serve(
      loginHandler('ADMIN'),
      hospitalsHandler,
      superHandler,
      departmentsHandler,
      (url, init) =>
        url === '/api/doctors' && init?.method === 'POST'
          ? json({ title: 'Doğrulama hatası', detail: 'Alanlar geçersiz.', code: 'validation.failed', errors: { email: ['Geçerli bir e-posta girin.'] } }, { status: 400 })
          : undefined,
    )
    await signIn('ADMIN')
    await goTo('Doktorlar')

    await userEvent.click(await screen.findByRole('button', { name: 'Yeni doktor' }))
    await userEvent.type(screen.getByLabelText('Ad'), 'Mehmet')
    await userEvent.type(screen.getByLabelText('Soyad'), 'Kaya')
    await userEvent.type(screen.getByLabelText('E-posta'), 'yanlis')
    await userEvent.type(screen.getByLabelText('Başlangıç parolası'), 'Parola123x')
    await userEvent.type(screen.getByLabelText('Uzmanlık'), 'Kardiyoloji Uzmanı')
    await userEvent.type(screen.getByLabelText('Diploma no'), 'D-77')
    await waitFor(() => expect(within(screen.getByLabelText('Bölüm')).getByRole('option', { name: 'Dahiliye' })).toBeInTheDocument())
    await userEvent.selectOptions(screen.getByLabelText('Bölüm'), 'dep1')
    await userEvent.click(screen.getByRole('button', { name: 'Doktoru oluştur' }))

    expect(await screen.findByText('Geçerli bir e-posta girin.')).toBeInTheDocument()
    expect(bodyOf(calls('POST', '/api/doctors')[0] as Parameters<typeof fetch>)).toEqual({
      firstName: 'Mehmet',
      lastName: 'Kaya',
      email: 'yanlis',
      password: 'Parola123x',
      specialty: 'Kardiyoloji Uzmanı',
      diplomaNo: 'D-77',
      phone: null,
      departmentId: 'dep1',
    })
  })

  it('hizmet talebi onaylanır: POST approve atılır ve liste yenilenir', async () => {
    let approved = false
    serve(
      loginHandler('ADMIN'),
      hospitalsHandler,
      superHandler,
      (url, init) => (url === '/api/doctors/d1/services/s1/approve' && init?.method === 'POST' ? ((approved = true), new Response(null, { status: 204 })) : undefined),
      (url) =>
        url.startsWith('/api/doctor-service-requests')
          ? paged(approved ? [] : [{ doctorId: 'd1', doctorName: 'Ayşe Yılmaz', hospitalId: 'h1', serviceId: 's1', serviceName: 'Kan Tahlili', status: 'PENDING', requestedAt: '2029-01-01T00:00:00Z' }])
          : undefined,
    )
    await signIn('ADMIN')
    await goTo('Hizmet talepleri')

    expect(await screen.findByText('Kan Tahlili')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Onayla' }))

    await waitFor(() => expect(calls('POST', '/api/doctors/d1/services/s1/approve').length).toBe(1))
    expect(await screen.findByText('Onay bekleyen talep yok.')).toBeInTheDocument()
  })

  it('yönetici randevuyu nedenle iptal eder', async () => {
    serve(
      loginHandler('ADMIN'),
      hospitalsHandler,
      superHandler,
      (url, init) => (url.endsWith('/api/appointments/a1/cancel') && init?.method === 'POST' ? new Response(null, { status: 204 }) : undefined),
      (url) =>
        url.startsWith('/api/appointments?')
          ? paged([
              {
                id: 'a1', hospitalId: 'h1', hospitalName: 'Merkez Şube', patientId: 'p1', patientName: 'Ali Veli', doctorId: 'd1', doctorName: 'Ayşe Yılmaz',
                serviceId: 's1', serviceName: 'Genel Muayene', startAt: '2030-01-10T07:00:00Z', endAt: '2030-01-10T07:15:00Z', durationMinutes: 15, price: 500,
                currency: 'TRY', status: 'APPROVED', patientNote: null, decisionReason: null, createdAt: '2029-12-01T00:00:00Z',
              },
            ])
          : undefined,
    )
    await signIn('ADMIN')

    await userEvent.click(await screen.findByRole('button', { name: 'İptal et' }))
    const submit = screen.getByRole('button', { name: 'Randevuyu iptal et' })
    expect(submit).toBeDisabled()
    await userEvent.type(screen.getByLabelText('İptal nedeni (hasta ve doktora iletilir)'), 'Doktor izinde')
    await userEvent.click(submit)

    await waitFor(() => expect(calls('POST', '/cancel').length).toBe(1))
    expect(bodyOf(calls('POST', '/cancel')[0] as Parameters<typeof fetch>)).toEqual({ reason: 'Doktor izinde' })
  })
})
