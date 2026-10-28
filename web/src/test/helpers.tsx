import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { vi } from 'vitest'
import App from '../App'

export const json = (body: unknown, init: ResponseInit = {}) =>
  new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' }, ...init })

/** Sayfalı liste yanıtı (X-Total-Count başlığıyla). */
export const paged = (items: unknown[], total = items.length) => json(items, { headers: { 'Content-Type': 'application/json', 'X-Total-Count': String(total) } })

export type Handler = (url: string, init?: RequestInit) => Response | Promise<Response> | undefined

export const fetchMock = vi.fn<typeof fetch>()

/** URL'ye göre ilk cevap veren işleyici kazanır; bilinmeyen istek boş sayfalı liste döner. */
export function serve(...handlers: Handler[]) {
  fetchMock.mockImplementation(async (input, init) => {
    const url = String(input)
    for (const h of handlers) {
      const r = h(url, init)
      if (r) return r
    }
    return paged([])
  })
}

export const tokensFor = (role: string) => ({
  accessToken: 'a',
  refreshToken: 'r',
  accessTokenExpiresAt: '',
  refreshTokenExpiresAt: '',
  user: { id: 'u1', email: `${role.toLowerCase()}@x.com`, role },
})

export const loginHandler =
  (role: string): Handler =>
  (url, init) =>
    url === '/api/auth/login' && init?.method === 'POST' ? json(tokensFor(role)) : undefined

export async function signIn(role: 'DOCTOR' | 'PATIENT' | 'ADMIN') {
  window.history.pushState({}, '', '/giris')
  render(<App />)
  await userEvent.type(await screen.findByLabelText('E-posta'), `${role.toLowerCase()}@x.com`)
  await userEvent.type(screen.getByLabelText('Parola'), 'Parola123x')
  await userEvent.click(screen.getByRole('button', { name: 'Giriş yap' }))
}

/** Belirli yöntem + URL parçasıyla atılmış isteklerin listesi. */
export const calls = (method: string, urlPart: string) => fetchMock.mock.calls.filter(([u, i]) => String(u).includes(urlPart) && (i?.method ?? 'GET') === method)

export const bodyOf = (call: Parameters<typeof fetch>) => JSON.parse(call[1]?.body as string)
