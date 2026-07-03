using System;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Deduplication;
using Microsoft.EntityFrameworkCore;

namespace IntegrationFlow.EntityFrameworkCore.Deduplication;

/// <summary>
/// EF Core реализация <see cref="IMessageDeduplicationStore"/>.
/// </summary>
public sealed class EfMessageDeduplicationStore<TContext> : IMessageDeduplicationStore
    where TContext : DbContext
{
    private readonly IDbContextFactory<TContext> contextFactory;
    private readonly MessageDeduplicationOptions options;

    public EfMessageDeduplicationStore(
        IDbContextFactory<TContext> contextFactory,
        MessageDeduplicationOptions options)
    {
        this.contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        this.options = options ?? new MessageDeduplicationOptions();
    }

    public async Task<DeduplicationBeginResult> TryBeginProcessingAsync(
        string messageId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            return DeduplicationBeginResult.Acquired;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var set = context.Set<ProcessedMessageEntity>();
        var now = DateTimeOffset.UtcNow;

        var existing = await set.FindAsync(new object[] { messageId }, cancellationToken).ConfigureAwait(false);
        if (existing != null)
        {
            if (existing.State == ProcessedMessageState.Processed &&
                (existing.ExpiresAt == null || existing.ExpiresAt > now))
            {
                return DeduplicationBeginResult.AlreadyProcessed;
            }

            if (existing.State == ProcessedMessageState.Processing)
            {
                return DeduplicationBeginResult.InProgress;
            }
        }

        try
        {
            set.Add(new ProcessedMessageEntity
            {
                MessageId = messageId,
                State = ProcessedMessageState.Processing,
                CreatedAt = now,
                ExpiresAt = null
            });
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return DeduplicationBeginResult.Acquired;
        }
        catch (DbUpdateException)
        {
            var retry = await set.FindAsync(new object[] { messageId }, cancellationToken).ConfigureAwait(false);
            if (retry == null)
            {
                throw;
            }

            return retry.State == ProcessedMessageState.Processed
                ? DeduplicationBeginResult.AlreadyProcessed
                : DeduplicationBeginResult.InProgress;
        }
    }

    public async Task MarkProcessedAsync(string messageId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            return;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await context.Set<ProcessedMessageEntity>()
            .FindAsync(new object[] { messageId }, cancellationToken)
            .ConfigureAwait(false);

        if (entity == null)
        {
            entity = new ProcessedMessageEntity
            {
                MessageId = messageId,
                CreatedAt = DateTimeOffset.UtcNow
            };
            context.Set<ProcessedMessageEntity>().Add(entity);
        }

        entity.State = ProcessedMessageState.Processed;
        entity.ExpiresAt = options.ProcessedRetention.HasValue
            ? DateTimeOffset.UtcNow.Add(options.ProcessedRetention.Value)
            : null;

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ReleaseProcessingAsync(string messageId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            return;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await context.Set<ProcessedMessageEntity>()
            .FindAsync(new object[] { messageId }, cancellationToken)
            .ConfigureAwait(false);

        if (entity != null && entity.State == ProcessedMessageState.Processing)
        {
            context.Set<ProcessedMessageEntity>().Remove(entity);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
