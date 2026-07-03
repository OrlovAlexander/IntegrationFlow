namespace IntegrationFlow.Contexts.Integrations._03Domain.Outbox
{
    /// <summary>
    /// Запись в transactional outbox без commit — для участия в TX вызывающего DbContext.
    /// </summary>
    public interface IOutboxEnqueue
    {
        /// <summary>
        /// Подготовить сообщение к сохранению (без SaveChanges).
        /// </summary>
        void Stage(OutboxMessage message);
    }
}
