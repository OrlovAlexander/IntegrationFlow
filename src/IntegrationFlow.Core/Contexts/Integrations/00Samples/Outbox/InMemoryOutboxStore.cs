using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._03Domain.Outbox;

namespace IntegrationFlow.Contexts.Integrations._00Samples.Outbox
{
    /// <summary>
    /// In-memory реализация outbox для тестов и примеров.
    /// </summary>
    public sealed class InMemoryOutboxStore : IOutboxStore
    {
        private const string LegacyWorkerId = "legacy";
        private readonly ConcurrentDictionary<Guid, OutboxMessage> entries = new();

        public Task EnqueueAsync(OutboxMessage message, CancellationToken cancellationToken = default)
        {
            entries[message.Id] = message;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<OutboxMessage>> ClaimPendingAsync(
            int batchSize,
            string workerId,
            TimeSpan lockDuration,
            CancellationToken cancellationToken = default)
        {
            ReleaseExpiredClaimsAsync(cancellationToken).GetAwaiter().GetResult();

            var now = DateTimeOffset.UtcNow;
            var claimed = new List<OutboxMessage>();
            var lockUntil = now.Add(lockDuration);

            foreach (var pair in entries.OrderBy(entry => entry.Value.CreatedAt))
            {
                if (claimed.Count >= Math.Max(1, batchSize))
                {
                    break;
                }

                var message = pair.Value;
                if (message.Status != OutboxMessageStatus.Pending)
                {
                    continue;
                }

                if (message.RetryAfter.HasValue && message.RetryAfter.Value > now)
                {
                    continue;
                }

                var claimedMessage = message.WithClaim(workerId, lockUntil);
                if (entries.TryUpdate(pair.Key, claimedMessage, message))
                {
                    claimed.Add(claimedMessage);
                }
            }

            return Task.FromResult((IReadOnlyList<OutboxMessage>)claimed);
        }

        public Task MarkPublishedAsync(Guid id, string workerId, CancellationToken cancellationToken = default)
        {
            if (entries.TryGetValue(id, out var message) && CanMark(message, workerId))
            {
                entries[id] = message.WithPublished();
            }

            return Task.CompletedTask;
        }

        public Task MarkFailedAsync(
            Guid id,
            string workerId,
            string error,
            TimeSpan retryAfter,
            CancellationToken cancellationToken = default)
        {
            if (!entries.TryGetValue(id, out var message) || !CanMark(message, workerId))
            {
                return Task.CompletedTask;
            }

            var nextAttempt = message.AttemptCount + 1;
            var retryAt = DateTimeOffset.UtcNow.Add(retryAfter);
            entries[id] = message.WithPendingRetry(nextAttempt, retryAt, error);
            return Task.CompletedTask;
        }

        public Task MarkAbandonedAsync(Guid id, string workerId, string reason, CancellationToken cancellationToken = default)
        {
            if (!entries.TryGetValue(id, out var message) || !CanMark(message, workerId))
            {
                return Task.CompletedTask;
            }

            entries[id] = message.WithFailedPermanently(message.AttemptCount, reason);
            return Task.CompletedTask;
        }

        public Task ReleaseExpiredClaimsAsync(CancellationToken cancellationToken = default)
        {
            var now = DateTimeOffset.UtcNow;

            foreach (var pair in entries.ToArray())
            {
                var message = pair.Value;
                if (message.Status == OutboxMessageStatus.InFlight &&
                    message.LockedUntil.HasValue &&
                    message.LockedUntil.Value <= now)
                {
                    entries.TryUpdate(pair.Key, message.WithReleasedClaim(), message);
                }
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int batchSize, CancellationToken cancellationToken = default)
        {
            var now = DateTimeOffset.UtcNow;
            var pending = entries.Values
                .Where(message => message.Status == OutboxMessageStatus.Pending)
                .Where(message => !message.RetryAfter.HasValue || message.RetryAfter.Value <= now)
                .OrderBy(message => message.CreatedAt)
                .Take(Math.Max(1, batchSize))
                .ToList();

            return Task.FromResult((IReadOnlyList<OutboxMessage>)pending);
        }

        public Task MarkPublishedAsync(Guid id, CancellationToken cancellationToken = default)
            => MarkPublishedAsync(id, LegacyWorkerId, cancellationToken);

        public Task MarkFailedAsync(Guid id, string error, CancellationToken cancellationToken = default)
            => MarkFailedAsync(id, LegacyWorkerId, error, TimeSpan.Zero, cancellationToken);

        private static bool CanMark(OutboxMessage message, string workerId)
        {
            if (message.Status == OutboxMessageStatus.Published)
            {
                return true;
            }

            if (string.Equals(workerId, LegacyWorkerId, StringComparison.Ordinal))
            {
                return message.Status == OutboxMessageStatus.Pending ||
                       (message.Status == OutboxMessageStatus.InFlight &&
                        string.Equals(message.LockedBy, workerId, StringComparison.Ordinal));
            }

            return message.Status == OutboxMessageStatus.InFlight &&
                   string.Equals(message.LockedBy, workerId, StringComparison.Ordinal);
        }
    }
}
