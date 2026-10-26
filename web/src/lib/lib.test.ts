import { describe, expect, it } from 'vitest'
import { patchDraft } from './draft'
import { addDays, bookingRange, formatMoney, isCancellable, statusLabel, toDateInput } from './format'

describe('tarih yardımcıları', () => {
  it('toDateInput yerel tarihi yyyy-MM-dd verir (UTC kaymasız)', () => {
    expect(toDateInput(new Date(2026, 0, 5, 0, 30))).toBe('2026-01-05')
    expect(toDateInput(new Date(2026, 10, 2, 23, 59))).toBe('2026-11-02')
  })

  it('bookingRange bugünden 60 gün ileriyi verir', () => {
    expect(bookingRange(new Date(2026, 9, 26))).toEqual({ min: '2026-10-26', max: '2026-12-25' })
  })

  it('addDays ay sonunu aşar', () => {
    expect(toDateInput(addDays(new Date(2026, 0, 30), 3))).toBe('2026-02-02')
  })
})

describe('biçimleyiciler', () => {
  it('para birimini tr-TR biçimler', () => {
    expect(formatMoney(500, 'TRY').replace(/\s/g, ' ')).toContain('500')
  })

  it('durum etiketleri Türkçe, bilinmeyen durum olduğu gibi', () => {
    expect(statusLabel('PENDING')).toBe('Onay bekliyor')
    expect(statusLabel('APPROVED')).toBe('Onaylandı')
    expect(statusLabel('YENI')).toBe('YENI')
  })

  it('yalnız bekleyen ve onaylı randevu iptal edilebilir', () => {
    expect(['PENDING', 'APPROVED'].every(isCancellable)).toBe(true)
    expect(['REJECTED', 'CANCELLED', 'COMPLETED'].some(isCancellable)).toBe(false)
  })
})

describe('randevu taslağı', () => {
  const full = { hospitalId: 'h', serviceId: 's', doctorId: 'd', date: '2026-10-28', startAt: 't' }

  it('üst seçim değişince alttakiler silinir', () => {
    expect(patchDraft(full, 'serviceId', 's2')).toEqual({ hospitalId: 'h', serviceId: 's2' })
    expect(patchDraft(full, 'date', '2026-10-29')).toEqual({ hospitalId: 'h', serviceId: 's', doctorId: 'd', date: '2026-10-29' })
    expect(patchDraft(full, 'hospitalId', 'h2')).toEqual({ hospitalId: 'h2' })
  })

  it('seçimi kaldırmak da alttakileri siler', () => {
    expect(patchDraft(full, 'startAt', undefined)).toEqual({ hospitalId: 'h', serviceId: 's', doctorId: 'd', date: '2026-10-28' })
  })
})
