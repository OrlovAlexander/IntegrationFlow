using System;
using System.Text;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndForgot.Transmitters;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Configurations;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.Cfg;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.Transmitter;
using RabbitMQ.Client;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Transmitters
{
    /// <summary>
    /// Request-reply transmitter для SentAndWait через RabbitMQ.
    /// </summary>
    internal sealed class RabbitMqRequestReplyTransmitter : ITransmitter
    {
        private readonly RabbitMqRequestReplyConfiguration configuration;
        private readonly Connections.RabbitMqRequestReplyConnection connection;
        private readonly object transmitSync = new();

        public RabbitMqRequestReplyTransmitter(
            IConfiguration configuration,
            Connections.RabbitMqRequestReplyConnection connection)
        {
            this.configuration = (RabbitMqRequestReplyConfiguration)configuration
                ?? throw new ArgumentNullException(nameof(configuration));
            this.connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        public ObtainedData Transmit(TransmitData transmitData)
        {
            lock (transmitSync)
            {
                configuration.Validate();

                if (connection.NeedReconnect() && !connection.Reconnect())
                {
                    return new ObtainedData(null, isFailed: true);
                }

                var channel = connection.PublishChannel;
                if (configuration.ValidateTopology)
                {
                    ValidateTopologyPassive(channel);
                }

                var correlationId = Guid.NewGuid().ToString("N");
                var messageId = Guid.NewGuid().ToString("N");
                var body = RabbitMqPublishTransmitter.SerializeBody(transmitData.Data);

                var properties = channel.CreateBasicProperties();
                properties.ContentType = configuration.ContentType;
                properties.DeliveryMode = configuration.Persistent ? (byte)2 : (byte)1;
                properties.CorrelationId = correlationId;
                properties.ReplyTo = connection.ReplyAddress;
                properties.MessageId = messageId;

                connection.BeginWaitingForResponse(correlationId);
                try
                {
                    channel.BasicPublish(
                        exchange: configuration.GetRequestExchange(),
                        routingKey: configuration.GetRequestRoutingKey(),
                        mandatory: configuration.Mandatory,
                        basicProperties: properties,
                        body: body);

                    var responseBody = connection.CompleteWaitingForResponse(configuration.GetResponseTimeout());
                    return CreateObtainedData(responseBody);
                }
                catch
                {
                    connection.CancelWaitingForResponse();
                    throw;
                }
            }
        }

        private static ObtainedData CreateObtainedData(byte[] responseBody)
        {
            if (responseBody == null || responseBody.Length == 0)
            {
                return new ObtainedData(null, isFailed: true);
            }

            return new ObtainedData(Encoding.UTF8.GetString(responseBody));
        }

        private void ValidateTopologyPassive(IModel channel)
        {
            if (configuration.RequestTarget == RabbitMqRequestReplyTarget.Queue)
            {
                channel.QueueDeclarePassive(configuration.QueueName);
                return;
            }

            channel.ExchangeDeclarePassive(configuration.Exchange);
        }
    }
}
