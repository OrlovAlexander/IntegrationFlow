using System;
using System.Threading;

namespace IntegrationFlow.Contexts.Integrations._03Domain.Outbox
{
    /// <summary>
    /// Адаптер <see cref="IOutboxStore"/> → <see cref="IOutboxEnqueue"/> для in-memory и legacy-сценариев.
    /// Каждый Stage выполняет отдельный SaveChanges (не атомарен с бизнес-TX).
    /// </summary>
    public sealed class OutboxStoreEnqueueAdapter : IOutboxEnqueue
    {
        private readonly IOutboxStore outboxStore;

        public OutboxStoreEnqueueAdapter(IOutboxStore outboxStore)
        {
            this.outboxStore = outboxStore ?? throw new ArgumentNullException(nameof(outboxStore));
        }

        /// <inheritdoc />
        public void Stage(OutboxMessage message)
        {
            outboxStore.EnqueueAsync(message, CancellationToken.None).GetAwaiter().GetResult();
        }
    }
}
