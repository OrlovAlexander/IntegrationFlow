namespace IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Validator
{
    /// <summary>
    /// Валидатор входящего сообщения, запроса и т.п.
    /// </summary>
    public interface IValidator
    {
        /// <summary>
        /// Проверить входящее сообщение, запрос и т.п.
        /// </summary>
        /// <param name="inboxMessage">Входящее сообщение, запрос и т.п.</param>
        void Validate(InboxMessage inboxMessage);
    }
}
