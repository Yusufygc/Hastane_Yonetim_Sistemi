import { useState, type FormEvent } from 'react'
import { ErrorMessage } from './ErrorMessage'

interface Props {
  label: string
  submitLabel: string
  busyLabel: string
  onSubmit: (reason: string) => Promise<void>
  onCancel?: () => void
  /** Nedensiz gönderilemez (backend de reddeder); varsayılan true. */
  required?: boolean
  danger?: boolean
}

/** Gerekçe isteyen küçük form: iptal, red gibi işlemler. Hata ProblemDetails mesajıyla gösterilir. */
export function ReasonForm({ label, submitLabel, busyLabel, onSubmit, onCancel, required = true, danger = true }: Props) {
  const [reason, setReason] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<unknown>()

  async function submit(event: FormEvent) {
    event.preventDefault()
    setBusy(true)
    setError(undefined)
    try {
      await onSubmit(reason.trim())
    } catch (e) {
      setError(e)
      setBusy(false)
    }
  }

  return (
    <form className="reason-form" onSubmit={submit}>
      <ErrorMessage error={error} />
      <label>
        {label}
        <input required={required} maxLength={500} value={reason} onChange={(e) => setReason(e.target.value)} />
      </label>
      <div className="row">
        <button className={`btn ${danger ? 'btn-danger' : 'btn-primary'}`} type="submit" disabled={busy || (required && reason.trim() === '')}>
          {busy ? busyLabel : submitLabel}
        </button>
        {onCancel && (
          <button className="btn btn-ghost" type="button" onClick={onCancel} disabled={busy}>
            Vazgeç
          </button>
        )}
      </div>
    </form>
  )
}
