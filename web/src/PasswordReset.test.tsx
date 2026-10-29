import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { session } from './api/http'
import App from './App'
import { bodyOf, calls, fetchMock, json, serve } from './test/helpers'

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

function open(path: string) {
  window.history.pushState({}, '', path)
  render(<App />)
}

describe('şifremi unuttum', () => {
  it('giriş sayfasında "Şifremi unuttum" bağlantısı vardır ve forma götürür', async () => {
    serve()
    open('/giris')

    await userEvent.click(await screen.findByRole('link', { name: 'Şifremi unuttum' }))

    expect(await screen.findByRole('heading', { name: 'Şifremi unuttum' })).toBeInTheDocument()
  })

  it('e-posta gönderilir; başarı mesajı hesap varlığını söylemez ("kayıtlıysa")', async () => {
    serve((url, init) => (url === '/api/auth/forgot-password' && init?.method === 'POST' ? new Response(null, { status: 202 }) : undefined))
    open('/sifremi-unuttum')

    await userEvent.type(await screen.findByLabelText('E-posta'), ' ayse@ornek.test ')
    await userEvent.click(screen.getByRole('button', { name: 'Sıfırlama bağlantısı gönder' }))

    expect(await screen.findByRole('heading', { name: 'E-postanızı kontrol edin' })).toBeInTheDocument()
    expect(screen.getByRole('status')).toHaveTextContent('ayse@ornek.test adresi sistemde kayıtlıysa')
    expect(bodyOf(calls('POST', '/api/auth/forgot-password')[0] as Parameters<typeof fetch>)).toEqual({ email: 'ayse@ornek.test' })
  })

  it('geçersiz e-posta biçimi alanın altında gösterilir', async () => {
    serve((url) =>
      url === '/api/auth/forgot-password'
        ? json({ title: 'Doğrulama hatası', detail: 'Alanlar geçersiz.', code: 'validation.failed', errors: { email: ['Geçerli bir e-posta girin.'] } }, { status: 400 })
        : undefined,
    )
    open('/sifremi-unuttum')

    await userEvent.type(await screen.findByLabelText('E-posta'), 'yanlis')
    await userEvent.click(screen.getByRole('button', { name: 'Sıfırlama bağlantısı gönder' }))

    expect(await screen.findByText('Geçerli bir e-posta girin.')).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'E-postanızı kontrol edin' })).not.toBeInTheDocument()
  })

  it('hız sınırı (429) mesajı gösterilir', async () => {
    serve(() => json({ title: 'Çok fazla istek', detail: 'Çok fazla istek gönderildi. 30 saniye sonra tekrar deneyin.', code: 'rate_limited' }, { status: 429 }))
    open('/sifremi-unuttum')

    await userEvent.type(await screen.findByLabelText('E-posta'), 'ayse@ornek.test')
    await userEvent.click(screen.getByRole('button', { name: 'Sıfırlama bağlantısı gönder' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('30 saniye sonra tekrar deneyin')
  })
})

describe('parola sıfırlama', () => {
  it('belirteç adres çubuğundan silinir; sıfırlama isteği belirteçle gider ve başarıda giriş bağlantısı çıkar', async () => {
    serve((url, init) => (url === '/api/auth/reset-password' && init?.method === 'POST' ? new Response(null, { status: 204 }) : undefined))
    open('/sifre-sifirla?token=GIZLI-TOKEN_123')

    expect(await screen.findByRole('heading', { name: 'Yeni parola belirle' })).toBeInTheDocument()
    await waitFor(() => expect(window.location.search).toBe('')) // belirteç URL'den kaldırıldı (sızmasın)

    await userEvent.type(screen.getByLabelText('Yeni parola'), 'YeniParola9x')
    await userEvent.type(screen.getByLabelText('Yeni parola (tekrar)'), 'YeniParola9x')
    await userEvent.click(screen.getByRole('button', { name: 'Parolayı kaydet' }))

    expect(await screen.findByRole('heading', { name: 'Parolanız değiştirildi' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Giriş yap' })).toHaveAttribute('href', '/giris')
    expect(bodyOf(calls('POST', '/api/auth/reset-password')[0] as Parameters<typeof fetch>)).toEqual({ token: 'GIZLI-TOKEN_123', newPassword: 'YeniParola9x' })
  })

  it('parolalar eşleşmiyorsa gönderme kapalıdır ve istek atılmaz', async () => {
    serve()
    open('/sifre-sifirla?token=T')

    await userEvent.type(await screen.findByLabelText('Yeni parola'), 'YeniParola9x')
    await userEvent.type(screen.getByLabelText('Yeni parola (tekrar)'), 'BaskaParola9y')

    expect(screen.getByText('Parolalar eşleşmiyor.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Parolayı kaydet' })).toBeDisabled()
    expect(calls('POST', '/api/auth/reset-password').length).toBe(0)
  })

  it('belirteç olmadan açılan sayfa "bağlantı geçersiz" der ve yeni bağlantı ister', async () => {
    serve()
    open('/sifre-sifirla')

    expect(await screen.findByRole('heading', { name: 'Bağlantı geçersiz' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Yeni bağlantı iste' })).toHaveAttribute('href', '/sifremi-unuttum')
  })

  it('sunucu belirteci geçersiz bulursa (süresi dolmuş/kullanılmış) "bağlantı geçersiz" ekranına geçer', async () => {
    serve((url) =>
      url === '/api/auth/reset-password'
        ? json({ title: 'Doğrulama hatası', detail: 'Parola sıfırlama bağlantısı geçersiz veya süresi dolmuş.', code: 'auth.invalid_reset_token', errors: { token: ['Bağlantı geçersiz veya süresi dolmuş.'] } }, { status: 400 })
        : undefined,
    )
    open('/sifre-sifirla?token=ESKI')

    await userEvent.type(await screen.findByLabelText('Yeni parola'), 'YeniParola9x')
    await userEvent.type(screen.getByLabelText('Yeni parola (tekrar)'), 'YeniParola9x')
    await userEvent.click(screen.getByRole('button', { name: 'Parolayı kaydet' }))

    expect(await screen.findByRole('heading', { name: 'Bağlantı geçersiz' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Yeni bağlantı iste' })).toBeInTheDocument()
  })

  it('zayıf parola hatası alanın altında gösterilir ve form açık kalır (belirteç harcanmaz)', async () => {
    serve((url) =>
      url === '/api/auth/reset-password'
        ? json({ title: 'Doğrulama hatası', detail: 'Alanlar geçersiz.', code: 'validation.failed', errors: { newPassword: ['Parola en az bir harf ve bir rakam içermelidir.'] } }, { status: 400 })
        : undefined,
    )
    open('/sifre-sifirla?token=T')

    await userEvent.type(await screen.findByLabelText('Yeni parola'), 'sadeceharfler')
    await userEvent.type(screen.getByLabelText('Yeni parola (tekrar)'), 'sadeceharfler')
    await userEvent.click(screen.getByRole('button', { name: 'Parolayı kaydet' }))

    expect(await screen.findByText('Parola en az bir harf ve bir rakam içermelidir.')).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Yeni parola belirle' })).toBeInTheDocument()
  })
})
