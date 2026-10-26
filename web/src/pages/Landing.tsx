import { lazy, Suspense, useEffect, useRef } from 'react'
import { Link } from 'react-router-dom'
import { ErrorBoundary } from '../components/ErrorBoundary'
import { useMotion } from '../lib/motion'

// 3B sahne (three.js) yalnız ana sayfada ve sade mod kapalıyken indirilir: diğer ekranlar hafif kalır.
const HeroScene = lazy(() => import('../three/HeroScene'))

const SECTIONS = [
  {
    title: 'Randevunuz, saniyeler içinde',
    text: 'Sıra beklemeden klinik ve hizmeti seçin, size uygun saati kendiniz belirleyin.',
    cta: { to: '/randevu-al', label: 'Randevu al' },
  },
  {
    title: 'Klinik ve hizmeti seçin',
    text: 'Şubenizi, ihtiyacınız olan muayene veya tetkiki ve süre/ücret bilgisini baştan görün.',
  },
  {
    title: 'Boş saatleri anında görün',
    text: 'Doktorun gerçek müsaitliği gösterilir; aynı saate iki kişi randevu alamaz.',
  },
  {
    title: 'Doktor onaylar, e-posta gelir',
    text: 'Talebiniz doktora iletilir. Onay, red veya iptal durumunda e-posta ile haberdar olursunuz.',
    cta: { to: '/kayit', label: 'Hemen kayıt ol' },
  },
]

/** Aynı içerik hem 3B (üstte kayan) hem düz (sade mod) görünümde kullanılır; gerçek HTML olduğu için erişilebilirdir. */
function Sections({ layered }: { layered: boolean }) {
  return (
    <>
      {SECTIONS.map((section, index) => (
        <section key={section.title} className={`landing-section ${layered ? 'is-layered' : ''} ${index % 2 ? 'is-right' : ''}`} aria-labelledby={`landing-${index}`}>
          <div className="landing-card">
            <p className="landing-step">
              {index + 1} / {SECTIONS.length}
            </p>
            {index === 0 ? (
              <h1 id="landing-0" className="landing-title">
                {section.title}
              </h1>
            ) : (
              <h2 id={`landing-${index}`} className="landing-title landing-title-sm">
                {section.title}
              </h2>
            )}
            <p>{section.text}</p>
            {section.cta && (
              <Link className="btn btn-primary btn-lg" to={section.cta.to}>
                {section.cta.label}
              </Link>
            )}
          </div>
        </section>
      ))}
    </>
  )
}

/** Sayfa kaydırma ilerlemesini (0..1) bir ref'e yazar; React yeniden çizimi tetiklenmez. */
function useScrollProgress(track: React.RefObject<HTMLDivElement | null>) {
  const progress = useRef(0)

  useEffect(() => {
    const update = () => {
      const el = track.current
      if (!el) return
      const rect = el.getBoundingClientRect()
      const scrollable = rect.height - window.innerHeight
      progress.current = scrollable > 0 ? Math.min(1, Math.max(0, -rect.top / scrollable)) : 0
    }
    update()
    window.addEventListener('scroll', update, { passive: true })
    window.addEventListener('resize', update)
    return () => {
      window.removeEventListener('scroll', update)
      window.removeEventListener('resize', update)
    }
  }, [track])

  return progress
}

function Layered() {
  const track = useRef<HTMLDivElement>(null)
  const progress = useScrollProgress(track)

  return (
    <div ref={track} className="hero-track">
      <div className="hero-sticky">
        <Suspense fallback={<p className="page-note hero-loading" role="status">3B sahne yükleniyor…</p>}>
          <HeroScene progress={progress} />
        </Suspense>
      </div>
      <div className="hero-sections">
        <Sections layered />
      </div>
    </div>
  )
}

export default function Landing() {
  const { simple } = useMotion()
  const flat = (
    <div className="landing-flat">
      <Sections layered={false} />
    </div>
  )

  if (simple) return flat

  return (
    <ErrorBoundary fallback={flat}>
      <Layered />
    </ErrorBoundary>
  )
}
