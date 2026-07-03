using System;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndForgot.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndForgot.Connections;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndForgot.Transmitters;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot;

namespace IntegrationFlow.Contexts.Integrations._03Domain.Outbox
{
    /// <summary>
    /// Релей pending-сообщений из transactional outbox в RabbitMQ.
    /// </summary>
    public sealed class OutboxRelayService
    {
        private readonly IOutboxStore outboxStore;
        private readonly IIntegrationLogger logger;

        public OutboxRelayService(IOutboxStore outboxStore, IIntegrationLogger logger)
        {
            this.outboxStore = outboxStore ?? throw new ArgumentNullException(nameof(outboxStore));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Публикует пакет pending-сообщений.
        /// </summary>
        public async Task RelayBatchAsync(int batchSize = 20, CancellationToken cancellationToken = default)
        {
            var pending = await outboxStore.GetPendingAsync(batchSize, cancellationToken).ConfigureAwait(false);
            foreach (var message in pending)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var configuration = RabbitMqPublishConfigurationLoader.LoadProfile(message.ProfileName);
                    using var connection = new RabbitMqPublishConnection(configuration);
                    var transmitter = new RabbitMqPublishTransmitter(configuration, connection);
                    var transmitData = new TransmitData(message.Payload, message.Id.ToString("N"))
                        .WithCorrelationId(message.Id.ToString("N"));

                    transmitter.TransmitWithResult(transmitData);
                    await outboxStore.MarkPublishedAsync(message.Id, cancellationToken).ConfigureAwait(false);
                    logger.LogInfo($"Outbox relay. Message '{message.Id}' published via profile '{message.ProfileName}'.");
                }
                catch (Exception ex)
                {
                    await outboxStore.MarkFailedAsync(message.Id, ex.Message, cancellationToken).ConfigureAwait(false);
                    logger.LogException($"Outbox relay. Failed to publish message '{message.Id}'.", ex);
                }
            }
        }
    }
}
