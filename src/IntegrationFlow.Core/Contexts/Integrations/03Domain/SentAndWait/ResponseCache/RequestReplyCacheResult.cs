namespace IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.ResponseCache
{
    /// <summary>
    /// Результат попытки начать идемпотентную RPC-обработку с кешем ответа.
    /// </summary>
    public enum RequestReplyCacheResult
    {
        /// <summary>
        /// Блокировка получена, можно выполнять handler.
        /// </summary>
        Acquired,

        /// <summary>
        /// Ответ уже сохранён — используйте <see cref="IRequestReplyResponseStore.GetCachedResponseAsync"/>.
        /// </summary>
        AlreadyProcessed,

        /// <summary>
        /// Запрос с тем же MessageId обрабатывается другой доставкой.
        /// </summary>
        InProgress
    }
}
