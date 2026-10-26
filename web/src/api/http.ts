import type { components } from './schema'

export type Schemas = components['schemas']

/** API'nin standart hata gövdesi (ProblemDetails + code, traceId, errors) ile oluşan hata. */
export class ApiError extends Error {
  readonly status: number
  readonly code: string | null
  readonly traceId: string | null
  /** Doğrulama hatalarında alan adı → mesajlar. */
  readonly fieldErrors: Record<string, string[]>

  constructor(status: number, message: string, code: string | null = null, traceId: string | null = null, fieldErrors: Record<string, string[]> = {}) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.code = code
    this.traceId = traceId
    this.fieldErrors = fieldErrors
  }
}

const REFRESH_KEY = 'hospital.refreshToken'
const baseUrl = (import.meta.env.VITE_API_URL ?? '').replace(/\/$/, '')

/**
 * Oturum belirteçleri. Erişim belirteci yalnız bellekte tutulur (sayfa yenilenince kaybolur, XSS ile kalıcı çalınmaz).
 * Yenileme belirteci oturum süresince (sekme kapanana dek) sessionStorage'dadır: sayfa yenilenince sessizce yeni erişim
 * belirteci alınır. Gerçek yetki denetimi her zaman backend'dedir.
 */
export const session = {
  accessToken: null as string | null,

  get refreshToken(): string | null {
    try {
      return sessionStorage.getItem(REFRESH_KEY)
    } catch {
      return null
    }
  },

  set(tokens: Pick<Schemas['AuthTokens'], 'accessToken' | 'refreshToken'>) {
    this.accessToken = tokens.accessToken
    try {
      sessionStorage.setItem(REFRESH_KEY, tokens.refreshToken)
    } catch {
      /* sessionStorage kapalıysa oturum yalnız bellekte yaşar */
    }
  },

  clear() {
    this.accessToken = null
    try {
      sessionStorage.removeItem(REFRESH_KEY)
    } catch {
      /* yok say */
    }
  },
}

/** Yenileme başarısız olup oturum bittiğinde tetiklenir; AuthProvider dinler. */
export const SESSION_EXPIRED_EVENT = 'hospital:session-expired'

type Query = Record<string, string | number | undefined | null>

export interface RequestOptions {
  body?: unknown
  query?: Query
  /** false: Authorization başlığı eklenmez ve 401'de yenileme denenmez (giriş, kayıt, katalog). */
  auth?: boolean
  signal?: AbortSignal
}

export interface ApiResponse<T> {
  data: T
  /** X-Total-Count (sayfalı uçlarda), yoksa null. */
  total: number | null
  correlationId: string | null
}

function buildUrl(path: string, query?: Query): string {
  const url = `${baseUrl}${path}`
  if (!query) return url
  const params = new URLSearchParams()
  for (const [key, value] of Object.entries(query)) {
    if (value !== undefined && value !== null && value !== '') params.set(key, String(value))
  }
  const qs = params.toString()
  return qs ? `${url}?${qs}` : url
}

async function toApiError(response: Response): Promise<ApiError> {
  const correlationId = response.headers.get('X-Correlation-Id')
  try {
    const problem = (await response.json()) as { title?: string; detail?: string; code?: string; traceId?: string; errors?: Record<string, string[]> }
    return new ApiError(response.status, problem.detail || problem.title || 'İstek başarısız oldu.', problem.code ?? null, problem.traceId ?? correlationId, problem.errors ?? {})
  } catch {
    return new ApiError(response.status, `İstek başarısız oldu (${response.status}).`, null, correlationId)
  }
}

let refreshing: Promise<boolean> | null = null

/** Süresi dolan erişim belirtecini yeniler; eşzamanlı 401'ler tek bir yenileme isteğini paylaşır (belirteç döndürme kuralı). */
export function refreshSession(): Promise<boolean> {
  if (refreshing) return refreshing

  refreshing = (async () => {
    const refreshToken = session.refreshToken
    if (!refreshToken) return false
    try {
      const response = await fetch(buildUrl('/api/auth/refresh'), {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken }),
      })
      if (!response.ok) {
        session.clear()
        return false
      }
      session.set((await response.json()) as Schemas['AuthTokens'])
      return true
    } catch {
      return false // ağ hatası: oturumu silme, kullanıcı yeniden deneyebilir
    }
  })().finally(() => {
    refreshing = null
  })

  return refreshing
}

export async function request<T>(method: string, path: string, options: RequestOptions = {}): Promise<ApiResponse<T>> {
  const { body, query, auth = true, signal } = options

  const send = () => {
    const headers: Record<string, string> = {}
    if (body !== undefined) headers['Content-Type'] = 'application/json'
    if (auth && session.accessToken) headers.Authorization = `Bearer ${session.accessToken}`
    return fetch(buildUrl(path, query), { method, headers, body: body === undefined ? undefined : JSON.stringify(body), signal })
  }

  let response: Response
  try {
    response = await send()
    if (response.status === 401 && auth && session.refreshToken) {
      if (await refreshSession()) {
        response = await send()
      } else if (!session.refreshToken) {
        window.dispatchEvent(new Event(SESSION_EXPIRED_EVENT))
      }
    }
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') throw error
    throw new ApiError(0, 'Sunucuya ulaşılamadı. İnternet bağlantınızı kontrol edip tekrar deneyin.')
  }

  if (!response.ok) throw await toApiError(response)

  const totalHeader = response.headers.get('X-Total-Count')
  const data = response.status === 204 ? (undefined as T) : ((await response.json()) as T)
  return { data, total: totalHeader === null ? null : Number(totalHeader), correlationId: response.headers.get('X-Correlation-Id') }
}
