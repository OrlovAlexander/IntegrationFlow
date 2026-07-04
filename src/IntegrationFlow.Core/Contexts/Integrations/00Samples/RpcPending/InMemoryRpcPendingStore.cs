using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._03Domain.RpcPending;

namespace IntegrationFlow.Contexts.Integrations._00Samples.RpcPending
{
    /// <summary>
    /// In-memory реализация <see cref="IRpcPendingStore"/> для тестов и примеров.
    /// </summary>
    public sealed class InMemoryRpcPendingStore : IRpcPendingStore
    {
        private readonly ConcurrentDictionary<Guid, RpcPendingRequest> entries = new();

        public Task EnqueueAsync(RpcPendingRequest request, CancellationToken cancellationToken = default)
        {
            entries[request.Id] = request;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<RpcPendingRequest>> ClaimPendingAsync(
            int batchSize,
            string workerId,
            TimeSpan lockDuration,
            CancellationToken cancellationToken = default)
        {
            ReleaseExpiredClaimsAsync(cancellationToken).GetAwaiter().GetResult();

            var now = DateTimeOffset.UtcNow;
            var claimed = new List<RpcPendingRequest>();
            var lockUntil = now.Add(lockDuration);

            foreach (var pair in entries.OrderBy(entry => entry.Value.CreatedAt))
            {
                if (claimed.Count >= Math.Max(1, batchSize))
                {
                    break;
                }

                var request = pair.Value;
                if (request.Status != RpcPendingStatus.Pending)
                {
                    continue;
                }

                if (request.RetryAfter.HasValue && request.RetryAfter.Value > now)
                {
                    continue;
                }

                var claimedRequest = request.WithClaim(workerId, lockUntil);
                if (entries.TryUpdate(pair.Key, claimedRequest, request))
                {
                    claimed.Add(claimedRequest);
                }
            }

            return Task.FromResult((IReadOnlyList<RpcPendingRequest>)claimed);
        }

        public Task MarkAwaitingResponseAsync(Guid id, string workerId, CancellationToken cancellationToken = default)
        {
            if (entries.TryGetValue(id, out var request) && CanMarkInFlight(request, workerId))
            {
                entries[id] = request.WithAwaitingResponse();
            }

            return Task.CompletedTask;
        }

        public Task CompleteAsync(Guid id, byte[] responsePayload, CancellationToken cancellationToken = default)
        {
            if (entries.TryGetValue(id, out var request) &&
                request.Status is not RpcPendingStatus.Completed and not RpcPendingStatus.Failed)
            {
                entries[id] = request.WithCompleted(responsePayload, DateTimeOffset.UtcNow);
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
            if (!entries.TryGetValue(id, out var request) || !CanMarkInFlight(request, workerId))
            {
                return Task.CompletedTask;
            }

            var nextAttempt = request.AttemptCount + 1;
            entries[id] = request.WithPendingRetry(nextAttempt, DateTimeOffset.UtcNow.Add(retryAfter), error);
            return Task.CompletedTask;
        }

        public Task MarkAbandonedAsync(Guid id, string workerId, string reason, CancellationToken cancellationToken = default)
        {
            if (!entries.TryGetValue(id, out var request) || !CanMarkInFlight(request, workerId))
            {
                return Task.CompletedTask;
            }

            entries[id] = request.WithFailed(request.AttemptCount, reason);
            return Task.CompletedTask;
        }

        public Task MarkTimedOutAsync(Guid id, string reason, CancellationToken cancellationToken = default)
        {
            if (entries.TryGetValue(id, out var request) &&
                request.Status is not RpcPendingStatus.Completed and not RpcPendingStatus.Failed)
            {
                entries[id] = request.WithTimedOut(reason);
            }

            return Task.CompletedTask;
        }

        public Task ReleaseExpiredClaimsAsync(CancellationToken cancellationToken = default)
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var pair in entries.ToArray())
            {
                var request = pair.Value;
                if (request.Status == RpcPendingStatus.InFlight &&
                    request.LockedUntil.HasValue &&
                    request.LockedUntil.Value <= now)
                {
                    entries.TryUpdate(pair.Key, request.WithReleasedClaim(), request);
                }
            }

            return Task.CompletedTask;
        }

        public Task<RpcPendingRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            entries.TryGetValue(id, out var request);
            return Task.FromResult(request);
        }

        public Task<bool> ReplayAbandonedAsync(
            Guid id,
            bool resetAttemptCount = false,
            CancellationToken cancellationToken = default)
        {
            if (!entries.TryGetValue(id, out var request) || request.Status != RpcPendingStatus.Failed)
            {
                return Task.FromResult(false);
            }

            entries[id] = new RpcPendingRequest(
                request.Id,
                request.ProfileName,
                request.RequestPayload,
                request.ContentType,
                request.CreatedAt,
                resetAttemptCount ? 0 : request.AttemptCount);
            return Task.FromResult(true);
        }

        public Task<int> GetAwaitingResponseCountAsync(CancellationToken cancellationToken = default)
        {
            var count = entries.Values.Count(request => request.Status == RpcPendingStatus.AwaitingResponse);
            return Task.FromResult(count);
        }

        public Task<IReadOnlyList<RpcPendingRequest>> GetCompensationCandidatesAsync(
            int batchSize,
            CancellationToken cancellationToken = default)
        {
            var candidates = entries.Values
                .Where(request =>
                    request.CompensatedAt == null &&
                    request.Status is RpcPendingStatus.Failed or RpcPendingStatus.TimedOut)
                .OrderBy(request => request.CreatedAt)
                .Take(Math.Max(1, batchSize))
                .ToList();

            return Task.FromResult((IReadOnlyList<RpcPendingRequest>)candidates);
        }

        public Task MarkCompensatedAsync(Guid id, CancellationToken cancellationToken = default)
        {
            if (entries.TryGetValue(id, out var request) && request.CompensatedAt == null)
            {
                entries[id] = request.WithCompensated(DateTimeOffset.UtcNow);
            }

            return Task.CompletedTask;
        }

        public Task<int> PurgeTerminalAsync(
            DateTimeOffset terminalBefore,
            int batchSize,
            CancellationToken cancellationToken = default)
        {
            var removed = 0;
            foreach (var pair in entries.ToArray())
            {
                if (removed >= Math.Max(1, batchSize))
                {
                    break;
                }

                var request = pair.Value;
                var terminalAt = request.CompensatedAt ?? request.CompletedAt;
                var purge =
                    (request.Status == RpcPendingStatus.Completed &&
                     terminalAt != null &&
                     terminalAt < terminalBefore) ||
                    (request.CompensatedAt != null && request.CompensatedAt < terminalBefore);

                if (purge && entries.TryRemove(pair.Key, out _))
                {
                    removed++;
                }
            }

            return Task.FromResult(removed);
        }

        private static bool CanMarkInFlight(RpcPendingRequest request, string workerId)
        {
            return request.Status == RpcPendingStatus.InFlight &&
                   string.Equals(request.LockedBy, workerId, StringComparison.Ordinal);
        }
    }
}
