namespace IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.InboxMessageProcessing
{
    /// <summary>
    /// Метод обработки входного сообщения, запроса и т.п. не прошедшего проверку
    /// </summary>
    public interface IInboxMessageFailedProcessing
    {
        /// <summary>
        /// Обработать входное сообщение, запрос и т.п. не прошедшее проверку
        /// </summary>
        /// <param name="inboxMessage">Входное сообщение, запрос и т.п.</param>
        void ProcessFailedInboxMessage(InboxMessage inboxMessage);
    }
}
