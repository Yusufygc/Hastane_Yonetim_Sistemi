using Hospital.Application.Features.Notifications;
using Hospital.Domain.Entities.Notifications;
using Hospital.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Hospital.Persistence.Features.Notifications;

/// <summary>
/// Outbox işleme: her mesaj kendi transaction'ında kilitlenir (<c>FOR UPDATE SKIP LOCKED</c>), gönderilir ve sonucu
/// aynı transaction'da yazılır. Süreç gönderimden sonra ama commit'ten önce çökerse mesaj yeniden gönderilebilir
/// (en az bir kez teslim); aksi halde tam bir kez. Kilit, eşzamanlı çalışan başka işleyicilerin aynı satırı almasını engeller.
/// </summary>
internal sealed class EmailOutboxStore(AppDbContext db) : IEmailOutboxStore
{
    public async Task<OutboxProcessResult> ProcessNextAsync(
        IEmailSender sender,
        RetryPolicy policy,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var candidates = await db.EmailOutbox
            .FromSqlInterpolated($"""
                SELECT * FROM email_outbox
                WHERE status = 'PENDING' AND next_attempt_at <= {now}
                ORDER BY created_at
                LIMIT 1
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            return new OutboxProcessResult(OutboxOutcome.Idle);
        }

        var message = candidates[0];
        OutboxOutcome outcome;

        try
        {
            await sender.SendAsync(new EmailMessage(message.ToAddress, message.ToName, message.Subject, message.Body), cancellationToken);
            message.MarkSent(now);
            outcome = OutboxOutcome.Sent;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Kapanış: transaction geri alınır, mesaj bekleyen kalır ve sonraki çalıştırmada gönderilir.
            throw;
        }
        catch (Exception ex)
        {
            message.RegisterFailure($"{ex.GetType().Name}: {ex.Message}", now, policy);
            outcome = message.Status == EmailStatus.Failed ? OutboxOutcome.GaveUp : OutboxOutcome.RetryScheduled;
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new OutboxProcessResult(outcome, message.Id);
    }
}
