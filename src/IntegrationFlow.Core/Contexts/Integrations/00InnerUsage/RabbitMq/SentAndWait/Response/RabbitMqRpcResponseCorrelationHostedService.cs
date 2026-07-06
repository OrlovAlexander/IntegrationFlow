#if NET8_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.Health;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Listeners;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Configurations;
using IntegrationFlow.Contexts.Integrations._01Infrastructure;
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
        private readonly IIntegrationFlowMetrics? metrics;
        private readonly RabbitMqTransportHealthRegistry? healthRegistry;
        private readonly IReadOnlyList<RabbitMqRequestReplyConfiguration> profiles;

        public RabbitMqRpcResponseCorrelationHostedService(
            IRpcPendingStore pendingStore,
            IIntegrationFlowMetrics? metrics = null,
            RabbitMqTransportHealthRegistry? healthRegistry = null)
        {
            this.pendingStore = pendingStore ?? throw new ArgumentNullException(nameof(pendingStore));
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
                            NullIntegrationLogger.Instance);

                        channel.QueueDeclarePassive(configuration.ResponseQueueName);
                        var consumer = new AsyncEventingBasicConsumer(channel);
                        consumer.Received += async (_, eventArgs) =>
                        {
                            try
                            {
                                var correlationId = eventArgs.BasicProperties?.CorrelationId;
                                if (!Guid.TryParse(correlationId, out var pendingId))
                                {
                                    acknowledgement.Acknowledge(eventArgs.DeliveryTag);
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

                                acknowledgement.Acknowledge(eventArgs.DeliveryTag);
                            }
                            catch
                            {
                                acknowledgement.NegativeAcknowledge(eventArgs.DeliveryTag, requeue: true);
                            }
                        };

                        channel.BasicConsume(
                            queue: configuration.ResponseQueueName,
                            autoAck: false,
                            consumer: consumer);

                        reconnectAttempt = 0;
                        healthRegistry?.ReportConnected(RabbitMqTransportKind.RpcCorrelation, profileName);

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
