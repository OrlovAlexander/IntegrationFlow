namespace IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot
{
    /// <summary>
    /// Передаваемые данные
    /// </summary>
    public struct TransmitData
    {
        public object Data { get; private set; }

        /// <summary>
        /// Идентификатор сообщения для трассировки и идемпотентности.
        /// </summary>
        public string MessageId { get; private set; }

        /// <summary>
        /// Correlation id для связи сообщений.
        /// </summary>
        public string CorrelationId { get; private set; }

        public TransmitData(object data)
        {
            Data = data;
            MessageId = string.Empty;
            CorrelationId = string.Empty;
        }

        public TransmitData(object data, string messageId, string correlationId = "")
        {
            Data = data;
            MessageId = messageId ?? string.Empty;
            CorrelationId = correlationId ?? string.Empty;
        }

        /// <summary>
        /// Создаёт копию с указанным MessageId.
        /// </summary>
        public TransmitData WithMessageId(string messageId)
            => new TransmitData(Data, messageId, CorrelationId);

        /// <summary>
        /// Создаёт копию с указанным CorrelationId.
        /// </summary>
        public TransmitData WithCorrelationId(string correlationId)
            => new TransmitData(Data, MessageId, correlationId);
    }
}
