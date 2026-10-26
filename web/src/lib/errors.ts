import { ApiError } from '../api/http'

/** Doğrulama hatasında ilgili alanın ilk mesajı (alan adları camelCase). */
export function fieldError(error: unknown, field: string): string | undefined {
  return error instanceof ApiError ? error.fieldErrors[field]?.[0] : undefined
}
