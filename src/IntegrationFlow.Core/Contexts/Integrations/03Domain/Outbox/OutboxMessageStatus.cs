namespace IntegrationFlow.Contexts.Integrations._03Domain.Outbox
{
    /// <summary>
    /// Статус сообщения transactional outbox.
    /// </summary>
    public enum OutboxMessageStatus
    {
        Pending,
        InFlight,
        Published,
        Failed
    }
}
