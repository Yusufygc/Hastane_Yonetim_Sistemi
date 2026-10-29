import { render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { session } from './api/http'
import App from './App'
import { calls, fetchMock, json, loginHandler, paged, serve, signIn, type Handler } from './test/helpers'

/**
 * Rol × rota erişim matrisi (arayüz kilidi). Kilit yalnız görseldir (yetkiyi backend uygular: 403/404), ama yanlış rolle girilen
 * sayfa açıklayıcı bir mesaj göstermeli ve o role ait API uçlarına hiç istek atılmamalıdır.
 */

const NO_ACCESS = 'Bu sayfaya erişiminiz yok'
const hospitals: Handler = (url) => (url === '/api/hospitals' ? json([{ id: 'h1', organizationId: 'o1', name: 'Merkez Şube', address: null, phone: null, timeZoneId: 'Europe/Istanbul', isActive: true }]) : undefined)

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

/** Girişten sonra istenen sayfaya gider (SPA içi gezinme, sayfa yenilemeden). */
async function visit(path: string) {
  window.history.pushState({}, '', path)
  window.dispatchEvent(new PopStateEvent('popstate'))
}

async function asRole(role: 'PATIENT' | 'DOCTOR' | 'ADMIN') {
  serve(loginHandler(role), hospitals)
  await signIn(role)
  // Girişten sonra rolün ana ekranı açılır; sayfanın yüklenmesini bekle.
  await waitFor(() => expect(window.location.pathname).not.toBe('/giris'))
}

describe('rol x rota erişimi', () => {
  it.each([
    ['/doktor', 'Doktor paneli'],
    ['/yonetim', 'Yönetim paneli'],
    ['/randevularim', 'Randevularım'],
  ])('anonim kullanıcı %s sayfasına girmeye çalışınca giriş sayfasına yönlendirilir (%s)', async (path) => {
    serve()
    window.history.pushState({}, '', path)
    render(<App />)

    expect(await screen.findByRole('heading', { name: 'Giriş yap' })).toBeInTheDocument()
    expect(calls('GET', '/api/doctors/me').length).toBe(0)
    expect(calls('GET', '/api/hospitals').length).toBe(0)
  })

  it('HASTA doktor paneline girmeye çalışır: engellenir, doktor uçlarına istek atılmaz', async () => {
    await asRole('PATIENT')
    await visit('/doktor')

    expect(await screen.findByRole('heading', { name: NO_ACCESS })).toBeInTheDocument()
    expect(screen.getByText(/Doktor paneli yalnız ilgili rolle giriş yapanlara açıktır/)).toBeInTheDocument()
    expect(calls('GET', '/api/doctors/me').length).toBe(0)
  })

  it('HASTA yönetim paneline girmeye çalışır: engellenir, yönetici uçlarına istek atılmaz', async () => {
    await asRole('PATIENT')
    await visit('/yonetim')

    expect(await screen.findByRole('heading', { name: NO_ACCESS })).toBeInTheDocument()
    expect(calls('GET', '/api/hospitals').length).toBe(0)
    expect(calls('GET', '/api/organization').length).toBe(0)
  })

  it('DOKTOR yönetim paneline girmeye çalışır: engellenir, yönetici uçlarına istek atılmaz', async () => {
    await asRole('DOCTOR')
    await visit('/yonetim')

    expect(await screen.findByRole('heading', { name: NO_ACCESS })).toBeInTheDocument()
    expect(screen.getByText(/Yönetim paneli yalnız ilgili rolle giriş yapanlara açıktır/)).toBeInTheDocument()
    expect(calls('GET', '/api/hospitals').length).toBe(0)
    expect(calls('GET', '/api/organization').length).toBe(0)
  })

  it('DOKTOR hasta sayfası Randevularım\'a girmeye çalışır: engellenir, hasta uçlarına istek atılmaz', async () => {
    await asRole('DOCTOR')
    await visit('/randevularim')

    expect(await screen.findByRole('heading', { name: NO_ACCESS })).toBeInTheDocument()
    expect(calls('GET', '/api/patients/me/appointments').length).toBe(0)
  })

  it('YÖNETİCİ doktor paneline ve Randevularım\'a girmeye çalışır: ikisi de engellenir', async () => {
    await asRole('ADMIN')

    await visit('/doktor')
    expect(await screen.findByRole('heading', { name: NO_ACCESS })).toBeInTheDocument()
    expect(calls('GET', '/api/doctors/me').length).toBe(0)

    await visit('/randevularim')
    expect(await screen.findByText(/Randevularım sayfası yalnız ilgili rolle/)).toBeInTheDocument()
    expect(calls('GET', '/api/patients/me/appointments').length).toBe(0)
  })

  it('her rol KENDİ sayfasına girebilir (kilit meşru kullanıcıyı engellemez)', async () => {
    serve(loginHandler('PATIENT'), hospitals)
    await signIn('PATIENT')
    expect(await screen.findByRole('heading', { name: 'Randevularım' })).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: NO_ACCESS })).not.toBeInTheDocument()
  })

  it('doktor ve yönetici girişten sonra kendi paneline düşer', async () => {
    serve(loginHandler('DOCTOR'), (url) => (url.startsWith('/api/doctors/me/appointments') ? paged([]) : undefined))
    await signIn('DOCTOR')
    expect(await screen.findByRole('heading', { name: 'Doktor paneli' })).toBeInTheDocument()
    expect(window.location.pathname).toBe('/doktor')
  })
})
