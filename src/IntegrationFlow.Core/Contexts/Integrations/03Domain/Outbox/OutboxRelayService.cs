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
        private readonly OutboxRelayOptions options;
        private readonly string workerId = Guid.NewGuid().ToString("N");

        public OutboxRelayService(IOutboxStore outboxStore, IIntegrationLogger logger, OutboxRelayOptions options)
        {
            this.outboxStore = outboxStore ?? throw new ArgumentNullException(nameof(outboxStore));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>
        /// Публикует пакет pending-сообщений.
        /// </summary>
        public async Task RelayBatchAsync(int batchSize = 20, CancellationToken cancellationToken = default)
        {
            await outboxStore.ReleaseExpiredClaimsAsync(cancellationToken).ConfigureAwait(false);

            var claimed = await outboxStore
                .ClaimPendingAsync(batchSize, workerId, options.LockDuration, cancellationToken)
                .ConfigureAwait(false);

            foreach (var message in claimed)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (message.AttemptCount >= options.MaxAttempts)
                {
                    await outboxStore
                        .MarkAbandonedAsync(message.Id, workerId, "Max attempts exceeded.", cancellationToken)
                        .ConfigureAwait(false);
                    logger.LogWarn(
                        $"Outbox relay. Message '{message.Id}' exceeded max attempts ({options.MaxAttempts}). Abandoned.");
                    continue;
                }

                try
                {
                    var configuration = RabbitMqPublishConfigurationLoader.LoadProfile(message.ProfileName);
                    using var connection = new RabbitMqPublishConnection(configuration);
                    var transmitter = new RabbitMqPublishTransmitter(configuration, connection);
                    var transmitData = new TransmitData(message.Payload, message.Id.ToString("N"))
                        .WithCorrelationId(message.Id.ToString("N"));

                    transmitter.TransmitWithResult(transmitData);
                    await outboxStore.MarkPublishedAsync(message.Id, workerId, cancellationToken).ConfigureAwait(false);
                    logger.LogInfo($"Outbox relay. Message '{message.Id}' published via profile '{message.ProfileName}'.");
                }
                catch (Exception ex)
                {
                    var retryAfter = CalculateRetryDelay(message.AttemptCount);
                    await outboxStore
                        .MarkFailedAsync(message.Id, workerId, ex.Message, retryAfter, cancellationToken)
                        .ConfigureAwait(false);
                    logger.LogException($"Outbox relay. Failed to publish message '{message.Id}'.", ex);
                }
            }
        }

        private TimeSpan CalculateRetryDelay(int attemptCount)
        {
            var multiplier = Math.Max(1, attemptCount + 1);
            return TimeSpan.FromTicks(options.RetryBackoffBase.Ticks * multiplier);
        }
    }
}
