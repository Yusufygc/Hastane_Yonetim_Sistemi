import { Link } from 'react-router-dom'

export default function NotFound() {
  return (
    <section className="page narrow">
      <h1>Sayfa bulunamadı</h1>
      <p>
        Aradığınız sayfa yok. <Link to="/">Ana sayfaya dönün</Link>.
      </p>
    </section>
  )
}
