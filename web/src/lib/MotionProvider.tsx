import { useCallback, useMemo, useState, type ReactNode } from 'react'
import { MOTION_KEY, MotionContext, loadMode, shouldSimplify, type MotionMode } from './motion'

export function MotionProvider({ children }: { children: ReactNode }) {
  const [mode, setModeState] = useState<MotionMode>(loadMode)

  const setMode = useCallback((next: MotionMode) => {
    setModeState(next)
    try {
      localStorage.setItem(MOTION_KEY, next)
    } catch {
      /* kalıcı olmasa da bu oturumda geçerli */
    }
  }, [])

  const value = useMemo(() => ({ mode, simple: mode === 'simple' || (mode === 'auto' && shouldSimplify()), setMode }), [mode, setMode])
  return <MotionContext.Provider value={value}>{children}</MotionContext.Provider>
}
