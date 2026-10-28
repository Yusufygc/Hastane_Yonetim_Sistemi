import { useState } from 'react'
import { ErrorMessage } from './ErrorMessage'

interface Props {
  label: string
  confirmLabel?: string
  busyLabel?: string
  onConfirm: () => Promise<unknown>
  danger?: boolean
  disabled?: boolean
}

/** İki adımlı düğme (önce "emin misiniz?"): geri alınması zor işlemlerde yanlış tıklamayı önler. Hata düğmenin altında gösterilir. */
export function ConfirmAction({ label, confirmLabel = 'Evet, onayla', busyLabel = 'İşleniyor…', onConfirm, danger = false, disabled }: Props) {
  const [asking, setAsking] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<unknown>()

  async function confirm() {
    setBusy(true)
    setError(undefined)
    try {
      await onConfirm()
      setAsking(false)
    } catch (e) {
      setError(e)
    } finally {
      setBusy(false)
    }
  }

  if (!asking) {
    return (
      <>
        <ErrorMessage error={error} />
        <button type="button" className="btn btn-ghost" disabled={disabled} onClick={() => setAsking(true)}>
          {label}
        </button>
      </>
    )
  }

  return (
    <>
      <ErrorMessage error={error} />
      <span className="row" role="group" aria-label={`${label} onayı`}>
        <button type="button" className={`btn ${danger ? 'btn-danger' : 'btn-primary'}`} disabled={busy} onClick={() => void confirm()}>
          {busy ? busyLabel : confirmLabel}
        </button>
        <button type="button" className="btn btn-ghost" disabled={busy} onClick={() => setAsking(false)}>
          Vazgeç
        </button>
      </span>
    </>
  )
}
