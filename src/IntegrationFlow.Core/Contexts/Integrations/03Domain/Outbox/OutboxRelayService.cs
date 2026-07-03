using System;
using System.Linq;
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

            foreach (var profileGroup in claimed.GroupBy(message => message.ProfileName))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await RelayProfileGroupAsync(profileGroup.Key, profileGroup, cancellationToken).ConfigureAwait(false);
            }
        }

        internal TimeSpan CalculateRetryDelay(int attemptCount)
        {
            if (!options.UseExponentialBackoff)
            {
                return TimeSpan.FromTicks(options.RetryBackoffBase.Ticks * Math.Max(1, attemptCount + 1));
            }

            var delay = TimeSpan.FromTicks(
                (long)(options.RetryBackoffBase.Ticks * Math.Pow(options.BackoffMultiplier, attemptCount)));
            return delay > options.MaxRetryDelay ? options.MaxRetryDelay : delay;
        }

        private async Task RelayProfileGroupAsync(
            string profileName,
            System.Collections.Generic.IEnumerable<OutboxMessage> messages,
            CancellationToken cancellationToken)
        {
            RabbitMqPublishConfiguration configuration = null;
            RabbitMqPublishConnection connection = null;
            RabbitMqPublishTransmitter transmitter = null;

            try
            {
                foreach (var message in messages)
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
                        if (connection == null || connection.NeedReconnect())
                        {
                            connection?.Dispose();
                            configuration = RabbitMqPublishConfigurationLoader.LoadProfile(profileName);
                            connection = new RabbitMqPublishConnection(configuration);
                            transmitter = new RabbitMqPublishTransmitter(configuration, connection);
                        }

                        var transmitData = new TransmitData(message.Payload, message.Id.ToString("N"))
                            .WithCorrelationId(message.Id.ToString("N"));

                        transmitter!.TransmitWithResult(transmitData);
                        await outboxStore.MarkPublishedAsync(message.Id, workerId, cancellationToken).ConfigureAwait(false);
                        logger.LogInfo($"Outbox relay. Message '{message.Id}' published via profile '{profileName}'.");
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
            finally
            {
                connection?.Dispose();
            }
        }
    }
}
