using System;
using System.Text;
using System.Text.Json;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndForgot.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndForgot.Connections;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot.Transmitter;
using RabbitMQ.Client;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndForgot.Transmitters
{
    /// <summary>
    /// Публикация сообщений в RabbitMQ для SentAndForgot.
    /// </summary>
    internal sealed class RabbitMqPublishTransmitter : ITransmitter
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
            configuration.Validate();

            var channel = connection.Channel;
            if (configuration.ValidateTopology)
            {
                ValidateTopologyPassive(channel);
            }

            var body = SerializeBody(transmitData.Data);
            var properties = channel.CreateBasicProperties();
            properties.ContentType = configuration.ContentType;
            properties.DeliveryMode = configuration.Persistent ? (byte)2 : (byte)1;

            channel.BasicPublish(
                exchange: configuration.GetPublishExchange(),
                routingKey: configuration.GetPublishRoutingKey(),
                mandatory: configuration.Mandatory,
                basicProperties: properties,
                body: body);
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
