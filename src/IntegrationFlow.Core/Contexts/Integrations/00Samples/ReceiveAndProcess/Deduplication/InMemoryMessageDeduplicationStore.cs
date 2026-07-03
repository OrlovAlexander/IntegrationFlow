using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Deduplication;

namespace IntegrationFlow.Contexts.Integrations._00Samples.ReceiveAndProcess.Deduplication
{
    /// <summary>
    /// In-memory реализация dedup store для тестов и примеров.
    /// </summary>
    public sealed class InMemoryMessageDeduplicationStore : IMessageDeduplicationStore
    {
        private readonly MessageDeduplicationOptions options;
        private readonly ConcurrentDictionary<string, DateTimeOffset> processing = new();
        private readonly ConcurrentDictionary<string, byte> processed = new();

        public InMemoryMessageDeduplicationStore()
            : this(null)
        {
        }

        public InMemoryMessageDeduplicationStore(MessageDeduplicationOptions options)
        {
            this.options = options ?? new MessageDeduplicationOptions();
        }

        public Task<DeduplicationBeginResult> TryBeginProcessingAsync(string messageId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(messageId))
            {
                return Task.FromResult(DeduplicationBeginResult.Acquired);
            }

            if (processed.ContainsKey(messageId))
            {
                return Task.FromResult(DeduplicationBeginResult.AlreadyProcessed);
            }

            if (processing.TryGetValue(messageId, out var startedAt))
            {
                if (startedAt.Add(options.ProcessingLockDuration) > DateTimeOffset.UtcNow)
                {
                    return Task.FromResult(DeduplicationBeginResult.InProgress);
                }

                processing.TryRemove(messageId, out _);
            }

            return Task.FromResult(
                processing.TryAdd(messageId, DateTimeOffset.UtcNow)
                    ? DeduplicationBeginResult.Acquired
                    : DeduplicationBeginResult.InProgress);
        }

        public Task MarkProcessedAsync(string messageId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(messageId))
            {
                return Task.CompletedTask;
            }

            processing.TryRemove(messageId, out _);
            processed.TryAdd(messageId, 0);
            return Task.CompletedTask;
        }

        public Task ReleaseProcessingAsync(string messageId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(messageId))
            {
                return Task.CompletedTask;
            }

            processing.TryRemove(messageId, out _);
            return Task.CompletedTask;
        }
    }
}
