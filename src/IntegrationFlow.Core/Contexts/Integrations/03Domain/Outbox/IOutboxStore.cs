using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace IntegrationFlow.Contexts.Integrations._03Domain.Outbox
{
    /// <summary>
    /// Хранилище transactional outbox.
    /// </summary>
    public interface IOutboxStore
    {
        Task EnqueueAsync(OutboxMessage message, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<OutboxMessage>> ClaimPendingAsync(
            int batchSize,
            string workerId,
            TimeSpan lockDuration,
            CancellationToken cancellationToken = default);

        Task MarkPublishedAsync(Guid id, string workerId, CancellationToken cancellationToken = default);

        Task MarkFailedAsync(
            Guid id,
            string workerId,
            string error,
            TimeSpan retryAfter,
            CancellationToken cancellationToken = default);

        Task MarkAbandonedAsync(Guid id, string workerId, string reason, CancellationToken cancellationToken = default);

        Task ReleaseExpiredClaimsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Возвращает pending-сообщения без claim. Для тестов и диагностики.
        /// </summary>
        Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int batchSize, CancellationToken cancellationToken = default);

        Task MarkPublishedAsync(Guid id, CancellationToken cancellationToken = default);

        Task MarkFailedAsync(Guid id, string error, CancellationToken cancellationToken = default);
    }
}
