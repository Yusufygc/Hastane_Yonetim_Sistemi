using Hospital.Application.Parameters;
using Microsoft.EntityFrameworkCore;

namespace Hospital.Persistence.Extensions;

internal static class PagingExt
{
    /// <summary>
    /// Toplam kaydı sayar ve istenen sayfayı döner. Sorgu çağıranda <b>deterministik sıralanmış</b> olmalıdır
    /// (aynı kayıt sayfalar arasında kaymasın diye benzersiz bir ikincil sıralama kullanın).
    /// </summary>
    public static async Task<PagedList<T>> ToPagedListAsync<T>(
        this IQueryable<T> orderedQuery,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var total = await orderedQuery.CountAsync(cancellationToken);

        IReadOnlyList<T> items = total == 0
            ? []
            : await orderedQuery.Skip(page.Skip).Take(page.PageSize).ToListAsync(cancellationToken);

        return new PagedList<T>(items, total, page);
    }
}
