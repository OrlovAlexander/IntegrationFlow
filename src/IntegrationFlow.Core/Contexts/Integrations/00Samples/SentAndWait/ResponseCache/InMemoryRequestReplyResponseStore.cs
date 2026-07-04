using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.ResponseCache;

namespace IntegrationFlow.Contexts.Integrations._00Samples.SentAndWait.ResponseCache
{
    /// <summary>
    /// In-memory реализация <see cref="IRequestReplyResponseStore"/> для sample и тестов.
    /// </summary>
    public sealed class InMemoryRequestReplyResponseStore : IRequestReplyResponseStore
    {
        private readonly ConcurrentDictionary<string, CacheEntry> entries = new();
        private readonly RequestReplyResponseCacheOptions options;

        public InMemoryRequestReplyResponseStore(RequestReplyResponseCacheOptions? options = null)
        {
            this.options = options ?? new RequestReplyResponseCacheOptions();
        }

        public Task<RequestReplyCacheResult> TryBeginAsync(string messageId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(messageId))
            {
                return Task.FromResult(RequestReplyCacheResult.Acquired);
            }

            cancellationToken.ThrowIfCancellationRequested();
            var now = DateTimeOffset.UtcNow;

            if (entries.TryGetValue(messageId, out var existing))
            {
                if (existing.State == CacheEntryState.Completed &&
                    existing.CompletedAt.Add(options.ResponseRetention) > now)
                {
                    return Task.FromResult(RequestReplyCacheResult.AlreadyProcessed);
                }

                if (existing.State == CacheEntryState.Processing &&
                    existing.StartedAt.Add(options.ProcessingLockDuration) > now)
                {
                    return Task.FromResult(RequestReplyCacheResult.InProgress);
                }
            }

            entries[messageId] = CacheEntry.Processing(now);
            return Task.FromResult(RequestReplyCacheResult.Acquired);
        }

        public Task StoreResponseAsync(string messageId, byte[] responseBody, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(messageId))
            {
                return Task.CompletedTask;
            }

            cancellationToken.ThrowIfCancellationRequested();
            entries[messageId] = CacheEntry.Completed(
                DateTimeOffset.UtcNow,
                responseBody ?? Array.Empty<byte>());
            return Task.CompletedTask;
        }

        public Task<byte[]?> GetCachedResponseAsync(string messageId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(messageId))
            {
                return Task.FromResult<byte[]?>(null);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (!entries.TryGetValue(messageId, out var entry))
            {
                return Task.FromResult<byte[]?>(null);
            }

            var now = DateTimeOffset.UtcNow;
            if (entry.State != CacheEntryState.Completed ||
                entry.CompletedAt.Add(options.ResponseRetention) <= now)
            {
                return Task.FromResult<byte[]?>(null);
            }

            return Task.FromResult<byte[]?>(entry.ResponseBody);
        }

        private enum CacheEntryState
        {
            Processing,
            Completed
        }

        private sealed class CacheEntry
        {
            private CacheEntry(CacheEntryState state, DateTimeOffset startedAt, DateTimeOffset completedAt, byte[] responseBody)
            {
                State = state;
                StartedAt = startedAt;
                CompletedAt = completedAt;
                ResponseBody = responseBody;
            }

            public CacheEntryState State { get; }

            public DateTimeOffset StartedAt { get; }

            public DateTimeOffset CompletedAt { get; }

            public byte[] ResponseBody { get; }

            public static CacheEntry Processing(DateTimeOffset startedAt)
                => new(CacheEntryState.Processing, startedAt, default, Array.Empty<byte>());

            public static CacheEntry Completed(DateTimeOffset completedAt, byte[] responseBody)
                => new(CacheEntryState.Completed, completedAt, completedAt, responseBody);
        }
    }
}
