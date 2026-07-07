using System;
using System.Text;
using System.Text.Json;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndForgot.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndForgot.Connections;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndForgot.Exceptions;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.Tracing;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot.Transmitter;
using RabbitMQ.Client;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndForgot.Transmitters
{
    /// <summary>
    /// Публикация сообщений в RabbitMQ для SentAndForgot.
    /// </summary>
    internal sealed class RabbitMqPublishTransmitter : ITransmitter, ITransmitterWithResult
    {
        private readonly RabbitMqPublishConfiguration configuration;
        private readonly RabbitMqPublishConnection connection;

        public RabbitMqPublishTransmitter(RabbitMqPublishConfiguration configuration, RabbitMqPublishConnection connection)
        {
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        public void Transmit(TransmitData transmitData)
        {
            TransmitWithResult(transmitData);
        }

        public TransmitResult TransmitWithResult(TransmitData transmitData)
        {
            configuration.Validate();

            var channel = connection.Channel;
            if (configuration.ValidateTopology)
            {
                ValidateTopologyPassive(channel);
            }

            var messageId = ResolveMessageId(transmitData);
            var body = SerializeBody(transmitData.Data);
            var destination = configuration.GetPublishRoutingKey();

            using (RabbitMqDistributedTracing.StartProducerActivity(
                       "publish",
                       configuration.Name,
                       destination,
                       messageId,
                       transmitData.CorrelationId))
            {
                var properties = channel.CreateBasicProperties();
                RabbitMqBasicPropertiesMapper.ApplyDeliveryProperties(
                    properties,
                    configuration.ContentType,
                    configuration.Persistent,
                    configuration.Priority,
                    configuration.ExpirationMilliseconds);
                properties.MessageId = messageId;

                if (!string.IsNullOrWhiteSpace(transmitData.CorrelationId))
                {
                    properties.CorrelationId = transmitData.CorrelationId;
                }

                RabbitMqTracePropagation.Inject(properties);

                connection.ResetUnroutableFlag();

                channel.BasicPublish(
                    exchange: configuration.GetPublishExchange(),
                    routingKey: configuration.GetPublishRoutingKey(),
                    mandatory: configuration.Mandatory,
                    basicProperties: properties,
                    body: body);

                if (configuration.PublisherConfirmsEnabled)
                {
                    RabbitMqPublisherConfirms.EnsureConfirmed(
                        channel,
                        configuration.PublisherConfirmsEnabled,
                        TimeSpan.FromSeconds(Math.Max(1, configuration.ConfirmTimeoutSeconds)));
                }

                if (configuration.Mandatory)
                {
                    connection.WaitForUnroutableProcessing(TimeSpan.FromMilliseconds(100));
                }

                connection.EnsureNotUnroutable();
            }

            return TransmitResult.Create(messageId);
        }

        private static string ResolveMessageId(TransmitData transmitData)
        {
            return string.IsNullOrWhiteSpace(transmitData.MessageId)
                ? Guid.NewGuid().ToString("N")
                : transmitData.MessageId;
        }

        private void ValidateTopologyPassive(IModel channel)
        {
            if (configuration.PublishTarget == RabbitMqPublishTarget.Queue)
            {
                channel.QueueDeclarePassive(configuration.QueueName);
                return;
            }

            channel.ExchangeDeclarePassive(configuration.Exchange);
        }

        internal static byte[] SerializeBody(object data)
        {
            return data switch
            {
                null => Array.Empty<byte>(),
                byte[] bytes => bytes,
                ReadOnlyMemory<byte> memory => memory.ToArray(),
                Memory<byte> memory => memory.ToArray(),
                string text => Encoding.UTF8.GetBytes(text),
                _ => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data))
            };
        }
    }
}
