import type { AppointmentStatus } from '../api/endpoints'

const pad = (n: number) => String(n).padStart(2, '0')

/** Yerel tarihi API'nin beklediği yyyy-MM-dd biçimine çevirir (toISOString UTC'ye kaydırır, kullanılmaz). */
export function toDateInput(date: Date): string {
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`
}

export function addDays(date: Date, days: number): Date {
  const copy = new Date(date)
  copy.setDate(copy.getDate() + days)
  return copy
}

/** Randevu penceresi: bugünden itibaren en çok 60 gün ([[randevu-kurallari]] ufuk kuralı). */
export const BOOKING_HORIZON_DAYS = 60

export function bookingRange(now = new Date()): { min: string; max: string } {
  return { min: toDateInput(now), max: toDateInput(addDays(now, BOOKING_HORIZON_DAYS)) }
}

export function formatMoney(price: number, currency: string): string {
  try {
    return new Intl.NumberFormat('tr-TR', { style: 'currency', currency }).format(price)
  } catch {
    return `${price} ${currency}`
  }
}

/** Şube saat dilimindeki tarih-saat ("26 Ekim 2026 Pazartesi, 09:15"). */
export function formatDateTime(iso: string, timeZone?: string): string {
  return new Intl.DateTimeFormat('tr-TR', { dateStyle: 'full', timeStyle: 'short', timeZone }).format(new Date(iso))
}

export function formatDateLong(date: string): string {
  const [y, m, d] = date.split('-').map(Number)
  return new Intl.DateTimeFormat('tr-TR', { dateStyle: 'full' }).format(new Date(y, m - 1, d))
}

export const STATUS_LABELS: Record<AppointmentStatus, string> = {
  PENDING: 'Onay bekliyor',
  APPROVED: 'Onaylandı',
  REJECTED: 'Reddedildi',
  CANCELLED: 'İptal edildi',
  COMPLETED: 'Tamamlandı',
}

export function statusLabel(status: string): string {
  return STATUS_LABELS[status as AppointmentStatus] ?? status
}

/** Hasta yalnız bekleyen veya onaylı randevuyu iptal edebilir (süre sınırını backend denetler). */
export function isCancellable(status: string): boolean {
  return status === 'PENDING' || status === 'APPROVED'
}
