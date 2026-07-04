namespace IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait
{
    /// <summary>
    /// Передаваемые данные
    /// </summary>
    public struct TransmitData
    {
        public object Data { get; private set; }

        /// <summary>
        /// Идентификатор сообщения для идемпотентности и retry после timeout.
        /// </summary>
        public string MessageId { get; private set; }

        public TransmitData(object data)
        {
            Data = data;
            MessageId = string.Empty;
        }

        public TransmitData(object data, string messageId)
        {
            Data = data;
            MessageId = messageId ?? string.Empty;
        }

        /// <summary>
        /// Создаёт копию с указанным MessageId.
        /// </summary>
        public TransmitData WithMessageId(string messageId)
            => new TransmitData(Data, messageId);
    }
}
