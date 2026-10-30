import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { THEME_KEY, ThemeContext, getSystemTheme, loadThemeMode, type ResolvedTheme, type ThemeMode } from './theme'

const ORDER: ThemeMode[] = ['light', 'dark', 'system']

export function ThemeProvider({ children }: { children: ReactNode }) {
  const [mode, setModeState] = useState<ThemeMode>(loadThemeMode)
  const [systemTheme, setSystemTheme] = useState<ResolvedTheme>(getSystemTheme)

  useEffect(() => {
    if (typeof matchMedia !== 'function') return
    const mq = matchMedia('(prefers-color-scheme: dark)')
    const onChange = (event: MediaQueryListEvent) => setSystemTheme(event.matches ? 'dark' : 'light')
    mq.addEventListener?.('change', onChange)
    return () => mq.removeEventListener?.('change', onChange)
  }, [])

  const resolved: ResolvedTheme = mode === 'system' ? systemTheme : mode

  useEffect(() => {
    const root = document.documentElement
    root.dataset.theme = resolved
    root.dataset.themeMode = mode
    root.style.colorScheme = resolved
  }, [mode, resolved])

  const setMode = useCallback((next: ThemeMode) => {
    setModeState(next)
    try {
      localStorage.setItem(THEME_KEY, next)
    } catch {
      /* kalıcı olmasa da bu oturumda geçerli */
    }
  }, [])

  const cycleMode = useCallback(() => {
    setModeState((prev) => {
      const next = ORDER[(ORDER.indexOf(prev) + 1) % ORDER.length]
      try {
        localStorage.setItem(THEME_KEY, next)
      } catch {
        /* yoksay */
      }
      return next
    })
  }, [])

  const value = useMemo(() => ({ mode, resolved, setMode, cycleMode }), [mode, resolved, setMode, cycleMode])
  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>
}
