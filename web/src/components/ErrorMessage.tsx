import { ApiError } from '../api/http'

/** API hatasını Türkçe mesaj + (varsa) izleme kimliğiyle gösterir; ekran okuyucuya hemen okunur (role=alert). */
export function ErrorMessage({ error }: { error: unknown }) {
  if (!error) return null
  const message = error instanceof ApiError ? error.message : 'Beklenmeyen bir hata oluştu.'
  const traceId = error instanceof ApiError ? error.traceId : null
  return (
    <div className="alert" role="alert">
      <p>{message}</p>
      {traceId && (
        <p className="alert-trace">
          Sorun sürerse bu kodu iletin: <code>{traceId}</code>
        </p>
      )}
    </div>
  )
}
