import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { session } from './api/http'
import App from './App'

const json = (body: unknown, init: ResponseInit = {}) =>
  new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' }, ...init })

type Handler = (url: string, init?: RequestInit) => Response | Promise<Response> | undefined

const fetchMock = vi.fn<typeof fetch>()
let handlers: Handler[] = []

const appointment = (over: Record<string, unknown> = {}) => ({
  id: 'a1',
  hospitalId: 'h1',
  hospitalName: 'Merkez Şube',
  patientId: 'p1',
  patientName: 'Ali Veli',
  doctorId: 'd1',
  doctorName: 'Ayşe Yılmaz',
  serviceId: 's1',
  serviceName: 'Genel Muayene',
  startAt: '2030-01-10T07:00:00Z',
  endAt: '2030-01-10T07:15:00Z',
  durationMinutes: 15,
  price: 500,
  currency: 'TRY',
  status: 'PENDING',
  patientNote: 'Baş ağrısı',
  decisionReason: null,
  createdAt: '2029-12-01T00:00:00Z',
  ...over,
})

const tokensFor = (role: string) => ({
  accessToken: 'a',
  refreshToken: 'r',
  accessTokenExpiresAt: '',
  refreshTokenExpiresAt: '',
  user: { id: 'u1', email: `${role.toLowerCase()}@x.com`, role },
})

/** URL'ye göre ilk cevap veren işleyici kazanır; bilinmeyen istek boş liste döner. */
function serve(...extra: Handler[]) {
  handlers = extra
  fetchMock.mockImplementation(async (input, init) => {
    const url = String(input)
    for (const h of handlers) {
      const r = h(url, init)
      if (r) return r
    }
    return json([], { headers: { 'Content-Type': 'application/json', 'X-Total-Count': '0' } })
  })
}

async function signIn(role: 'DOCTOR' | 'PATIENT') {
  window.history.pushState({}, '', '/giris')
  render(<App />)
  await userEvent.type(await screen.findByLabelText('E-posta'), `${role.toLowerCase()}@x.com`)
  await userEvent.type(screen.getByLabelText('Parola'), 'Parola123x')
  await userEvent.click(screen.getByRole('button', { name: 'Giriş yap' }))
}

const loginHandler = (role: string): Handler => (url, init) => (url === '/api/auth/login' && init?.method === 'POST' ? json(tokensFor(role)) : undefined)

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

const calls = (method: string, urlPart: string) => fetchMock.mock.calls.filter(([u, i]) => String(u).includes(urlPart) && (i?.method ?? 'GET') === method)

describe('doktor paneli', () => {
  it('doktor girişi panele yönlendirir; üst menüde Doktor paneli var, hasta menüsü yok', async () => {
    serve(loginHandler('DOCTOR'))
    await signIn('DOCTOR')

    expect(await screen.findByRole('heading', { name: 'Doktor paneli' })).toBeInTheDocument()
    expect(window.location.pathname).toBe('/doktor')
    const nav = screen.getByRole('navigation', { name: 'Ana menü' })
    expect(within(nav).getByRole('link', { name: 'Doktor paneli' })).toBeInTheDocument()
    expect(within(nav).queryByRole('link', { name: 'Randevu al' })).not.toBeInTheDocument()
  })

  it('hasta panele giremez: açıklayıcı mesaj gösterilir ve doktor uçlarına istek atılmaz', async () => {
    serve(loginHandler('PATIENT'))
    await signIn('PATIENT')
    await screen.findByRole('heading', { name: 'Randevularım' })

    window.history.pushState({}, '', '/doktor')
    window.dispatchEvent(new PopStateEvent('popstate'))

    expect(await screen.findByRole('heading', { name: 'Bu sayfaya erişiminiz yok' })).toBeInTheDocument()
    expect(calls('GET', '/api/doctors/me').length).toBe(0)
  })

  it('bekleyen talebi onaylar: POST approve atılır ve liste yenilenir', async () => {
    let approved = false
    serve(
      loginHandler('DOCTOR'),
      (url, init) => (url.endsWith('/api/appointments/a1/approve') && init?.method === 'POST' ? ((approved = true), new Response(null, { status: 204 })) : undefined),
      (url) => (url.startsWith('/api/doctors/me/appointments') ? json(approved ? [] : [appointment()], { headers: { 'Content-Type': 'application/json', 'X-Total-Count': approved ? '0' : '1' } }) : undefined),
    )
    await signIn('DOCTOR')

    expect(await screen.findByText('Ali Veli', { exact: false })).toBeInTheDocument()
    expect(screen.getByText('Hasta notu: Baş ağrısı')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Onayla' }))

    await waitFor(() => expect(calls('POST', '/api/appointments/a1/approve').length).toBe(1))
    expect(await screen.findByText('Onay bekleyen talep yok.')).toBeInTheDocument()
  })

  it('reddetmek için neden zorunlu: nedensiz gönderilemez, nedenle POST reject gövdesinde reason gider', async () => {
    serve(
      loginHandler('DOCTOR'),
      (url, init) => (url.endsWith('/api/appointments/a1/reject') && init?.method === 'POST' ? new Response(null, { status: 204 }) : undefined),
      (url) => (url.startsWith('/api/doctors/me/appointments') ? json([appointment()], { headers: { 'Content-Type': 'application/json', 'X-Total-Count': '1' } }) : undefined),
    )
    await signIn('DOCTOR')

    await userEvent.click(await screen.findByRole('button', { name: 'Reddet' }))
    const submit = screen.getByRole('button', { name: 'Talebi reddet' })
    expect(submit).toBeDisabled()

    await userEvent.type(screen.getByLabelText('Red nedeni (hastaya iletilir)'), 'Bu tarihte yokum')
    await userEvent.click(submit)

    await waitFor(() => expect(calls('POST', '/reject').length).toBe(1))
    expect(JSON.parse(calls('POST', '/reject')[0][1]?.body as string)).toEqual({ reason: 'Bu tarihte yokum' })
  })

  it('API hatası (409) kartta Türkçe mesajla gösterilir', async () => {
    serve(
      loginHandler('DOCTOR'),
      (url, init) =>
        url.endsWith('/api/appointments/a1/approve') && init?.method === 'POST'
          ? json({ title: 'Çakışma', detail: 'Bu randevu artık onaylanamaz.', code: 'appointment.invalid_transition', traceId: 'tr-9' }, { status: 409 })
          : undefined,
      (url) => (url.startsWith('/api/doctors/me/appointments') ? json([appointment()], { headers: { 'Content-Type': 'application/json', 'X-Total-Count': '1' } }) : undefined),
    )
    await signIn('DOCTOR')

    await userEvent.click(await screen.findByRole('button', { name: 'Onayla' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Bu randevu artık onaylanamaz.')
    expect(screen.getByRole('alert')).toHaveTextContent('tr-9')
  })

  it('başlamamış onaylı randevu tamamlanamaz (düğme kapalı); iptal neden ister', async () => {
    serve(
      loginHandler('DOCTOR'),
      (url) =>
        url.startsWith('/api/doctors/me/appointments')
          ? json([appointment({ status: 'APPROVED' })], { headers: { 'Content-Type': 'application/json', 'X-Total-Count': '1' } })
          : undefined,
    )
    await signIn('DOCTOR')

    expect(await screen.findByRole('button', { name: 'Tamamlandı olarak işaretle' })).toBeDisabled()
    await userEvent.click(screen.getByRole('button', { name: 'İptal et' }))
    expect(screen.getByLabelText('İptal nedeni (hastaya iletilir)')).toBeInTheDocument()
  })

  it('hizmet talebi: yalnız henüz alınmamış hizmetler listelenir; POST services gönderilir; onaylı hizmet geri çekilemez', async () => {
    serve(
      loginHandler('DOCTOR'),
      (url, init) => (url === '/api/doctors/me/services' && init?.method === 'POST' ? new Response(null, { status: 202 }) : undefined),
      (url) => (url === '/api/doctors/me/services' ? json([{ serviceId: 's1', serviceName: 'Genel Muayene', status: 'APPROVED', requestedAt: '2029-01-01T00:00:00Z', decidedAt: '2029-01-02T00:00:00Z' }]) : undefined),
      (url) => (url === '/api/doctors/me' ? json({ id: 'd1', hospitalId: 'h1', firstName: 'Ayşe', lastName: 'Yılmaz', email: 'd@x.com', departmentId: 'dep', departmentName: 'Dahiliye', specialty: 'Dahiliye', diplomaNo: '1', phone: null, isActive: true, services: [] }) : undefined),
      (url) =>
        url === '/api/catalog/hospitals/h1/services'
          ? json([
              { id: 's1', departmentId: 'dep', name: 'Genel Muayene', durationMinutes: 15, price: 500, currency: 'TRY' },
              { id: 's2', departmentId: 'dep', name: 'Kan Tahlili', durationMinutes: 10, price: 250, currency: 'TRY' },
            ])
          : undefined,
    )
    await signIn('DOCTOR')
    await userEvent.click(await screen.findByRole('link', { name: 'Hizmetlerim' }))

    const select = await screen.findByLabelText('Yeni hizmet talebi')
    await waitFor(() => expect(within(select).getByRole('option', { name: /Kan Tahlili/ })).toBeInTheDocument())
    expect(within(select).queryByRole('option', { name: /Genel Muayene/ })).not.toBeInTheDocument()
    expect(screen.getByText('Onaylı hizmeti yalnız yönetici kaldırabilir.')).toBeInTheDocument()

    await userEvent.selectOptions(select, 's2')
    await userEvent.click(screen.getByRole('button', { name: 'Talep et' }))

    await waitFor(() => expect(calls('POST', '/api/doctors/me/services').length).toBe(1))
    expect(JSON.parse(calls('POST', '/api/doctors/me/services')[0][1]?.body as string)).toEqual({ serviceId: 's2' })
    expect(await screen.findByText('Talebiniz yöneticiye iletildi.')).toBeInTheDocument()
  })

  it('uygunluk penceresi eklenir: gün/saat/tarih API gövdesine doğru gider', async () => {
    serve(
      loginHandler('DOCTOR'),
      (url, init) => (url === '/api/doctors/me/availability' && init?.method === 'POST' ? json({ id: 'v1' }, { status: 201 }) : undefined),
    )
    await signIn('DOCTOR')
    await userEvent.click(await screen.findByRole('link', { name: 'Uygunluk ve izin' }))

    await userEvent.selectOptions(await screen.findByLabelText('Gün'), '3')
    await userEvent.click(screen.getByRole('button', { name: 'Pencere ekle' }))

    await waitFor(() => expect(calls('POST', '/api/doctors/me/availability').length).toBe(1))
    expect(JSON.parse(calls('POST', '/api/doctors/me/availability')[0][1]?.body as string)).toMatchObject({ day: 3, startTime: '09:00', endTime: '12:00', validTo: null })
  })
})
