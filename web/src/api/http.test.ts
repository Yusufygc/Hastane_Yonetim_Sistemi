import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError, request, session } from './http'

const json = (body: unknown, init: ResponseInit = {}) =>
  new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' }, ...init })

const bearerOf = (call: unknown[]) => ((call[1] as RequestInit | undefined)?.headers as Record<string, string> | undefined)?.Authorization

const tokens = (suffix: string) => ({ accessToken: `access-${suffix}`, refreshToken: `refresh-${suffix}`, accessTokenExpiresAt: '', refreshTokenExpiresAt: '', user: { id: '1', email: 'a@b.c', role: 'PATIENT' } })

describe('http istemcisi', () => {
  const fetchMock = vi.fn<typeof fetch>()

  beforeEach(() => {
    vi.stubGlobal('fetch', fetchMock)
    session.clear()
  })

  afterEach(() => {
    fetchMock.mockReset()
    vi.unstubAllGlobals()
  })

  it('erişim belirtecini Authorization başlığına ekler, auth:false isteklerine eklemez', async () => {
    session.accessToken = 'abc'
    fetchMock.mockImplementation(async () => json({ ok: true })) // her çağrıya yeni Response (gövde bir kez okunur)

    await request('GET', '/api/patients/me')
    await request('GET', '/api/catalog/hospitals', { auth: false })

    expect(bearerOf(fetchMock.mock.calls[0])).toBe('Bearer abc')
    expect(bearerOf(fetchMock.mock.calls[1])).toBeUndefined()
  })

  it('sorgu parametrelerini kodlar, boş değerleri atar ve X-Total-Count başlığını okur', async () => {
    fetchMock.mockResolvedValue(json([], { headers: { 'Content-Type': 'application/json', 'X-Total-Count': '42', 'X-Correlation-Id': 'cid-1' } }))

    const result = await request<unknown[]>('GET', '/api/x', { query: { a: 'ü ş', b: undefined, c: '', page: 2 }, auth: false })

    expect(fetchMock.mock.calls[0][0]).toBe('/api/x?a=%C3%BC+%C5%9F&page=2')
    expect(result.total).toBe(42)
    expect(result.correlationId).toBe('cid-1')
  })

  it('204 yanıtında gövde okumaz', async () => {
    fetchMock.mockResolvedValue(new Response(null, { status: 204 }))
    const result = await request<void>('POST', '/api/appointments/1/cancel', { body: { reason: 'x' } })
    expect(result.data).toBeUndefined()
  })

  it('202 Accepted gibi gövdesiz başarılı yanıt hata vermez', async () => {
    fetchMock.mockImplementation(async () => new Response(null, { status: 202 }))
    const result = await request<void>('POST', '/api/doctors/me/services', { body: { serviceId: 's' } })
    expect(result.data).toBeUndefined()
  })

  it('ProblemDetails gövdesini ApiError olarak fırlatır (mesaj, kod, traceId, alan hataları)', async () => {
    fetchMock.mockResolvedValue(
      json({ title: 'Doğrulama hatası', detail: 'Alanlar geçersiz.', code: 'validation.failed', traceId: 'tr-1', errors: { email: ['Geçersiz e-posta'] } }, { status: 400 }),
    )

    const error = await request('POST', '/api/auth/register', { body: {}, auth: false }).catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ApiError)
    const api = error as ApiError
    expect(api.status).toBe(400)
    expect(api.code).toBe('validation.failed')
    expect(api.traceId).toBe('tr-1')
    expect(api.message).toBe('Alanlar geçersiz.')
    expect(api.fieldErrors.email).toEqual(['Geçersiz e-posta'])
  })

  it('ağ hatasını Türkçe ApiError(0) yapar', async () => {
    fetchMock.mockRejectedValue(new TypeError('Failed to fetch'))
    const error = (await request('GET', '/api/x').catch((e: unknown) => e)) as ApiError
    expect(error.status).toBe(0)
    expect(error.message).toMatch(/Sunucuya ulaşılamadı/)
  })

  it('401 alınca bir kez yeniler ve isteği yeni belirteçle tekrarlar', async () => {
    session.set(tokens('eski'))
    fetchMock
      .mockResolvedValueOnce(new Response(null, { status: 401 })) // ilk istek
      .mockResolvedValueOnce(json(tokens('yeni'))) // refresh
      .mockResolvedValueOnce(json({ id: 'me' })) // tekrar

    const result = await request<{ id: string }>('GET', '/api/auth/me')

    expect(result.data.id).toBe('me')
    expect(fetchMock.mock.calls[1][0]).toBe('/api/auth/refresh')
    expect(JSON.parse(fetchMock.mock.calls[1][1]?.body as string)).toEqual({ refreshToken: 'refresh-eski' })
    expect(bearerOf(fetchMock.mock.calls[2])).toBe('Bearer access-yeni')
    expect(session.refreshToken).toBe('refresh-yeni') // döndürülen yenileme belirteci saklandı
  })

  it('eşzamanlı 401\'ler tek bir yenileme isteği paylaşır (belirteç döndürme tekrar kullanımı engellemesin)', async () => {
    session.set(tokens('eski'))
    let refreshCalls = 0
    fetchMock.mockImplementation(async (input, init) => {
      if (input === '/api/auth/refresh') {
        refreshCalls++
        await new Promise((r) => setTimeout(r, 10))
        return json(tokens('yeni'))
      }
      const bearer = bearerOf([input, init])
      return bearer === 'Bearer access-yeni' ? json({ ok: true }) : new Response(null, { status: 401 })
    })

    await Promise.all([request('GET', '/api/a'), request('GET', '/api/b'), request('GET', '/api/c')])

    expect(refreshCalls).toBe(1)
  })

  it('yenileme reddedilirse oturumu temizler, oturum-bitti olayı yayar ve 401 ApiError fırlatır', async () => {
    session.set(tokens('eski'))
    const expired = vi.fn()
    window.addEventListener('hospital:session-expired', expired)
    fetchMock.mockResolvedValueOnce(new Response(null, { status: 401 })).mockResolvedValueOnce(new Response(null, { status: 401 }))

    const error = (await request('GET', '/api/auth/me').catch((e: unknown) => e)) as ApiError

    expect(error.status).toBe(401)
    expect(session.refreshToken).toBeNull()
    expect(session.accessToken).toBeNull()
    expect(expired).toHaveBeenCalledTimes(1)
    window.removeEventListener('hospital:session-expired', expired)
  })
})
