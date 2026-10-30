import { createContext, useContext } from 'react'

/** Kullanıcının seçtiği tema tercihi. 'system': işletim sistemi tercihini izler. */
export type ThemeMode = 'system' | 'light' | 'dark'

/** Ekrana fiilen uygulanan tema. */
export type ResolvedTheme = 'light' | 'dark'

export const THEME_KEY = 'hospital.theme'

export function loadThemeMode(): ThemeMode {
  try {
    const value = localStorage.getItem(THEME_KEY)
    return value === 'light' || value === 'dark' || value === 'system' ? value : 'system'
  } catch {
    return 'system'
  }
}

export function getSystemTheme(): ResolvedTheme {
  try {
    return typeof matchMedia === 'function' && matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'
  } catch {
    return 'light'
  }
}

export function resolveTheme(mode: ThemeMode): ResolvedTheme {
  return mode === 'system' ? getSystemTheme() : mode
}

export interface ThemeContextValue {
  mode: ThemeMode
  resolved: ResolvedTheme
  setMode: (mode: ThemeMode) => void
  cycleMode: () => void
}

export const ThemeContext = createContext<ThemeContextValue | null>(null)

export function useTheme(): ThemeContextValue {
  const context = useContext(ThemeContext)
  if (!context) {
    // Testlerde ThemeProvider dışında tekil bileşen render edilirse güvenli varsayılan döner.
    const mode = loadThemeMode()
    return {
      mode,
      resolved: resolveTheme(mode),
      setMode: () => undefined,
      cycleMode: () => undefined,
    }
  }
  return context
}
