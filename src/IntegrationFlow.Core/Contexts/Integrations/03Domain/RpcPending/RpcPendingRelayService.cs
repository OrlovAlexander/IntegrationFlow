using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.Logging;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndForgot.Transmitters;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Connections;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.Tracing;
using IntegrationFlow.Contexts.Integrations._01Infrastructure;
using IntegrationFlow.Contexts.Integrations._01Infrastructure.Localization;
using IntegrationFlow.Contexts.Integrations._03Domain.Metrics;
using RabbitMQ.Client;

namespace IntegrationFlow.Contexts.Integrations._03Domain.RpcPending
{
    /// <summary>
    /// Relay pending async RPC requests to RabbitMQ request queue.
    /// </summary>
    public sealed class RpcPendingRelayService
    {
        private readonly IRpcPendingStore pendingStore;
        private readonly IIntegrationLogger logger;
        private readonly RpcPendingRelayOptions options;
        private readonly IIntegrationFlowMetrics? metrics;
        private readonly Func<string, RabbitMqRequestReplyConfiguration> configurationLoader;
        private readonly string workerId = Guid.NewGuid().ToString("N");

        public RpcPendingRelayService(
            IRpcPendingStore pendingStore,
            IIntegrationLogger logger,
            RpcPendingRelayOptions options,
            Func<string, RabbitMqRequestReplyConfiguration>? configurationLoader = null,
            IIntegrationFlowMetrics? metrics = null)
        {
            this.pendingStore = pendingStore ?? throw new ArgumentNullException(nameof(pendingStore));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            this.configurationLoader = configurationLoader ?? RabbitMqRequestReplyConfigurationLoader.LoadProfile;
            this.metrics = metrics;
        }

        public async Task RelayBatchAsync(int batchSize = 20, CancellationToken cancellationToken = default)
        {
            await pendingStore.ReleaseExpiredClaimsAsync(cancellationToken).ConfigureAwait(false);

            var claimed = await pendingStore
                .ClaimPendingAsync(batchSize, workerId, options.LockDuration, cancellationToken)
                .ConfigureAwait(false);

            var publishedCount = 0;
            var failedCount = 0;
            var abandonedCount = 0;

            foreach (var profileGroup in claimed.GroupBy(request => request.ProfileName))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await RelayProfileGroupAsync(profileGroup.Key, profileGroup, cancellationToken)
                    .ConfigureAwait(false);
                publishedCount += result.Published;
                failedCount += result.Failed;
                abandonedCount += result.Abandoned;
            }

            metrics?.RecordRpcPendingRelayPublished(publishedCount);
            metrics?.RecordRpcPendingRelayFailed(failedCount);
            metrics?.RecordRpcPendingRelayAbandoned(abandonedCount);
            var awaiting = await pendingStore.GetAwaitingResponseCountAsync(cancellationToken).ConfigureAwait(false);
            metrics?.RecordRpcPendingAwaiting(awaiting);
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
            System.Collections.Generic.IEnumerable<RpcPendingRequest> requests,
            CancellationToken cancellationToken)
        {
            RabbitMqRequestReplyConfiguration? configuration = null;
            RabbitMqRequestReplyConnection? connection = null;
            var published = 0;
            var failed = 0;
            var abandoned = 0;

            try
            {
                foreach (var request in requests)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (request.AttemptCount >= options.MaxAttempts)
                    {
                        await pendingStore
                            .MarkAbandonedAsync(request.Id, workerId, "Max attempts exceeded.", cancellationToken)
                            .ConfigureAwait(false);
                        using (RabbitMqStructuredLogging.BeginPublishScope(
                                   logger,
                                   profileName,
                                   request.Id.ToString("N"),
                                   request.Id.ToString("N"),
                                   RabbitMqTransportLogKind.RequestReply))
                        using (IntegrationStructuredLogging.BeginOutcomeScope(logger, "abandoned"))
                        {
                            logger.LogWarn(
                                $"Rpc pending relay. Request exceeded max attempts ({options.MaxAttempts}).");
                        }

                        abandoned++;
                        continue;
                    }

                    try
                    {
                        if (connection == null || connection.NeedReconnect())
                        {
                            connection?.Dispose();
                            configuration = configurationLoader(profileName);
                            EnsureAsyncOutboxConfiguration(configuration);
                            connection = new RabbitMqRequestReplyConnection(configuration);
                        }

                        var messageId = request.Id.ToString("N");
                        using (RabbitMqStructuredLogging.BeginPublishScope(
                                   logger,
                                   profileName,
                                   messageId,
                                   messageId,
                                   RabbitMqTransportLogKind.RequestReply))
                        {
                            PublishPendingRequest(configuration!, connection!, request);
                            await pendingStore
                                .MarkAwaitingResponseAsync(request.Id, workerId, cancellationToken)
                                .ConfigureAwait(false);
                            using (IntegrationStructuredLogging.BeginOutcomeScope(logger, "published"))
                            {
                                logger.LogInfo("Rpc pending relay. Request published.");
                            }
                        }

                        published++;
                    }
                    catch (Exception ex)
                    {
                        var retryAfter = CalculateRetryDelay(request.AttemptCount);
                        await pendingStore
                            .MarkFailedAsync(request.Id, workerId, ex.Message, retryAfter, cancellationToken)
                            .ConfigureAwait(false);
                        using (RabbitMqStructuredLogging.BeginPublishScope(
                                   logger,
                                   profileName,
                                   request.Id.ToString("N"),
                                   request.Id.ToString("N"),
                                   RabbitMqTransportLogKind.RequestReply))
                        using (IntegrationStructuredLogging.BeginOutcomeScope(logger, "publish_failed"))
                        {
                            logger.LogException("Rpc pending relay. Failed to publish request.", ex);
                        }

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

        private static void EnsureAsyncOutboxConfiguration(RabbitMqRequestReplyConfiguration configuration)
        {
            configuration.Validate();
            if (configuration.RequestMode != RabbitMqRequestReplyRequestMode.AsyncOutbox)
            {
                throw new InvalidOperationException(
                    $"Profile '{configuration.Name}' must use RequestMode=AsyncOutbox for rpc pending relay.");
            }

            if (string.IsNullOrWhiteSpace(configuration.ResponseQueueName))
            {
                throw new InvalidOperationException(
                    $"Profile '{configuration.Name}' requires ResponseQueueName for AsyncOutbox mode.");
            }
        }

        private static void PublishPendingRequest(
            RabbitMqRequestReplyConfiguration configuration,
            RabbitMqRequestReplyConnection connection,
            RpcPendingRequest request)
        {
            var channel = connection.PublishChannel;
            if (configuration.ValidateTopology)
            {
                EnsureRequestTopology(channel, configuration);
            }

            var messageId = request.Id.ToString("N");
            var body = request.RequestPayload;
            var destination = configuration.GetRequestRoutingKey();

            using (RabbitMqDistributedTracing.StartProducerActivity(
                       "request",
                       configuration.Name,
                       destination,
                       messageId,
                       messageId))
            {
                var properties = channel.CreateBasicProperties();
                RabbitMqBasicPropertiesMapper.ApplyDeliveryProperties(
                    properties,
                    request.ContentType,
                    configuration.Persistent,
                    configuration.Priority,
                    configuration.ExpirationMilliseconds);
                properties.CorrelationId = messageId;
                properties.ReplyTo = configuration.ResponseQueueName;
                properties.MessageId = messageId;

                RabbitMqTracePropagation.Inject(properties);

                channel.BasicPublish(
                    exchange: configuration.GetRequestExchange(),
                    routingKey: configuration.GetRequestRoutingKey(),
                    mandatory: configuration.Mandatory,
                    basicProperties: properties,
                    body: body);
            }
        }

        private static void EnsureRequestTopology(IModel channel, RabbitMqRequestReplyConfiguration configuration)
        {
            var options = new RabbitMqTopologyHelper.TopologyOptions
            {
                ValidateTopology = true,
                DeclareTopologyOnStartup = configuration.DeclareTopologyOnStartup,
                Durable = configuration.Persistent,
                ExchangeType = configuration.ExchangeType,
            };

            if (configuration.RequestTarget == RabbitMqRequestReplyTarget.Queue)
            {
                RabbitMqTopologyHelper.EnsureQueue(
                    channel,
                    configuration.QueueName,
                    options,
                    profileName: configuration.Name);
                return;
            }

            RabbitMqTopologyHelper.EnsureExchange(
                channel,
                configuration.Exchange,
                options,
                profileName: configuration.Name);
        }
    }
}
