using System.Text;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Messages
{
    /// <summary>
    /// Сообщение, полученное из очереди RabbitMQ.
    /// </summary>
    public sealed class RabbitMqReceivedMessage : IIntegrationMessageMetadata
    {
        /// <summary>
        /// Тело сообщения.
        /// </summary>
        public byte[] Body { get; }

        /// <summary>
        /// Тело сообщения в виде UTF-8 строки.
        /// </summary>
        public string BodyText => Encoding.UTF8.GetString(Body);

        /// <summary>
        /// Ключ маршрутизации.
        /// </summary>
        public string RoutingKey { get; }

        /// <summary>
        /// Идентификатор сообщения из AMQP properties.
        /// </summary>
        public string MessageId { get; }

        /// <summary>
        /// Correlation id из AMQP properties.
        /// </summary>
        public string CorrelationId { get; }

        /// <summary>
        /// Тег доставки для подтверждения на стороне брокера.
        /// </summary>
        internal ulong DeliveryTag { get; }

        internal RabbitMqReceivedMessage(
            byte[] body,
            ulong deliveryTag,
            string routingKey,
            string messageId,
            string correlationId)
        {
            Body = body;
            DeliveryTag = deliveryTag;
            RoutingKey = routingKey ?? string.Empty;
            MessageId = messageId ?? string.Empty;
            CorrelationId = correlationId ?? string.Empty;
        }
    }
}
