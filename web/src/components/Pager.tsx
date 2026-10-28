interface Props {
  page: number
  total: number
  pageSize: number
  onPage: (page: number) => void
  noun?: string
}

/** X-Total-Count tabanlı sayfalama çubuğu; tek sayfa varsa çizilmez. */
export function Pager({ page, total, pageSize, onPage, noun = 'kayıt' }: Props) {
  const pageCount = Math.max(1, Math.ceil(total / pageSize))
  if (pageCount <= 1) return null

  return (
    <nav className="pager" aria-label="Sayfalama">
      <button type="button" className="btn" disabled={page <= 1} onClick={() => onPage(page - 1)}>
        Önceki
      </button>
      <span>
        Sayfa {page} / {pageCount} ({total} {noun})
      </span>
      <button type="button" className="btn" disabled={page >= pageCount} onClick={() => onPage(page + 1)}>
        Sonraki
      </button>
    </nav>
  )
}
