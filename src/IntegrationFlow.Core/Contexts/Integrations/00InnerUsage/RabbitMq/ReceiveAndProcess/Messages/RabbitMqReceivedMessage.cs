using System.Collections.Generic;
using System.Text;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Messages
{
    /// <summary>
    /// Сообщение, полученное из очереди RabbitMQ.
    /// </summary>
    public sealed class RabbitMqReceivedMessage : IIntegrationMessageMetadata
    {
        private static readonly IReadOnlyDictionary<string, object> EmptyHeaders = RabbitMqMessageHeaders.Empty;

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
        /// Адрес очереди для ответа из AMQP properties (request-reply).
        /// </summary>
        public string ReplyTo { get; }

        /// <summary>
        /// AMQP headers сообщения (read-only снимок).
        /// </summary>
        public IReadOnlyDictionary<string, object> Headers { get; }

        /// <summary>
        /// Сообщение является RPC-запросом с ожиданием ответа.
        /// </summary>
        public bool IsRequestReply => !string.IsNullOrWhiteSpace(ReplyTo);

        /// <summary>
        /// Тег доставки для подтверждения на стороне брокера.
        /// </summary>
        internal ulong DeliveryTag { get; }

        internal RabbitMqReceivedMessage(
            byte[] body,
            ulong deliveryTag,
            string routingKey,
            string messageId,
            string correlationId,
            string replyTo = "",
            IDictionary<string, object>? headers = null)
        {
            Body = body;
            DeliveryTag = deliveryTag;
            RoutingKey = routingKey ?? string.Empty;
            MessageId = messageId ?? string.Empty;
            CorrelationId = correlationId ?? string.Empty;
            ReplyTo = replyTo ?? string.Empty;
            Headers = headers == null || headers.Count == 0
                ? EmptyHeaders
                : RabbitMqMessageHeaders.Snapshot(headers);
        }
    }
}
