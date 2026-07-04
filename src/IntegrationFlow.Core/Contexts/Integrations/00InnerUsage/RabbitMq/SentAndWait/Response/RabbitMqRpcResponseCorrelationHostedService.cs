#if NET8_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Configurations;
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
        private readonly IReadOnlyList<RabbitMqRequestReplyConfiguration> profiles;

        public RabbitMqRpcResponseCorrelationHostedService(
            IRpcPendingStore pendingStore,
            IIntegrationFlowMetrics? metrics = null)
        {
            this.pendingStore = pendingStore ?? throw new ArgumentNullException(nameof(pendingStore));
            this.metrics = metrics;
            profiles = LoadAsyncOutboxProfiles();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (profiles.Count == 0)
            {
                return;
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
            var factory = RabbitMqConnectionFactory.Create(configuration.ToConnectionSettings());
            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            channel.QueueDeclarePassive(configuration.ResponseQueueName);
            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.Received += async (_, eventArgs) =>
            {
                try
                {
                    var correlationId = eventArgs.BasicProperties?.CorrelationId;
                    if (!Guid.TryParse(correlationId, out var pendingId))
                    {
                        channel.BasicAck(eventArgs.DeliveryTag, multiple: false);
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

                    channel.BasicAck(eventArgs.DeliveryTag, multiple: false);
                }
                catch
                {
                    channel.BasicNack(eventArgs.DeliveryTag, multiple: false, requeue: true);
                }
            };

            channel.BasicConsume(
                queue: configuration.ResponseQueueName,
                autoAck: false,
                consumer: consumer);

            try
            {
                await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
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
