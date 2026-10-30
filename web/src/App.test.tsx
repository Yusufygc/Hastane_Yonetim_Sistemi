import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { session } from './api/http'
import App from './App'

const json = (body: unknown, init: ResponseInit = {}) =>
  new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' }, ...init })

const fetchMock = vi.fn<typeof fetch>()

beforeEach(() => {
  vi.stubGlobal('fetch', fetchMock)
  session.clear()
  localStorage.setItem('hospital.motionMode', 'simple') // jsdom'da WebGL yok; düz sayfa
  localStorage.removeItem('hospital.theme')
})

afterEach(() => {
  fetchMock.mockReset()
  vi.unstubAllGlobals()
  window.history.pushState({}, '', '/')
})

describe('uygulama', () => {
  it('sade modda ana sayfa tek h1 ile içeriği düz gösterir', async () => {
    render(<App />)
    expect(await screen.findByRole('heading', { level: 1, name: /saniyeler içinde/i })).toBeInTheDocument()
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1)
    expect(screen.getByRole('link', { name: 'İçeriğe geç' })).toBeInTheDocument()
  })

  it('3 durumlu tema seçici (Açık / Koyu / Sistem) kök data-theme özniteliğini ve localStorage tercihini günceller', async () => {
    render(<App />)
    const group = await screen.findByRole('group', { name: 'Tema seçimi' })
    const darkBtn = within(group).getByRole('button', { name: 'Koyu' })
    const lightBtn = within(group).getByRole('button', { name: 'Açık' })

    await userEvent.click(darkBtn)
    expect(document.documentElement.dataset.theme).toBe('dark')
    expect(localStorage.getItem('hospital.theme')).toBe('dark')
    expect(darkBtn).toHaveAttribute('aria-pressed', 'true')

    await userEvent.click(lightBtn)
    expect(document.documentElement.dataset.theme).toBe('light')
    expect(localStorage.getItem('hospital.theme')).toBe('light')
    expect(lightBtn).toHaveAttribute('aria-pressed', 'true')
  })

  it('girişsiz Randevularım giriş sayfasına yönlendirir', async () => {
    window.history.pushState({}, '', '/randevularim')
    render(<App />)
    expect(await screen.findByRole('heading', { name: 'Giriş yap' })).toBeInTheDocument()
  })

  it('yanlış parolada API hatasını ve izleme kodunu gösterir', async () => {
    fetchMock.mockResolvedValue(json({ title: 'Kimlik doğrulaması gerekli', detail: 'E-posta veya parola hatalı.', code: 'auth.invalid_credentials', traceId: 'tr-77' }, { status: 401 }))
    window.history.pushState({}, '', '/giris')
    render(<App />)

    await userEvent.type(await screen.findByLabelText('E-posta'), 'a@b.com')
    await userEvent.type(screen.getByLabelText('Parola'), 'yanlis')
    await userEvent.click(screen.getByRole('button', { name: 'Giriş yap' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('E-posta veya parola hatalı.')
    expect(screen.getByRole('alert')).toHaveTextContent('tr-77')
  })

  it('başarılı girişte randevularım listelenir ve kullanıcı e-postası üst çubukta görünür', async () => {
    fetchMock.mockImplementation(async (input) => {
      if (input === '/api/auth/login') {
        return json({ accessToken: 'a', refreshToken: 'r', accessTokenExpiresAt: '', refreshTokenExpiresAt: '', user: { id: '1', email: 'hasta@x.com', role: 'PATIENT' } })
      }
      return json([], { headers: { 'Content-Type': 'application/json', 'X-Total-Count': '0' } })
    })
    window.history.pushState({}, '', '/giris')
    render(<App />)

    await userEvent.type(await screen.findByLabelText('E-posta'), 'hasta@x.com')
    await userEvent.type(screen.getByLabelText('Parola'), 'Parola123x')
    await userEvent.click(screen.getByRole('button', { name: 'Giriş yap' }))

    expect(await screen.findByRole('heading', { name: 'Randevularım' })).toBeInTheDocument()
    await waitFor(() => expect(screen.getByText('Gösterilecek randevu yok.')).toBeInTheDocument())
    expect(screen.getByText('hasta@x.com')).toBeInTheDocument()
  })

  it('randevu sihirbazı şubeleri anonim listeler ve seçilince hizmetleri getirir', async () => {
    fetchMock.mockImplementation(async (input) => {
      if (input === '/api/catalog/hospitals') return json([{ id: 'h1', name: 'Merkez Şube', address: null, phone: null, timeZoneId: 'Europe/Istanbul', workingHours: [] }])
      if (input === '/api/catalog/hospitals/h1/services') return json([{ id: 's1', departmentId: 'd', name: 'Genel Muayene', durationMinutes: 15, price: 500, currency: 'TRY' }])
      return json([])
    })
    window.history.pushState({}, '', '/randevu-al')
    render(<App />)

    await userEvent.click(await screen.findByRole('radio', { name: /Merkez Şube/ }))

    expect(await screen.findByRole('radio', { name: /Genel Muayene/ })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: /Hizmet seçin/ })).toBeInTheDocument()
  })
})
