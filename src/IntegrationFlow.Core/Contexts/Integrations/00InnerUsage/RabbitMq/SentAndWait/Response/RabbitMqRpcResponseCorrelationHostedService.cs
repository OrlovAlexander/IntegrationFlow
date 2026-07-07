#if NET8_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.Health;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.Logging;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Listeners;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.Tracing;
using IntegrationFlow.Contexts.Integrations._01Infrastructure;
using IntegrationFlow.Contexts.Integrations._03Domain;
using IntegrationFlow.Contexts.Integrations._03Domain.Metrics;
using IntegrationFlow.Contexts.Integrations._03Domain.RpcPending;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Response
{
    /// <summary>
    /// Consumes AsyncOutbox response queues and completes pending RPC requests.
    /// </summary>
    internal sealed class RabbitMqRpcResponseCorrelationHostedService : BackgroundService
    {
        private readonly IRpcPendingStore pendingStore;
        private readonly IIntegrationLogger logger;
        private readonly IIntegrationFlowMetrics? metrics;
        private readonly RabbitMqTransportHealthRegistry? healthRegistry;
        private readonly IReadOnlyList<RabbitMqRequestReplyConfiguration> profiles;

        public RabbitMqRpcResponseCorrelationHostedService(
            IRpcPendingStore pendingStore,
            IIntegrationLogger logger,
            IIntegrationFlowMetrics? metrics = null,
            RabbitMqTransportHealthRegistry? healthRegistry = null)
        {
            this.pendingStore = pendingStore ?? throw new ArgumentNullException(nameof(pendingStore));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.metrics = metrics;
            this.healthRegistry = healthRegistry;
            profiles = LoadAsyncOutboxProfiles();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (profiles.Count == 0)
            {
                return;
            }

            foreach (var profile in profiles)
            {
                healthRegistry?.Register(RabbitMqTransportKind.RpcCorrelation, profile.Name);
            }

            var workers = profiles
                .Select(profile => RunProfileConsumerAsync(profile, stoppingToken))
                .ToArray();

            await Task.WhenAll(workers).ConfigureAwait(false);
        }

        private async Task RunProfileConsumerAsync(
            RabbitMqRequestReplyConfiguration configuration,
            CancellationToken stoppingToken)
        {
            var profileName = configuration.Name;
            var reconnectAttempt = 0;

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    IConnection? connection = null;
                    IModel? channel = null;

                    try
                    {
                        var factory = RabbitMqConnectionFactory.Create(configuration.ToConnectionSettings());
                        connection = factory.CreateConnection();
                        channel = connection.CreateModel();
                        var channelSync = new object();
                        var acknowledgement = new RabbitMqChannelAcknowledgement(
                            channelSync,
                            () => channel,
                            logger);

                        RabbitMqTopologyHelper.EnsureQueue(
                            channel,
                            configuration.ResponseQueueName,
                            new RabbitMqTopologyHelper.TopologyOptions
                            {
                                ValidateTopology = configuration.ValidateTopology,
                                DeclareTopologyOnStartup = configuration.DeclareTopologyOnStartup,
                                Durable = configuration.Persistent,
                            },
                            logger,
                            profileName);
                        var consumer = new AsyncEventingBasicConsumer(channel);
                        consumer.Received += async (_, eventArgs) =>
                        {
                            var correlationId = eventArgs.BasicProperties?.CorrelationId ?? string.Empty;
                            var messageId = eventArgs.BasicProperties?.MessageId ?? correlationId;
                            using (RabbitMqDistributedTracing.StartConsumerActivity(
                                       eventArgs.BasicProperties?.Headers,
                                       "response",
                                       profileName,
                                       messageId,
                                       correlationId,
                                       eventArgs.DeliveryTag))
                            using (IntegrationStructuredLogging.BeginScope(
                                       logger,
                                       (IntegrationStructuredLogFields.Profile, profileName),
                                       (IntegrationStructuredLogFields.MessageId, messageId),
                                       (IntegrationStructuredLogFields.CorrelationId, correlationId),
                                       (IntegrationStructuredLogFields.DeliveryTag, eventArgs.DeliveryTag),
                                       (IntegrationStructuredLogFields.Kind, "rpc_correlation")))
                            {
                                try
                                {
                                    using (IntegrationStructuredLogging.BeginOutcomeScope(logger, "consume_started"))
                                    {
                                        logger.Log("RabbitMQ rpc correlation. Получен ответ.");
                                    }

                                    if (!Guid.TryParse(correlationId, out var pendingId))
                                    {
                                        using (IntegrationStructuredLogging.BeginOutcomeScope(logger, "ack"))
                                        {
                                            acknowledgement.Acknowledge(eventArgs.DeliveryTag);
                                            logger.Log("RabbitMQ rpc correlation. Ответ без pending id подтверждён.");
                                        }

                                        return;
                                    }

                                    var pending = await pendingStore
                                        .GetByIdAsync(pendingId, stoppingToken)
                                        .ConfigureAwait(false);

                                    await pendingStore
                                        .CompleteAsync(pendingId, eventArgs.Body.ToArray(), stoppingToken)
                                        .ConfigureAwait(false);

                                    if (pending != null)
                                    {
                                        metrics?.RecordRpcPendingCompleted(
                                            pending.ProfileName,
                                            DateTimeOffset.UtcNow - pending.CreatedAt,
                                            success: true);
                                    }

                                    using (IntegrationStructuredLogging.BeginOutcomeScope(logger, "ack"))
                                    {
                                        acknowledgement.Acknowledge(eventArgs.DeliveryTag);
                                        logger.Log("RabbitMQ rpc correlation. Ответ обработан.");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    logger.LogException("RabbitMQ rpc correlation. Ошибка обработки ответа.", ex);
                                    using (IntegrationStructuredLogging.BeginOutcomeScope(logger, "requeue"))
                                    {
                                        acknowledgement.NegativeAcknowledge(eventArgs.DeliveryTag, requeue: true);
                                        logger.Log("RabbitMQ rpc correlation. Ответ возвращён в очередь.");
                                    }
                                }
                            }
                        };

                        channel.BasicConsume(
                            queue: configuration.ResponseQueueName,
                            autoAck: false,
                            consumer: consumer);

                        reconnectAttempt = 0;
                        healthRegistry?.ReportConnected(RabbitMqTransportKind.RpcCorrelation, profileName);
                        using (RabbitMqStructuredLogging.BeginTransportScope(
                                   logger,
                                   profileName,
                                   RabbitMqTransportLogKind.RpcCorrelation))
                        {
                            logger.LogInfo(
                                $"RabbitMQ rpc correlation. Подключение к очереди '{configuration.ResponseQueueName}' установлено.");
                        }

                        var sessionEndedByCancellation = await RabbitMqConsumerSessionLifecycle
                            .WaitForSessionEndAsync(connection, stoppingToken)
                            .ConfigureAwait(false);

                        if (sessionEndedByCancellation)
                        {
                            break;
                        }

                        reconnectAttempt++;
                        healthRegistry?.ReportReconnecting(
                            RabbitMqTransportKind.RpcCorrelation,
                            profileName,
                            reconnectAttempt);
                        await RabbitMqConsumerSessionLifecycle
                            .DelayReconnectAsync(reconnectAttempt, stoppingToken)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        reconnectAttempt++;
                        healthRegistry?.ReportReconnecting(
                            RabbitMqTransportKind.RpcCorrelation,
                            profileName,
                            reconnectAttempt,
                            ex.Message);
                        using (RabbitMqStructuredLogging.BeginTransportScope(
                                   logger,
                                   profileName,
                                   RabbitMqTransportLogKind.RpcCorrelation))
                        {
                            logger.LogException(
                                $"RabbitMQ rpc correlation. Ошибка прослушивания очереди '{configuration.ResponseQueueName}'.",
                                ex);
                        }

                        if (stoppingToken.IsCancellationRequested)
                        {
                            break;
                        }

                        await RabbitMqConsumerSessionLifecycle
                            .DelayReconnectAsync(reconnectAttempt, stoppingToken)
                            .ConfigureAwait(false);
                    }
                    finally
                    {
                        try
                        {
                            channel?.Close();
                        }
                        catch
                        {
                        }
                        finally
                        {
                            channel?.Dispose();
                        }

                        try
                        {
                            connection?.Close();
                        }
                        catch
                        {
                        }
                        finally
                        {
                            connection?.Dispose();
                        }

                        healthRegistry?.ReportDisconnected(RabbitMqTransportKind.RpcCorrelation, profileName);
                    }
                }
            }
            finally
            {
                healthRegistry?.ReportStopped(RabbitMqTransportKind.RpcCorrelation, profileName);
            }
        }

        private static IReadOnlyList<RabbitMqRequestReplyConfiguration> LoadAsyncOutboxProfiles()
        {
            try
            {
                return RabbitMqRequestReplyConfigurationLoader
                    .LoadAll()
                    .Where(profile => profile.RequestMode == RabbitMqRequestReplyRequestMode.AsyncOutbox)
                    .Where(profile => !string.IsNullOrWhiteSpace(profile.ResponseQueueName))
                    .ToArray();
            }
            catch
            {
                return Array.Empty<RabbitMqRequestReplyConfiguration>();
            }
        }
    }
}
#endif
