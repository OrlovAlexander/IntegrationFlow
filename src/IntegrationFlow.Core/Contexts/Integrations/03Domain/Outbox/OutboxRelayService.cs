using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.Health;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndForgot.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndForgot.Connections;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndForgot.Transmitters;
using IntegrationFlow.Contexts.Integrations._03Domain.Metrics;
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
        private readonly IIntegrationFlowMetrics? metrics;
        private readonly RabbitMqTransportHealthRegistry? healthRegistry;
        private readonly string workerId = Guid.NewGuid().ToString("N");

        public OutboxRelayService(
            IOutboxStore outboxStore,
            IIntegrationLogger logger,
            OutboxRelayOptions options,
            IIntegrationFlowMetrics? metrics = null,
            RabbitMqTransportHealthRegistry? healthRegistry = null)
        {
            this.outboxStore = outboxStore ?? throw new ArgumentNullException(nameof(outboxStore));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            this.metrics = metrics;
            this.healthRegistry = healthRegistry;
        }

        /// <summary>
        /// Публикует пакет pending-сообщений.
        /// </summary>
        public async Task RelayBatchAsync(int batchSize = 20, CancellationToken cancellationToken = default)
        {
            try
            {
                await outboxStore.ReleaseExpiredClaimsAsync(cancellationToken).ConfigureAwait(false);

                var claimed = await outboxStore
                    .ClaimPendingAsync(batchSize, workerId, options.LockDuration, cancellationToken)
                    .ConfigureAwait(false);

                var publishedCount = 0;
                var failedCount = 0;
                var abandonedCount = 0;

                foreach (var profileGroup in claimed.GroupBy(message => message.ProfileName))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var result = await RelayProfileGroupAsync(profileGroup.Key, profileGroup, cancellationToken)
                        .ConfigureAwait(false);
                    publishedCount += result.Published;
                    failedCount += result.Failed;
                    abandonedCount += result.Abandoned;
                }

                metrics?.RecordOutboxRelayPublished(publishedCount);
                metrics?.RecordOutboxRelayFailed(failedCount);
                metrics?.RecordOutboxRelayAbandoned(abandonedCount);

                var pending = await outboxStore
                    .GetPendingAsync(int.MaxValue, cancellationToken)
                    .ConfigureAwait(false);
                metrics?.RecordOutboxPending(pending.Count);

                if (failedCount > 0 && publishedCount == 0 && claimed.Count > 0)
                {
                    healthRegistry?.ReportOutboxRelayBatchFailure("All publishes in batch failed.");
                }
                else
                {
                    healthRegistry?.ReportOutboxRelayBatchSuccess();
                }
            }
            catch (Exception ex)
            {
                healthRegistry?.ReportOutboxRelayBatchFailure(ex.Message);
                throw;
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

        private async Task<RelayProfileGroupResult> RelayProfileGroupAsync(
            string profileName,
            System.Collections.Generic.IEnumerable<OutboxMessage> messages,
            CancellationToken cancellationToken)
        {
            RabbitMqPublishConfiguration configuration = null;
            RabbitMqPublishConnection connection = null;
            RabbitMqPublishTransmitter transmitter = null;
            var published = 0;
            var failed = 0;
            var abandoned = 0;

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
                        abandoned++;
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
                        published++;
                    }
                    catch (Exception ex)
                    {
                        var retryAfter = CalculateRetryDelay(message.AttemptCount);
                        await outboxStore
                            .MarkFailedAsync(message.Id, workerId, ex.Message, retryAfter, cancellationToken)
                            .ConfigureAwait(false);
                        logger.LogException($"Outbox relay. Failed to publish message '{message.Id}'.", ex);
                        failed++;
                    }
                }
            }
            finally
            {
                connection?.Dispose();
            }

            return new RelayProfileGroupResult(published, failed, abandoned);
        }

        private readonly struct RelayProfileGroupResult
        {
            public RelayProfileGroupResult(int published, int failed, int abandoned)
            {
                Published = published;
                Failed = failed;
                Abandoned = abandoned;
            }

            public int Published { get; }

            public int Failed { get; }

            public int Abandoned { get; }
        }
    }
}
