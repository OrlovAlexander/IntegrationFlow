using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.Health;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.Logging;
using IntegrationFlow.Contexts.Integrations._01Infrastructure;
using IntegrationFlow.Contexts.Integrations._03Domain;
using IntegrationFlow.Contexts.Integrations._03Domain.Metrics;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot;

namespace IntegrationFlow.Contexts.Integrations._03Domain.Outbox
{
    /// <summary>
    /// Релей pending-сообщений из transactional outbox в RabbitMQ или REST.
    /// </summary>
    public sealed class OutboxRelayService
    {
        private readonly IOutboxStore outboxStore;
        private readonly IIntegrationLogger logger;
        private readonly OutboxRelayOptions options;
        private readonly IIntegrationFlowMetrics? metrics;
        private readonly RabbitMqTransportHealthRegistry? healthRegistry;
        private readonly IOutboxTransportResolver transportResolver;
        private readonly string workerId = Guid.NewGuid().ToString("N");

        public OutboxRelayService(
            IOutboxStore outboxStore,
            IIntegrationLogger logger,
            OutboxRelayOptions options,
            IIntegrationFlowMetrics? metrics = null,
            RabbitMqTransportHealthRegistry? healthRegistry = null,
            IOutboxTransportResolver? transportResolver = null)
        {
            this.outboxStore = outboxStore ?? throw new ArgumentNullException(nameof(outboxStore));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            this.metrics = metrics;
            this.healthRegistry = healthRegistry;
            this.transportResolver = transportResolver ?? new OutboxTransportResolver();
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
            var published = 0;
            var failed = 0;
            var abandoned = 0;

            using var publisher = transportResolver.CreatePublisher(profileName);
            var transmitter = publisher.Transmitter;

            foreach (var message in messages)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (message.AttemptCount >= options.MaxAttempts)
                {
                    await outboxStore
                        .MarkAbandonedAsync(message.Id, workerId, "Max attempts exceeded.", cancellationToken)
                        .ConfigureAwait(false);
                    using (BeginRelayScope(publisher.TransportKind, profileName, message.Id.ToString("N")))
                    using (IntegrationStructuredLogging.BeginOutcomeScope(logger, "abandoned"))
                    {
                        logger.LogWarn(
                            $"Outbox relay. Message exceeded max attempts ({options.MaxAttempts}).");
                    }

                    abandoned++;
                    continue;
                }

                try
                {
                    var messageId = message.Id.ToString("N");
                    var transmitData = new TransmitData(message.Payload, messageId)
                        .WithCorrelationId(messageId);

                    using (BeginRelayScope(publisher.TransportKind, profileName, messageId))
                    {
                        transmitter.TransmitWithResult(transmitData);
                        await outboxStore.MarkPublishedAsync(message.Id, workerId, cancellationToken).ConfigureAwait(false);
                        using (IntegrationStructuredLogging.BeginOutcomeScope(logger, "published"))
                        {
                            logger.LogInfo("Outbox relay. Message published.");
                        }
                    }

                    published++;
                }
                catch (Exception ex) when (ex is INonRetryableOutboxPublishException)
                {
                    await outboxStore
                        .MarkAbandonedAsync(message.Id, workerId, ex.Message, cancellationToken)
                        .ConfigureAwait(false);
                    using (BeginRelayScope(publisher.TransportKind, profileName, message.Id.ToString("N")))
                    using (IntegrationStructuredLogging.BeginOutcomeScope(logger, "abandoned"))
                    {
                        logger.LogException("Outbox relay. Non-retryable publish failure.", ex);
                    }

                    abandoned++;
                }
                catch (Exception ex)
                {
                    var retryAfter = CalculateRetryDelay(message.AttemptCount);
                    await outboxStore
                        .MarkFailedAsync(message.Id, workerId, ex.Message, retryAfter, cancellationToken)
                        .ConfigureAwait(false);
                    using (BeginRelayScope(publisher.TransportKind, profileName, message.Id.ToString("N")))
                    using (IntegrationStructuredLogging.BeginOutcomeScope(logger, "publish_failed"))
                    {
                        logger.LogException("Outbox relay. Failed to publish message.", ex);
                    }

                    failed++;
                }
            }

            return new RelayProfileGroupResult(published, failed, abandoned);
        }

        private IDisposable BeginRelayScope(OutboxTransportKind transportKind, string profileName, string messageId)
        {
            return transportKind == OutboxTransportKind.RabbitMq
                ? RabbitMqStructuredLogging.BeginPublishScope(
                    logger,
                    profileName,
                    messageId,
                    messageId,
                    RabbitMqTransportLogKind.OutboxRelay)
                : IntegrationStructuredLogging.BeginScope(
                    logger,
                    ("transport", "rest"),
                    ("profile", profileName),
                    ("messageId", messageId),
                    ("kind", "outbox_relay"));
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
