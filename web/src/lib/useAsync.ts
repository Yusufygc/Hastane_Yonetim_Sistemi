/* eslint-disable react-hooks/exhaustive-deps -- bağımlılıklar çağıran tarafından açıkça verilir */
import { useEffect, useState } from 'react'

export interface AsyncState<T> {
  data: T | undefined
  error: unknown
  loading: boolean
}

/** `load` değişince önceki isteği iptal eder; yarış durumunda eski yanıt ekranı ezmez. `load` null ise bekler. */
export function useAsync<T>(load: ((signal: AbortSignal) => Promise<T>) | null, deps: unknown[]): AsyncState<T> {
  const [state, setState] = useState<AsyncState<T>>({ data: undefined, error: undefined, loading: load !== null })

  useEffect(() => {
    if (!load) {
      setState({ data: undefined, error: undefined, loading: false })
      return
    }
    const controller = new AbortController()
    setState((s) => ({ data: s.data, error: undefined, loading: true }))
    load(controller.signal).then(
      (data) => !controller.signal.aborted && setState({ data, error: undefined, loading: false }),
      (error: unknown) => !controller.signal.aborted && setState({ data: undefined, error, loading: false }),
    )
    return () => controller.abort()
  }, deps)

  return state
}
