import { useMemo, useState } from 'react'
import { doctor } from '../../api/endpoints'
import { ErrorMessage } from '../../components/ErrorMessage'
import { addDays, formatDateLong, localDayKey, toDateInput } from '../../lib/format'
import { useReload } from '../../lib/reload'
import { useAsync } from '../../lib/useAsync'
import { AppointmentCard } from './AppointmentCard'

/** Haftanın Pazartesi 00:00'ı (yerel). */
function startOfWeek(date: Date): Date {
  const copy = new Date(date.getFullYear(), date.getMonth(), date.getDate())
  const shift = (copy.getDay() + 6) % 7
  copy.setDate(copy.getDate() - shift)
  return copy
}

export default function DoctorSchedule() {
  const [weekStart, setWeekStart] = useState(() => startOfWeek(new Date()))
  const [version, reload] = useReload()

  const from = weekStart.toISOString()
  const to = addDays(weekStart, 7).toISOString()
  const result = useAsync((signal) => doctor.schedule({ from, to }, signal).then((r) => r.data), [from, to, version])

  const days = useMemo(() => {
    const groups = new Map<string, NonNullable<typeof result.data>>()
    for (const a of result.data ?? []) {
      const key = localDayKey(a.startAt)
      groups.set(key, [...(groups.get(key) ?? []), a])
    }
    return [...groups.entries()].sort(([a], [b]) => a.localeCompare(b))
  }, [result.data])

  const lastDay = addDays(weekStart, 6)

  return (
    <>
      <h2>Takvim</h2>
      <p className="page-note">Yalnız onaylı randevular gösterilir.</p>
      <div className="week-nav">
        <button type="button" className="btn" onClick={() => setWeekStart((w) => addDays(w, -7))}>
          ← Önceki hafta
        </button>
        <strong>
          {toDateInput(weekStart)} – {toDateInput(lastDay)}
        </strong>
        <button type="button" className="btn" onClick={() => setWeekStart((w) => addDays(w, 7))}>
          Sonraki hafta →
        </button>
        <button type="button" className="btn btn-ghost" onClick={() => setWeekStart(startOfWeek(new Date()))}>
          Bu hafta
        </button>
      </div>

      {result.loading && (
        <p className="page-note" role="status">
          Takvim yükleniyor…
        </p>
      )}
      <ErrorMessage error={result.error} />
      {result.data && days.length === 0 && <p className="page-note">Bu hafta onaylı randevu yok.</p>}

      {days.map(([day, items]) => (
        <section key={day} className="day-group" aria-labelledby={`gun-${day}`}>
          <h3 id={`gun-${day}`}>{formatDateLong(day)}</h3>
          <ul className="appointment-list">
            {items.map((a) => (
              <AppointmentCard key={a.id} appointment={a} onChanged={reload} />
            ))}
          </ul>
        </section>
      ))}
    </>
  )
}
