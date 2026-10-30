import { lazy, Suspense, useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { ErrorBoundary } from '../components/ErrorBoundary'
import { useMotion } from '../lib/motion'
import { useTheme } from '../lib/theme'

// 3B sahne (three.js) yalnız ana sayfada ve sade mod kapalıyken indirilir: diğer ekranlar hafif kalır.
const HeroScene = lazy(() => import('../three/HeroScene'))

const SECTIONS = [
  {
    badge: '3B STETESKOP · DİJİTAL MUAYENE',
    title: 'Randevunuz, saniyeler içinde',
    text: 'Sıra beklemeden klinik ve hizmeti seçin, size uygun saati kendiniz belirleyin.',
    chips: ['7/24 Online Erişim', 'Anında Poliklinik Seçimi', 'Şeffaf Süre & Ücret'],
    cta: { to: '/randevu-al', label: 'Randevu al' },
  },
  {
    badge: '3B HASTA ODASI · ŞUBE AĞI',
    title: 'Klinik ve hizmeti seçin',
    text: 'Şubenizi, ihtiyacınız olan muayene veya tetkiki ve süre/ücret bilgisini baştan görün.',
    chips: ['Çoklu Şube Desteği', 'Uzman Hekim Kadrosu', 'Bölüm Bazlı Katalog'],
  },
  {
    badge: '3B MİKROSKOP · GERÇEK ZAMANLI SLOT',
    title: 'Boş saatleri anında görün',
    text: 'Doktorun gerçek müsaitliği gösterilir; aynı saate iki kişi randevu alamaz.',
    chips: ['Çakışmasız Rezervasyon', 'Yerel Saat Dilimi', '60 Günlük Pencere'],
  },
  {
    badge: '3B DOKTOR ONAYI · ANLIK BİLDİRİM',
    title: 'Doktor onaylar, e-posta gelir',
    text: 'Talebiniz doktora iletilir. Onay, red veya iptal durumunda e-posta ile haberdar olursunuz.',
    chips: ['Doğrudan Hekim Onayı', 'E-posta Bildirimi', 'Kolay İptal & Takip'],
    cta: { to: '/kayit', label: 'Hemen kayıt ol' },
  },
]

/** Aynı içerik hem 3B (üstte kayan) hem düz (sade mod) görünümde kullanılır; gerçek HTML olduğu için erişilebilirdir. */
function Sections({ layered }: { layered: boolean }) {
  return (
    <>
      {SECTIONS.map((section, index) => (
        <section
          key={section.title}
          id={`bolum-${index}`}
          className={`landing-section ${layered ? 'is-layered' : ''} ${index % 2 ? 'is-right' : ''}`}
          aria-labelledby={`landing-${index}`}
        >
          <div className="landing-card">
            <div className="landing-card-top">
              <span className="landing-step">
                0{index + 1} / 0{SECTIONS.length}
              </span>
              <span className="landing-badge">{section.badge}</span>
            </div>
            {index === 0 ? (
              <h1 id="landing-0" className="landing-title">
                {section.title}
              </h1>
            ) : (
              <h2 id={`landing-${index}`} className="landing-title landing-title-sm">
                {section.title}
              </h2>
            )}
            <p className="landing-desc">{section.text}</p>
            <ul className="bento-chips" aria-label="Öne çıkan özellikler">
              {section.chips.map((chip) => (
                <li key={chip} className="bento-chip">
                  <span className="bento-dot" aria-hidden="true" />
                  {chip}
                </li>
              ))}
            </ul>
            {section.cta && (
              <div className="landing-actions">
                <Link className="btn btn-primary btn-lg" to={section.cta.to}>
                  {section.cta.label}
                </Link>
                {index === 0 && (
                  <Link className="btn btn-ghost btn-lg" to="/giris">
                    Hasta / Doktor Girişi
                  </Link>
                )}
              </div>
            )}
          </div>
        </section>
      ))}
    </>
  )
}

/** Sayfa kaydırma ilerlemesini (0..1) bir ref'e yazar ve aktif bölümü döndürür. */
function useScrollProgress(track: React.RefObject<HTMLDivElement | null>) {
  const progress = useRef(0)
  const [activeIndex, setActiveIndex] = useState(0)

  useEffect(() => {
    const update = () => {
      const el = track.current
      if (!el) return
      const rect = el.getBoundingClientRect()
      const scrollable = rect.height - window.innerHeight
      const p = scrollable > 0 ? Math.min(1, Math.max(0, -rect.top / scrollable)) : 0
      progress.current = p
      const idx = Math.min(SECTIONS.length - 1, Math.max(0, Math.round(p * (SECTIONS.length - 1))))
      setActiveIndex((prev) => (prev === idx ? prev : idx))
    }
    update()
    window.addEventListener('scroll', update, { passive: true })
    window.addEventListener('resize', update)
    return () => {
      window.removeEventListener('scroll', update)
      window.removeEventListener('resize', update)
    }
  }, [track])

  return { progress, activeIndex }
}

function Layered() {
  const track = useRef<HTMLDivElement>(null)
  const { progress, activeIndex } = useScrollProgress(track)
  const { resolved } = useTheme()

  return (
    <div ref={track} className="hero-track">
      <div className="hero-sticky">
        <Suspense
          fallback={
            <p className="page-note hero-loading" role="status">
              3B sahne yükleniyor…
            </p>
          }
        >
          <HeroScene progress={progress} theme={resolved} />
        </Suspense>
      </div>
      <aside className="scroll-rail" aria-label="Bölüm göstergesi">
        {SECTIONS.map((s, idx) => (
          <a
            key={s.title}
            href={`#bolum-${idx}`}
            className={`scroll-dot ${activeIndex === idx ? 'is-active' : ''}`}
            aria-label={`${idx + 1}. bölüm: ${s.title}`}
            title={s.title}
          >
            <span className="sr-only">{s.title}</span>
          </a>
        ))}
      </aside>
      <div className="hero-sections">
        <Sections layered />
      </div>
      <p className="model-credits">
        3B modeller: Phillip Stephens, Uğur Yakışık, Eugen Vahrushin, BrushDip — CC BY 4.0, kaynak:{' '}
        <a href="/models/CREDITS.md" target="_blank" rel="noreferrer">
          Sketchfab
        </a>
      </p>
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
