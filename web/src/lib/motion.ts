import { createContext, useContext } from 'react'

/** 3B animasyon tercihi. auto: sistem tercihine ve cihaz gücüne göre karar verilir. */
export type MotionMode = 'auto' | 'full' | 'simple'

export const MOTION_KEY = 'hospital.motionMode'

export function loadMode(): MotionMode {
  try {
    const value = localStorage.getItem(MOTION_KEY)
    return value === 'full' || value === 'simple' ? value : 'auto'
  } catch {
    return 'auto'
  }
}

export function hasWebGL(): boolean {
  try {
    const canvas = document.createElement('canvas')
    return !!(canvas.getContext('webgl2') ?? canvas.getContext('webgl'))
  } catch {
    return false
  }
}

/** Hareket azaltma isteyen, az çekirdekli/az belleği olan veya WebGL'i olmayan cihazlar sadeleştirilmiş modda açılır. */
export function shouldSimplify(): boolean {
  const reduced = typeof matchMedia === 'function' && matchMedia('(prefers-reduced-motion: reduce)').matches
  const nav = navigator as Navigator & { deviceMemory?: number }
  const weak = (nav.hardwareConcurrency ?? 8) <= 2 || (nav.deviceMemory ?? 8) <= 2
  return reduced || weak || !hasWebGL()
}

export interface MotionContextValue {
  mode: MotionMode
  /** true ise 3B sahne çizilmez; içerik düz sayfa olarak gösterilir. */
  simple: boolean
  setMode: (mode: MotionMode) => void
}

export const MotionContext = createContext<MotionContextValue | null>(null)

export function useMotion(): MotionContextValue {
  const context = useContext(MotionContext)
  if (!context) throw new Error('useMotion, MotionProvider içinde kullanılmalıdır.')
  return context
}
