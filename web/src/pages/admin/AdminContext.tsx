import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from 'react'
import { admin, type AdminHospital } from '../../api/admin'
import { ErrorMessage } from '../../components/ErrorMessage'
import { useReload } from '../../lib/reload'
import { useAsync } from '../../lib/useAsync'

const KEY = 'hospital.adminHospital'

interface AdminContextValue {
  hospitals: AdminHospital[]
  /** Şube listelerinin süzüldüğü seçili şube (şube yöneticisinde tek şube). */
  hospitalId: string | undefined
  hospital: AdminHospital | undefined
  selectHospital: (id: string) => void
  /** Şube/kurum ekleme-güncelleme yalnız süper yöneticide açık (GET /api/organization yalnız ona 200 döner). */
  isSuper: boolean
  organizationName: string | undefined
  reloadHospitals: () => void
}

const AdminContext = createContext<AdminContextValue | null>(null)

function loadSelected(): string | undefined {
  try {
    return sessionStorage.getItem(KEY) ?? undefined
  } catch {
    return undefined
  }
}

/** Yönetim panelinin ortak verisi: erişilebilir şubeler, seçili şube ve süper yönetici bilgisi. */
export function AdminProvider({ children }: { children: ReactNode }) {
  const [version, reloadHospitals] = useReload()
  const hospitals = useAsync((signal) => admin.hospitals(signal).then((r) => r.data), [version])
  // 403 = şube yöneticisi (hata değil bilgi); yalnız süper yönetici kurumu okuyabilir.
  const organization = useAsync(
    (signal) =>
      admin.organization(signal).then(
        (r) => r.data,
        () => null,
      ),
    [],
  )
  const [selected, setSelected] = useState<string | undefined>(loadSelected)

  const selectHospital = useCallback((id: string) => {
    setSelected(id)
    try {
      sessionStorage.setItem(KEY, id)
    } catch {
      /* depolama kapalıysa seçim bu sayfa ömrüyle sınırlı kalır */
    }
  }, [])

  const list = hospitals.data
  const effective = list?.find((h) => h.id === selected)?.id ?? list?.find((h) => h.isActive)?.id ?? list?.[0]?.id

  const value = useMemo<AdminContextValue>(
    () => ({
      hospitals: list ?? [],
      hospitalId: effective,
      hospital: list?.find((h) => h.id === effective),
      selectHospital,
      isSuper: !!organization.data,
      organizationName: organization.data?.name,
      reloadHospitals,
    }),
    [list, effective, selectHospital, organization.data, reloadHospitals],
  )

  if (hospitals.loading && !list) {
    return (
      <p className="page-note" role="status">
        Şubeler yükleniyor…
      </p>
    )
  }
  if (hospitals.error) return <ErrorMessage error={hospitals.error} />

  return <AdminContext.Provider value={value}>{children}</AdminContext.Provider>
}

// eslint-disable-next-line react-refresh/only-export-components
export function useAdmin(): AdminContextValue {
  const context = useContext(AdminContext)
  if (!context) throw new Error('useAdmin, AdminProvider içinde kullanılmalıdır.')
  return context
}
