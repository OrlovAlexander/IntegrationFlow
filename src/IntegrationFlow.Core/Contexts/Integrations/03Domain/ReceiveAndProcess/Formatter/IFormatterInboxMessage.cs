namespace IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Formatter
{
    /// <summary>
    /// Форматировщик входящего сообщения
    /// </summary>
    public interface IFormatterInboxMessage
    {
        /// <summary>
        /// Форматировать входящее сообщение
        /// </summary>
        /// <param name="inboxMessage">Входящее сообщение, запрос и т.п.</param>
        InboxMessage FormatInboxMessage(InboxMessage inboxMessage);
    }
}
