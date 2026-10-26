import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { auth, type AuthTokens, type RegisterBody } from '../api/endpoints'
import { refreshSession, request, session, SESSION_EXPIRED_EVENT } from '../api/http'

export type AuthUser = AuthTokens['user']

type AuthState = { status: 'loading' } | { status: 'anonymous' } | { status: 'authenticated'; user: AuthUser }

interface AuthContextValue {
  state: AuthState
  login: (email: string, password: string) => Promise<AuthUser>
  register: (body: RegisterBody) => Promise<AuthUser>
  logout: () => Promise<void>
}

const AuthContext = createContext<AuthContextValue | null>(null)

/** Rol bilgisi yalnızca ekranı kişiselleştirmek içindir; yetkiyi backend uygular. */
export function AuthProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<AuthState>({ status: 'loading' })

  // Sayfa yenilenince: sessionStorage'da yenileme belirteci varsa sessizce yeni erişim belirteci al.
  useEffect(() => {
    let active = true
    void (async () => {
      if (!session.refreshToken || !(await refreshSession())) {
        if (active) setState({ status: 'anonymous' })
        return
      }
      const user = await readUser()
      if (active) setState(user ? { status: 'authenticated', user } : { status: 'anonymous' })
    })()
    return () => {
      active = false
    }
  }, [])

  useEffect(() => {
    const onExpired = () => setState({ status: 'anonymous' })
    window.addEventListener(SESSION_EXPIRED_EVENT, onExpired)
    return () => window.removeEventListener(SESSION_EXPIRED_EVENT, onExpired)
  }, [])

  const login = useCallback(async (email: string, password: string) => {
    const { data } = await auth.login(email, password)
    session.set(data)
    setState({ status: 'authenticated', user: data.user })
    return data.user
  }, [])

  const register = useCallback(
    async (body: RegisterBody) => {
      await auth.register(body)
      return login(body.email, body.password)
    },
    [login],
  )

  const logout = useCallback(async () => {
    const refreshToken = session.refreshToken
    try {
      if (refreshToken) await auth.logout(refreshToken)
    } catch {
      /* sunucuya ulaşılamasa da yerel oturum kapanır */
    }
    session.clear()
    setState({ status: 'anonymous' })
  }, [])

  const value = useMemo(() => ({ state, login, register, logout }), [state, login, register, logout])
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

async function readUser(): Promise<AuthUser | null> {
  try {
    const { data } = await request<{ id: string; email: string; role: string }>('GET', '/api/auth/me')
    return { id: data.id, email: data.email, role: data.role }
  } catch {
    return null
  }
}

// eslint-disable-next-line react-refresh/only-export-components
export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext)
  if (!context) throw new Error('useAuth, AuthProvider içinde kullanılmalıdır.')
  return context
}
