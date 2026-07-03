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

        Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int batchSize, CancellationToken cancellationToken = default);

        Task MarkPublishedAsync(Guid id, CancellationToken cancellationToken = default);

        Task MarkFailedAsync(Guid id, string error, CancellationToken cancellationToken = default);
    }
}
