using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace IntegrationFlow.Contexts.Integrations._03Domain.RpcPending
{
    /// <summary>
    /// Хранилище async RPC pending requests.
    /// </summary>
    public interface IRpcPendingStore
    {
        Task EnqueueAsync(RpcPendingRequest request, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<RpcPendingRequest>> ClaimPendingAsync(
            int batchSize,
            string workerId,
            TimeSpan lockDuration,
            CancellationToken cancellationToken = default);

        Task MarkAwaitingResponseAsync(Guid id, string workerId, CancellationToken cancellationToken = default);

        Task CompleteAsync(Guid id, byte[] responsePayload, CancellationToken cancellationToken = default);

        Task MarkFailedAsync(
            Guid id,
            string workerId,
            string error,
            TimeSpan retryAfter,
            CancellationToken cancellationToken = default);

        Task MarkAbandonedAsync(Guid id, string workerId, string reason, CancellationToken cancellationToken = default);

        Task MarkTimedOutAsync(Guid id, string reason, CancellationToken cancellationToken = default);

        Task ReleaseExpiredClaimsAsync(CancellationToken cancellationToken = default);

        Task<RpcPendingRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        Task<bool> ReplayAbandonedAsync(
            Guid id,
            bool resetAttemptCount = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns count of requests waiting for RPC response.
        /// </summary>
        Task<int> GetAwaitingResponseCountAsync(CancellationToken cancellationToken = default);

        Task<IReadOnlyList<RpcPendingRequest>> GetCompensationCandidatesAsync(
            int batchSize,
            CancellationToken cancellationToken = default);

        Task MarkCompensatedAsync(Guid id, CancellationToken cancellationToken = default);

        Task<int> PurgeTerminalAsync(
            DateTimeOffset terminalBefore,
            int batchSize,
            CancellationToken cancellationToken = default);
    }
}
