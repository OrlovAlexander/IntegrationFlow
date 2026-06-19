namespace IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Logging
{
    /// <summary>
    /// Логерирование данных
    /// </summary>
    public interface ILogging
    {
        /// <summary>
        /// Логирование входного сообщения, запроса и т.п.
        /// </summary>
        /// <param name="message">Входное сообщение, запрос и т.п.</param>
        void LogInboxMessage(InboxMessage message);
    }
}
