import { useCallback, useState } from 'react'

/** Randevu sihirbazının seçimleri; giriş sayfasına gidip dönünce kaybolmasın diye sessionStorage'da tutulur. */
export interface BookingDraft {
  hospitalId?: string
  serviceId?: string
  doctorId?: string
  date?: string
  startAt?: string
}

const KEY = 'hospital.bookingDraft'

function load(): BookingDraft {
  try {
    return JSON.parse(sessionStorage.getItem(KEY) ?? '{}') as BookingDraft
  } catch {
    return {}
  }
}

function save(draft: BookingDraft) {
  try {
    sessionStorage.setItem(KEY, JSON.stringify(draft))
  } catch {
    /* depolama kapalıysa taslak yalnız bellekte kalır */
  }
}

/** Bir üst seçim değişince alttakiler geçersizleşir (hastane → hizmet → doktor → tarih → saat). */
const ORDER: (keyof BookingDraft)[] = ['hospitalId', 'serviceId', 'doctorId', 'date', 'startAt']

export function patchDraft(current: BookingDraft, key: keyof BookingDraft, value: string | undefined): BookingDraft {
  const next: BookingDraft = { ...current }
  const index = ORDER.indexOf(key)
  for (const lower of ORDER.slice(index + 1)) delete next[lower]
  if (value === undefined) delete next[key]
  else next[key] = value
  return next
}

export function clearDraft() {
  try {
    sessionStorage.removeItem(KEY)
  } catch {
    /* yok say */
  }
}

export function useBookingDraft() {
  const [draft, setDraft] = useState<BookingDraft>(load)

  const select = useCallback((key: keyof BookingDraft, value: string | undefined) => {
    setDraft((current) => {
      const next = patchDraft(current, key, value)
      save(next)
      return next
    })
  }, [])

  const reset = useCallback(() => {
    clearDraft()
    setDraft({})
  }, [])

  return { draft, select, reset }
}
