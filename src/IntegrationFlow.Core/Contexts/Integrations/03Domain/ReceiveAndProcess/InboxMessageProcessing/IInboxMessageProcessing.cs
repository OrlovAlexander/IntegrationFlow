namespace IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.InboxMessageProcessing
{
    /// <summary>
    /// Метод обработки входного сообщения, запроса и т.п.
    /// </summary>
    public interface IInboxMessageProcessing
    {
        /// <summary>
        /// Обработать входное сообщение, запрос и т.п.
        /// </summary>
        /// <param name="inboxMessage">Входное сообщение, запрос и т.п.</param>
        void ProcessInboxMessage(InboxMessage inboxMessage);
    }
}
