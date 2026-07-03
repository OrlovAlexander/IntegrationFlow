namespace IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot.Transmitter
{
    /// <summary>
    /// Результат передачи данных противоположной стороне.
    /// </summary>
    public sealed class TransmitResult
    {
        /// <summary>
        /// Идентификатор сообщения (MessageId).
        /// </summary>
        public string MessageId { get; set; } = string.Empty;

        /// <summary>
        /// Создаёт результат передачи.
        /// </summary>
        public static TransmitResult Create(string messageId)
            => new TransmitResult { MessageId = messageId ?? string.Empty };
    }
}
