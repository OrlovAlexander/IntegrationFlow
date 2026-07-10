using System;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.Logging;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Connections;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.Tracing;
using IntegrationFlow.Contexts.Integrations._01Infrastructure;
using RabbitMQ.Client;

namespace IntegrationFlow.Contexts.Integrations._03Domain.RpcPending;

internal sealed class RabbitMqRpcPendingPublisher : IRpcPendingPublisher
{
    private readonly RabbitMqRequestReplyConfiguration configuration;
    private RabbitMqRequestReplyConnection? connection;

    public RabbitMqRpcPendingPublisher(RabbitMqRequestReplyConfiguration configuration)
    {
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        EnsureAsyncOutboxConfiguration(configuration);
    }

    public RpcPendingTransportKind TransportKind => RpcPendingTransportKind.RabbitMq;

    public void PublishPendingRequest(RpcPendingRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (connection == null || connection.NeedReconnect())
        {
            connection?.Dispose();
            connection = new RabbitMqRequestReplyConnection(configuration);
        }

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

    public void Dispose()
    {
        connection?.Dispose();
    }

    internal static void EnsureAsyncOutboxConfiguration(RabbitMqRequestReplyConfiguration configuration)
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
