import { useCallback, useState } from 'react'

/** Veri değişince (onay, silme, ekleme) `useAsync` bağımlılığına koymak için artan sayaç. */
export function useReload(): [number, () => void] {
  const [version, setVersion] = useState(0)
  const reload = useCallback(() => setVersion((v) => v + 1), [])
  return [version, reload]
}
