using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._03Domain.Outbox;
using IntegrationFlow.EntityFrameworkCore.Outbox.Claim;
using Microsoft.EntityFrameworkCore;

namespace IntegrationFlow.EntityFrameworkCore.Outbox;

/// <summary>
/// EF Core реализация <see cref="IOutboxStore"/> для relay worker (claim/mark).
/// Для enqueue в TX приложения используйте <see cref="IOutboxEnqueue"/> или <see cref="DbContextOutboxExtensions"/>.
/// </summary>
public sealed class EfOutboxStore<TContext> : IOutboxStore
    where TContext : DbContext
{
    private readonly IDbContextFactory<TContext> contextFactory;

    public EfOutboxStore(IDbContextFactory<TContext> contextFactory)
    {
        this.contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    /// <inheritdoc />
    /// <remarks>
    /// Создаёт отдельный DbContext и commit. Для атомарности с бизнес-данными используйте
    /// <see cref="IOutboxEnqueue.Stage"/> или <see cref="DbContextOutboxExtensions.EnqueueOutboxMessage"/>.
    /// </remarks>
    public async Task EnqueueAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        context.Set<OutboxMessageEntity>().Add(EfOutboxMapper.ToEntity(message));
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<OutboxMessage>> ClaimPendingAsync(
        int batchSize,
        string workerId,
        TimeSpan lockDuration,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await context.Database
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);

        var now = DateTimeOffset.UtcNow;
        var lockUntil = now.Add(lockDuration);
        var set = context.Set<OutboxMessageEntity>();

        await ReleaseExpiredClaimsInternalAsync(set, now, cancellationToken).ConfigureAwait(false);

        var strategy = OutboxClaimStrategyResolver.Resolve(context);
        var claimed = await strategy
            .ClaimAsync(context, set, batchSize, workerId, lockUntil, now, cancellationToken)
            .ConfigureAwait(false);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return claimed.Select(EfOutboxMapper.ToDomain).ToList();
    }

    public async Task MarkPublishedAsync(Guid id, string workerId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await context.Set<OutboxMessageEntity>().FindAsync(new object[] { id }, cancellationToken).ConfigureAwait(false);
        if (entity == null || !CanMark(entity, workerId))
        {
            return;
        }

        entity.Status = OutboxMessageStatus.Published;
        entity.LockedBy = null;
        entity.LockedUntil = null;
        entity.RetryAfter = null;
        entity.LastError = null;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task MarkFailedAsync(
        Guid id,
        string workerId,
        string error,
        TimeSpan retryAfter,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await context.Set<OutboxMessageEntity>().FindAsync(new object[] { id }, cancellationToken).ConfigureAwait(false);
        if (entity == null || !CanMark(entity, workerId))
        {
            return;
        }

        entity.AttemptCount += 1;
        entity.Status = OutboxMessageStatus.Pending;
        entity.LockedBy = null;
        entity.LockedUntil = null;
        entity.RetryAfter = DateTimeOffset.UtcNow.Add(retryAfter);
        entity.LastError = error;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task MarkAbandonedAsync(Guid id, string workerId, string reason, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await context.Set<OutboxMessageEntity>().FindAsync(new object[] { id }, cancellationToken).ConfigureAwait(false);
        if (entity == null || !CanMark(entity, workerId))
        {
            return;
        }

        entity.Status = OutboxMessageStatus.Failed;
        entity.LockedBy = null;
        entity.LockedUntil = null;
        entity.LastError = reason;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ReleaseExpiredClaimsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await ReleaseExpiredClaimsInternalAsync(context.Set<OutboxMessageEntity>(), DateTimeOffset.UtcNow, cancellationToken)
            .ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow;
        var pending = await OutboxClaimHelper
            .SelectPendingCandidatesAsync(context.Set<OutboxMessageEntity>(), batchSize, now, cancellationToken)
            .ConfigureAwait(false);

        return pending.Select(EfOutboxMapper.ToDomain).ToList();
    }

    public Task MarkPublishedAsync(Guid id, CancellationToken cancellationToken = default)
        => MarkPublishedAsync(id, "legacy", cancellationToken);

    public Task MarkFailedAsync(Guid id, string error, CancellationToken cancellationToken = default)
        => MarkFailedAsync(id, "legacy", error, TimeSpan.Zero, cancellationToken);

    public async Task<bool> ReplayAbandonedAsync(
        Guid id,
        bool resetAttemptCount = false,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await context.Set<OutboxMessageEntity>().FindAsync(new object[] { id }, cancellationToken).ConfigureAwait(false);
        if (entity == null || entity.Status != OutboxMessageStatus.Failed)
        {
            return false;
        }

        entity.Status = OutboxMessageStatus.Pending;
        entity.LockedBy = null;
        entity.LockedUntil = null;
        entity.RetryAfter = null;
        entity.LastError = null;
        if (resetAttemptCount)
        {
            entity.AttemptCount = 0;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static async Task ReleaseExpiredClaimsInternalAsync(
        DbSet<OutboxMessageEntity> set,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var expired = await set
            .Where(message => message.Status == OutboxMessageStatus.InFlight && message.LockedUntil != null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var message in expired)
        {
            if (message.LockedUntil!.Value <= now)
            {
                message.Status = OutboxMessageStatus.Pending;
                message.LockedBy = null;
                message.LockedUntil = null;
            }
        }
    }

    private static bool CanMark(OutboxMessageEntity entity, string workerId)
    {
        if (entity.Status == OutboxMessageStatus.Published)
        {
            return true;
        }

        if (string.Equals(workerId, "legacy", StringComparison.Ordinal))
        {
            return entity.Status == OutboxMessageStatus.Pending ||
                   (entity.Status == OutboxMessageStatus.InFlight &&
                    string.Equals(entity.LockedBy, workerId, StringComparison.Ordinal));
        }

        return entity.Status == OutboxMessageStatus.InFlight &&
               string.Equals(entity.LockedBy, workerId, StringComparison.Ordinal);
    }
}
