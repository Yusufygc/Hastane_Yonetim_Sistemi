using Hospital.Application.Features.Notifications;
using Hospital.Domain.Entities.Notifications;
using Hospital.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Hospital.Persistence.Features.Notifications;

/// <summary>Satırı çağıranın DbContext'ine ekler; kaydetme (ve dolayısıyla atomiklik) çağıran SaveChanges'tedir.</summary>
internal sealed class EmailOutbox(AppDbContext db) : IEmailOutbox
{
    public async Task<bool> EnqueueAsync(EmailOutboxMessage message, CancellationToken cancellationToken)
    {
        // Aynı SaveChanges içinde (henüz yazılmamış) veya daha önce yazılmış aynı anahtar → eklenmez.
        var exists = db.EmailOutbox.Local.Any(m => m.DedupKey == message.DedupKey)
                     || await db.EmailOutbox.AnyAsync(m => m.DedupKey == message.DedupKey, cancellationToken);

        if (exists)
        {
            return false;
        }

        db.EmailOutbox.Add(message);
        return true;
    }
}
