using System;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.ResponseCache;
using Microsoft.EntityFrameworkCore;

namespace IntegrationFlow.EntityFrameworkCore.ResponseCache;

/// <summary>
/// EF Core реализация <see cref="IRequestReplyResponseStore"/>.
/// </summary>
public sealed class EfRequestReplyResponseStore<TContext> : IRequestReplyResponseStore
    where TContext : DbContext
{
    private readonly IDbContextFactory<TContext> contextFactory;
    private readonly RequestReplyResponseCacheOptions options;

    public EfRequestReplyResponseStore(
        IDbContextFactory<TContext> contextFactory,
        RequestReplyResponseCacheOptions options)
    {
        this.contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        this.options = options ?? new RequestReplyResponseCacheOptions();
    }

    public async Task<RequestReplyCacheResult> TryBeginAsync(string messageId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            return RequestReplyCacheResult.Acquired;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var set = context.Set<RpcResponseCacheEntity>();
        var now = DateTimeOffset.UtcNow;

        var existing = await set.FindAsync(new object[] { messageId }, cancellationToken).ConfigureAwait(false);
        if (existing != null)
        {
            if (existing.State == RpcResponseCacheState.Completed &&
                (existing.ExpiresAt == null || existing.ExpiresAt > now))
            {
                return RequestReplyCacheResult.AlreadyProcessed;
            }

            if (existing.State == RpcResponseCacheState.Processing &&
                existing.CreatedAt.Add(options.ProcessingLockDuration) > now)
            {
                return RequestReplyCacheResult.InProgress;
            }

            set.Remove(existing);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            set.Add(new RpcResponseCacheEntity
            {
                MessageId = messageId,
                State = RpcResponseCacheState.Processing,
                ResponseBody = Array.Empty<byte>(),
                CreatedAt = now,
                CompletedAt = null,
                ExpiresAt = null
            });
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return RequestReplyCacheResult.Acquired;
        }
        catch (DbUpdateException)
        {
            var retry = await set.FindAsync(new object[] { messageId }, cancellationToken).ConfigureAwait(false);
            if (retry == null)
            {
                throw;
            }

            if (retry.State == RpcResponseCacheState.Completed &&
                (retry.ExpiresAt == null || retry.ExpiresAt > now))
            {
                return RequestReplyCacheResult.AlreadyProcessed;
            }

            if (retry.State == RpcResponseCacheState.Processing &&
                retry.CreatedAt.Add(options.ProcessingLockDuration) > now)
            {
                return RequestReplyCacheResult.InProgress;
            }

            throw;
        }
    }

    public async Task StoreResponseAsync(string messageId, byte[] responseBody, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            return;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var set = context.Set<RpcResponseCacheEntity>();
        var entity = await set.FindAsync(new object[] { messageId }, cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.Add(options.ResponseRetention);

        if (entity == null)
        {
            set.Add(new RpcResponseCacheEntity
            {
                MessageId = messageId,
                State = RpcResponseCacheState.Completed,
                ResponseBody = responseBody ?? Array.Empty<byte>(),
                CreatedAt = now,
                CompletedAt = now,
                ExpiresAt = expiresAt
            });
        }
        else
        {
            entity.State = RpcResponseCacheState.Completed;
            entity.ResponseBody = responseBody ?? Array.Empty<byte>();
            entity.CompletedAt = now;
            entity.ExpiresAt = expiresAt;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<byte[]?> GetCachedResponseAsync(string messageId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            return null;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await context.Set<RpcResponseCacheEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(entry => entry.MessageId == messageId, cancellationToken)
            .ConfigureAwait(false);

        if (entity == null ||
            entity.State != RpcResponseCacheState.Completed ||
            (entity.ExpiresAt != null && entity.ExpiresAt <= DateTimeOffset.UtcNow))
        {
            return null;
        }

        return entity.ResponseBody;
    }

    public async Task<int> PurgeExpiredAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var set = context.Set<RpcResponseCacheEntity>();
        var now = DateTimeOffset.UtcNow;
        var staleProcessingBefore = now.Subtract(options.ProcessingLockDuration);

        var expired = await set
            .Where(entry =>
                (entry.ExpiresAt != null && entry.ExpiresAt <= now) ||
                (entry.State == RpcResponseCacheState.Processing && entry.CreatedAt <= staleProcessingBefore))
            .Take(500)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (expired.Count == 0)
        {
            return 0;
        }

        set.RemoveRange(expired);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return expired.Count;
    }
}
