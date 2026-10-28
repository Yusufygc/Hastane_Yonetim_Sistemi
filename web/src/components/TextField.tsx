import type { InputHTMLAttributes } from 'react'

interface Props extends Omit<InputHTMLAttributes<HTMLInputElement>, 'onChange' | 'value'> {
  label: string
  value: string
  onChange: (value: string) => void
  /** Alan bazlı doğrulama hatası (ProblemDetails errors). */
  error?: string
}

/** Etiketli metin girdisi; hata varsa `aria-invalid` ve alan altı mesaj. */
export function TextField({ label, value, onChange, error, ...rest }: Props) {
  const id = rest.name ? `${rest.name}-hata` : undefined
  return (
    <label>
      {label}
      <input {...rest} value={value} onChange={(e) => onChange(e.target.value)} aria-invalid={error ? true : undefined} aria-describedby={error ? id : undefined} />
      {error && (
        <span id={id} className="field-error">
          {error}
        </span>
      )}
    </label>
  )
}
