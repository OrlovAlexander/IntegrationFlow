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
        private readonly ConcurrentDictionary<Guid, OutboxEntry> entries = new();

        public Task EnqueueAsync(OutboxMessage message, CancellationToken cancellationToken = default)
        {
            entries[message.Id] = new OutboxEntry(message, OutboxEntryStatus.Pending);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int batchSize, CancellationToken cancellationToken = default)
        {
            var pending = entries.Values
                .Where(entry => entry.Status == OutboxEntryStatus.Pending)
                .OrderBy(entry => entry.Message.CreatedAt)
                .Take(Math.Max(1, batchSize))
                .Select(entry => entry.Message)
                .ToList();

            return Task.FromResult((IReadOnlyList<OutboxMessage>)pending);
        }

        public Task MarkPublishedAsync(Guid id, CancellationToken cancellationToken = default)
        {
            if (entries.TryGetValue(id, out var entry))
            {
                entries[id] = new OutboxEntry(entry.Message, OutboxEntryStatus.Published);
            }

            return Task.CompletedTask;
        }

        public Task MarkFailedAsync(Guid id, string error, CancellationToken cancellationToken = default)
        {
            if (entries.TryGetValue(id, out var entry))
            {
                var updatedMessage = new OutboxMessage(
                    entry.Message.Id,
                    entry.Message.ProfileName,
                    entry.Message.Payload,
                    entry.Message.ContentType,
                    entry.Message.CreatedAt,
                    entry.Message.AttemptCount + 1);

                entries[id] = new OutboxEntry(updatedMessage, OutboxEntryStatus.Pending);
            }

            return Task.CompletedTask;
        }

        private enum OutboxEntryStatus
        {
            Pending,
            Published
        }

        private sealed class OutboxEntry
        {
            public OutboxEntry(OutboxMessage message, OutboxEntryStatus status)
            {
                Message = message;
                Status = status;
            }

            public OutboxMessage Message { get; }

            public OutboxEntryStatus Status { get; }
        }
    }
}
