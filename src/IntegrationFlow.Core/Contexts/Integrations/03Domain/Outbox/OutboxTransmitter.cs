using System;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._01Infrastructure;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot.Transmitter;

namespace IntegrationFlow.Contexts.Integrations._03Domain.Outbox
{
    /// <summary>
    /// Передатчик, записывающий сообщение в transactional outbox вместо прямой публикации.
    /// </summary>
    internal sealed class OutboxTransmitter : ITransmitter, ITransmitterWithResult
    {
        private readonly IOutboxStore outboxStore;
        private readonly IOutboxEnqueue outboxEnqueue;
        private readonly string profileName;

        public OutboxTransmitter(IOutboxStore outboxStore, string profileName, string contentType = "application/json")
            : this(outboxStore, null, profileName, contentType)
        {
        }

        public OutboxTransmitter(IOutboxEnqueue outboxEnqueue, string profileName, string contentType = "application/json")
            : this(null, outboxEnqueue, profileName, contentType)
        {
        }

        private OutboxTransmitter(
            IOutboxStore outboxStore,
            IOutboxEnqueue outboxEnqueue,
            string profileName,
            string contentType)
        {
            if (outboxStore == null && outboxEnqueue == null)
            {
                throw new ArgumentException("Either outboxStore or outboxEnqueue must be provided.");
            }

            this.outboxStore = outboxStore;
            this.outboxEnqueue = outboxEnqueue;
            this.profileName = profileName ?? throw new ArgumentNullException(nameof(profileName));
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/json" : contentType;
        }

        internal string ContentType { get; }

        public void Transmit(TransmitData transmitData)
        {
            TransmitWithResult(transmitData);
        }

        public TransmitResult TransmitWithResult(TransmitData transmitData)
        {
            var outboxMessage = BuildOutboxMessage(transmitData);

            if (outboxEnqueue != null)
            {
                outboxEnqueue.Stage(outboxMessage);
            }
            else
            {
                outboxStore.EnqueueAsync(outboxMessage, CancellationToken.None).GetAwaiter().GetResult();
            }

            return TransmitResult.Create(outboxMessage.Id.ToString("N"));
        }

        private OutboxMessage BuildOutboxMessage(TransmitData transmitData)
        {
            var id = ResolveMessageGuid(transmitData);
            var payload = IntegrationPayloadSerializer.SerializeToBytes(transmitData.Data);
            return new OutboxMessage(
                id,
                profileName,
                payload,
                ContentType,
                DateTimeOffset.UtcNow,
                attemptCount: 0);
        }

        private static Guid ResolveMessageGuid(TransmitData transmitData)
        {
            if (!string.IsNullOrWhiteSpace(transmitData.MessageId) &&
                Guid.TryParse(transmitData.MessageId, out var parsed))
            {
                return parsed;
            }

            return Guid.NewGuid();
        }
    }
}
