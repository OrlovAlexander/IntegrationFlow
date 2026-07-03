namespace IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Deduplication
{
    /// <summary>
    /// Результат попытки начать идемпотентную обработку сообщения.
    /// </summary>
    public enum DeduplicationBeginResult
    {
        /// <summary>
        /// Блокировка получена, можно обрабатывать.
        /// </summary>
        Acquired,

        /// <summary>
        /// Сообщение уже успешно обработано ранее.
        /// </summary>
        AlreadyProcessed,

        /// <summary>
        /// Сообщение обрабатывается другой попыткой доставки.
        /// </summary>
        InProgress
    }
}
