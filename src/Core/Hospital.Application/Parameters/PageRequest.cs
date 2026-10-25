namespace Hospital.Application.Parameters;

/// <summary>
/// Liste uçlarının sayfalama isteği: <c>page</c> (1'den başlar) ve <c>pageSize</c> (en çok <see cref="MaxPageSize"/>).
/// Boş bırakılırsa ilk sayfa, <see cref="DefaultPageSize"/> kayıt döner.
/// </summary>
public sealed record PageRequest(int Page = 1, int PageSize = PageRequest.DefaultPageSize)
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;

    public int Skip => (Page - 1) * PageSize;

    public static PageRequest From(int? page, int? pageSize) => new(page ?? 1, pageSize ?? DefaultPageSize);

    /// <summary>Sayfalamayı atlayıp tüm sonuçlardan en fazla <see cref="MaxPageSize"/> kayıt isteyen (sınırlı) istek.</summary>
    public static PageRequest Largest => new(1, MaxPageSize);
}

/// <summary>Bir sayfa sonuç ve filtreye uyan toplam kayıt sayısı. API gövdesi yalnız <see cref="Items"/>; toplam başlıkta döner.</summary>
public sealed record PagedList<T>(IReadOnlyList<T> Items, int TotalCount, PageRequest Page)
{
    public PagedList<TOut> Map<TOut>(Func<T, TOut> selector) =>
        new([.. Items.Select(selector)], TotalCount, Page);
}
