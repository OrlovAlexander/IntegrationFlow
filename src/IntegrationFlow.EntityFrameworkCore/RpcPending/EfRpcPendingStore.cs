using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._03Domain.RpcPending;
using Microsoft.EntityFrameworkCore;

namespace IntegrationFlow.EntityFrameworkCore.RpcPending;

/// <summary>
/// EF Core реализация <see cref="IRpcPendingStore"/>.
/// </summary>
public sealed class EfRpcPendingStore<TContext> : IRpcPendingStore
    where TContext : DbContext
{
    private readonly IDbContextFactory<TContext> contextFactory;

    public EfRpcPendingStore(IDbContextFactory<TContext> contextFactory)
    {
        this.contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    public async Task EnqueueAsync(RpcPendingRequest request, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        context.Set<RpcPendingRequestEntity>().Add(EfRpcPendingMapper.ToEntity(request));
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<RpcPendingRequest>> ClaimPendingAsync(
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
        var set = context.Set<RpcPendingRequestEntity>();

        await ReleaseExpiredClaimsInternalAsync(set, now, cancellationToken).ConfigureAwait(false);

        var candidates = await RpcPendingClaimHelper
            .SelectPendingCandidatesAsync(set, batchSize, now, cancellationToken)
            .ConfigureAwait(false);

        RpcPendingClaimHelper.MarkClaimed(candidates, workerId, lockUntil);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return candidates.Select(EfRpcPendingMapper.ToDomain).ToList();
    }

    public async Task MarkAwaitingResponseAsync(Guid id, string workerId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await context.Set<RpcPendingRequestEntity>().FindAsync(new object[] { id }, cancellationToken).ConfigureAwait(false);
        if (entity == null || !CanMarkInFlight(entity, workerId))
        {
            return;
        }

        entity.Status = RpcPendingStatus.AwaitingResponse;
        entity.LockedBy = null;
        entity.LockedUntil = null;
        entity.RetryAfter = null;
        entity.LastError = null;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task CompleteAsync(Guid id, byte[] responsePayload, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await context.Set<RpcPendingRequestEntity>().FindAsync(new object[] { id }, cancellationToken).ConfigureAwait(false);
        if (entity == null || entity.Status == RpcPendingStatus.Completed)
        {
            return;
        }

        entity.Status = RpcPendingStatus.Completed;
        entity.ResponsePayload = responsePayload ?? Array.Empty<byte>();
        entity.CompletedAt = DateTimeOffset.UtcNow;
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
        var entity = await context.Set<RpcPendingRequestEntity>().FindAsync(new object[] { id }, cancellationToken).ConfigureAwait(false);
        if (entity == null || !CanMarkInFlight(entity, workerId))
        {
            return;
        }

        entity.AttemptCount += 1;
        entity.Status = RpcPendingStatus.Pending;
        entity.LockedBy = null;
        entity.LockedUntil = null;
        entity.RetryAfter = DateTimeOffset.UtcNow.Add(retryAfter);
        entity.LastError = error;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task MarkAbandonedAsync(Guid id, string workerId, string reason, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await context.Set<RpcPendingRequestEntity>().FindAsync(new object[] { id }, cancellationToken).ConfigureAwait(false);
        if (entity == null || !CanMarkInFlight(entity, workerId))
        {
            return;
        }

        entity.Status = RpcPendingStatus.Failed;
        entity.LockedBy = null;
        entity.LockedUntil = null;
        entity.LastError = reason;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task MarkTimedOutAsync(Guid id, string reason, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await context.Set<RpcPendingRequestEntity>().FindAsync(new object[] { id }, cancellationToken).ConfigureAwait(false);
        if (entity == null || entity.Status is RpcPendingStatus.Completed or RpcPendingStatus.Failed)
        {
            return;
        }

        entity.Status = RpcPendingStatus.TimedOut;
        entity.LockedBy = null;
        entity.LockedUntil = null;
        entity.LastError = reason;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ReleaseExpiredClaimsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await ReleaseExpiredClaimsInternalAsync(context.Set<RpcPendingRequestEntity>(), DateTimeOffset.UtcNow, cancellationToken)
            .ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<RpcPendingRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await context.Set<RpcPendingRequestEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(entry => entry.Id == id, cancellationToken)
            .ConfigureAwait(false);
        return entity == null ? null : EfRpcPendingMapper.ToDomain(entity);
    }

    public async Task<bool> ReplayAbandonedAsync(
        Guid id,
        bool resetAttemptCount = false,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await context.Set<RpcPendingRequestEntity>().FindAsync(new object[] { id }, cancellationToken).ConfigureAwait(false);
        if (entity == null || entity.Status != RpcPendingStatus.Failed)
        {
            return false;
        }

        entity.Status = RpcPendingStatus.Pending;
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
        DbSet<RpcPendingRequestEntity> set,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var expired = await set
            .Where(request => request.Status == RpcPendingStatus.InFlight && request.LockedUntil != null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var request in expired)
        {
            if (request.LockedUntil!.Value <= now)
            {
                request.Status = RpcPendingStatus.Pending;
                request.LockedBy = null;
                request.LockedUntil = null;
            }
        }
    }

    private static bool CanMarkInFlight(RpcPendingRequestEntity entity, string workerId)
    {
        return entity.Status == RpcPendingStatus.InFlight &&
               string.Equals(entity.LockedBy, workerId, StringComparison.Ordinal);
    }
}

internal static class RpcPendingClaimHelper
{
    internal static void MarkClaimed(
        IEnumerable<RpcPendingRequestEntity> entities,
        string workerId,
        DateTimeOffset lockUntil)
    {
        foreach (var entity in entities)
        {
            entity.Status = RpcPendingStatus.InFlight;
            entity.LockedBy = workerId;
            entity.LockedUntil = lockUntil;
        }
    }

    internal static async Task<List<RpcPendingRequestEntity>> SelectPendingCandidatesAsync(
        DbSet<RpcPendingRequestEntity> set,
        int batchSize,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var pending = await set
            .Where(request => request.Status == RpcPendingStatus.Pending)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return pending
            .Where(request => request.RetryAfter == null || request.RetryAfter <= now)
            .OrderBy(request => request.CreatedAt)
            .Take(Math.Max(1, batchSize))
            .ToList();
    }
}
