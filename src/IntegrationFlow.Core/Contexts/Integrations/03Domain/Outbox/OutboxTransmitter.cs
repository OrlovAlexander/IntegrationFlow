using System;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndForgot.Transmitters;
using IntegrationFlow.Contexts.Integrations._03Domain.Outbox;
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
        private readonly string profileName;

        public OutboxTransmitter(IOutboxStore outboxStore, string profileName, string contentType = "application/json")
        {
            this.outboxStore = outboxStore ?? throw new ArgumentNullException(nameof(outboxStore));
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
            var id = ResolveMessageGuid(transmitData);
            var messageId = id.ToString("N");
            var payload = RabbitMqPublishTransmitter.SerializeBody(transmitData.Data);
            var outboxMessage = new OutboxMessage(
                id,
                profileName,
                payload,
                ContentType,
                DateTimeOffset.UtcNow,
                attemptCount: 0);

            outboxStore.EnqueueAsync(outboxMessage, CancellationToken.None).GetAwaiter().GetResult();
            return TransmitResult.Create(messageId);
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
